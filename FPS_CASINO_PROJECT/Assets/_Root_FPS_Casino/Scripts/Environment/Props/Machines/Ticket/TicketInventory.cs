using System;
using UnityEngine;

/// <summary>
/// El ticket que llevas encima (solo cabe uno). Lo mete el dispensador de la ruleta (TryAdd)
/// y lo saca el barman al darte la botella (Take). TicketHUD escucha sus eventos.
/// La ruleta mira HasTicket para saber si está bloqueada.
/// </summary>
public class TicketInventory : MonoBehaviour
{
    public static TicketInventory Instance { get; private set; }

    [Header("Depuración")]
    [Tooltip("Muestra el ticket que llevas (o \"vacío\") en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    public TicketData Ticket { get; private set; }
    public bool HasTicket => Ticket != null;

    public event Action<TicketData> OnTicketAdded;
    public event Action<TicketData> OnTicketRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TryAdd(TicketData ticket)
    {
        if (ticket == null || HasTicket) return false;
        Ticket = ticket;
        OnTicketAdded?.Invoke(ticket);
        return true;
    }

    /// <summary>Saca el ticket y lo devuelve, o null si no había.</summary>
    public TicketData Take()
    {
        TicketData ticket = Ticket;
        if (ticket == null) return null;
        Ticket = null;
        OnTicketRemoved?.Invoke(ticket);
        return ticket;
    }

    [ContextMenu("TEST: Quitar ticket")]
    private void TestTake() => Take();

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f,
            $"Ticket: {(HasTicket ? Ticket.displayName : "vacío")}");
    }
#endif
}
