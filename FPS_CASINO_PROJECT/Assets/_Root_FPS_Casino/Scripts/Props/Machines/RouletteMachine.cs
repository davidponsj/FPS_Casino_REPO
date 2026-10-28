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
    [Tooltip("Hijo de la rueda que gira. Su flecha azul apunta al borde entre Amarillo y Verde")]
    [SerializeField] private Transform wheelCenter;
    [SerializeField] private Transform ball;

    [Header("Segmentos (sentido horario visto desde arriba)")]
    [SerializeField] private Segment[] segments;

    [Header("Coste")]
    [SerializeField] private int cost = 100;
    [Tooltip("Debe implementar IPointsService (ej. TestPointsWallet)")]
    [SerializeField] private MonoBehaviour pointsSource;

    [Header("Animación")]
    [SerializeField] private float outerRadius = 0.9f;
    [SerializeField] private float pocketRadius = 0.8f;
    [SerializeField] private float ballHeight = 0.05f;
    [SerializeField] private int minSpins = 4;
    [SerializeField] private int maxSpins = 7;
    [SerializeField] private float minDuration = 7f;
    [SerializeField] private float maxDuration = 9f;
    [Tooltip("Fracción de la tirada a velocidad máxima constante (0.4 = el primer 40%)")]
    [SerializeField, Range(0f, 0.8f)] private float cruisePhase = 0.4f;
    [Tooltip("Bajo = frena más tarde y más brusco. Alto = frena de forma más larga y suave")]
    [SerializeField, Range(1.5f, 6f)] private float brakingPower = 2.5f;
    [Tooltip("Parte final de la tirada en la que la bola baja del borde a la casilla (0.2 = el último 20%)")]
    [SerializeField, Range(0.05f, 0.5f)] private float dropPhase = 0.2f;
    [Tooltip("Segundos que la bola sigue girando con la rueda antes de desaparecer")]
    [SerializeField] private float ballVisibleAfterLanding = 3f;

    [Header("Spawner de tickets")]
    [SerializeField] private TicketSpawner ticketSpawner;

    [Header("Botella (para saber si la tienes en la mano)")]
    [SerializeField] private BottleHolder bottleHolder;

    [Header("Eventos (opcionales, para sonidos/VFX)")]
    public UnityEvent onSpinStart;
    public UnityEvent onNotEnoughPoints;
    public UnityEvent onBlockedByTicket;
    public SegmentEvent onSpinFinished;

    private IPointsService points;
    private bool spinning;

    private float StepAngle => 360f / segments.Length;

    public string InteractionPrompt
    {
        get
        {
            if (spinning) return "Girando...";

            if (ticketSpawner != null && ticketSpawner.HasTicketWaiting)
                return $"Coger {ticketSpawner.WaitingTicketName} [E]";

            var inv = TicketInventory.Instance;
            if (inv != null && inv.HasTicket) return "Gasta tu ticket";
            if (bottleHolder != null && bottleHolder.HasBottle) return "Usa tu botella";
            if (inv != null && inv.IsLocked) return "Gasta tu ticket";

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
        // La bola solo se ve durante la tirada
        if (ball != null) ball.gameObject.SetActive(false);
    }

    public void Interact(GameObject interactor)
    {
        if (spinning || segments == null || segments.Length == 0) return;
        if (wheelCenter == null || ball == null)
        {
            Debug.LogWarning("[Ruleta] Falta asignar Wheel Center o Ball.", this);
            return;
        }

        // 1) Si hay un ticket esperando en la ruleta, se coge desde aquí
        if (ticketSpawner != null && ticketSpawner.HasTicketWaiting)
        {
            ticketSpawner.TryCollect();
            return;
        }

        // 2) Bloqueada si tienes ticket en la mano, botella sin usar, o sigue bloqueada
        var inventory = TicketInventory.Instance;
        bool hasBottle = bottleHolder != null && bottleHolder.HasBottle;
        if (inventory != null && (inventory.HasTicket || inventory.IsLocked) || hasBottle)
        {
            Debug.Log("[Ruleta] Gasta tu ticket y usa tu botella antes de volver a jugar.");
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

        // Ángulos medidos sobre la rueda: sentido horario desde la flecha azul de wheelCenter.
        // El jitter es pequeño para que la bola nunca quede en el borde entre dos casillas.
        float jitter = UnityEngine.Random.Range(-0.25f, 0.25f) * StepAngle;
        float targetAngle = (winner + 0.5f) * StepAngle + jitter;
        float startAngle = UnityEngine.Random.Range(0f, 360f);

        // La bola avanza en sentido antihorario respecto a la rueda
        float delta = Mathf.Repeat(startAngle - targetAngle, 360f);
        float totalRotation = delta + 360f * UnityEngine.Random.Range(minSpins, maxSpins + 1);
        float duration = UnityEngine.Random.Range(minDuration, maxDuration);

        PlaceBall(startAngle, outerRadius);
        ball.gameObject.SetActive(true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);

            float angle = startAngle - totalRotation * SpinEase(k);
            float drop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - dropPhase, 1f, k));
            PlaceBall(angle, Mathf.Lerp(outerRadius, pocketRadius, drop));
            yield return null;
        }

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

        // La bola se queda en su casilla y gira con la rueda
        float elapsed = 0f;
        while (elapsed < ballVisibleAfterLanding)
        {
            elapsed += Time.deltaTime;
            PlaceBall(targetAngle, pocketRadius);
            yield return null;
        }

        ball.gameObject.SetActive(false);
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

    // angleDeg: sentido horario desde la flecha azul de wheelCenter, así la bola sigue a la rueda al girar
    private Vector3 WheelPoint(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 dir = wheelCenter.rotation * new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        return wheelCenter.position + dir * radius + wheelCenter.up * ballHeight;
    }

    private void PlaceBall(float angleDeg, float radius)
    {
        ball.position = WheelPoint(angleDeg, radius);
    }

    private void OnDrawGizmosSelected()
    {
        if (segments == null || segments.Length == 0 || wheelCenter == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            // Alpha forzado a 1 para que se vean aunque el color tenga transparencia
            Color c = segments[i].color;
            Gizmos.color = new Color(c.r, c.g, c.b, 1f);
            Gizmos.DrawSphere(WheelPoint((i + 0.5f) * StepAngle, pocketRadius), 0.04f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(WheelPoint(0f, outerRadius), 0.04f);
    }
}