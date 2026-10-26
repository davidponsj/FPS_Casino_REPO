using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Coordina a la horda alrededor del objetivo (se añade AL JUGADOR, no a los enemigos).
/// Separa dos preguntas distintas:
///
///  1. ¿POR DÓNDE llego? → Puestos (slots). Solo sirven para repartir la aproximación,
///     para que no lleguen todos por el mismo lado ni se apilen. Al llegar, el enemigo deja de seguirlos.
///  2. ¿PUEDO ATACAR? → Fichas de ataque (tokens, la técnica de DOOM 2016). Solo maxAttackers
///     enemigos pueden estar pegados al jugador a la vez; el resto espera detrás, sin retroceder.
///
/// Anillos de puestos:
///  - Anillo 0 (ataque): pegado al jugador, exige paso libre en línea recta desde él.
///  - Anillos exteriores (presión): donde se quedan los que no caben delante o no pueden llegar
///    (p. ej. el jugador está en una zona cerrada: le rodean por fuera).
/// </summary>
public class HordeAttackSlots : MonoBehaviour
{
    [Serializable]
    private class Ring
    {
        [Min(1)] public int slotCount = 6;
        public float radius = 1.2f;

        [Tooltip("Exige paso libre en línea recta desde el jugador (sin pared entre medias). Úsalo en el anillo de ataque.")]
        public bool requireClearLine = true;
    }

    [Header("Fichas de ataque")]
    [Tooltip("Máximo de enemigos pegados al jugador a la vez. Los demás esperan detrás.")]
    [SerializeField, Min(1)] private int maxAttackers = 6;
    [Tooltip("Un puesto queda bloqueado si hay un enemigo atacando a menos de esta distancia de él.")]
    [SerializeField] private float engagedBlockRadius = 0.8f;

    [Header("Anillos (el primero es el de ataque, los siguientes de presión)")]
    [SerializeField]
    private Ring[] rings =
    {
        new Ring { slotCount = 6, radius = 1.2f, requireClearLine = true },
        new Ring { slotCount = 10, radius = 2.4f, requireClearLine = false },
    };

    [Tooltip("Cada puesto varía su radio ±esto (fijo por puesto).")]
    [SerializeField] private float radiusJitter = 0.1f;
    [Tooltip("Cada puesto varía su ángulo ±esto en grados (fijo por puesto).")]
    [SerializeField] private float angleJitter = 5f;
    [Tooltip("Giro de los anillos en grados/segundo. Déjalo en 0: si giran, los enemigos los persiguen y orbitan.")]
    [SerializeField] private float ringRotationSpeed = 0f;

    [Header("Preferencias")]
    [Tooltip("Penalización en metros por cada anillo hacia fuera. Alto = prefieren el anillo interior aunque les quede más lejos.")]
    [SerializeField] private float ringPenalty = 6f;

    [Header("Cola y alcance")]
    [Tooltip("Radio de la cola: donde se quedan los que no caben en la horda (más de fichas + puestos). Si ya están más cerca, no retroceden.")]
    [FormerlySerializedAs("waitRadius")]
    [SerializeField] private float queueRadius = 3.5f;
    [Tooltip("Desvío angular máximo de cada enemigo en la cola, para que no se amontonen en el mismo punto.")]
    [FormerlySerializedAs("waitAngleSpread")]
    [SerializeField] private float queueAngleSpread = 40f;
    [Tooltip("Los enemigos a menos de esta distancia pueden reclamar un puesto.")]
    [SerializeField] private float claimRange = 6f;
    [Tooltip("Histéresis: un enemigo no pierde su puesto hasta superar claimRange + este margen.")]
    [SerializeField] private float releaseMargin = 3f;

    [Header("Persistencia")]
    [Tooltip("Tiempo mínimo que un enemigo conserva un puesto antes de poder cambiarlo por otro mejor.")]
    [SerializeField] private float minHoldTime = 1.5f;
    [Tooltip("Solo cambia de puesto si el nuevo mejora su puntuación al menos estos metros.")]
    [SerializeField] private float switchMargin = 0.8f;

