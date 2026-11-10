using System.Globalization;
using UnityEngine;

/// <summary>
/// Qué hace una botella. Es un asset (Create → Casino → Bottle Effect), uno por color, y se
/// configura entero desde el Inspector: al beberla, al lanzarla (sobre ti), el impacto y el charco.
/// Así se pueden ajustar números y duraciones sin tocar código.
///
/// Cada apartado es independiente: una botella puede tener efecto al beber, impacto y charco a la
/// vez, o solo alguno. Lo que no se use se deja en None / 0.
/// </summary>
[CreateAssetMenu(menuName = "Casino/Bottle Effect", fileName = "BottleEffect_")]
public class BottleEffect : ScriptableObject
{
    public enum DrinkEffect
    {
        None,
        KillSelf,          // mueres en el acto
        HealPercent,       // value = porcentaje de vida máxima (0.4 = 40%)
        SpeedBoost,        // value = multiplicador de velocidad (1.3 = +30%)
        InfiniteMagazine,  // disparas sin gastar balas del cargador
        FireRateBoost,     // value = multiplicador de cadencia (1.5 = +50%)
        SlotLuck           // value = probabilidad extra de premio en la tragaperras (0.3 = 30%)
    }

    public enum ThrowSelfEffect
    {
        None,
        ReloadTime         // value = multiplicador del tiempo de recarga (0.5 = el doble de rápido)
    }

    [Header("Al beberla")]
    public DrinkEffect drinkEffect = DrinkEffect.None;
    [Tooltip("Depende del efecto: % de vida (0.4), multiplicador (1.3 = +30%) o probabilidad (0.3). No se usa en KillSelf ni InfiniteMagazine.")]
    public float drinkValue = 1.3f;
    [Tooltip("Segundos que dura. No se usa en los instantáneos (KillSelf, HealPercent).")]
    public float drinkDuration = 10f;

    [Header("Al lanzarla: efecto sobre ti")]
    public ThrowSelfEffect throwSelfEffect = ThrowSelfEffect.None;
    [Tooltip("ReloadTime: multiplicador del tiempo de recarga. Menos de 1 = recargas más rápido, más de 1 = más lento.")]
    public float throwSelfValue = 0.5f;
    public float throwSelfDuration = 10f;

    [Header("Al lanzarla: impacto")]
    [Tooltip("Radio del impacto en metros. 0 = el impacto no hace nada.")]
    public float impactRadius = 0f;
    [Tooltip("Daño a los enemigos dentro del radio.")]
    public float impactDamage = 0f;
    [Tooltip("Si está activo, el impacto mata en el acto a los enemigos del radio (ignora Impact Damage).")]
    public bool impactKills = false;

    [Header("Al lanzarla: charco")]
    [Tooltip("Segundos que dura el charco. 0 = no deja charco.")]
    public float puddleLifetime = 8f;
    [Tooltip("Velocidad de los enemigos que lo pisan. 1 = normal, 0.5 = la mitad, 0 = paralizados.")]
    [Range(0f, 1f)]
    public float enemySpeedMultiplier = 1f;
    [Tooltip("Daño por segundo a los enemigos mientras lo pisan (veneno). 0 = nada.")]
    public float enemyDamagePerSecond = 0f;
    [Tooltip("Si está activo, la ralentización/parálisis dura hasta que el charco desaparece aunque salgan de él.")]
    public bool holdUntilPuddleEnds = false;

    /// <summary>True si el efecto al beber dura un tiempo (y por tanto sale el temporizador en el HUD).</summary>
    public bool HasTimedDrinkEffect =>
        drinkDuration > 0f && drinkEffect != DrinkEffect.None
        && drinkEffect != DrinkEffect.KillSelf && drinkEffect != DrinkEffect.HealPercent;

    public bool HasImpact => impactRadius > 0f && (impactKills || impactDamage > 0f);

    public bool PuddleAffectsEnemies => enemySpeedMultiplier < 1f || enemyDamagePerSecond > 0f;

    // ---------------- RESUMEN PARA EL HUD ----------------
    // Lo usa el bocadillo del barman (BartenderSpeech). Se genera con los números de arriba, así siempre dice la
    // verdad aunque se cambien los valores en el Inspector. Va en inglés (el idioma del juego) y con los números
    // siempre con punto decimal, sea cual sea el idioma del ordenador. Nbsp es un espacio que no deja partir la
    // línea ahí (para que no quede "10" en una línea y "s" en la siguiente).
    private const string Nbsp = "\u00A0";

    /// <summary>Qué hace al beberla, en corto. Ej.: "+30% move speed · 10 s". Vacío si no hace nada.</summary>
    public string DrinkSummary()
    {
        string duration = drinkDuration > 0f ? $"{Nbsp}· {Num(drinkDuration, "0")}{Nbsp}s" : "";
        switch (drinkEffect)
        {
            case DrinkEffect.KillSelf: return "Kills you instantly";
            case DrinkEffect.HealPercent: return $"Heals {Num(drinkValue * 100f, "0")}% health";
            case DrinkEffect.SpeedBoost: return $"{Percent(drinkValue - 1f)} move speed{duration}";
            case DrinkEffect.InfiniteMagazine: return $"Infinite ammo{duration}";
            case DrinkEffect.FireRateBoost: return $"{Percent(drinkValue - 1f)} fire rate{duration}";
            case DrinkEffect.SlotLuck: return $"{Percent(drinkValue)} slot machine luck{duration}";
            default: return "";
        }
    }

    /// <summary>Qué hace al lanzarla, en corto. Ej.: "Kills everything within 3 m · Puddle: slows 50% and poisons 10/s". Vacío si nada.</summary>
    public string ThrowSummary()
    {
        var parts = new System.Collections.Generic.List<string>();

        if (throwSelfEffect == ThrowSelfEffect.ReloadTime && throwSelfValue > 0f && !Mathf.Approximately(throwSelfValue, 1f))
        {
            string speed = throwSelfValue < 1f ? $"Reload x{Num(1f / throwSelfValue, "0.#")} faster" : $"Reload x{Num(throwSelfValue, "0.#")} slower";
            parts.Add(throwSelfDuration > 0f ? $"{speed}{Nbsp}· {Num(throwSelfDuration, "0")}{Nbsp}s" : speed);
        }

        if (HasImpact)
            parts.Add(impactKills ? $"Kills everything within {Num(impactRadius, "0.#")}{Nbsp}m" : $"{Num(impactDamage, "0")} damage within {Num(impactRadius, "0.#")}{Nbsp}m");

        if (puddleLifetime > 0f && PuddleAffectsEnemies)
        {
            var puddle = new System.Collections.Generic.List<string>();
            if (enemySpeedMultiplier <= 0f) puddle.Add("paralyzes");
            else if (enemySpeedMultiplier < 1f) puddle.Add($"slows {Num((1f - enemySpeedMultiplier) * 100f, "0")}%");
            if (enemyDamagePerSecond > 0f) puddle.Add($"poisons {Num(enemyDamagePerSecond, "0")}/s");
            parts.Add("Puddle: " + string.Join(" and ", puddle));
        }

        return string.Join($"{Nbsp}· ", parts);
    }

    private static string Percent(float fraction) => $"{(fraction >= 0f ? "+" : "")}{Num(fraction * 100f, "0")}%";

    private static string Num(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}