using System;
using System.Collections.Generic;
using UnityEngine;

public class TicketInventory : MonoBehaviour
{
    public static TicketInventory Instance { get; private set; }

    private readonly List<TicketData> tickets = new List<TicketData>();
    public IReadOnlyList<TicketData> Tickets => tickets;
    public bool HasTicket => tickets.Count > 0;
    public bool IsLocked { get; private set; }

    public event Action<TicketData> OnTicketAdded;
    public event Action<TicketData> OnTicketRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Lock() => IsLocked = true;

    public bool TryAdd(TicketData ticket)
    {
        if (ticket == null || HasTicket) return false;
        tickets.Add(ticket);
        OnTicketAdded?.Invoke(ticket);
        return true;
    }

    public bool Consume()
    {
        if (!HasTicket) return false;
        TicketData ticket = tickets[0];
        tickets.RemoveAt(0);
        IsLocked = false;
        OnTicketRemoved?.Invoke(ticket);
        return true;
    }

    public void ResetState()
    {
        tickets.Clear();
        IsLocked = false;
    }

    [ContextMenu("TEST: Consumir ticket")]
    private void TestConsume() => Consume();
}