    [Header("Validación")]
    [SerializeField] private float refreshInterval = 0.1f;
    [SerializeField] private float navSampleDistance = 1f;
    [Tooltip("Si el punto navegable más cercano queda más lejos que esto del puesto ideal, el puesto no se usa.")]
    [SerializeField] private float maxSnapDistance = 0.8f;
    [Tooltip("Máximo de rutas que se calculan por petición. Limita el coste de CPU.")]
    [SerializeField, Min(1)] private int maxPathChecks = 4;

    [Header("Depuración")]
    [SerializeField] private bool drawGizmos = true;

    private class Slot
    {
        public int ring;
        public int index;
        public Vector3 position;
        public Vector3 desiredPosition; // dónde debería estar (para depurar los puestos inválidos)
        public bool valid;
        public EnemyAI occupant;
        public float assignedAt;
        public float radiusOffset;
        public float angleOffset;
    }

    private struct Candidate
    {
        public Slot slot;
        public float score;
    }

    private static readonly Comparison<Candidate> ByScore = (a, b) => a.score.CompareTo(b.score);

    private readonly List<Slot> slots = new List<Slot>();
    private readonly Dictionary<EnemyAI, Slot> assignments = new Dictionary<EnemyAI, Slot>();
    private readonly List<EnemyAI> engaged = new List<EnemyAI>();
    private readonly List<Candidate> candidates = new List<Candidate>();

    private NavMeshPath pathBuffer;
    private float ringAngle;
    private float nextRefreshTime;

    public float ClaimRange => claimRange;
    public float ReleaseRange => claimRange + releaseMargin;
    public float QueueRadius => queueRadius;

    /// <summary>True si queda alguna ficha de ataque libre.</summary>
    public bool HasFreeToken => engaged.Count < maxAttackers;

    // ---------------------------------------------------------------
    //  Ciclo de vida
    // ---------------------------------------------------------------

