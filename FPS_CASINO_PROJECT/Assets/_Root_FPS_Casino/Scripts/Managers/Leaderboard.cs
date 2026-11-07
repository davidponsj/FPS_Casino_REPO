using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ranking local del juego: las 10 mejores partidas, guardadas en el ordenador (PlayerPrefs, como las teclas del menú).
/// El puesto lo decide SOLO TotalEarned (todos los puntos ganados en la partida, aunque luego los gastaras apostando).
/// Cada partida guarda además la ronda, las bajas y el tiempo, solo para enseñarlos.
///
/// No va en ningún objeto: es una clase estática. La usa DeathScreen al morir (y podría usarla el menú para enseñar el ranking).
/// </summary>
public static class Leaderboard
{
    [Serializable]
    public class Entry
    {
        public string name;
        public int points;  // TotalEarned: lo que decide el puesto
        public int round;
        public int kills;
        public float seconds;
    }

    [Serializable]
    private class SaveData
    {
        public List<Entry> entries = new List<Entry>();
    }

    /// <summary>Partidas que se guardan.</summary>
    public const int MaxEntries = 10;
    private const string PrefsKey = "Casino_Leaderboard";

    private static SaveData data;

    /// <summary>Las partidas del ranking, de la mejor a la peor.</summary>
    public static IReadOnlyList<Entry> Entries => Load().entries;

    /// <summary>True si una partida con estos puntos entra en el ranking (hay hueco o supera a la última).</summary>
    public static bool Qualifies(int points)
    {
        List<Entry> entries = Load().entries;
        return entries.Count < MaxEntries || points > entries[entries.Count - 1].points;
    }

    /// <summary>Guarda la partida y devuelve su puesto (0 = primera). -1 si no ha entrado.</summary>
    public static int Add(Entry entry)
    {
        List<Entry> entries = Load().entries;
        entries.Add(entry);
        Sort(entries);

        if (entries.Count > MaxEntries)
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

        Save();
        return entries.IndexOf(entry);
    }

    /// <summary>Borra el ranking entero (para pruebas).</summary>
    public static void Clear()
    {
        Load().entries.Clear();
        Save();
    }

    private static SaveData Load()
    {
        if (data != null) return data;

        string json = PlayerPrefs.GetString(PrefsKey, "");
        data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);
        if (data == null) data = new SaveData();
        Sort(data.entries);
        return data;
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Load()));
        PlayerPrefs.Save();
    }

    /// <summary>Más puntos primero; si empatan, la que llegó más lejos (ronda) y luego la que más bajas hizo.</summary>
    private static void Sort(List<Entry> entries)
    {
        entries.Sort((a, b) =>
        {
            int byPoints = b.points.CompareTo(a.points);
            if (byPoints != 0) return byPoints;
            int byRound = b.round.CompareTo(a.round);
            return byRound != 0 ? byRound : b.kills.CompareTo(a.kills);
        });
    }
}
