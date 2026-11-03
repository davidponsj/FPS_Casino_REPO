using System;
using UnityEngine;

/// <summary>
/// Vida del jugador. Va en el Player. Implementa IDamageable, así que cualquier cosa que haga daño
/// (balas, explosiones, los enemigos con EnemyAttack) puede dañar al jugador sin saber nada de él.
///
/// Todo cambio de vida pasa por TakeDamage / Heal / Kill (y la regeneración) y avisa con eventos
/// (OnHealthChanged, OnDamaged, OnHealed, OnDied): la barra de vida (HealthBarHUD), los sonidos o la
/// pantalla de muerte se enganchan a esos eventos en vez de mirar la vida cada frame.
///
/// Regeneración estilo CoD: si pasas Regen Delay Seconds sin recibir daño, la vida sube sola hasta el máximo.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [Tooltip("Vida con la que empiezas y tope al curarte. 150 = Black Ops 3: con golpes de 50 aguantas 2 y caes al 3º.")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 150f;

    [Header("Regeneración")]
    [Tooltip("Marcado = la vida sube sola cuando llevas un rato sin recibir daño, como en CoD. Desmarcado = solo te curan las botellas.")]
    [SerializeField] private bool regenEnabled = true;
    [Tooltip("Segundos sin recibir daño antes de empezar a regenerar. Cada golpe reinicia la cuenta. 4 = tienes que esconderte un momento.")]
    [Min(0f)]
    [SerializeField] private float regenDelaySeconds = 4f;
    [Tooltip("Vida por segundo que recuperas mientras regeneras. 20 = de 0 a 150 en unos 7 s.")]
    [Min(0f)]
    [SerializeField] private float regenPerSecond = 20f;

    [Header("Depuración")]
    [Tooltip("Muestra la vida y la regeneración sobre el jugador en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    /// <summary>(vida actual, vida máxima). Se dispara en cualquier cambio de vida.</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>Daño recibido.</summary>
    public event Action<float> OnDamaged;
    /// <summary>Vida curada de verdad por una cura (botella...), sin contar lo que sobrepasa el máximo. La regeneración no lo lanza.</summary>
    public event Action<float> OnHealed;
    public event Action OnDied;

    private float currentHealth;
    private float lastDamageTime = float.NegativeInfinity;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>True mientras la vida está subiendo sola.</summary>
    public bool IsRegenerating =>
        regenEnabled && !IsDead && currentHealth < maxHealth && Time.time >= lastDamageTime + regenDelaySeconds;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // En Start y no en Awake: así la UI ya se ha suscrito y recibe el valor inicial.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (!IsRegenerating) return;

        currentHealth = Mathf.Min(currentHealth + regenPerSecond * Time.deltaTime, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        lastDamageTime = Time.time;

        OnDamaged?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        float healed = currentHealth - before;
        if (healed <= 0f) return; // ya estabas a tope

        OnHealed?.Invoke(healed);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>Cura un porcentaje de la vida MÁXIMA (0.3 = un 30%). Lo usa la botella verde.</summary>
    public void HealPercent(float percent01)
    {
        Heal(maxHealth * percent01);
    }

    /// <summary>Muerte instantánea, sin pasar por el daño (p. ej. por el blackout de beber demasiado).</summary>
    public void Kill()
    {
        if (IsDead) return;

        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Die();
    }

    private void Die()
    {
        IsDead = true;
        OnDied?.Invoke();
    }

    [ContextMenu("TEST: Recibir 20 de daño")]
    private void TestDamage() => TakeDamage(20f);

    [ContextMenu("TEST: Curar 30%")]
    private void TestHeal() => HealPercent(0.3f);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        string regen = "";
        if (Application.isPlaying && regenEnabled && !IsDead && currentHealth < maxHealth)
        {
            regen = IsRegenerating
                ? $" · regenerando +{regenPerSecond:0}/s"
                : $" · regenera en {lastDamageTime + regenDelaySeconds - Time.time:0.0} s";
        }

        string text = IsDead ? "Vida: MUERTO" : $"Vida: {currentHealth:0}/{maxHealth:0}{regen}";
        DebugLabels.Above(transform, DebugLabels.PlayerHealthLine, text, DebugLabels.Health);
    }
#endif
}