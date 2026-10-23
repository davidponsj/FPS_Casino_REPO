using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Ruleta del casino. Pagas spinCost puntos (E), la bola gira y cae en un segmento elegido por
/// pesos (WeightedRandom). Si el segmento tiene ticket, sale por el RouletteTicketDispenser y se
/// coge pulsando E otra vez sobre la ruleta. Mientras tengas ticket o botella no puedes volver a
/// jugar (IsBlocked): el barman cambia el ticket por la botella (Bartender → BottleHolder).
/// </summary>
public class RouletteMachine : MonoBehaviour, IInteractable
{
    [Serializable]
    public class Segment
    {
        [Tooltip("Nombre del color, solo para leerlo en el Inspector.")]
        public string colorName = "Rojo";
        [Tooltip("Color de la bolita que marca el segmento en la vista Scene.")]
        public Color color = Color.red;
        [Tooltip("Probabilidad relativa. Un segmento con 2 sale el doble que uno con 1; 0 = no sale nunca.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Ticket que da este segmento (SO_Ticket_*). Vacío = no da nada.")]
        public TicketData ticket;
    }

    [Serializable] public class SegmentEvent : UnityEvent<Segment> { }

    [Header("Referencias")]
    [Tooltip("Centro de la rueda. Vacío = este objeto.")]
    [SerializeField] private Transform wheelCenter;
    [Tooltip("La bola que gira.")]
    [SerializeField] private Transform ball;
    [Tooltip("Dispensador por donde sale el ticket ganado.")]
    [FormerlySerializedAs("ticketSpawner")]
    [SerializeField] private RouletteTicketDispenser ticketDispenser;
    [Tooltip("BottleHolder del jugador: con una botella en la mano no se puede jugar.")]
    [SerializeField] private BottleHolder bottleHolder;

    [Header("Segmentos (sentido horario visto desde arriba)")]
    [SerializeField] private Segment[] segments;

    [Header("Coste")]
    [Tooltip("Puntos que cuesta cada tirada. 100 = barata; 0 = gratis.")]
    [FormerlySerializedAs("cost")]
    [Min(0)]
    [SerializeField] private int spinCost = 100;

    [Header("Animación")]
    [Tooltip("Distancia del centro a la que gira la bola, en metros. Ajústala al radio de las casillas del modelo.")]
    [FormerlySerializedAs("pocketRadius")]
    [Min(0f)]
    [SerializeField] private float ballRadiusMeters = 1.5f;
    [Tooltip("Altura de la bola sobre el centro, en metros. 0.05 = apoyada en la rueda.")]
    [FormerlySerializedAs("ballHeight")]
    [SerializeField] private float ballHeightMeters = 0.05f;
    [Tooltip("Vueltas completas mínimas de la bola antes de pararse.")]
    [Min(0)]
    [SerializeField] private int minSpins = 4;
    [Tooltip("Vueltas completas máximas. Cada tirada elige entre el mínimo y este valor.")]
    [Min(0)]
    [SerializeField] private int maxSpins = 7;
    [Tooltip("Segundos mínimos que dura una tirada. 7 = da tiempo a ponerse nervioso.")]
    [FormerlySerializedAs("minDuration")]
    [Min(0.1f)]
    [SerializeField] private float minSpinSeconds = 7f;
    [Tooltip("Segundos máximos que dura una tirada. Cada tirada elige entre el mínimo y este valor.")]
    [FormerlySerializedAs("maxDuration")]
    [Min(0.1f)]
    [SerializeField] private float maxSpinSeconds = 9f;
    [Tooltip("Fracción de la tirada a velocidad máxima constante (0.4 = el primer 40%)")]
    [SerializeField, Range(0f, 0.8f)] private float cruisePhase = 0.4f;
    [Tooltip("Bajo = frena más tarde y más brusco. Alto = frena de forma más larga y suave")]
    [SerializeField, Range(1.5f, 6f)] private float brakingPower = 2.5f;

    [Header("Eventos (opcionales, para sonidos/VFX)")]
    public UnityEvent onSpinStart;
    public UnityEvent onNotEnoughPoints;
    public UnityEvent onBlockedByTicket;
    public SegmentEvent onSpinFinished;

