using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Todo lo relativo a CÓMO se mueve un enemigo. Es la única pieza que habla con el NavMeshAgent:
/// las IAs piden "ve allí" o "para", y este componente decide cómo hacerlo.
///
///  - Navegación: ir a un punto con repath limitado, parar, girar, colocar (Warp), detectar atascos.
///  - Sensación arcade: aceleración y giro altos, variación individual de velocidad.
///  - Cuerpo: radio, peso y colliders que usa el HordeCollision para el contacto físico.
///    El enemigo nunca resuelve colisiones por su cuenta: se apunta al HordeCollision y él lo corrige.
///  - Ralentización (IMovementModifiable): cada charco pone su multiplicador y manda el más lento.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour, IMovementModifiable
{
    [Header("Sensación arcade")]
    [Tooltip("Velocidad en m/s antes de la variación individual y de los charcos. 3.5 = andando rápido; 7 = corriendo.")]
    [Min(0f)]
    [SerializeField] private float baseSpeed = 3.5f;

    [Tooltip("Variación individual de velocidad (0.1 = ±10%). Evita el efecto clon.")]
    [SerializeField, Range(0f, 0.3f)] private float speedVariation = 0.1f;

    [Tooltip("Aceleración en m/s². Alta = reacciona casi al instante cuando el jugador cambia de dirección. 30 = arcade.")]
    [Min(0f)]
    [SerializeField] private float acceleration = 30f;

    [Tooltip("Velocidad de giro mientras se mueve (grados por segundo). Alta = gira casi en seco, baja = curvas amplias. 540 = casi en seco.")]
    [FormerlySerializedAs("angularSpeed")]
    [Min(0f)]
    [SerializeField] private float turnSpeed = 540f;

    [Tooltip("Metros al destino a los que se considera que ha llegado. 0.2 = llega justo.")]
    [Min(0f)]
    [SerializeField] private float stoppingDistance = 0.2f;

    [Header("Avoidance")]
    [Tooltip("Cada enemigo recibe una prioridad aleatoria en este rango. Menor número = más prioridad.")]
    [SerializeField] private Vector2Int avoidancePriorityRange = new Vector2Int(30, 70);

    [Tooltip("Prioridad cuando está anclado (pegado al jugador). Más baja que el rango normal: los que pasan le rodean.")]
    [SerializeField, Range(0, 99)] private int anchoredPriority = 10;

    [Header("Cuerpo")]
    [Tooltip("Peso cuando está anclado (el normal es 1): cede menos ante otros enemigos y ante los empujones del jugador. 3 = cuesta apartarle.")]
    [Min(1f)]
    [SerializeField] private float anchoredWeight = 3f;

    [Header("Control de repath")]
    [Tooltip("Segundos de media entre recálculos de ruta (cada enemigo lo varía un ±20%). 0.2 = sigue bien al jugador sin gastar mucho.")]
    [FormerlySerializedAs("repathInterval")]
    [Min(0.02f)]
    [SerializeField] private float repathSeconds = 0.2f;

    [Tooltip("Metros: si el objetivo se ha movido menos que esto, no se recalcula la ruta. 0.5 = ignora pasitos.")]
    [Min(0f)]
    [SerializeField] private float minTargetMoveDistance = 0.5f;

    [Tooltip("Metros de búsqueda del punto más cercano sobre el NavMesh. 2 = vale para puntos un poco fuera.")]
    [Min(0.1f)]
    [SerializeField] private float sampleDistance = 2f;

    [Header("Detección de atasco")]
    [Tooltip("Segundos entre comprobaciones de atasco. 1 = detecta rápido sin falsos avisos.")]
    [FormerlySerializedAs("stuckCheckInterval")]
    [Min(0.1f)]
    [SerializeField] private float stuckCheckSeconds = 1f;

    [Tooltip("Metros: si en ese tiempo avanza menos que esto teniendo que moverse, se considera atascado y recalcula la ruta.")]
    [Min(0f)]
    [SerializeField] private float stuckMinProgress = 0.3f;

    [Header("Depuración")]
    [Tooltip("Muestra la velocidad, el multiplicador de los charcos, la ruta del NavMesh y una marca roja si está atascado, en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private NavMeshAgent agent;
    private Collider[] colliders = Array.Empty<Collider>();

    private NavMeshPath lanePath;
    private readonly Vector3[] cornerBuffer = new Vector3[32];

    private float individualSpeedFactor = 1f; // fijo por enemigo (aleatorio al nacer)
    // Multiplicador de cada charco que pisa (clave = el charco). Solo cambia al entrar o salir.
    private readonly Dictionary<object, float> slowMultipliers = new Dictionary<object, float>();
    private float slowMultiplier = 1f;
    private int basePriority;
    private ObstacleAvoidanceType baseAvoidanceType;
    private float weight = 1f;
    private Vector3 pendingShove;

    private float nextRepathTime;
    private Vector3 lastRequestedTarget;
    private bool hasTarget;

    private float nextStuckCheckTime;
    private Vector3 lastStuckCheckPosition;
    private bool isStuck;

    // ---------------------------------------------------------------
    //  Propiedades
    // ---------------------------------------------------------------

    /// <summary>Multiplicador de los charcos que pisa: el más bajo (1 = normal, 0 = paralizado).</summary>
    public float SlowMultiplier => slowMultiplier;

    /// <summary>True si tiene un destino pedido (para la depuración).</summary>
    public bool HasDestination => hasTarget && agent != null && agent.hasPath;

    /// <summary>Destino actual del agente.</summary>
    public Vector3 Destination => agent.destination;

    public bool IsStopped => agent.isOnNavMesh && agent.isStopped;

    /// <summary>Distancia de parada del agente. La IA puede cambiarla según su estado.</summary>
    public float StoppingDistance
    {
        get => agent.stoppingDistance;
        set => agent.stoppingDistance = value;
    }

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

    /// <summary>Peso relativo para el HordeCollision: con más peso, cede menos.</summary>
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
        nextRepathTime = Time.time + UnityEngine.Random.value * repathSeconds;
        nextStuckCheckTime = Time.time + stuckCheckSeconds;
    }

    private void OnEnable()
    {
        HordeCollision.Register(this);
    }

    private void OnDisable()
    {
        HordeCollision.Unregister(this);
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
        nextRepathTime = Time.time + repathSeconds * UnityEngine.Random.Range(0.8f, 1.2f);
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
        nextRepathTime = Time.time + repathSeconds * UnityEngine.Random.Range(0.8f, 1.2f);
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
    /// alrededor de sus vecinos y acaba dando vueltas. El contacto ya lo resuelve el HordeCollision.
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
    //  IMovementModifiable (charcos)
    // ---------------------------------------------------------------

    public void SetSpeedMultiplier(object source, float multiplier)
    {
        slowMultipliers[source] = Mathf.Clamp01(multiplier);
        RecalculateSlow();
    }

    public void ClearSpeedMultiplier(object source)
    {
        if (slowMultipliers.Remove(source))
            RecalculateSlow();
    }

    private void RecalculateSlow()
    {
        slowMultiplier = 1f;
        foreach (float multiplier in slowMultipliers.Values)
            slowMultiplier = Mathf.Min(slowMultiplier, multiplier);
        ApplySpeed();
    }

    // ---------------------------------------------------------------
    //  API para el HordeCollision
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
        agent.speed = baseSpeed * individualSpeedFactor * slowMultiplier;
    }

    /// <summary>
    /// Detecta si el enemigo debería estar avanzando pero no lo hace.
    /// Remedio: recalcular la ruta ignorando los límites de repath.
    /// </summary>
    private void CheckStuck()
    {
        if (Time.time < nextStuckCheckTime) return;
        nextStuckCheckTime = Time.time + stuckCheckSeconds;
        isStuck = false;

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
                isStuck = true;
                MoveTo(lastRequestedTarget, force: true);
            }
        }

        lastStuckCheckPosition = transform.position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || agent == null) return;

        string slow = slowMultiplier < 1f ? $" · charco ×{slowMultiplier:0.0#}" : "";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
            $"velocidad {agent.velocity.magnitude:0.0} / {agent.speed:0.0} m/s{slow}");

        if (agent.hasPath)
        {
            Vector3[] corners = agent.path.corners; // solo en el editor: aquí no importa crear el array
            Gizmos.color = Color.cyan;
            for (int i = 0; i < corners.Length - 1; i++)
                Gizmos.DrawLine(corners[i], corners[i + 1]);
        }

        if (isStuck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.6f, 0.25f);
        }
    }
#endif
}
