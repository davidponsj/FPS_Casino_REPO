using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estadísticas de la partida en curso, para la pantalla de muerte y el ranking: puntos ganados (TotalEarned de
/// PlayerPoints), ronda alcanzada (RoundManager), bajas, tiempo sobrevivido y los puntos que ha dado cada truco
/// (No scope, 360°, A la cabeza...: las líneas de KillScore). El tiempo se para al morir.
///
/// Va en el Player, junto a PlayerHealth, PlayerPoints y KillScore. Cuenta TODAS las bajas de enemigos
/// (EnemyHealth.OnAnyDied): balas, granadas, charcos... todas son tuyas.
/// </summary>
public class RunStats : MonoBehaviour
{
    /// <summary>Lo que ha dado un truco en toda la partida: puntos sumados y veces que ha salido.</summary>
    public class TrickTotal
    {
        public string Label;
        public int Points;
        public int Count;
    }

    [Header("Referencias")]
    [Tooltip("Puntos del jugador. Vacío = lo busca en este objeto.")]
    [SerializeField] private PlayerPoints points;
    [Tooltip("Puntuación de las bajas (los trucos). Vacío = la busca en este objeto.")]
    [SerializeField] private KillScore killScore;
    [Tooltip("Vida del jugador (para parar el tiempo al morir). Vacío = la busca en este objeto.")]
    [SerializeField] private PlayerHealth health;
    [Tooltip("Gestor de rondas de la escena. Vacío = lo busca en la escena.")]
    [SerializeField] private RoundManager roundManager;

    [Header("Depuración")]
    [Tooltip("Muestra sobre el jugador las estadísticas de la partida.")]
    [SerializeField] private bool showDebug;

    private float startTime;
    private float endTime = -1f; // -1 = sigue vivo
    private readonly Dictionary<string, TrickTotal> tricks = new Dictionary<string, TrickTotal>();

    public int Kills { get; private set; }
    public int TotalPoints => points != null ? points.TotalEarned : 0;
    public int Round => roundManager != null ? roundManager.CurrentRound : 0;
    public float SecondsSurvived => (endTime >= 0f ? endTime : Time.time) - startTime;

    private void Awake()
    {
        if (points == null) points = GetComponent<PlayerPoints>();
        if (killScore == null) killScore = GetComponent<KillScore>();
        if (health == null) health = GetComponent<PlayerHealth>();
        if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();
        startTime = Time.time;
    }

    private void OnEnable()
    {
        EnemyHealth.OnAnyDied += HandleEnemyDied;
        if (health != null) health.OnDied += HandlePlayerDied;
        if (killScore != null) killScore.OnKillScored += HandleKillScored;
    }

    private void OnDisable()
    {
        EnemyHealth.OnAnyDied -= HandleEnemyDied;
        if (health != null) health.OnDied -= HandlePlayerDied;
        if (killScore != null) killScore.OnKillScored -= HandleKillScored;
    }

    /// <summary>Suma los trucos de la baja que acaba de puntuar (las mismas líneas que salen en el popup).</summary>
    private void HandleKillScored()
    {
        if (endTime >= 0f) return;

        foreach (KillScore.ScoreLine line in killScore.LastKillLines)
        {
            if (!tricks.TryGetValue(line.Label, out TrickTotal total))
            {
                total = new TrickTotal { Label = line.Label };
                tricks.Add(line.Label, total);
            }
            total.Points += line.Points;
            total.Count++;
        }
    }

    /// <summary>Los trucos de la partida, del que más puntos ha dado al que menos.</summary>
    public List<TrickTotal> GetTricksByPoints()
    {
        var list = new List<TrickTotal>(tricks.Values);
        list.Sort((a, b) => b.Points.CompareTo(a.Points));
        return list;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (endTime < 0f) Kills++; // las que mueran después de ti no cuentan
    }

    private void HandlePlayerDied()
    {
        if (endTime < 0f) endTime = Time.time;
    }

    /// <summary>La partida tal cual para guardarla en el ranking con el nombre que escriba el jugador.</summary>
    public Leaderboard.Entry ToEntry(string playerName) => new Leaderboard.Entry
    {
        name = playerName,
        points = TotalPoints,
        round = Round,
        kills = Kills,
        seconds = SecondsSurvived
    };

    /// <summary>"05:32": minutos y segundos.</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        DebugLabels.Above(transform, DebugLabels.PlayerBuffsLine + 1,
            $"Partida · {TotalPoints} pts · ronda {Round} · {Kills} bajas · {FormatTime(SecondsSurvived)}", DebugLabels.Player);
    }
#endif
}
