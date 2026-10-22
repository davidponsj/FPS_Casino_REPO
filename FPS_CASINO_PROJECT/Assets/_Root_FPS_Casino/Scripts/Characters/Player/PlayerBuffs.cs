using UnityEngine;

/// <summary>
/// Mejoras (y penalizaciones) temporales del jugador. Cada una tiene un valor y una hora de fin;
/// pasada esa hora vuelve sola a su valor normal, sin corrutinas ni temporizadores.
///
/// Los demás scripts solo LEEN de aquí:
/// - PlayerController → MoveSpeedMultiplier
/// - WeaponBase → FireRateMultiplier, ReloadTimeMultiplier, InfiniteMagazine
/// - SlotMachine → SlotLuck
///
/// Y las botellas ESCRIBEN aquí con ApplyDrinkEffect / ApplyThrowSelfEffect.
/// </summary>
public class PlayerBuffs : MonoBehaviour
{
    public enum Stat { MoveSpeed, FireRate, ReloadTime, InfiniteMagazine, SlotLuck }

    // Valor de cada estadística cuando no hay ningún efecto activo (mismo orden que el enum).
    private static readonly float[] NeutralValues = { 1f, 1f, 1f, 0f, 0f };

    [SerializeField] private PlayerHealth playerHealth;

    private readonly float[] values = new float[NeutralValues.Length];
    private readonly float[] endTimes = new float[NeutralValues.Length];

    public float MoveSpeedMultiplier => Get(Stat.MoveSpeed);
    public float FireRateMultiplier => Get(Stat.FireRate);
    public float ReloadTimeMultiplier => Get(Stat.ReloadTime);
    public bool InfiniteMagazine => Get(Stat.InfiniteMagazine) > 0f;
    public float SlotLuck => Get(Stat.SlotLuck);

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
    }

    /// <summary>Activa una mejora durante unos segundos. Si ya estaba activa, se sustituye (no se acumula).</summary>
    public void Apply(Stat stat, float value, float duration)
    {
        values[(int)stat] = value;
        endTimes[(int)stat] = Time.time + duration;
    }

    public float Get(Stat stat)
    {
        int i = (int)stat;
        return Time.time < endTimes[i] ? values[i] : NeutralValues[i];
    }

    // ---------------- BOTELLAS ----------------

    public void ApplyDrinkEffect(BottleEffect effect)
    {
        if (effect == null) return;

        switch (effect.drinkEffect)
        {
            case BottleEffect.DrinkEffect.KillSelf:
                if (playerHealth != null) playerHealth.Kill();
                break;
            case BottleEffect.DrinkEffect.HealPercent:
                if (playerHealth != null) playerHealth.HealPercent(effect.drinkValue);
                break;
            case BottleEffect.DrinkEffect.SpeedBoost:
                Apply(Stat.MoveSpeed, effect.drinkValue, effect.drinkDuration);
                break;
            case BottleEffect.DrinkEffect.InfiniteMagazine:
                Apply(Stat.InfiniteMagazine, 1f, effect.drinkDuration);
                break;
            case BottleEffect.DrinkEffect.FireRateBoost:
                Apply(Stat.FireRate, effect.drinkValue, effect.drinkDuration);
                break;
            case BottleEffect.DrinkEffect.SlotLuck:
                Apply(Stat.SlotLuck, effect.drinkValue, effect.drinkDuration);
                break;
        }
    }

    public void ApplyThrowSelfEffect(BottleEffect effect)
    {
        if (effect == null) return;

        switch (effect.throwSelfEffect)
        {
            case BottleEffect.ThrowSelfEffect.ReloadTime:
                Apply(Stat.ReloadTime, effect.throwSelfValue, effect.throwSelfDuration);
                break;
        }
    }
}