using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rondas estilo CoD Zombies: cada ronda salen más enemigos (de momento todos igual de fuertes).
/// Spawnea de uno en uno, con un retraso que se acorta cada ronda, sin pasar de un máximo de vivos a la vez.
/// Los enemigos aparecen alrededor del jugador, fuera de su vista y desde direcciones distintas.
/// Cuando se han spawneado todos los de la ronda y no queda ninguno vivo, descanso y siguiente ronda.
/// </summary>
public class RoundManager : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemyPrefab;

    [Header("Cantidad")]
    [Tooltip("Máximo de enemigos vivos a la vez. El resto espera a que mueran los primeros.")]
    [SerializeField, Min(1)] private int maxAlive = 24;

    [Header("Tiempos")]
    [Tooltip("Segundos entre spawns en la ronda 1.")]
    [SerializeField, Min(0f)] private float firstRoundSpawnDelay = 2f;
    [Tooltip("En cada ronda el retraso entre spawns se multiplica por esto.")]
    [SerializeField, Range(0.5f, 1f)] private float spawnDelayMultiplier = 0.95f;
    [SerializeField, Min(0f)] private float minSpawnDelay = 0.08f;
    [SerializeField, Min(0f)] private float secondsBetweenRounds = 10f;

    [Header("Spawn alrededor del jugador")]
    [SerializeField, Min(0f)] private float minSpawnDistance = 10f;
    [SerializeField, Min(0f)] private float maxSpawnDistance = 25f;
    [Tooltip("Longitud máxima del camino por NavMesh, en múltiplos de la distancia máxima. Evita spawns al otro lado de una pared.")]
    [SerializeField, Min(1f)] private float maxPathFactor = 1.5f;
    [Tooltip("Grados mínimos respecto a las direcciones de los últimos spawns. Hace que lleguen de todas partes.")]
    [SerializeField, Range(0f, 180f)] private float minAngleBetweenSpawns = 45f;
    [Tooltip("Capas que tapan la vista del jugador. Sin la capa Enemy, o un enemigo taparía el spawn de otro.")]
    [SerializeField] private LayerMask visionBlockers = ~0;
    [Tooltip("Altura sobre el suelo a la que se mira si el jugador ve el punto (más o menos el pecho del enemigo).")]
    [SerializeField] private float visibilityCheckHeight = 1f;
    [SerializeField, Min(1)] private int candidatesPerSpawn = 10;

    public event Action<int> OnRoundStarted;

    public int CurrentRound { get; private set; }
    public int AliveCount { get; private set; }

    // Distancia máxima para pegar un punto al NavMesh.
    private const float NavSampleDistance = 2f;
    private const int RecentSpawnCount = 3;

    // Rondas 1-5 de CoD: una fracción del total normal para empezar suave.
    private static readonly float[] EarlyRoundFactors = { 0.25f, 0.3f, 0.5f, 0.7f, 0.9f };

    private Transform player;
    private Camera playerCamera;

    // Reutilizados en cada spawn para no asignar memoria.
    private NavMeshPath path;
    private readonly Vector3[] pathCorners = new Vector3[32];
    private readonly Vector3[] recentDirections = new Vector3[RecentSpawnCount];
    private readonly Vector3[] recentPositions = new Vector3[RecentSpawnCount];
    private int recentCount;
    private int recentIndex;

    private WaitUntil waitForFreeSlot;
    private WaitUntil waitForRoundCleared;

    /// <summary>Total de enemigos de la ronda con la fórmula de CoD Zombies en solitario (6, 8, 13, 18, 24, 27...).</summary>
    public static int EnemiesForRound(int round)
    {
        float multiplier = Mathf.Max(1f, round / 5f);
        if (round >= 10) multiplier *= round * 0.15f;

        int total = 24 + Mathf.FloorToInt(3f * multiplier);
        return round <= EarlyRoundFactors.Length
            ? Mathf.FloorToInt(total * EarlyRoundFactors[round - 1])
            : total;
    }

    public static float SpawnDelayForRound(int round, float firstRoundDelay, float multiplier, float minDelay) =>
        Mathf.Max(minDelay, firstRoundDelay * Mathf.Pow(multiplier, round - 1));

    private void Reset()
    {
        visionBlockers = ~LayerMask.GetMask("Enemy");
    }

    private void Awake()
    {
        path = new NavMeshPath();

        // Cacheados para no crear uno nuevo en cada espera.
        waitForFreeSlot = new WaitUntil(() => AliveCount < maxAlive);
        waitForRoundCleared = new WaitUntil(() => AliveCount == 0);
    }

    private IEnumerator Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
        playerCamera = Camera.main;

        if (enemyPrefab == null || player == null)
        {
            Debug.LogWarning($"{name}: falta el prefab de enemigo o no hay ningún objeto con el tag Player.", this);
            yield break;
        }

        while (true)
        {
            CurrentRound++;
            OnRoundStarted?.Invoke(CurrentRound);

            int toSpawn = EnemiesForRound(CurrentRound);
            var spawnDelay = new WaitForSeconds(
                SpawnDelayForRound(CurrentRound, firstRoundSpawnDelay, spawnDelayMultiplier, minSpawnDelay));

            for (int spawned = 0; spawned < toSpawn;)
            {
                yield return waitForFreeSlot;
                if (TrySpawn()) spawned++;
                yield return spawnDelay;
            }

            yield return waitForRoundCleared;
            yield return new WaitForSeconds(secondsBetweenRounds);
        }
    }

    private bool TrySpawn()
    {
        if (!TryFindSpawnPoint(out Vector3 position))
        {
            Debug.LogWarning($"{name}: no se encontró sitio para spawnear alrededor del jugador (¿está fuera del NavMesh?).", this);
            return false;
        }

        Quaternion facePlayer = Quaternion.LookRotation(Flat(player.position - position));
        EnemyHealth enemy = Instantiate(enemyPrefab, position, facePlayer);
        if (enemy.TryGetComponent(out EnemyMovement movement))
            movement.Warp(position);

        AliveCount++;
        enemy.OnDied += HandleEnemyDied;
        return true;
    }

    /// <summary>
    /// Prueba puntos al azar en un anillo alrededor del jugador. Vale el primero que esté en el NavMesh,
    /// con camino corto hasta el jugador, fuera de su vista y en una dirección distinta a los últimos spawns.
    /// Si ninguno cumple todo, usa el más lejano por camino para que la ronda nunca se atasque.
    /// </summary>
    private bool TryFindSpawnPoint(out Vector3 spawnPoint)
    {
        spawnPoint = default;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit playerHit, NavSampleDistance, NavMesh.AllAreas))
            return false;

        float maxPathLength = maxSpawnDistance * maxPathFactor;
        float fallbackPathLength = -1f;
        Vector3 fallbackPoint = default;

        for (int i = 0; i < candidatesPerSpawn; i++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                             * UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);

            if (!NavMesh.SamplePosition(player.position + offset, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
                continue;

            float pathLength = PathLength(hit.position, playerHit.position);
            if (pathLength < 0f || pathLength > maxPathLength)
                continue;

            if (pathLength > fallbackPathLength)
            {
                fallbackPathLength = pathLength;
                fallbackPoint = hit.position;
            }

            Vector3 direction = Flat(hit.position - player.position);
            if (IsVisibleToPlayer(hit.position) || IsCloseToRecentDirection(direction))
                continue;

            spawnPoint = hit.position;
            RememberSpawn(spawnPoint);
            return true;
        }

        if (fallbackPathLength < 0f)
            return false;

        spawnPoint = fallbackPoint;
        RememberSpawn(spawnPoint);
        return true;
    }

    /// <summary>Longitud del camino por NavMesh, o -1 si no se puede llegar.</summary>
    private float PathLength(Vector3 from, Vector3 to)
    {
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            return -1f;

        int corners = path.GetCornersNonAlloc(pathCorners);
        float length = 0f;
        for (int i = 1; i < corners; i++)
            length += Vector3.Distance(pathCorners[i - 1], pathCorners[i]);
        return length;
    }

    private bool IsVisibleToPlayer(Vector3 point)
    {
        if (playerCamera == null) return false;

        Vector3 target = point + Vector3.up * visibilityCheckHeight;
        Vector3 viewport = playerCamera.WorldToViewportPoint(target);
        bool onScreen = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        return onScreen && !Physics.Linecast(playerCamera.transform.position, target, visionBlockers, QueryTriggerInteraction.Ignore);
    }

    private bool IsCloseToRecentDirection(Vector3 direction)
    {
        for (int i = 0; i < recentCount; i++)
            if (Vector3.Angle(direction, recentDirections[i]) < minAngleBetweenSpawns)
                return true;
        return false;
    }

    private void RememberSpawn(Vector3 position)
    {
        recentDirections[recentIndex] = Flat(position - player.position);
        recentPositions[recentIndex] = position;
        recentIndex = (recentIndex + 1) % RecentSpawnCount;
        recentCount = Mathf.Min(recentCount + 1, RecentSpawnCount);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        AliveCount--;
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, minSpawnDistance);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, maxSpawnDistance);
#endif

        Gizmos.color = Color.red;
        for (int i = 0; i < recentCount; i++)
            Gizmos.DrawWireSphere(recentPositions[i], 0.5f);
    }
}
