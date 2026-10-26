using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elección al azar por pesos: un elemento con peso 2 sale el doble que uno con peso 1, y uno con peso 0
/// no sale nunca. La usan la ruleta (sus segmentos) y la tragaperras (sus símbolos).
/// </summary>
public static class WeightedRandom
{
    /// <summary>
    /// Índice elegido al azar según el peso de cada elemento. Los pesos negativos cuentan como 0.
    /// Si todos pesan 0 devuelve 0; si la lista está vacía, -1.
    /// </summary>
    public static int PickIndex<T>(IReadOnlyList<T> items, Func<T, float> weight) =>
        PickIndex(items, weight, UnityEngine.Random.value);

    /// <summary>Igual que el anterior, con la tirada (de 0 a 1) dada desde fuera. Sirve para los tests.</summary>
    public static int PickIndex<T>(IReadOnlyList<T> items, Func<T, float> weight, float roll01)
    {
        if (items.Count == 0) return -1;

        float total = 0f;
        for (int i = 0; i < items.Count; i++) total += Mathf.Max(0f, weight(items[i]));
        if (total <= 0f) return 0;

        float roll = Mathf.Clamp01(roll01) * total;
        float accumulated = 0f;
        int lastWithWeight = 0;
        for (int i = 0; i < items.Count; i++)
        {
            float w = Mathf.Max(0f, weight(items[i]));
            if (w <= 0f) continue;

            accumulated += w;
            lastWithWeight = i;
            if (roll < accumulated) return i;
        }
        // Solo llega aquí con una tirada de exactamente 1.
        return lastWithWeight;
    }
}
