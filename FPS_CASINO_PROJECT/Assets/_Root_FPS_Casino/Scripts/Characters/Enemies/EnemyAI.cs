using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// IA del enemigo (el zombi). Conoce siempre la posición del jugador y va a por él.
///
///   Chase    → lejos: corre hacia el jugador.
///   Approach → cerca: va hacia su puesto del corro, solo para repartirse y no llegar todos por el mismo lado.
///   Engage   → tiene ficha de ataque: se pega al jugador a distancia de melee y le sigue. Nunca retrocede.
///   Queue    → la horda está llena (fichas y puestos ocupados): se queda detrás, sin retroceder,
///              hasta que se libere un hueco. Con menos de ~16 zombis no debería verse.
///
/// Regla de oro: un zombi nunca se aleja del jugador para "recolocarse". Solo avanza o espera.
/// El contacto físico (no solaparse, no empujar al jugador) no es cosa de la IA: lo resuelve el HordeCollision.
///
/// Los estados son clases anidadas: viven en este archivo y pueden usar los campos privados de EnemyAI.
/// Todo el movimiento pasa por EnemyMovement (nunca toca el NavMeshAgent directamente).
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public class EnemyAI : MonoBehaviour
{
    private static Transform cachedPlayer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

    [Header("Visual")]
    [Tooltip("Renderer del cuerpo, para los cambios de color por estado. Vacío = el primero que encuentre.")]
    [SerializeField] private Renderer bodyRenderer;

    [Header("Persecución (carriles: de frente, pero lado a lado)")]
    [Tooltip("Metros que se separa como mucho un zombi de la ruta central. El frente de la horda mide el doble. 0 = fila india, 1.5 = horda ancha.")]
    [Min(0f)]
    [SerializeField] private float laneWidth = 1.5f;
    [Tooltip("Metros por delante, siguiendo la ruta, a los que apunta cada zombi. Alto = trayectorias más rectas y suaves; bajo = pegadas a la ruta en las esquinas. 4 = equilibrado.")]
    [Min(0.5f)]
    [SerializeField] private float laneLookahead = 4f;
    [Tooltip("Metros al jugador a partir de los que cada zombi va por su carril completo. 9 = la horda se abre lejos.")]
    [Min(0f)]
    [SerializeField] private float laneFullDistance = 9f;
    [Tooltip("Metros al jugador por debajo de los que los carriles se cierran y van directos a él. Menor que Lane Full Distance. 5 = convergen cerca.")]
    [Min(0f)]
    [SerializeField] private float laneZeroDistance = 5f;
    [Tooltip("Metros de oscilación suave dentro del carril. 0 = trayectoria limpia, 0.4 = se nota sin ser nervioso.")]
    [Min(0f)]
    [SerializeField] private float wobbleAmplitude = 0.4f;
    [Tooltip("Velocidad de esa oscilación. Bajo = curvas largas, alto = zigzag nervioso. 0.25 = curvas largas.")]
    [Min(0f)]
    [SerializeField] private float wobbleFrequency = 0.25f;

    [Header("Melee")]
    [Tooltip("Metros al jugador a los que intenta pegarse (pide ficha de ataque). 1.6 = casi tocándole.")]
    [Min(0f)]
    [SerializeField] private float engageRange = 1.6f;
    [Tooltip("Metros a los que se queda pegado. No bajar de la distancia de contacto del HordeCollision (~1.1 con radios 0.5).")]
    [Min(0f)]
    [SerializeField] private float meleeDistance = 1.15f;
    [Tooltip("Metros: si el jugador se aleja más que esto, suelta la ficha y vuelve a aproximarse. Mayor que Engage Range. 2.6 = aguanta un paso atrás.")]
    [Min(0f)]
    [SerializeField] private float engageExitRange = 2.6f;

    [Header("Aproximación")]
    [Tooltip("Metros de margen: se para cuando está tan cerca del jugador como su puesto (+ este margen). Así nunca retrocede.")]
    [Min(0f)]
    [SerializeField] private float holdTolerance = 0.2f;
    [Tooltip("Metros de histéresis: vuelve a moverse si se queda más lejos que su puesto + este margen. Mayor que Hold Tolerance.")]
    [Min(0f)]
    [SerializeField] private float resumeTolerance = 0.6f;
    [Tooltip("Segundos entre comprobaciones de si hay un puesto claramente mejor (calcula rutas, no conviene hacerlo cada frame). 0.5 = reacciona rápido sin gastar.")]
    [FormerlySerializedAs("reevaluateInterval")]
    [Min(0.05f)]
    [SerializeField] private float reevaluateSeconds = 0.5f;

    [Header("Estados")]
    [Tooltip("Segundos mínimos en un estado antes de cambiar. Evita que tiemble entre dos estados. 0.3 = no se nota.")]
    [FormerlySerializedAs("stateMinDuration")]
    [Min(0f)]
    [SerializeField] private float stateMinSeconds = 0.3f;
    [Tooltip("Metros de parada si el jugador no tiene HordeAttackSlots (persigue directo).")]
    [Min(0f)]
    [SerializeField] private float fallbackStoppingDistance = 1.5f;

    [Header("Feedback visual")]
    [Tooltip("Color del cuerpo mientras está pegado al jugador.")]
    [FormerlySerializedAs("inPositionColor")]
    [SerializeField] private Color engagedColor = new Color(1f, 0.55f, 0f);

    [Header("Depuración")]
    [Tooltip("Muestra sobre la cabeza el estado, los segundos en él y la distancia al jugador, y una línea hasta su destino, en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private StateMachine<EnemyAI> machine;
    private HordeAttackSlots slots;
    private MaterialPropertyBlock propertyBlock;
    private Color baseColor = Color.white;

    private ChaseState chase;
    private ApproachState approach;
    private EngageState engage;
    private QueueState queue;

    // Reparto de carriles: con la proporción áurea, zombis consecutivos caen en carriles bien separados
    // (no al azar, donde varios podrían caer en el mismo).
    private static int laneCounter;

    private float personalLane; // -1..1, fijo por zombi: su carril respecto a la ruta central
    private float wobbleSeed;

    public EnemyMovement Movement { get; private set; }
    public Transform Target { get; private set; }

    /// <summary>Nombre del estado actual, para depuración.</summary>
    public string CurrentStateName => machine?.Current?.Name ?? "None";

    public float DistanceToTarget =>
        Target == null ? float.PositiveInfinity : FlatDistance(transform.position, Target.position);

    // ---------------------------------------------------------------
    //  Ciclo de vida
    // ---------------------------------------------------------------

    private void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        SetupVisual();

        machine = new StateMachine<EnemyAI>();
        chase = new ChaseState(this);
        approach = new ApproachState(this);
        engage = new EngageState(this);
        queue = new QueueState(this);

        personalLane = Mathf.Repeat(laneCounter++ * 0.618034f, 1f) * 2f - 1f;
        wobbleSeed = Random.value * 100f;
    }

    private void Start()
    {
        Target = FindPlayer();

        if (Target == null)
            Debug.LogWarning($"{name}: no se encontró ningún objeto con el tag Player.", this);
        else
            slots = Target.GetComponent<HordeAttackSlots>();

        if (slots == null)
            Debug.LogWarning($"{name}: el jugador no tiene HordeAttackSlots. Persigue directamente (se apilará).", this);

        machine.ChangeState(chase, force: true);
    }

    private void Update()
    {
        machine.Tick();
    }

    private void OnDisable()
    {
        // Muerte, pooling o desactivación: puesto y ficha quedan libres para otro.
        if (slots != null)
            slots.ReleaseAll(this);
    }

    /// <summary>
    /// Busca al jugador una sola vez y lo comparte entre todos los enemigos
    /// (con 30 enemigos, una búsqueda en vez de 30).
    /// </summary>
    private static Transform FindPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) cachedPlayer = player.transform;
        }

        return cachedPlayer;
    }

    /// <summary>Distancia en el plano horizontal (ignora la altura).</summary>
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------
    //  Utilidades compartidas por los estados
    // ---------------------------------------------------------------

    /// <summary>Si está a distancia de melee y hay ficha libre, se pega al jugador.</summary>
    private bool TryStartEngage()
    {
        if (DistanceToTarget > engageRange || !slots.TryEngage(this)) return false;

        machine.ChangeState(engage, force: true);
        return true;
    }

    /// <summary>Pierde el contacto: busca puesto (Approach), hace cola (Queue) o, si está muy lejos, persigue (Chase).</summary>
    private void FallBack()
    {
        if (slots == null || DistanceToTarget > slots.ReleaseRange)
            machine.ChangeState(chase, force: true);
        else if (slots.TryAcquire(this))
            machine.ChangeState(approach, force: true);
        else
            machine.ChangeState(queue, force: true);
    }

    /// <summary>
    /// Carril actual del zombi en metros respecto a la ruta central. Lejos usa su carril completo;
    /// al acercarse se va cerrando hasta 0 para converger sobre el jugador.
    /// </summary>
    private float GetChaseLane()
    {
        float fade = Mathf.InverseLerp(laneZeroDistance, laneFullDistance, DistanceToTarget);
        if (fade <= 0f) return 0f;

        float wobble = (Mathf.PerlinNoise(wobbleSeed, Time.time * wobbleFrequency) * 2f - 1f) * wobbleAmplitude;
        return (personalLane * laneWidth + wobble) * fade;
    }

    /// <summary>
    /// Punto de melee en MI lado del jugador: jugador + (dirección del jugador hacia mí) × meleeDistance.
    /// Moverse hacia él es un movimiento puramente radial, así que el zombi nunca rodea al jugador.
    /// </summary>
    private Vector3 GetMeleePoint()
    {
        Vector3 fromTarget = transform.position - Target.position;
        fromTarget.y = 0f;

        if (fromTarget.sqrMagnitude < 0.001f)
        {
            fromTarget = -transform.forward;
            fromTarget.y = 0f;
        }

        return Target.position + fromTarget.normalized * meleeDistance;
    }

    /// <summary>Se para (una sola vez) y mira al jugador.</summary>
    private void HoldAndFace()
    {
        if (!Movement.IsStopped) Movement.Stop();
        Movement.FaceTowards(Target.position);
    }

    // ---------------------------------------------------------------
    //  Feedback visual
    // ---------------------------------------------------------------

    private void SetupVisual()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        if (bodyRenderer == null) return;

        propertyBlock = new MaterialPropertyBlock();

        Material material = bodyRenderer.sharedMaterial;
        if (material != null)
        {
            if (material.HasProperty(BaseColorId)) baseColor = material.GetColor(BaseColorId);
            else if (material.HasProperty(ColorId)) baseColor = material.GetColor(ColorId);
        }
    }

    /// <summary>Cambia el color del cuerpo sin crear materiales nuevos (MaterialPropertyBlock).</summary>
    private void SetBodyColor(Color color)
    {
        if (bodyRenderer == null) return;

        bodyRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        bodyRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>Naranja mientras está pegado al jugador; al soltarse vuelve al color del material.</summary>
    private void SetEngagedVisual(bool isEngaged)
    {
        SetBodyColor(isEngaged ? engagedColor : baseColor);
    }

    // ===============================================================
    //  ESTADOS
    // ===============================================================

    /// <summary>Persecución a distancia. Al entrar en el rango de reclamo pide puesto (Approach) o hace cola (Queue).</summary>
    private class ChaseState : State<EnemyAI>
    {
        public ChaseState(EnemyAI owner) : base(owner) { }

        public override float MinDuration => owner.stateMinSeconds;

        public override void Enter()
        {
            if (owner.slots == null)
                owner.Movement.StoppingDistance = owner.fallbackStoppingDistance;
        }

        public override void Tick()
        {
            if (owner.Target == null) return;

            // Sigue la ruta por su carril. Si no se puede calcular, va directo al jugador.
            if (!owner.Movement.MoveAlongLane(owner.Target.position, owner.GetChaseLane(), owner.laneLookahead))
                owner.Movement.MoveTo(owner.Target.position);

            HordeAttackSlots slots = owner.slots;
            if (slots == null || owner.DistanceToTarget > slots.ClaimRange) return;

            // Pedir puesto calcula rutas: solo cuando de verdad podemos cambiar de estado.
            if (!owner.machine.CanLeave) return;

            if (slots.TryAcquire(owner)) owner.machine.ChangeState(owner.approach);
            else owner.machine.ChangeState(owner.queue);
        }
    }

    /// <summary>
    /// Va hacia su puesto para repartirse alrededor del jugador. Si llega a distancia de melee, se pega (Engage).
    /// Si ya está tan cerca del jugador como su puesto, se queda quieto: nunca retrocede.
    /// </summary>
    private class ApproachState : State<EnemyAI>
    {
        private bool holding;
        private float nextReevaluateTime;

        public ApproachState(EnemyAI owner) : base(owner) { }

        public override float MinDuration => owner.stateMinSeconds;

        public override void Enter()
        {
            holding = false;
            nextReevaluateTime = Time.time + Random.Range(0.2f, owner.reevaluateSeconds);

            // Ruta nueva hacia el puesto ya: no arrastramos la anterior, que podría ser parcial.
            if (owner.slots != null && owner.slots.TryGetSlotPosition(owner, out Vector3 slotPosition))
                owner.Movement.MoveTo(slotPosition, force: true);
        }

        public override void Tick()
        {
            HordeAttackSlots slots = owner.slots;

            if (owner.Target == null || slots == null)
            {
                owner.machine.ChangeState(owner.chase, force: true);
                return;
            }

            if (owner.DistanceToTarget > slots.ReleaseRange)
            {
                slots.Release(owner);
                owner.machine.ChangeState(owner.chase, force: true);
                return;
            }

            if (owner.TryStartEngage()) return;

            if (Time.time >= nextReevaluateTime)
            {
                nextReevaluateTime = Time.time + owner.reevaluateSeconds;
                slots.Reevaluate(owner);
            }

            if (!slots.TryGetSlotPosition(owner, out Vector3 slotPosition))
            {
                owner.machine.ChangeState(owner.queue, force: true);
                return;
            }

            if (!holding && owner.Movement.PathIsPartial)
            {
                slots.Release(owner);
                owner.machine.ChangeState(owner.queue, force: true);
                return;
            }

            // Comparamos MI distancia al jugador con la de MI puesto al jugador.
            // Si ya estoy igual de cerca (o más), ir al puesto significaría retroceder u orbitar: me quedo.
            float myDistance = owner.DistanceToTarget;
            float slotDistance = FlatDistance(slotPosition, owner.Target.position);

            if (holding)
            {
                if (myDistance > slotDistance + owner.resumeTolerance)
                    holding = false;
            }
            else if (myDistance <= slotDistance + owner.holdTolerance)
            {
                holding = true;
            }

            if (holding) owner.HoldAndFace();
            else owner.Movement.MoveTo(slotPosition);
        }
    }

    /// <summary>
    /// Tiene ficha de ataque: se pega al jugador a distancia de melee y le sigue si se mueve un poco.
    /// No sigue ningún puesto, así que ni orbita ni retrocede. Anclado: los demás le rodean.
    /// </summary>
    private class EngageState : State<EnemyAI>
    {
        private bool closing;

        public EngageState(EnemyAI owner) : base(owner) { }

        public override float MinDuration => owner.stateMinSeconds;

        public override void Enter()
        {
            owner.SetEngagedVisual(true);
            owner.Movement.SetAnchored(true);

            // El destino ya es el punto de melee (no el centro del jugador), así que la parada es la normal.
            owner.Movement.ResetStoppingDistance();
            closing = owner.DistanceToTarget > owner.meleeDistance + 0.05f;
        }

        public override void Tick()
        {
            if (owner.Target == null || owner.slots == null)
            {
                owner.machine.ChangeState(owner.chase, force: true);
                return;
            }

            float distance = owner.DistanceToTarget;

            // El jugador se ha escapado: suelta la ficha para que la use otro.
            if (distance > owner.engageExitRange)
            {
                owner.slots.Disengage(owner);
                owner.FallBack();
                return;
            }

            // Histéresis: se acerca si se ha separado, se para al llegar a distancia de melee.
            if (closing)
            {
                if (distance <= owner.meleeDistance + 0.05f) closing = false;
            }
            else if (distance > owner.meleeDistance + 0.3f)
            {
                closing = true;
            }

            // Antes iba al centro del jugador: inalcanzable, así que la avoidance lo deslizaba de lado
            // alrededor de sus vecinos y daba vueltas. Ahora va a su punto de melee, en línea recta hacia dentro.
            if (closing) owner.Movement.MoveTo(owner.GetMeleePoint());
            else owner.HoldAndFace();
        }

        public override void Exit()
        {
            owner.SetEngagedVisual(false);
            owner.Movement.SetAnchored(false);
            owner.Movement.ResetStoppingDistance();
            owner.slots?.Disengage(owner);
        }
    }

    /// <summary>
    /// La horda está llena: se acerca hasta la cola y se queda ahí mirando al jugador.
    /// Si ya está más cerca que la cola, se queda donde está (no retrocede).
    /// </summary>
    private class QueueState : State<EnemyAI>
    {
        private float nextAcquireTime;

        public QueueState(EnemyAI owner) : base(owner) { }

        public override float MinDuration => owner.stateMinSeconds;

        public override void Enter()
        {
            // Desfase aleatorio: que no consulten todos a la vez.
            nextAcquireTime = Time.time + Random.Range(0.1f, 0.4f);
        }

        public override void Tick()
        {
            HordeAttackSlots slots = owner.slots;

            if (owner.Target == null || slots == null)
            {
                owner.machine.ChangeState(owner.chase, force: true);
                return;
            }

            if (owner.DistanceToTarget > slots.ReleaseRange)
            {
                owner.machine.ChangeState(owner.chase);
                return;
            }

            if (owner.TryStartEngage()) return;

            if (Time.time >= nextAcquireTime && owner.machine.CanLeave)
            {
                nextAcquireTime = Time.time + Random.Range(0.2f, 0.5f);

                if (slots.TryAcquire(owner))
                {
                    owner.machine.ChangeState(owner.approach);
                    return;
                }
            }

            // Hay fichas libres: no tiene sentido hacer cola, va directo a por el jugador
            // y se pegará en cuanto entre en Engage Range (aunque no haya conseguido puesto).
            if (slots.HasFreeToken)
                owner.Movement.MoveTo(owner.Target.position);
            else if (owner.DistanceToTarget > slots.QueueRadius + 0.5f)
                owner.Movement.MoveTo(slots.GetQueuePoint(owner));
            else
                owner.HoldAndFace();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || machine == null) return;

        string distance = Target != null ? $"{DistanceToTarget:0.0} m del jugador" : "sin jugador";
        DebugLabels.Above(transform, DebugLabels.EnemyAILine, $"{CurrentStateName} · {machine.TimeInState:0.0} s · {distance}", DebugLabels.AI);

        if (Movement != null && Movement.HasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, Movement.Destination);
        }
    }
#endif
}
