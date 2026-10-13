using System;
using UnityEngine;

/// <summary>
/// Tabla que dice qué efecto tiene cada ticket (Create → Casino → Bottle Effect Library).
/// Así no hay que tocar TicketData (que es de las máquinas): el BottleHolder y la botella lanzada
/// le preguntan a esta tabla "¿qué hace la botella de este ticket?".
/// </summary>
[CreateAssetMenu(menuName = "Casino/Bottle Effect Library", fileName = "BottleEffectLibrary")]
public class BottleEffectLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public TicketData ticket;
        public BottleEffect effect;
    }

    [SerializeField] private Entry[] entries;

    /// <summary>El efecto del ticket, o null si no está en la tabla.</summary>
    public BottleEffect Get(TicketData ticket)
    {
        if (ticket == null || entries == null) return null;

        foreach (Entry entry in entries)
        {
            if (entry.ticket == ticket)
                return entry.effect;
        }

        Debug.LogWarning($"[Botellas] El ticket {ticket.displayName} no tiene efecto en la BottleEffectLibrary.", this);
        return null;
    }
}