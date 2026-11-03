using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Vida del enemigo. Implementa IDamageable, así que las armas, granadas y charcos ya le hacen daño
/// sin saber nada de él. Los enemigos de las rondas reciben su vida del RoundManager al aparecer
/// (sube cada ronda, como en CoD Zombies); uno puesto a mano en la escena usa la del Inspector.
///
/// Al morir: avisa (OnDied, OnAnyDied), lanza la animación de muerte y deja el cuerpo Corpse Seconds en el
/// suelo con todo lo demás apagado (IA, movimiento, ataque, colliders), lo hunde Sink Meters en Sink Seconds y se destruye.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    /// <summary>Se lanza una sola vez, al morir (el RoundManager cuenta las bajas con esto).</summary>
    public event Action<EnemyHealth> OnDied;

    /// <summary>Lo mismo que OnDied, pero de CUALQUIER enemigo: KillScore se entera así de todas las bajas sin buscarlas.</summary>
    public static event Action<EnemyHealth> OnAnyDied;

    [Header("Vida")]
    [Tooltip("Vida si pones el enemigo a mano en la escena. Los de las rondas la reciben del RoundManager. 150 = ronda 1 de CoD.")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 150f;

    [Header("Muerte")]
    [Tooltip("Animator del modelo. Vacío = lo busca en los hijos. Sin Animator, el cuerpo se queda quieto hasta desaparecer.")]
    [SerializeField] private Animator animator;
    [Tooltip("Nombre del Trigger del Animator que lanza la animación de muerte.")]
    [SerializeField] private string dieTrigger = "Die";
    [Tooltip("Segundos que se queda el cuerpo en el suelo antes de hundirse. 0 = se hunde (o desaparece) al instante.")]
    [Min(0f)]
    [SerializeField] private float corpseSeconds = 3f;
    [Tooltip("Segundos que tarda el cuerpo en hundirse en el suelo después de Corpse Seconds. 0 = desaparece de golpe. 1 = baja despacio, casi no se nota.")]
    [Min(0f)]
    [SerializeField] private float sinkSeconds = 1f;
    [Tooltip("Metros que baja el cuerpo al hundirse. Más que su altura, para que no asome nada. 1.5 = un robot de ~1,5 m.")]
    [Min(0f)]
    [SerializeField] private float sinkMeters = 1.5f;

    [Header("Depuración")]
    [Tooltip("Muestra la vida sobre el enemigo en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private float currentHealth;

    // Vida al recibir el primer daño de este frame (todos los perdigones de un escopetazo llegan en el mismo frame).
    private int damageFrame = -1;
    private float healthAtFrameStart;

    private int dieTriggerId;
    private bool sinking;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>True si el daño de este frame le ha pillado a vida completa (para el truco Un tiro, una baja).</summary>
    public bool WasFullHealthThisFrame => damageFrame == Time.frameCount && healthAtFrameStart >= maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        dieTriggerId = Animator.StringToHash(dieTrigger);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    /// <summary>Lo llama el RoundManager justo después de crearlo, con la vida de la ronda actual.</summary>
    public void SetMaxHealth(float health)
    {
        maxHealth = Mathf.Max(1f, health);
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        // Muerto ya no recibe daño: ni los perdigones que llegan en el mismo frame ni balas al cuerpo.
        if (IsDead || amount <= 0f) return;

        if (damageFrame != Time.frameCount)
        {
            damageFrame = Time.frameCount;
            healthAtFrameStart = currentHealth;
        }

        currentHealth -= amount;
        if (currentHealth > 0f) return;

        IsDead = true;
        OnDied?.Invoke(this); // primero el RoundManager, para que KillScore ya vea la ronda actualizada
        OnAnyDied?.Invoke(this);

        StopBody();
        if (animator != null) animator.SetTrigger(dieTriggerId);
        StartCoroutine(SinkAndDestroy());
    }

    /// <summary>Espera Corpse Seconds, hunde el cuerpo Sink Meters en Sink Seconds y lo destruye.</summary>
    private IEnumerator SinkAndDestroy()
    {
        if (corpseSeconds > 0f) yield return new WaitForSeconds(corpseSeconds);

        if (sinkSeconds > 0f)
        {
            sinking = true;
            float speed = sinkMeters / sinkSeconds;
            // El NavMeshAgent ya está apagado (StopBody), por eso aquí sí se puede mover el transform.
            for (float t = 0f; t < sinkSeconds; t += Time.deltaTime)
            {
                transform.position += Vector3.down * (speed * Time.deltaTime);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Apaga todo lo demás del enemigo para que el cuerpo no persiga, deje su sitio en la horda
    /// (EnemyAI lo libera al apagarse) y las balas lo atraviesen. Así ningún otro script tiene que comprobar si está muerto.
    /// </summary>
    private void StopBody()
    {
        foreach (Behaviour behaviour in GetComponents<Behaviour>())
            if (behaviour != this) behaviour.enabled = false;

        foreach (Collider bodyCollider in GetComponentsInChildren<Collider>())
            bodyCollider.enabled = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string text = IsDead ? (sinking ? "Vida · muerto · hundiéndose" : "Vida · muerto") : $"Vida · {currentHealth:0}/{maxHealth:0}";
        DebugLabels.Above(transform, DebugLabels.EnemyHealthLine, text, DebugLabels.Health);
    }
#endif
}