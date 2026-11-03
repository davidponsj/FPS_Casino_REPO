using UnityEngine;

/// <summary>
/// Ataque cuerpo a cuerpo del enemigo, estilo Black Ops Zombies: zarpazo con un aviso corto y sin pararse.
/// No decide CUÁNDO puede atacar: eso lo decide EnemyAI (solo en Engage, con ficha de HordeAttackSlots),
/// que llama a Begin al pegarse al jugador, a Tick cada frame mientras sigue pegado y a Cancel al soltarse.
///
///   Listo → (jugador a Attack Range y delante) → Aviso (Windup Seconds) → Golpe: acierta si sigue
///   a Hit Range y delante → Espera (Cooldown Seconds ± Cooldown Jitter Seconds) → Listo...
///
/// El daño va por IDamageable: vale contra el jugador o contra cualquier cosa que pueda recibir daño.
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    private enum Phase { Ready, Windup, Cooldown }

    [Header("Daño")]
    [Tooltip("Daño de cada golpe. No sube con las rondas, como en CoD. 50 = con 150 de vida aguantas 2 golpes y caes al 3º.")]
    [Min(0f)]
    [SerializeField] private float damage = 50f;

    [Header("Ritmo")]
    [Tooltip("Segundos de aviso desde que empieza el golpe hasta que hace daño. Es el tiempo que tienes para apartarte. 0.3 = rápido pero se ve venir.")]
    [Min(0f)]
    [SerializeField] private float windupSeconds = 0.3f;
    [Tooltip("Segundos de espera entre un golpe y el siguiente aviso. 1 = un zarpazo por segundo.")]
    [Min(0f)]
    [SerializeField] private float cooldownSeconds = 1f;
    [Tooltip("Segundos de variación al azar, arriba o abajo, en cada espera. Desincroniza a los que te rodean. 0.25 = esperan entre 0.75 y 1.25 s.")]
    [Min(0f)]
    [SerializeField] private float cooldownJitterSeconds = 0.25f;
    [Tooltip("Segundos máximos de retraso al azar antes del primer golpe al pegarse. Así los que llegan juntos no golpean a la vez. 0.5 = entre 0 y 0.5 s.")]
    [Min(0f)]
    [SerializeField] private float firstAttackMaxDelaySeconds = 0.5f;

    [Header("Alcance")]
    [Tooltip("Metros al jugador a los que empieza el golpe. Igual o mayor que el Melee Distance de EnemyAI. 1.6 = como su Engage Range.")]
    [Min(0f)]
    [SerializeField] private float attackRange = 1.6f;
    [Tooltip("Metros a los que el golpe todavía acierta al acabar el aviso. Mayor que Attack Range. 2 = para esquivarlo hay que dar un paso atrás de verdad.")]
    [Min(0f)]
    [SerializeField] private float hitRange = 2f;
    [Tooltip("Grados del cono delante del enemigo en el que tiene que estar el jugador para empezar y para acertar el golpe. 120 = 60 a cada lado.")]
    [Range(0f, 360f)]
    [SerializeField] private float hitAngle = 120f;

    [Header("Movimiento")]
    [Tooltip("Marcado = sigue andando mientras golpea (como en Black Ops, no se para). Desmarcado = se queda quieto durante el aviso.")]
    [SerializeField] private bool moveWhileAttacking = true;

    [Header("Animación (opcional)")]
    [Tooltip("Animator del enemigo. Vacío = sin animación: solo se ve el color de aviso de EnemyAI.")]
    [SerializeField] private Animator animator;
    [Tooltip("Nombre del Trigger del Animator que lanza la animación de ataque. Solo cuenta si hay Animator. Attack = el nombre por defecto.")]
    [SerializeField] private string attackTrigger = "Attack";

    [Header("Depuración")]
    [Tooltip("Muestra la fase del ataque y si el último golpe acertó, y dibuja el cono de alcance (rojo durante el aviso), en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private Phase phase = Phase.Ready;
    private float phaseEndTime;
    private Transform target;
    private IDamageable targetDamageable;
    private int attackTriggerId;

    // Solo para la depuración.
    private string lastResult = "ninguno";

    /// <summary>True durante el aviso: EnemyAI lo usa para pintar el color de aviso.</summary>
    public bool IsWindingUp => phase == Phase.Windup;

    /// <summary>True si ahora mismo tiene que quedarse quieto (aviso con Move While Attacking desmarcado).</summary>
    public bool HoldsStill => IsWindingUp && !moveWhileAttacking;

    private void Awake()
    {
        attackTriggerId = Animator.StringToHash(attackTrigger);
    }

    /// <summary>Lo llama EnemyAI al pegarse al jugador. Empieza esperando un poco al azar para desincronizar a la horda.</summary>
    public void Begin(Transform newTarget)
    {
        if (newTarget != target)
        {
            target = newTarget;
            targetDamageable = target != null ? target.GetComponentInParent<IDamageable>() : null;

            if (target != null && targetDamageable == null)
                Debug.LogWarning($"{name}: {target.name} no tiene ningún componente IDamageable (PlayerHealth). El ataque no hará daño.", this);
        }

        phase = Phase.Cooldown;
        phaseEndTime = Time.time + Random.Range(0f, firstAttackMaxDelaySeconds);
    }

    /// <summary>Lo llama EnemyAI cada frame mientras está pegado al jugador.</summary>
    public void Tick()
    {
        if (target == null) return;

        switch (phase)
        {
            case Phase.Ready:
                if (IsInReach(attackRange)) StartWindup();
                break;

            case Phase.Windup:
                if (Time.time >= phaseEndTime) Strike();
                break;

            case Phase.Cooldown:
                if (Time.time >= phaseEndTime) phase = Phase.Ready;
                break;
        }
    }

    /// <summary>Lo llama EnemyAI al soltarse del jugador: el golpe a medias se pierde.</summary>
    public void Cancel()
    {
        if (phase == Phase.Windup && animator != null)
            animator.ResetTrigger(attackTriggerId);

        phase = Phase.Ready;
    }

    private void StartWindup()
    {
        phase = Phase.Windup;
        phaseEndTime = Time.time + windupSeconds;

        if (animator != null)
            animator.SetTrigger(attackTriggerId);
    }

    private void Strike()
    {
        // Se comprueba otra vez al final del aviso: si el jugador se ha apartado, el golpe falla.
        bool hit = IsInReach(hitRange);
        if (hit) targetDamageable?.TakeDamage(damage);

        lastResult = hit ? "acierto" : "fallo";
        phase = Phase.Cooldown;
        phaseEndTime = Time.time + Mathf.Max(0f, cooldownSeconds + Random.Range(-cooldownJitterSeconds, cooldownJitterSeconds));
    }

    /// <summary>¿Está el objetivo a menos de "range" metros (en horizontal) y dentro del cono de delante?</summary>
    private bool IsInReach(float range)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > range * range) return false;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, toTarget) <= hitAngle * 0.5f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        // El cono se ve también sin jugar, para ajustar los alcances en el Inspector.
        Vector3 forward = transform.forward;
        forward.y = 0f;
        Vector3 from = Quaternion.Euler(0f, -hitAngle * 0.5f, 0f) * forward.normalized;

        UnityEditor.Handles.color = IsWindingUp ? Color.red : new Color(1f, 0.5f, 0.5f, 0.6f);
        UnityEditor.Handles.DrawWireArc(transform.position, Vector3.up, from, hitAngle, attackRange);
        UnityEditor.Handles.DrawWireArc(transform.position, Vector3.up, from, hitAngle, hitRange);

        if (!Application.isPlaying) return;

        float remaining = Mathf.Max(0f, phaseEndTime - Time.time);
        string state = phase switch
        {
            Phase.Windup => $"aviso {remaining:0.0} s",
            Phase.Cooldown => $"espera {remaining:0.0} s",
            _ => "listo",
        };
        DebugLabels.Above(transform, DebugLabels.EnemyAttackLine, $"Ataque · {state} · último golpe: {lastResult}", DebugLabels.Health);
    }
#endif
}