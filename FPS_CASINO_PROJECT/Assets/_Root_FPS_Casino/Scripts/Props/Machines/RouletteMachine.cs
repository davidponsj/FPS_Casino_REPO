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

    [Header("Bola en la madera (parte fija)")]
    [Tooltip("Radio por el que rueda la bola sobre la madera")]
    [SerializeField] private float outerRadius = 0.9f;
    [Tooltip("Altura de la bola sobre la madera, medida desde WheelPivot")]
    [SerializeField] private float rimHeight = 0.05f;
    [Tooltip("Velocidad al aparecer (grados por segundo)")]
    [SerializeField] private float rimStartSpeed = 540f;
    [Tooltip("Velocidad justo antes de caer a los colores (grados por segundo)")]
    [SerializeField] private float rimEndSpeed = 200f;
    [Tooltip("Segundos rodando por la madera")]
    [SerializeField] private float minDuration = 4f;
    [SerializeField] private float maxDuration = 6f;

    [Header("Botes en las casillas")]
    [SerializeField] private float pocketRadius = 0.8f;
    [Tooltip("Altura de la bola sobre las casillas, medida desde WheelPivot")]
    [SerializeField] private float ballHeight = 0.05f;
    [Tooltip("Grados que avanza el primer bote (de la madera a los colores)")]
    [SerializeField] private float firstHopDegrees = 60f;
    [SerializeField] private float firstHopHeight = 0.06f;
    [SerializeField] private float firstHopDuration = 0.3f;
    [Tooltip("Cuánto se reduce cada bote respecto al anterior (0.5 = la mitad)")]
    [SerializeField, Range(0.2f, 0.9f)] private float bounceDecay = 0.55f;
    [SerializeField] private int minBounces = 2;
    [SerializeField] private int maxBounces = 4;
    [Tooltip("Segundos que la bola sigue girando con la rueda antes de desaparecer")]
    [SerializeField] private float ballVisibleAfterLanding = 3f;

    [Header("Spawner de tickets")]
    [SerializeField] private TicketSpawner ticketSpawner;

    [Header("Botella (para saber si la tienes en la mano)")]
    [SerializeField] private BottleHolder bottleHolder;

    [Header("Eventos (opcionales, para sonidos/VFX)")]
    public UnityEvent onSpinStart;
    public UnityEvent onBallBounce;
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

        // Ángulos sobre la rueda: sentido horario desde la flecha azul de wheelCenter.
        // El jitter es pequeño para que la bola nunca quede en el borde entre dos casillas.
        float jitter = UnityEngine.Random.Range(-0.25f, 0.25f) * StepAngle;
        float targetAngle = (winner + 0.5f) * StepAngle + jitter;

        // Botes planificados: cada uno más corto, más bajo y más rápido que el anterior
        int hops = UnityEngine.Random.Range(minBounces, maxBounces + 1) + 1;
        float[] hopDegrees = new float[hops];
        float[] hopHeights = new float[hops];
        float[] hopDurations = new float[hops];
        float plannedTravel = 0f;
        for (int i = 0; i < hops; i++)
        {
            float f = Mathf.Pow(bounceDecay, i);
            hopDegrees[i] = firstHopDegrees * f;
            hopHeights[i] = firstHopHeight * f;
            hopDurations[i] = firstHopDuration * Mathf.Sqrt(f);
            plannedTravel += hopDegrees[i];
        }

        // 1) Rueda por la madera. La madera no gira, así que se usa un marco fijo
        Quaternion rimFrame = wheelCenter.rotation;
        float rimAngle = UnityEngine.Random.Range(0f, 360f);
        float rimTime = UnityEngine.Random.Range(minDuration, maxDuration);

        ball.position = RimPoint(rimFrame, rimAngle);
        ball.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < rimTime)
        {
            elapsed += Time.deltaTime;
            float speed = Mathf.Lerp(rimStartSpeed, rimEndSpeed, elapsed / rimTime);
            rimAngle -= speed * Time.deltaTime;   // antihorario
            ball.position = RimPoint(rimFrame, rimAngle);
            yield return null;
        }

        // Sigue rodando a velocidad final hasta el punto desde el que los botes
        // acaban justo en la casilla ganadora
        float prevGap = DropGap(targetAngle, plannedTravel);
        float wait = 0f;
        while (wait < 5f)
        {
            wait += Time.deltaTime;
            rimAngle -= rimEndSpeed * Time.deltaTime;
            ball.position = RimPoint(rimFrame, rimAngle);

            float gap = DropGap(targetAngle, plannedTravel);
            if (gap > prevGap + 180f) break;   // acaba de pasar el punto de caída
            prevGap = gap;
            yield return null;
        }

        // 2) Bote de la madera a los colores y botes entre casillas (medido sobre la rueda)
        float fromAngle = BallWheelAngle();
        float travel = Mathf.Repeat(fromAngle - targetAngle, 360f);
        travel += 360f * Mathf.Max(0f, Mathf.Round((plannedTravel - travel) / 360f));
        float scale = plannedTravel > 0f ? travel / plannedTravel : 1f;

        float fromRadius = outerRadius;
        float fromHeight = rimHeight;
        for (int i = 0; i < hops; i++)
        {
            float toAngle = fromAngle - hopDegrees[i] * scale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / hopDurations[i];
                float k = Mathf.Clamp01(t);

                float angle = Mathf.Lerp(fromAngle, toAngle, k);
                float radius = Mathf.Lerp(fromRadius, pocketRadius, k);
                float height = Mathf.Lerp(fromHeight, ballHeight, k) + 4f * hopHeights[i] * k * (1f - k);
                ball.position = WheelPoint(angle, radius, height);
                yield return null;
            }

            onBallBounce?.Invoke();
            fromAngle = toAngle;
            fromRadius = pocketRadius;
            fromHeight = ballHeight;
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

        // 3) La bola se queda en su casilla y gira con la rueda
        elapsed = 0f;
        while (elapsed < ballVisibleAfterLanding)
        {
            elapsed += Time.deltaTime;
            ball.position = WheelPoint(targetAngle, pocketRadius, ballHeight);
            yield return null;
        }

        ball.gameObject.SetActive(false);
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

    // Grados (sobre la rueda) que le faltan a la bola para llegar al punto ideal de caída
    private float DropGap(float targetAngle, float plannedTravel)
    {
        float remaining = Mathf.Repeat(BallWheelAngle() - targetAngle, 360f);
        return Mathf.Repeat(remaining - plannedTravel, 360f);
    }

    // Ángulo de la bola medido sobre la rueda (sentido horario desde la flecha azul)
    private float BallWheelAngle()
    {
        Vector3 d = Quaternion.Inverse(wheelCenter.rotation) * (ball.position - wheelCenter.position);
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    private static Vector3 Dir(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    // Punto sobre la madera (no gira con la rueda)
    private Vector3 RimPoint(Quaternion frame, float angleDeg)
    {
        return wheelCenter.position + frame * Dir(angleDeg) * outerRadius + wheelCenter.up * rimHeight;
    }

    // Punto sobre la rueda (gira con ella)
    private Vector3 WheelPoint(float angleDeg, float radius, float height)
    {
        return wheelCenter.position + wheelCenter.rotation * Dir(angleDeg) * radius + wheelCenter.up * height;
    }

    private void OnDrawGizmosSelected()
    {
        if (segments == null || segments.Length == 0 || wheelCenter == null) return;

        // Casillas: una bolita de color en el centro de cada una
        for (int i = 0; i < segments.Length; i++)
        {
            // Alpha forzado a 1 para que se vean aunque el color tenga transparencia
            Color c = segments[i].color;
            Gizmos.color = new Color(c.r, c.g, c.b, 1f);
            Gizmos.DrawSphere(WheelPoint((i + 0.5f) * StepAngle, pocketRadius, ballHeight), 0.04f);
        }

        // Madera: bolitas blancas por donde rueda la bola al principio
        Gizmos.color = Color.white;
        for (int i = 0; i < 12; i++)
            Gizmos.DrawWireSphere(RimPoint(wheelCenter.rotation, i * 30f), 0.04f);
    }
}