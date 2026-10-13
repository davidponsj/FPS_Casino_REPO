using System;
using UnityEngine;

/// <summary>
/// Vida del jugador. Implementa IDamageable, así que cualquier cosa que ya haga daño
/// (balas, explosiones, y los enemigos cuando existan) puede dañar al jugador sin saber nada de él.
///
/// Todo cambio de vida pasa por TakeDamage / Heal y avisa con eventos (OnHealthChanged,
/// OnDamaged, OnHealed, OnDied): la barra de vida, los sonidos o la pantalla de muerte se
/// enganchan a esos eventos en vez de mirar la vida cada frame.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    /// <summary>(vida actual, vida máxima). Se dispara en cualquier cambio de vida.</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>Daño recibido de verdad (ya aplicado el multiplicador).</summary>
    public event Action<float> OnDamaged;
    /// <summary>Vida curada de verdad (sin contar lo que sobrepasa el máximo).</summary>
    public event Action<float> OnHealed;
    public event Action OnDied;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float Health01 => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public bool IsDead { get; private set; }

    /// <summary>
    /// Multiplicador del daño que recibes (1 = normal, 0.5 = la mitad). Pensado para efectos
    /// futuros (escudos, dificultad...). No lo toques directamente desde varios sitios a la vez.
    /// </summary>
    public float DamageTakenMultiplier { get; set; } = 1f;

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

        float damage = amount * DamageTakenMultiplier;
        currentHealth = Mathf.Max(currentHealth - damage, 0f);

        OnDamaged?.Invoke(damage);
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

    /// <summary>Cura un porcentaje de la vida MÁXIMA (0.3 = un 30%). Lo usará la botella verde.</summary>
    public void HealPercent(float percent01)
    {
        Heal(maxHealth * percent01);
    }

    private void Die()
    {
        IsDead = true;
        Debug.Log("[Vida] El jugador ha muerto");
        OnDied?.Invoke();
    }

    [ContextMenu("TEST: Recibir 20 de daño")]
    private void TestDamage() => TakeDamage(20f);

    [ContextMenu("TEST: Curar 30%")]
    private void TestHeal() => HealPercent(0.3f);
}
