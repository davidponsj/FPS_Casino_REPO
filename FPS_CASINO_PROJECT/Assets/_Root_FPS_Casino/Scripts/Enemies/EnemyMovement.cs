using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Todo lo relativo a CÓMO se mueve un enemigo. Es la única pieza que habla con el NavMeshAgent:
/// las IAs piden "ve allí" o "para", y este componente decide cómo hacerlo.
///
///  - Navegación: ir a un punto con repath limitado, parar, girar, colocar (Warp), detectar atascos.
///  - Sensación arcade: aceleración y giro altos, variación individual de velocidad.
///  - Cuerpo: radio, peso y colliders que usa el CrowdSolver para el contacto físico.
///    El enemigo nunca resuelve colisiones por su cuenta: se apunta al CrowdSolver y él lo corrige.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Sensación arcade")]
    [SerializeField] private float baseSpeed = 3.5f;

    [Tooltip("Variación individual de velocidad (0.1 = ±10%). Evita el efecto clon.")]
    [SerializeField, Range(0f, 0.3f)] private float speedVariation = 0.1f;

    [Tooltip("Aceleración alta = reacciona casi al instante cuando el jugador cambia de dirección.")]
    [SerializeField] private float acceleration = 30f;

    [Tooltip("Velocidad de giro mientras se mueve (grados por segundo). Alta = gira casi en seco, baja = curvas amplias.")]
    [FormerlySerializedAs("angularSpeed")]
    [SerializeField] private float turnSpeed = 540f;

    [Tooltip("Distancia a la que se considera que ha llegado al destino.")]
    [SerializeField] private float stoppingDistance = 0.2f;

    [Header("Avoidance")]
    [Tooltip("Cada enemigo recibe una prioridad aleatoria en este rango. Menor número = más prioridad.")]
    [SerializeField] private Vector2Int avoidancePriorityRange = new Vector2Int(30, 70);

    [Tooltip("Prioridad cuando está anclado (pegado al jugador). Más baja que el rango normal: los que pasan le rodean.")]
    [SerializeField, Range(0, 99)] private int anchoredPriority = 10;

    [Header("Cuerpo")]
    [Tooltip("Peso cuando está anclado: cede menos ante otros enemigos y ante los empujones del jugador.")]
    [SerializeField] private float anchoredWeight = 3f;

    [Header("Control de repath")]
    [Tooltip("Tiempo medio entre recálculos de ruta (cada enemigo lo varía un ±20%).")]
    [SerializeField] private float repathInterval = 0.2f;

    [Tooltip("Si el objetivo se ha movido menos que esto, no se recalcula la ruta.")]
    [SerializeField] private float minTargetMoveDistance = 0.5f;

    [Tooltip("Radio de búsqueda del punto más cercano sobre el NavMesh.")]
    [SerializeField] private float sampleDistance = 2f;

    [Header("Detección de atasco")]
    [SerializeField] private float stuckCheckInterval = 1f;

    [Tooltip("Si en el intervalo avanza menos que esto teniendo que moverse, se considera atascado.")]
    [SerializeField] private float stuckMinProgress = 0.3f;

    private NavMeshAgent agent;
    private Collider[] colliders = Array.Empty<Collider>();

    private NavMeshPath lanePath;
    private readonly Vector3[] cornerBuffer = new Vector3[32];

    private float individualSpeedFactor = 1f; // fijo por enemigo (aleatorio al nacer)
    private float speedMultiplier = 1f;       // lo modifica quien quiera (p. ej. velocidad adaptativa)
    private int basePriority;
    private ObstacleAvoidanceType baseAvoidanceType;
    private float weight = 1f;
    private Vector3 pendingShove;

    private float nextRepathTime;
    private Vector3 lastRequestedTarget;
    private bool hasTarget;

    private float nextStuckCheckTime;
    private Vector3 lastStuckCheckPosition;

    /// <summary>Se lanza cuando se detecta un atasco, tras intentar recalcular la ruta.</summary>
    public event Action Stuck;

    // ---------------------------------------------------------------
    //  Propiedades
    // ---------------------------------------------------------------

    public NavMeshAgent Agent => agent;

    /// <summary>Velocidad base de este enemigo (sin multiplicador externo).</summary>
    public float BaseSpeed
    {
        get => baseSpeed;
        set { baseSpeed = value; ApplySpeed(); }
    }

    /// <summary>Multiplicador externo de velocidad (1 = normal). Útil para acercar a los rezagados.</summary>
    public float SpeedMultiplier
    {
        get => speedMultiplier;
        set { speedMultiplier = value; ApplySpeed(); }
    }

    /// <summary>Velocidad real actual (útil para el Animator).</summary>
    public float CurrentSpeed => agent.velocity.magnitude;

    public bool IsStopped => agent.isOnNavMesh && agent.isStopped;

    /// <summary>Distancia de parada del agente. La IA puede cambiarla según su estado.</summary>
    public float StoppingDistance
    {
        get => agent.stoppingDistance;
        set => agent.stoppingDistance = value;
    }

    public bool HasReachedDestination =>
        hasTarget &&
        !agent.pathPending &&
        agent.remainingDistance <= agent.stoppingDistance;

    /// <summary>True si el destino actual no es alcanzable (el NavMesh solo llega al punto más cercano).</summary>
    public bool PathIsPartial =>
        hasTarget &&
        !agent.pathPending &&
        agent.pathStatus == NavMeshPathStatus.PathPartial;

    /// <summary>
    /// Posición de los pies sobre el NavMesh. El pivote de una cápsula está en su centro
    /// (Base Offset del agente), así que transform.position queda ~1 m por encima del suelo.
    /// </summary>
    public Vector3 NavPosition => transform.position - Vector3.up * agent.baseOffset;

    /// <summary>Radio del cuerpo en el plano (el del agente, escalado).</summary>
    public float Radius => agent.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

    /// <summary>Peso relativo para el CrowdSolver: con más peso, cede menos.</summary>
    public float Weight => weight;

    public Collider[] Colliders => colliders;

    // ---------------------------------------------------------------
    //  Ciclo de vida
    // ---------------------------------------------------------------

    private void Reset()
    {
        ConfigureRigidbody();
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>();
        lanePath = new NavMeshPath();
        ConfigureRigidbody();

        // Personalidad física individual: cada enemigo nace ligeramente distinto.
        individualSpeedFactor = 1f + UnityEngine.Random.Range(-speedVariation, speedVariation);
        basePriority = UnityEngine.Random.Range(avoidancePriorityRange.x, avoidancePriorityRange.y + 1);
        agent.avoidancePriority = basePriority;
        baseAvoidanceType = agent.obstacleAvoidanceType;

        agent.acceleration = acceleration;
        agent.angularSpeed = turnSpeed;
        agent.stoppingDistance = stoppingDistance;
        ApplySpeed();

        // Desfase inicial aleatorio para que no recalculen todos en el mismo frame.
        nextRepathTime = Time.time + UnityEngine.Random.value * repathInterval;
        nextStuckCheckTime = Time.time + stuckCheckInterval;
    }

    private void OnEnable()
    {
        CrowdSolver.Register(this);
    }

    private void OnDisable()
    {
        CrowdSolver.Unregister(this);
    }

    private void Update()
    {
        if (!agent.isOnNavMesh) return;

        CheckStuck();
    }

    // ---------------------------------------------------------------
    //  API para la IA
    // ---------------------------------------------------------------

    /// <summary>
    /// Pide ir hacia un punto. Puedes llamarlo cada frame: internamente limita
    /// la frecuencia de recálculo y lo ignora si el objetivo casi no se ha movido.
    /// Devuelve false si no se ha podido (agente o punto fuera del NavMesh).
    /// </summary>
    public bool MoveTo(Vector3 target, bool force = false)
    {
        if (!agent.isOnNavMesh) return false;

        agent.isStopped = false;

        if (!force)
        {
            if (Time.time < nextRepathTime)
                return true;

            bool targetBarelyMoved =
                (target - lastRequestedTarget).sqrMagnitude <
                minTargetMoveDistance * minTargetMoveDistance;

            if (hasTarget && targetBarelyMoved)
                return true;
        }

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            return false;

        agent.SetDestination(hit.position);

        lastRequestedTarget = target;
        nextRepathTime = Time.time + repathInterval * UnityEngine.Random.Range(0.8f, 1.2f);
        hasTarget = true;
        return true;
    }

    /// <summary>
    /// Sigue la ruta hacia target, pero por su propio carril: desplazado laneOffset metros a un lado de la ruta
    /// (+ derecha, − izquierda). Técnica de "zanahoria": se calcula la ruta completa, se toma un punto
    /// lookahead metros por delante, se desplaza en perpendicular y ese es el destino inmediato.
    /// Resultado: varios enemigos avanzan en paralelo, lado a lado, en vez de en fila india.
    /// El desplazamiento nunca atraviesa paredes (se recorta contra el borde del NavMesh).
    /// </summary>
    public bool MoveAlongLane(Vector3 target, float laneOffset, float lookahead)
    {
        if (!agent.isOnNavMesh) return false;
        if (Mathf.Abs(laneOffset) < 0.05f) return MoveTo(target);

        agent.isStopped = false;

        if (hasTarget && Time.time < nextRepathTime)
            return true;

        if (!NavMesh.SamplePosition(target, out NavMeshHit targetHit, sampleDistance, NavMesh.AllAreas))
            return false;

        if (!NavMesh.CalculatePath(NavPosition, targetHit.position, NavMesh.AllAreas, lanePath) ||
            lanePath.status == NavMeshPathStatus.PathInvalid)
            return false;

        int count = lanePath.GetCornersNonAlloc(cornerBuffer);
        if (count < 2) return MoveTo(target);

        // Punto a 'lookahead' metros recorriendo la ruta, y dirección del tramo en ese punto.
        Vector3 carrot = cornerBuffer[count - 1];
        Vector3 direction = cornerBuffer[count - 1] - cornerBuffer[count - 2];
        float remaining = lookahead;

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 segment = cornerBuffer[i + 1] - cornerBuffer[i];
            float length = segment.magnitude;
            if (length < 0.001f) continue;

            direction = segment;

            if (remaining <= length)
            {
                carrot = cornerBuffer[i] + segment / length * remaining;
                break;
            }

            remaining -= length;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return MoveTo(target);

        Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
        Vector3 shifted = carrot + right * laneOffset;

        // Si el carril choca con una pared, se queda un poco antes del borde.
        if (NavMesh.Raycast(carrot, shifted, out NavMeshHit wallHit, NavMesh.AllAreas))
            shifted = Vector3.Lerp(carrot, wallHit.position, 0.8f);

        agent.SetDestination(shifted);

        // Guardamos el destino real: así un MoveTo posterior al jugador no se lo salta por "no haberse movido".
        lastRequestedTarget = shifted;
        nextRepathTime = Time.time + repathInterval * UnityEngine.Random.Range(0.8f, 1.2f);
        hasTarget = true;
        return true;
    }

    /// <summary>Detiene al enemigo y olvida su destino.</summary>
    public void Stop()
    {
        if (!agent.isOnNavMesh) return;

        agent.ResetPath();
        agent.isStopped = true;
        hasTarget = false;
    }

    /// <summary>Vuelve a la distancia de parada configurada en el Inspector.</summary>
    public void ResetStoppingDistance()
    {
        agent.stoppingDistance = stoppingDistance;
    }

    /// <summary>
    /// Anclado = pegado al jugador. Más prioridad de avoidance y más peso:
    /// los que pasan le rodean y el jugador tiene que empujar más para apartarle.
    /// Además apaga su avoidance (RVO): pegado al jugador, la avoidance lo hace deslizarse de lado
    /// alrededor de sus vecinos y acaba dando vueltas. El contacto ya lo resuelve el CrowdSolver.
    /// </summary>
    public void SetAnchored(bool anchored)
    {
        agent.avoidancePriority = anchored ? anchoredPriority : basePriority;
        agent.obstacleAvoidanceType = anchored ? ObstacleAvoidanceType.NoObstacleAvoidance : baseAvoidanceType;
        weight = anchored ? anchoredWeight : 1f;
    }

    /// <summary>
    /// Rota hacia un punto sin moverse. El agente no rota solo cuando está parado.
    /// smoothing es un factor de suavizado (más alto = gira antes), no grados por segundo.
    /// </summary>
    public void FaceTowards(Vector3 point, float smoothing = 8f)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing * Time.deltaTime);
    }

    /// <summary>Coloca al enemigo en un punto (para los spawners). Usar esto, no transform.position.</summary>
    public bool Warp(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            return false;

        hasTarget = false;
        lastStuckCheckPosition = hit.position;
        return agent.Warp(hit.position);
    }

    // ---------------------------------------------------------------
    //  API para el CrowdSolver
    // ---------------------------------------------------------------

    /// <summary>El jugador le está empujando (velocidad en m/s). Se queda con el empujón más fuerte del frame.</summary>
    public void AddShove(Vector3 velocity)
    {
        if (velocity.sqrMagnitude > pendingShove.sqrMagnitude)
            pendingShove = velocity;
    }

    public Vector3 ConsumeShove()
    {
        Vector3 shove = pendingShove;
        pendingShove = Vector3.zero;
        return shove;
    }

    /// <summary>Aplica una corrección de posición. agent.Move nunca saca al enemigo del NavMesh.</summary>
    public void ApplyCorrection(Vector3 delta)
    {
        if (agent.isOnNavMesh)
            agent.Move(delta);
    }

    // ---------------------------------------------------------------
    //  Interno
    // ---------------------------------------------------------------

    /// <summary>
    /// Un collider que se mueve cada frame debe llevar un Rigidbody cinemático: es lo que recomienda
    /// Unity para colliders en movimiento. Cinemático = el jugador choca con él pero no lo desplaza por física.
    /// </summary>
    private void ConfigureRigidbody()
    {
        if (!TryGetComponent(out Rigidbody body))
            body = gameObject.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
    }

    private void ApplySpeed()
    {
        if (agent == null) return;
        agent.speed = baseSpeed * individualSpeedFactor * speedMultiplier;
    }

    /// <summary>
    /// Detecta si el enemigo debería estar avanzando pero no lo hace.
    /// Primer remedio: recalcular la ruta ignorando los límites. Después avisa con el evento Stuck.
    /// </summary>
    private void CheckStuck()
    {
        if (Time.time < nextStuckCheckTime) return;
        nextStuckCheckTime = Time.time + stuckCheckInterval;

        bool shouldBeMoving =
            hasTarget &&
            !agent.isStopped &&
            !agent.pathPending &&
            agent.remainingDistance > agent.stoppingDistance + 0.5f;

        if (shouldBeMoving)
        {
            float moved = (transform.position - lastStuckCheckPosition).magnitude;

            if (moved < stuckMinProgress)
            {
                MoveTo(lastRequestedTarget, force: true);
                Stuck?.Invoke();
            }
        }

        lastStuckCheckPosition = transform.position;
    }
}