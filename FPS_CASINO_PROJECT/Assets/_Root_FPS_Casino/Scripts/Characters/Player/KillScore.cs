using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puntuación de las bajas estilo How to Fish. Cada baja da una base y, según CÓMO matas (en el aire,
/// por la pared, girando 360°, a la cabeza...), suma trucos. Las bajas seguidas forman un combo con
/// multiplicador, que se cobra (va a PlayerPoints) cuando pasas Combo Window Seconds sin matar.
///
/// Cómo se entera de las bajas:
/// - De TODAS: escucha EnemyHealth.OnAnyDied. Las que no son de tus balas (charcos, granadas...) dan
///   la base y los trucos que no dependen de ti: multikill, Resbalón, Cierra la ronda y JACKPOT.
/// - De las de tus balas, además, el arma le avisa con RegisterHit justo ANTES del daño. Así, si ese
///   impacto mata, ya sabe dónde dio la bala y si era la última del cargador.
///
/// Aquí no hay nada visual: la UI (KillScoreHUD) se engancha a OnKillScored, OnComboCashed y OnComboLost.
/// Va en el Player, junto a PlayerController, PlayerHealth, PlayerPoints y PlayerBlackout.
/// </summary>
[RequireComponent(typeof(PlayerPoints))]
public class KillScore : MonoBehaviour
{
    public enum DamageReaction { Nothing, CashOut, Lose }

    /// <summary>Un truco del Inspector: si cuenta, qué texto sale en pantalla y cuántos puntos da.</summary>
    [Serializable]
    public class Trick
    {
        [Tooltip("Marcado = el truco cuenta.")]
        public bool enabled = true;
        [Tooltip("Texto que sale en pantalla.")]
        public string label;
        [Tooltip("Puntos que suma a la baja.")]
        [Min(0)] public int points;

        public Trick(string label, int points)
        {
            this.label = label;
            this.points = points;
        }
    }

    /// <summary>Una línea de la puntuación de una baja: lo que enseña la UI.</summary>
    public struct ScoreLine
    {
        public string Label;
        public int Points;
        public bool IsJackpot;
    }

    [Header("Referencias")]
    [Tooltip("RoundManager de la escena. Vacío = lo busca solo. Sin él no hay truco Cierra la ronda.")]
    [SerializeField] private RoundManager roundManager;

    [Header("Puntos básicos")]
    [Tooltip("Puntos por cada bala que da a un enemigo vivo. Van directos al contador, sin popup ni combo. 10 = CoD.")]
    [Min(0)]
    [SerializeField] private int pointsPerHit = 10;
    [Tooltip("La baja en sí: sale en todos los popups.")]
    [SerializeField] private Trick kill = new Trick("Baja", 100);

    [Header("Combo")]
    [Tooltip("Segundos que tienes para la siguiente baja. Cada baja reinicia la cuenta; si se acaba, el combo se cobra. 3 = da tiempo a buscar al siguiente.")]
    [Min(0.1f)]
    [SerializeField] private float comboWindowSeconds = 3f;
    [Tooltip("Lo que sube el multiplicador por cada baja del combo a partir de la segunda. 0.1 = ×1,1 con 2 bajas, ×2 con 11.")]
    [Min(0f)]
    [SerializeField] private float multiplierPerKill = 0.1f;
    [Tooltip("Tope del multiplicador. 3 = como mucho ×3.")]
    [Min(1f)]
    [SerializeField] private float maxMultiplier = 3f;
    [Tooltip("Qué pasa con el combo si te pegan demasiado mientras lo haces. Nothing = nada, Cash Out = se cobra en ese momento, Lose = se pierde.")]
    [SerializeField] private DamageReaction damageReaction = DamageReaction.Lose;
    [Tooltip("Porcentaje de tu vida máxima que puedes perder durante el combo antes de que pase lo de Damage Reaction. 60 = con 150 de vida y golpes de 50, el 1er golpe no hace nada y el 2º sí.")]
    [Range(0f, 100f)]
    [SerializeField] private float damageTolerancePercent = 60f;

    [Header("Trucos · Disparo (solo con balas)")]
    [Tooltip("Matas a un enemigo pegado a ti.")]
    [SerializeField] private Trick pointBlank = new Trick("A quemarropa", 50);
    [Tooltip("Metros al enemigo como mucho para A quemarropa. 2.5 = casi tocándole.")]
    [Min(0f)]
    [SerializeField] private float pointBlankMaxMeters = 2.5f;
    [Tooltip("La bala que mata da en la parte alta del cuerpo.")]
    [SerializeField] private Trick headshot = new Trick("¡A la cabeza!", 100);
    [Tooltip("Altura del impacto en el cuerpo del enemigo, en % desde los pies, a partir de la que cuenta como cabeza. 80 = el 20% de arriba.")]
    [Range(0f, 100f)]
    [SerializeField] private float headshotHeightPercent = 80f;
    [Tooltip("Estaba a vida completa y muere de un solo disparo (los perdigones de un escopetazo cuentan como uno).")]
    [SerializeField] private Trick oneShot = new Trick("Un tiro, una baja", 100);
    [Tooltip("Matas a un enemigo lejano.")]
    [SerializeField] private Trick longShot = new Trick("Larga distancia", 150);
    [Tooltip("Metros al enemigo como mínimo para Larga distancia. 25 = media sala.")]
    [Min(0f)]
    [SerializeField] private float longShotMinMeters = 25f;
    [Tooltip("La bala que mata te deja el cargador a 0.")]
    [SerializeField] private Trick lastBullet = new Trick("Última bala", 150);

    [Header("Trucos · Multikill (todas las bajas)")]
    [Tooltip("2 bajas seguidas en menos de Multi Kill Seconds.")]
    [SerializeField] private Trick doubleKill = new Trick("¡Doble!", 200);
    [Tooltip("3 bajas seguidas en menos de Multi Kill Seconds.")]
    [SerializeField] private Trick tripleKill = new Trick("¡Triple!", 400);
    [Tooltip("4 bajas o más seguidas.")]
    [SerializeField] private Trick massacre = new Trick("¡MASACRE!", 800);
    [Tooltip("Segundos como mucho entre una baja y la siguiente para que cuenten juntas. 0.5 = el mismo escopetazo o una ráfaga rápida.")]
    [Min(0f)]
    [SerializeField] private float multiKillSeconds = 0.5f;

    [Header("Trucos · Movimiento (solo con balas)")]
    [Tooltip("Matas en el aire (saltando o cayendo).")]
    [SerializeField] private Trick airborne = new Trick("Aéreo", 100);
    [Tooltip("Matas en pleno slide.")]
    [SerializeField] private Trick sliding = new Trick("Deslizando", 100);
    [Tooltip("Matas yendo muy rápido.")]
    [SerializeField] private Trick fullSpeed = new Trick("A toda pastilla", 100);
    [Tooltip("Metros por segundo como mínimo para A toda pastilla. 12 = por encima del sprint (8.5) y del slide (11): hace falta momentum.")]
    [Min(0f)]
    [SerializeField] private float fullSpeedMinMetersPerSecond = 12f;
    [Tooltip("Matas poco después de soltarte de una pared, aún en el aire.")]
    [SerializeField] private Trick wallLaunch = new Trick("Despegue", 150);
    [Tooltip("Segundos como mucho desde que te sueltas de la pared para Despegue. 1 = justo al saltar.")]
    [Min(0f)]
    [SerializeField] private float wallLaunchSeconds = 1f;
    [Tooltip("2ª, 3ª... baja seguida sin aterrizar. Los puntos se multiplican: ×1 en la 2ª, ×2 en la 3ª...")]
    [SerializeField] private Trick noGround = new Trick("Sin tocar el suelo", 150);
    [Tooltip("Matas corriendo por la pared.")]
    [SerializeField] private Trick wallRun = new Trick("Por la pared", 200);
    [Tooltip("Has girado una vuelta entera en poco tiempo antes de matar.")]
    [SerializeField] private Trick spin = new Trick("360°", 300);
    [Tooltip("Grados que tienes que girar en el mismo sentido. 360 = una vuelta entera.")]
    [Min(0f)]
    [SerializeField] private float spinDegrees = 360f;
    [Tooltip("Segundos en los que tienes que haber girado esos grados. 1 = un giro rápido.")]
    [Min(0.1f)]
    [SerializeField] private float spinWindowSeconds = 1f;

    [Header("Trucos · Supervivencia (solo con balas)")]
    [Tooltip("Hay muchos enemigos pegados a ti al matar.")]
    [SerializeField] private Trick surrounded = new Trick("Rodeado", 150);
    [Tooltip("Enemigos como mínimo a tu alrededor para Rodeado (contando al que matas). 4 = ya agobia.")]
    [Min(1)]
    [SerializeField] private int surroundedMinEnemies = 4;
    [Tooltip("Metros a tu alrededor en los que se cuentan los enemigos para Rodeado. 3 = pegados a ti.")]
    [Min(0f)]
    [SerializeField] private float surroundedRadiusMeters = 3f;
    [Tooltip("Capas de los enemigos, para contar los que te rodean.")]
    [SerializeField] private LayerMask enemyLayers;
    [Tooltip("Matas a un enemigo que estaba en el aviso de su golpe (rojo).")]
    [SerializeField] private Trick closeCall = new Trick("Por los pelos", 150);
    [Tooltip("Matas con poca vida.")]
    [SerializeField] private Trick lowHealth = new Trick("Al límite", 200);
    [Tooltip("Porcentaje de tu vida por debajo del que cuenta Al límite. 30 = te queda menos de un tercio.")]
    [Range(0f, 100f)]
    [SerializeField] private float lowHealthPercent = 30f;

    [Header("Trucos · Casino")]
    [Tooltip("El enemigo muere pisando un charco de botella. Vale para todas las bajas.")]
    [SerializeField] private Trick slippery = new Trick("Resbalón", 100);
    [Tooltip("Matas con la barra de borrachera a partir de Tipsy Percent (solo con balas).")]
    [SerializeField] private Trick tipsy = new Trick("Con un par de copas", 100);
    [Tooltip("Porcentaje de la barra de borrachera para Con un par de copas. 50 = a medias.")]
    [Range(0f, 100f)]
    [SerializeField] private float tipsyPercent = 50f;
    [Tooltip("Matas con la barra de borrachera a partir de Wasted Percent (solo con balas). Sustituye a Con un par de copas.")]
    [SerializeField] private Trick wasted = new Trick("Borracho perdido", 250);
    [Tooltip("Porcentaje de la barra de borrachera para Borracho perdido. Mayor que Tipsy Percent. 80 = a punto del blackout.")]
    [Range(0f, 100f)]
    [SerializeField] private float wastedPercent = 80f;
    [Tooltip("Matas al último enemigo de la ronda. Vale para todas las bajas.")]
    [SerializeField] private Trick roundCloser = new Trick("¡Cierra la ronda!", 300);
    [Tooltip("Premio sorpresa al azar en cualquier baja, como una tragaperras.")]
    [SerializeField] private Trick jackpot = new Trick("¡JACKPOT!", 777);
    [Tooltip("Probabilidad en % de JACKPOT en cada baja. 2 = una de cada 50 bajas, más o menos.")]
    [Range(0f, 100f)]
    [SerializeField] private float jackpotChancePercent = 2f;

    [Header("Depuración")]
    [Tooltip("Muestra el combo (bajas, multiplicador, puntos, segundos para cobrar) y los trucos de la última baja en el panel de arriba a la derecha de la vista Game.")]
    [SerializeField] private bool showDebug;

    /// <summary>Una baja acaba de puntuar: sus líneas están en LastKillLines, ya ordenadas de menos a más puntos.</summary>
    public event Action OnKillScored;
    /// <summary>El combo se ha cobrado: puntos totales (con el multiplicador) que han ido a PlayerPoints.</summary>
    public event Action<int> OnComboCashed;
    /// <summary>El combo se ha perdido por recibir demasiado daño.</summary>
    public event Action OnComboLost;

    private const int SpinSamples = 256; // frames que se recuerdan para el 360° (de sobra para 1 s a 240 fps)

    private readonly List<ScoreLine> lines = new List<ScoreLine>(16);
    private readonly Collider[] nearbyColliders = new Collider[32];
    private readonly EnemyHealth[] nearbyEnemies = new EnemyHealth[32];
    private readonly SpinTracker spinTracker = new SpinTracker(SpinSamples);

    private PlayerPoints points;
    private PlayerController controller;
    private PlayerHealth health;
    private PlayerBlackout blackout;

    // Último impacto de bala: si el enemigo muere en este mismo frame, la baja es de esa bala.
    private EnemyHealth pendingEnemy;
    private int pendingFrame = -1;
    private float pendingHeight01;
    private bool pendingLastBullet;

    private float lastYaw;
    private float lastWallRunTime = float.NegativeInfinity;
    private float lastKillTime = float.NegativeInfinity;
    private int multiKillCount;
    private int airKills;

    private int comboKills;
    private int comboPoints;
    private float comboEndTime;
    private float comboDamage;

    // Solo para la depuración.
    private int lastCashed;

    public IReadOnlyList<ScoreLine> LastKillLines => lines;
    public bool IsComboActive => comboKills > 0;
    public int ComboKills => comboKills;
    /// <summary>Puntos del combo sin el multiplicador.</summary>
    public int ComboPoints => comboPoints;
    public float ComboMultiplier => GetComboMultiplier(comboKills, multiplierPerKill, maxMultiplier);
    public float ComboSecondsLeft => IsComboActive ? Mathf.Max(0f, comboEndTime - Time.time) : 0f;

    /// <summary>Multiplicador de un combo de "kills" bajas: ×1 con una, sube "perKill" por cada baja más, hasta "max".</summary>
    public static float GetComboMultiplier(int kills, float perKill, float max) =>
        kills <= 1 ? 1f : Mathf.Min(1f + perKill * (kills - 1), max);

    /// <summary>Ordena de menos a más puntos sin cambiar el orden de las que empatan (la Baja sale antes que otro truco de 100).</summary>
    public static void SortByPoints(List<ScoreLine> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            ScoreLine line = list[i];
            int j = i - 1;
            while (j >= 0 && list[j].Points > line.Points)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = line;
        }
    }

    private void Reset()
    {
        enemyLayers = LayerMask.GetMask("Enemy");
    }

    private void Awake()
    {
        points = GetComponent<PlayerPoints>();
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        blackout = GetComponent<PlayerBlackout>();
        lastYaw = transform.eulerAngles.y;
    }

    private void OnEnable()
    {
        EnemyHealth.OnAnyDied += HandleEnemyDied;
        if (health != null) health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        EnemyHealth.OnAnyDied -= HandleEnemyDied;
        if (health != null) health.OnDamaged -= HandleDamaged;
    }

    private void Start()
    {
        if (roundManager == null)
            roundManager = FindFirstObjectByType<RoundManager>();
    }

    private void Update()
    {
        // El jugador gira entero (la cámara solo sube y baja), así que su giro es el del 360°.
        float yaw = transform.eulerAngles.y;
        spinTracker.Add(Time.time, Mathf.DeltaAngle(lastYaw, yaw));
        lastYaw = yaw;

        if (controller != null)
        {
            if (controller.CurrentState == PlayerController.PlayerMovementState.WallRunning) lastWallRunTime = Time.time;
            if (controller.IsGrounded) airKills = 0;
        }

        if (IsComboActive && Time.time >= comboEndTime)
            CashOut();
    }

    /// <summary>
    /// Lo llama el arma justo ANTES de hacer daño a un enemigo con una bala. Da los puntos por impacto
    /// y apunta cómo ha sido, por si esa bala lo mata.
    /// </summary>
    public void RegisterHit(EnemyHealth enemy, RaycastHit hit, WeaponBase weapon)
    {
        if (enemy == null || enemy.IsDead) return;

        points.AddPoints(pointsPerHit);

        Bounds body = hit.collider.bounds;
        pendingEnemy = enemy;
        pendingFrame = Time.frameCount;
        pendingHeight01 = Mathf.InverseLerp(body.min.y, body.max.y, hit.point.y);
        pendingLastBullet = weapon != null && weapon.CurrentAmmo == 0;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        bool byBullet = enemy == pendingEnemy && pendingFrame == Time.frameCount;
        pendingEnemy = null;

        lines.Clear();
        AddLine(kill);
        AddMultiKill();

        if (byBullet)
        {
            AddBulletTricks(enemy);
            AddMovementTricks();
            AddSurvivalTricks(enemy);
            AddDrunkTrick();
        }

        if (slippery.enabled && enemy.TryGetComponent(out EnemyMovement movement) && movement.SlowMultiplier < 1f)
            AddLine(slippery);

        if (roundManager != null && roundManager.EnemiesLeftInRound == 0)
            AddLine(roundCloser);

        if (jackpot.enabled && UnityEngine.Random.value * 100f < jackpotChancePercent)
            lines.Add(new ScoreLine { Label = jackpot.label, Points = jackpot.points, IsJackpot = true });

        SortByPoints(lines);

        int killPoints = 0;
        foreach (ScoreLine line in lines)
            killPoints += line.Points;

        comboKills++;
        comboPoints += killPoints;
        comboEndTime = Time.time + comboWindowSeconds;

        OnKillScored?.Invoke();
    }

    private void AddMultiKill()
    {
        multiKillCount = Time.time - lastKillTime <= multiKillSeconds ? multiKillCount + 1 : 1;
        lastKillTime = Time.time;

        if (multiKillCount == 2) AddLine(doubleKill);
        else if (multiKillCount == 3) AddLine(tripleKill);
        else if (multiKillCount >= 4) AddLine(massacre);
    }

    private void AddBulletTricks(EnemyHealth enemy)
    {
        float distance = Vector3.Distance(transform.position, enemy.transform.position);
        if (distance <= pointBlankMaxMeters) AddLine(pointBlank);
        if (distance >= longShotMinMeters) AddLine(longShot);

        if (pendingHeight01 * 100f >= headshotHeightPercent) AddLine(headshot);
        if (enemy.WasFullHealthThisFrame) AddLine(oneShot);
        if (pendingLastBullet) AddLine(lastBullet);
    }

    private void AddMovementTricks()
    {
        if (spinTracker.NetDegrees(Time.time, spinWindowSeconds) >= spinDegrees)
            AddLine(spin);

        if (controller == null) return;

        PlayerController.PlayerMovementState state = controller.CurrentState;
        if (state == PlayerController.PlayerMovementState.Airborne) AddLine(airborne);
        if (state == PlayerController.PlayerMovementState.Sliding) AddLine(sliding);
        if (state == PlayerController.PlayerMovementState.WallRunning) AddLine(wallRun);

        if (controller.CurrentHorizontalSpeed >= fullSpeedMinMetersPerSecond) AddLine(fullSpeed);

        if (controller.IsGrounded) return;

        if (state != PlayerController.PlayerMovementState.WallRunning && Time.time - lastWallRunTime <= wallLaunchSeconds)
            AddLine(wallLaunch);

        airKills++;
        if (airKills >= 2 && noGround.enabled)
            AddLine($"{noGround.label} ×{airKills - 1}", noGround.points * (airKills - 1));
    }

    private void AddSurvivalTricks(EnemyHealth enemy)
    {
        if (closeCall.enabled && enemy.TryGetComponent(out EnemyAttack attack) && attack.IsWindingUp)
            AddLine(closeCall);

        if (health != null && health.CurrentHealth < health.MaxHealth * lowHealthPercent / 100f)
            AddLine(lowHealth);

        if (surrounded.enabled && CountNearbyEnemies() >= surroundedMinEnemies)
            AddLine(surrounded);
    }

    private void AddDrunkTrick()
    {
        if (blackout == null) return;

        float drunkPercent = blackout.Blackout01 * 100f;
        if (wasted.enabled && drunkPercent >= wastedPercent) AddLine(wasted);
        else if (drunkPercent >= tipsyPercent) AddLine(tipsy);
    }

    /// <summary>Enemigos distintos a menos de Surrounded Radius Meters (un enemigo puede tener varios colliders).</summary>
    private int CountNearbyEnemies()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, surroundedRadiusMeters, nearbyColliders, enemyLayers, QueryTriggerInteraction.Ignore);
        int count = 0;

        for (int i = 0; i < hits; i++)
        {
            EnemyHealth enemy = nearbyColliders[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null || Array.IndexOf(nearbyEnemies, enemy, 0, count) >= 0) continue;
            nearbyEnemies[count++] = enemy;
        }

        Array.Clear(nearbyEnemies, 0, count); // no guardar referencias a enemigos que van a morir
        return count;
    }

    private void AddLine(Trick trick)
    {
        if (trick.enabled) AddLine(trick.label, trick.points);
    }

    private void AddLine(string label, int linePoints)
    {
        lines.Add(new ScoreLine { Label = label, Points = linePoints });
    }

    private void HandleDamaged(float amount)
    {
        if (!IsComboActive || damageReaction == DamageReaction.Nothing) return;

        comboDamage += amount;
        if (comboDamage < health.MaxHealth * damageTolerancePercent / 100f) return;

        if (damageReaction == DamageReaction.CashOut) CashOut();
        else LoseCombo();
    }

    private void CashOut()
    {
        int total = Mathf.RoundToInt(comboPoints * ComboMultiplier);
        points.AddPoints(total);
        lastCashed = total;

        ResetCombo();
        OnComboCashed?.Invoke(total);
    }

    private void LoseCombo()
    {
        lastCashed = 0;
        ResetCombo();
        OnComboLost?.Invoke();
    }

    private void ResetCombo()
    {
        comboKills = 0;
        comboPoints = 0;
        comboDamage = 0f;
    }

    /// <summary>
    /// Recuerda cuánto ha girado el jugador en cada frame reciente, para saber si ha dado una vuelta entera.
    /// Cuenta el giro NETO: ir 200° a la derecha y volver 200° a la izquierda no es un 360°.
    /// </summary>
    public class SpinTracker
    {
        private readonly float[] deltas;
        private readonly float[] times;
        private int next;

        public SpinTracker(int samples)
        {
            deltas = new float[samples];
            times = new float[samples];
            for (int i = 0; i < samples; i++) times[i] = float.NegativeInfinity;
        }

        /// <summary>Apunta lo que ha girado en este frame (grados, con signo).</summary>
        public void Add(float time, float deltaDegrees)
        {
            deltas[next] = deltaDegrees;
            times[next] = time;
            next = (next + 1) % deltas.Length;
        }

        /// <summary>Grados girados en el mismo sentido en los últimos "windowSeconds" segundos.</summary>
        public float NetDegrees(float now, float windowSeconds)
        {
            float sum = 0f;
            for (int i = 0; i < deltas.Length; i++)
                if (now - times[i] <= windowSeconds) sum += deltas[i];
            return Mathf.Abs(sum);
        }
    }

#if UNITY_EDITOR
    // El combo va en el panel de arriba a la derecha de la vista Game, debajo del de la ronda y la horda.
    private void OnGUI()
    {
        if (!showDebug) return;

        string combo = IsComboActive
            ? $"COMBO ×{ComboMultiplier:0.0} · {comboKills} bajas · {comboPoints} pts · se cobra en {ComboSecondsLeft:0.0} s · daño {comboDamage:0}"
            : $"sin combo · último cobro {lastCashed} pts";

        var last = new System.Text.StringBuilder("última baja:");
        foreach (ScoreLine line in lines)
            last.Append($" {line.Label} {line.Points} ·");

        DebugLabels.Panel(DebugLabels.KillScorePanelLine, $"{combo}\n{last}", DebugLabels.Player);
    }
#endif
}