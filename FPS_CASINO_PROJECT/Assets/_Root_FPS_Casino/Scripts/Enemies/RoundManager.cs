using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Rondas estilo CoD Zombies: cada ronda salen más enemigos (de momento todos igual de fuertes).
/// Spawnea de uno en uno en un punto aleatorio, sin pasar de un máximo de vivos a la vez.
/// Cuando se han spawneado todos los de la ronda y no queda ninguno vivo, descanso y siguiente ronda.
/// </summary>
public class RoundManager : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemyPrefab;

    [Tooltip("Puntos de aparición. Si se deja vacío, se usan los hijos de este objeto.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Cantidad")]
    [SerializeField, Min(1)] private int enemiesFirstRound = 6;
    [Tooltip("Enemigos de más en cada ronda siguiente.")]
    [SerializeField, Min(0)] private int extraEnemiesPerRound = 3;
    [Tooltip("Máximo de enemigos vivos a la vez. El resto espera a que mueran los primeros.")]
    [SerializeField, Min(1)] private int maxAlive = 24;

    [Header("Tiempos")]
    [SerializeField, Min(0f)] private float secondsBetweenSpawns = 1f;
    [SerializeField, Min(0f)] private float secondsBetweenRounds = 10f;

    public event Action<int> OnRoundStarted;

    public int CurrentRound { get; private set; }
    public int AliveCount { get; private set; }

    private WaitUntil waitForFreeSlot;
    private WaitUntil waitForRoundCleared;

    public static int EnemiesForRound(int round, int firstRound, int extraPerRound) =>
        firstRound + extraPerRound * (round - 1);

    private void Awake()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = new Transform[transform.childCount];
            for (int i = 0; i < spawnPoints.Length; i++)
                spawnPoints[i] = transform.GetChild(i);
        }

        // Cacheados para no crear uno nuevo en cada espera.
        waitForFreeSlot = new WaitUntil(() => AliveCount < maxAlive);
        waitForRoundCleared = new WaitUntil(() => AliveCount == 0);
    }

    private IEnumerator Start()
    {
        if (enemyPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"{name}: falta el prefab de enemigo o los puntos de spawn.", this);
            yield break;
        }

        while (true)
        {
            CurrentRound++;
            OnRoundStarted?.Invoke(CurrentRound);

            int toSpawn = EnemiesForRound(CurrentRound, enemiesFirstRound, extraEnemiesPerRound);
            for (int i = 0; i < toSpawn; i++)
            {
                yield return waitForFreeSlot;
                Spawn();
                yield return new WaitForSeconds(secondsBetweenSpawns);
            }

            yield return waitForRoundCleared;
            yield return new WaitForSeconds(secondsBetweenRounds);
        }
    }

    private void Spawn()
    {
        Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        EnemyHealth enemy = Instantiate(enemyPrefab, point.position, point.rotation);

        // Warp lo pega al NavMesh aunque el punto esté un poco por encima del suelo.
        if (enemy.TryGetComponent(out EnemyMovement movement) && !movement.Warp(point.position))
            Debug.LogWarning($"{name}: el punto {point.name} no está cerca del NavMesh.", point);

        AliveCount++;
        enemy.OnDied += HandleEnemyDied;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        AliveCount--;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Transform child in transform)
            Gizmos.DrawWireSphere(child.position, 0.5f);
    }
}