    private void Awake()
    {
        pathBuffer = new NavMeshPath();

        for (int r = 0; r < rings.Length; r++)
        {
            for (int i = 0; i < rings[r].slotCount; i++)
            {
                slots.Add(new Slot
                {
                    ring = r,
                    index = i,
                    radiusOffset = UnityEngine.Random.Range(-radiusJitter, radiusJitter),
                    angleOffset = UnityEngine.Random.Range(-angleJitter, angleJitter)
                });
            }
        }

        ringAngle = UnityEngine.Random.Range(0f, 360f);
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        ringAngle = (ringAngle + ringRotationSpeed * Time.deltaTime) % 360f;

        // Recalculamos unas pocas veces por segundo, no cada frame.
        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + refreshInterval;
            Refresh();
        }
    }

    // ---------------------------------------------------------------
    //  Fichas de ataque
    // ---------------------------------------------------------------

    /// <summary>
    /// Pide una ficha para pegarse al jugador. Si la consigue, suelta su puesto de aproximación:
    /// a partir de ahora persigue al jugador directamente a distancia de melee.
    /// </summary>
    public bool TryEngage(EnemyAI enemy)
    {
        if (engaged.Contains(enemy)) return true;
        if (engaged.Count >= maxAttackers) return false;

        Release(enemy);
        engaged.Add(enemy);
        return true;
    }

    public void Disengage(EnemyAI enemy)
    {
        engaged.Remove(enemy);
    }

    /// <summary>Suelta puesto y ficha (al morir o desactivarse).</summary>
    public void ReleaseAll(EnemyAI enemy)
    {
        Release(enemy);
        Disengage(enemy);
    }

    // ---------------------------------------------------------------
    //  Puestos de aproximación
    // ---------------------------------------------------------------

    /// <summary>Intenta reservar un puesto alcanzable. Si ya tiene uno, devuelve true.</summary>
    public bool TryAcquire(EnemyAI enemy)
    {
        if (assignments.ContainsKey(enemy)) return true;

        Slot best = FindBestReachableSlot(enemy, float.PositiveInfinity);
        if (best == null) return false;

        Assign(best, enemy);
        return true;
    }

    public void Release(EnemyAI enemy)
    {
        if (!assignments.TryGetValue(enemy, out Slot slot)) return;

        slot.occupant = null;
        assignments.Remove(enemy);
    }

    /// <summary>Posición actual del puesto del enemigo. False si no tiene (o lo perdió).</summary>
    public bool TryGetSlotPosition(EnemyAI enemy, out Vector3 position)
    {
        if (assignments.TryGetValue(enemy, out Slot slot) && slot.valid)
        {
            position = slot.position;
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>
    /// Punto de la cola en el lado por el que viene el enemigo, con un desvío angular propio
    /// para que los que llegan por el mismo lado no se amontonen en el mismo sitio.
    /// </summary>
    public Vector3 GetQueuePoint(EnemyAI enemy)
    {
        Vector3 center = transform.position;
        Vector3 direction = enemy.transform.position - center;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;

        float personalOffset = (Mathf.Repeat(enemy.GetInstanceID() * 0.618034f, 1f) * 2f - 1f) * queueAngleSpread;
        direction = Quaternion.Euler(0f, personalOffset, 0f) * direction.normalized;

        return center + direction * queueRadius;
    }

    /// <summary>
    /// Persistencia: mantiene el puesto actual salvo que haya pasado el tiempo mínimo
    /// y exista otro alcanzable claramente mejor (por ejemplo, un hueco libre en el anillo interior).
    /// Calcula rutas, así que conviene llamarlo cada cierto tiempo, no cada frame.
    /// </summary>
    public void Reevaluate(EnemyAI enemy)
    {
        if (!assignments.TryGetValue(enemy, out Slot current)) return;
        if (Time.time - current.assignedAt < minHoldTime) return;

        float currentScore = Score(current, enemy.transform.position);
        Slot better = FindBestReachableSlot(enemy, currentScore - switchMargin);
        if (better == null) return;

        Release(enemy);
        Assign(better, enemy);
    }

    // ---------------------------------------------------------------
    //  Interno
    // ---------------------------------------------------------------

    /// <summary>Distancia al puesto + penalización por anillo. Menor = mejor.</summary>
    private float Score(Slot slot, Vector3 from)
    {
        return EnemyAI.FlatDistance(slot.position, from) + slot.ring * ringPenalty;
    }

    /// <summary>
    /// Ordena los puestos libres por puntuación y devuelve el primero al que el enemigo puede llegar.
    /// Solo calcula rutas para los mejores candidatos (maxPathChecks), para acotar el coste.
    /// </summary>
    private Slot FindBestReachableSlot(EnemyAI enemy, float maxScore)
    {
        Vector3 from = enemy.transform.position;

        // Muestreamos desde los pies: el pivote de la cápsula está ~1 m por encima del NavMesh (Base Offset).
        // Antes se muestreaba desde el pivote y la búsqueda fallaba: por eso caían todos en "Wait".
        if (!NavMesh.SamplePosition(enemy.Movement.NavPosition, out NavMeshHit fromHit, 1f, NavMesh.AllAreas))
            return null;

        candidates.Clear();

        foreach (Slot slot in slots)
        {
            if (!slot.valid || slot.occupant != null) continue;

            float score = Score(slot, from);
            if (score < maxScore)
                candidates.Add(new Candidate { slot = slot, score = score });
        }

        candidates.Sort(ByScore);

        int checks = Mathf.Min(maxPathChecks, candidates.Count);
        for (int i = 0; i < checks; i++)
        {
            if (IsReachable(fromHit.position, candidates[i].slot.position))
                return candidates[i].slot;
        }

        return null;
    }

    private bool IsReachable(Vector3 from, Vector3 to)
    {
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, pathBuffer) &&
               pathBuffer.status == NavMeshPathStatus.PathComplete;
    }

    private void Assign(Slot slot, EnemyAI enemy)
    {
        slot.occupant = enemy;
        slot.assignedAt = Time.time;
        assignments[enemy] = slot;
    }

    private bool IsBlockedByEngaged(Vector3 position)
    {
        float sqrRadius = engagedBlockRadius * engagedBlockRadius;

        for (int i = 0; i < engaged.Count; i++)
        {
            Vector3 offset = engaged[i].transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude < sqrRadius) return true;
        }

        return false;
    }

    /// <summary>Recalcula la posición de cada puesto y comprueba que sigue siendo utilizable.</summary>
    private void Refresh()
    {
        // Limpieza por si algún enemigo se destruyó sin pasar por OnDisable.
        for (int i = engaged.Count - 1; i >= 0; i--)
            if (engaged[i] == null) engaged.RemoveAt(i);

        bool targetOnNavMesh = NavMesh.SamplePosition(transform.position, out NavMeshHit targetHit, 2f, NavMesh.AllAreas);

        // El anillo se construye a ras de suelo (los pies del jugador sobre el NavMesh), no a la altura
        // de su pivote: el pivote está ~1 m por encima y la búsqueda del NavMesh fallaba en todos los puestos.
        Vector3 center = targetOnNavMesh ? targetHit.position : transform.position;

        foreach (Slot slot in slots)
        {
            Ring ring = rings[slot.ring];
            float step = 360f / ring.slotCount;

            // Anillos alternos desfasados medio puesto: los de fuera quedan en los huecos de los de dentro.
            float spin = slot.ring % 2 == 0 ? ringAngle : -ringAngle;
            float angle = (spin + slot.index * step + slot.ring * step * 0.5f + slot.angleOffset) * Mathf.Deg2Rad;

            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 desired = center + direction * (ring.radius + slot.radiusOffset);

            slot.desiredPosition = desired;
            slot.valid = false;

            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navSampleDistance, NavMesh.AllAreas))
            {
                Vector3 snap = hit.position - desired;
                snap.y = 0f;

                bool lineOk = !ring.requireClearLine ||
                              (targetOnNavMesh && !NavMesh.Raycast(targetHit.position, hit.position, out NavMeshHit _, NavMesh.AllAreas));

                // Un puesto donde ya hay un enemigo atacando no sirve para que otro llegue a él.
                if (snap.magnitude <= maxSnapDistance && lineOk && !IsBlockedByEngaged(hit.position))
                {
                    slot.position = hit.position;
                    slot.valid = true;
                }
            }

            // Si el puesto se ha invalidado, su ocupante lo pierde (lo notará en su siguiente Tick).
            if (!slot.valid && slot.occupant != null)
                Release(slot.occupant);
        }
    }

    // ---------------------------------------------------------------
    //  Depuración
    // ---------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        foreach (Slot slot in slots)
        {
            // Puestos inválidos (pared, fuera del NavMesh, ocupados por un atacante): gris.
            if (!slot.valid)
            {
                if (!Application.isPlaying) continue;
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                Gizmos.DrawWireSphere(slot.desiredPosition + Vector3.up * 0.1f, 0.12f);
                continue;
            }

            bool attackRing = slot.ring == 0;

            if (slot.occupant != null) Gizmos.color = attackRing ? Color.red : new Color(1f, 0.5f, 0f);
            else Gizmos.color = attackRing ? Color.green : Color.cyan;

            Gizmos.DrawSphere(slot.position + Vector3.up * 0.1f, attackRing ? 0.2f : 0.15f);
        }

        // Los que tienen ficha de ataque: un aro magenta en los pies.
        Gizmos.color = Color.magenta;
        foreach (EnemyAI enemy in engaged)
            if (enemy != null) Gizmos.DrawWireSphere(enemy.transform.position + Vector3.up * 0.1f, 0.4f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0f, 0.6f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, queueRadius);

        UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.4f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, claimRange);
#endif
    }
}