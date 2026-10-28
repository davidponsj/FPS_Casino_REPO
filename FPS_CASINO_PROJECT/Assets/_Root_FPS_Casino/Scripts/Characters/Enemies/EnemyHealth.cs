using System;
using UnityEngine;

/// <summary>
/// Vida del enemigo. Implementa IDamageable, así que las armas, granadas y charcos ya le hacen daño
/// sin saber nada de él. Los enemigos de las rondas reciben su vida del RoundManager al aparecer
/// (sube cada ronda, como en CoD Zombies); uno puesto a mano en la escena usa la del Inspector.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    /// <summary>Se lanza una sola vez, justo antes de destruir el enemigo (el RoundManager cuenta las bajas con esto).</summary>
    public event Action<EnemyHealth> OnDied;

    [Header("Vida")]
    [Tooltip("Vida si pones el enemigo a mano en la escena. Los de las rondas la reciben del RoundManager. 150 = ronda 1 de CoD.")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 150f;

    [Header("Depuración")]
    [Tooltip("Muestra la vida sobre el enemigo en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>Lo llama el RoundManager justo después de crearlo, con la vida de la ronda actual.</summary>
    public void SetMaxHealth(float health)
    {
        maxHealth = Mathf.Max(1f, health);
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        // Destroy no es inmediato: los perdigones que llegan en el mismo frame no deben contar otra muerte.
        if (IsDead || amount <= 0f) return;

        currentHealth -= amount;
        if (currentHealth > 0f) return;

        IsDead = true;
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string text = IsDead ? "Vida · muerto" : $"Vida · {currentHealth:0}/{maxHealth:0}";
        DebugLabels.Above(transform, DebugLabels.EnemyHealthLine, text, DebugLabels.Health);
    }
#endif
}