using System;
using UnityEngine;

/// <summary>
/// Vida del enemigo. Implementa IDamageable, así que las armas, granadas y charcos ya le hacen daño
/// sin saber nada de él. De momento muere al primer impacto, sea del daño que sea, para poder probar las rondas.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    /// <summary>Se lanza una sola vez, justo antes de destruir el enemigo (el RoundManager cuenta las bajas con esto).</summary>
    public event Action<EnemyHealth> OnDied;

    public bool IsDead { get; private set; }

    public void TakeDamage(float amount)
    {
        // Una escopeta llama a TakeDamage una vez por perdigón: sin esto contaría varias muertes.
        if (IsDead) return;

        IsDead = true;
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}
