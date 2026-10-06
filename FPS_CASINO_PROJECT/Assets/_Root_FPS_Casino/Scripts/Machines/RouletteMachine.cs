using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class RouletteMachine : MonoBehaviour, IInteractable
{
    [Serializable]
    public class Segment
    {
        public string colorName = "Rojo";
        public Color color = Color.red;
        [Min(0f)] public float weight = 1f;   // probabilidad relativa
        public TicketData ticket;             // ticket/botella de ese color
    }

    [Serializable] public class SegmentEvent : UnityEvent<Segment> { }

    [Header("Referencias")]
    [SerializeField] private Transform wheelCenter;
    [SerializeField] private Transform ball;

    [Header("Segmentos (sentido horario visto desde arriba)")]
    [SerializeField] private Segment[] segments;

    [Header("Coste")]
    [SerializeField] private int cost = 100;
    [Tooltip("Debe implementar IPointsService (ej. TestPointsWallet)")]
    [SerializeField] private MonoBehaviour pointsSource;

    [Header("Animación")]
    [SerializeField] private float outerRadius = 2.0f;
    [SerializeField] private float pocketRadius = 1.5f;
    [SerializeField] private float ballHeight = 0.05f;
    [SerializeField] private int minSpins = 4;
    [SerializeField] private int maxSpins = 7;
    [SerializeField] private float minDuration = 4f;
    [SerializeField] private float maxDuration = 6f;
    [SerializeField, Range(1f, 6f)] private float brakingPower = 3.5f;

    [Header("Spawner de tickets")]
    [SerializeField] private TicketSpawner ticketSpawner;

    [Header("Eventos (opcionales, para sonidos/VFX)")]
    public UnityEvent onSpinStart;
    public UnityEvent onNotEnoughPoints;
    public UnityEvent onBlockedByTicket;
    public SegmentEvent onSpinFinished;

    private IPointsService points;
    private bool spinning;
    private float currentAngle = 90f;

    private float StepAngle => 360f / segments.Length;

    public string InteractionPrompt
    {
        get
        {
            if (spinning) return "Girando...";
            if (TicketInventory.Instance != null && TicketInventory.Instance.IsLocked)
                return "Gasta tu ticket antes de jugar [E]";
            return $"Jugar a la ruleta ({cost} pts) [E]";
        }
    }

    private void OnValidate()
    {
        if (pointsSource != null && !(pointsSource is IPointsService))
        {
            Debug.LogWarning($"{pointsSource.name} no implementa IPointsService.", this);
            pointsSource = null;
        }
    }

    private void Awake()
    {
        points = pointsSource as IPointsService;
        if (points == null)
            Debug.LogWarning("[Ruleta] Sin sistema de puntos asignado: la tirada será gratis.", this);
    }

    private void Start()
    {
        PlaceBall(currentAngle, outerRadius);
    }

    public void Interact(GameObject interactor)
    {
        if (spinning || segments == null || segments.Length == 0) return;

        var inventory = TicketInventory.Instance;
        if (inventory != null && inventory.IsLocked)
        {
            Debug.Log("[Ruleta] Gasta tu ticket antes de volver a jugar.");
            onBlockedByTicket?.Invoke();
            return;
        }

        if (points != null && cost > 0 && !points.TrySpend(cost))
        {
            Debug.Log("[Ruleta] Puntos insuficientes.");
            onNotEnoughPoints?.Invoke();
            return;
        }

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        spinning = true;
        onSpinStart?.Invoke();

        int winner = PickWeightedIndex();

        float jitter = UnityEngine.Random.Range(-0.35f, 0.35f) * StepAngle;
        float targetAngle = 90f - (winner + 0.5f) * StepAngle + jitter;

        float startAngle = currentAngle;
        float delta = Mathf.Repeat(startAngle - targetAngle, 360f);
        float totalRotation = delta + 360f * UnityEngine.Random.Range(minSpins, maxSpins + 1);
        float duration = UnityEngine.Random.Range(minDuration, maxDuration);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            float eased = 1f - Mathf.Pow(1f - k, brakingPower);
            currentAngle = startAngle - totalRotation * eased;
            PlaceBall(currentAngle, Mathf.Lerp(outerRadius, pocketRadius, eased));
            yield return null;
        }

        currentAngle = targetAngle;
        PlaceBall(currentAngle, pocketRadius);

        Segment result = segments[winner];
        Debug.Log($"[Ruleta] Ha salido: {result.colorName} (ticket: {(result.ticket ? result.ticket.displayName : "nada")})");

        if (result.ticket != null)
        {
            if (TicketInventory.Instance != null)
                TicketInventory.Instance.Lock();

            if (ticketSpawner != null)
                ticketSpawner.Spawn(result);
        }

        onSpinFinished?.Invoke(result);
        spinning = false;
    }

    private int PickWeightedIndex()
    {
        float total = 0f;
        foreach (var s in segments) total += s.weight;

        float roll = UnityEngine.Random.Range(0f, total);
        float acc = 0f;
        for (int i = 0; i < segments.Length; i++)
        {
            acc += segments[i].weight;
            if (roll <= acc) return i;
        }
        return segments.Length - 1;
    }

    private void PlaceBall(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 center = wheelCenter ? wheelCenter.position : transform.position;
        Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
        ball.position = center + offset + Vector3.up * ballHeight;
    }

    private void OnDrawGizmosSelected()
    {
        if (segments == null || segments.Length == 0) return;
        Vector3 center = wheelCenter ? wheelCenter.position : transform.position;
        for (int i = 0; i < segments.Length; i++)
        {
            float a = (90f - (i + 0.5f) * StepAngle) * Mathf.Deg2Rad;
            Gizmos.color = segments[i].color;
            Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * pocketRadius;
            Gizmos.DrawSphere(pos, 0.12f);
        }
    }
}
