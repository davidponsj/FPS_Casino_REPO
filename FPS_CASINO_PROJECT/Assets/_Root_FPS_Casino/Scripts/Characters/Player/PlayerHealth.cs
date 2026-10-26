using System;
using UnityEngine;

/// <summary>
/// Vida del jugador. Va en el Player. Implementa IDamageable, así que cualquier cosa que haga daño
/// (balas, explosiones, y los enemigos cuando ataquen) puede dañar al jugador sin saber nada de él.
///
/// Todo cambio de vida pasa por TakeDamage / Heal / Kill y avisa con eventos (OnHealthChanged,
/// OnDamaged, OnHealed, OnDied): la barra de vida (HealthBarHUD), los sonidos o la pantalla de muerte
/// se enganchan a esos eventos en vez de mirar la vida cada frame.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [Tooltip("Vida con la que empiezas y tope al curarte. 100 = normal.")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 100f;

    [Header("Depuración")]
    [Tooltip("Muestra la vida sobre el jugador en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    /// <summary>(vida actual, vida máxima). Se dispara en cualquier cambio de vida.</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>Daño recibido.</summary>
    public event Action<float> OnDamaged;
    /// <summary>Vida curada de verdad (sin contar lo que sobrepasa el máximo).</summary>
    public event Action<float> OnHealed;
    public event Action OnDied;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // En Start y no en Awake: así la UI ya se ha suscrito y recibe el valor inicial.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);

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
        string text = IsDead ? "Vida: MUERTO" : $"Vida: {currentHealth:0}/{maxHealth:0}";
        DebugLabels.Above(transform, DebugLabels.PlayerHealthLine, text, DebugLabels.Health);
    }
#endif
}