    [Header("Depuración")]
    [Tooltip("Muestra el coste, si está bloqueada y el último resultado sobre la ruleta en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private bool spinning;
    private float currentAngle = 90f;
    private Segment lastResult;

    private float StepAngle => 360f / segments.Length;

    public string InteractionPrompt
    {
        get
        {
            if (spinning) return "Girando...";

            if (ticketDispenser != null && ticketDispenser.HasTicketWaiting)
                return $"Coger {ticketDispenser.WaitingTicketName} [E]";

            if (PlayerHasTicket) return "Gasta tu ticket";
            if (PlayerHasBottle) return "Usa tu botella";

            return $"Jugar a la ruleta ({spinCost} pts) [E]";
        }
    }

    private bool PlayerHasTicket => TicketInventory.Instance != null && TicketInventory.Instance.HasTicket;
    private bool PlayerHasBottle => bottleHolder != null && bottleHolder.HasBottle;

    /// <summary>
    /// La única regla de bloqueo: no se puede jugar mientras haya un ticket esperando en la ruleta,
    /// un ticket en el inventario o una botella en la mano. Al gastar la botella se desbloquea sola.
    /// </summary>
    private bool IsBlocked => (ticketDispenser != null && ticketDispenser.HasTicketWaiting) || PlayerHasTicket || PlayerHasBottle;

    private void Start()
    {
        if (ball == null) Debug.LogWarning("[Ruleta] Falta asignar la bola.", this);
        if (ticketDispenser == null) Debug.LogWarning("[Ruleta] Falta el dispensador: los tickets ganados no saldrán.", this);
        PlaceBall(currentAngle, ballRadiusMeters);
    }

    public void Interact(GameObject interactor)
    {
        if (spinning || segments == null || segments.Length == 0) return;

        // 1) Si hay un ticket esperando en la ruleta, se coge desde aquí
        if (ticketDispenser != null && ticketDispenser.HasTicketWaiting)
        {
            ticketDispenser.TryCollect();
            return;
        }

        // 2) Bloqueada si tienes ticket o botella sin usar
        if (IsBlocked)
        {
            onBlockedByTicket?.Invoke();
            return;
        }

        // Paga el jugador que interactúa.
        PlayerPoints points = interactor != null ? interactor.GetComponentInParent<PlayerPoints>() : null;
        if (points == null)
            Debug.LogWarning("[Ruleta] El jugador no tiene PlayerPoints: la tirada será gratis.", this);
        else if (spinCost > 0 && !points.TrySpend(spinCost))
        {
            onNotEnoughPoints?.Invoke();
            return;
        }

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        spinning = true;
        onSpinStart?.Invoke();

        int winner = WeightedRandom.PickIndex(segments, s => s.weight);

        float jitter = UnityEngine.Random.Range(-0.35f, 0.35f) * StepAngle;
        float targetAngle = 90f - (winner + 0.5f) * StepAngle + jitter;

        float startAngle = currentAngle;
        float delta = Mathf.Repeat(startAngle - targetAngle, 360f);
        float totalRotation = delta + 360f * UnityEngine.Random.Range(minSpins, maxSpins + 1);
        float duration = UnityEngine.Random.Range(minSpinSeconds, maxSpinSeconds);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            float eased = SpinEase(k);
            currentAngle = startAngle - totalRotation * eased;
            PlaceBall(currentAngle, ballRadiusMeters);
            yield return null;
        }

        currentAngle = targetAngle;
        PlaceBall(currentAngle, ballRadiusMeters);

        Segment result = segments[winner];
        lastResult = result;

        if (result.ticket != null && ticketDispenser != null)
            ticketDispenser.Spawn(result);

        onSpinFinished?.Invoke(result);
        spinning = false;
    }

    // Velocidad: máxima desde el primer frame, se mantiene durante "cruisePhase"
    // y luego baja de forma progresiva hasta 0.
    // Devuelve el progreso (0..1) para un tiempo k (0..1).
    private float SpinEase(float k)
    {
        float c = cruisePhase;
        float p = brakingPower;

        float cruiseArea = c;                // distancia recorrida a velocidad máxima
        float brakeArea = (1f - c) / p;      // distancia recorrida frenando
        float total = cruiseArea + brakeArea;

        if (k < c)
            return k / total;

        float s = (k - c) / (1f - c);
        return (cruiseArea + brakeArea * (1f - Mathf.Pow(1f - s, p))) / total;
    }

    private void PlaceBall(float angleDeg, float radius)
    {
        if (ball == null) return;
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 center = wheelCenter ? wheelCenter.position : transform.position;
        Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
        ball.position = center + offset + Vector3.up * ballHeightMeters;
    }

    private void OnDrawGizmosSelected()
    {
        if (segments == null || segments.Length == 0) return;
        Vector3 center = wheelCenter ? wheelCenter.position : transform.position;
        for (int i = 0; i < segments.Length; i++)
        {
            float a = (90f - (i + 0.5f) * StepAngle) * Mathf.Deg2Rad;
            Gizmos.color = segments[i].color;
            Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ballRadiusMeters;
            Gizmos.DrawSphere(pos, 0.12f);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        string state;
        if (spinning) state = "girando";
        else if (ticketDispenser != null && ticketDispenser.HasTicketWaiting) state = "bloqueada: ticket esperando";
        else if (PlayerHasTicket) state = "bloqueada: tienes ticket";
        else if (PlayerHasBottle) state = "bloqueada: tienes botella";
        else state = "libre";

        string last = lastResult == null ? "-"
            : $"{lastResult.colorName} ({(lastResult.ticket != null ? lastResult.ticket.displayName : "sin ticket")})";

        Vector3 center = wheelCenter ? wheelCenter.position : transform.position;
        UnityEditor.Handles.Label(center + Vector3.up * 1.5f,
            $"Ruleta · {spinCost} pts · {state}\nÚltimo resultado: {last}");
    }
#endif
}