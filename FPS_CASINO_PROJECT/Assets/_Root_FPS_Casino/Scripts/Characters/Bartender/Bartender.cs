using System;
using UnityEngine;

/// <summary>
/// El barman. Al pulsar E con un ticket en el TicketInventory, se lo quita y te da su botella
/// (BottleHolder.Give). Con una botella ya en la mano no hace nada: primero hay que gastarla.
/// Avisa con BottleGiven (al dar la botella) y Refused (al pulsar E sin ticket o con botella), para que
/// BartenderAnimation ponga la animación que toca.
/// </summary>
public class Bartender : MonoBehaviour, IInteractable
{
    [Header("Referencias")]
    [Tooltip("BottleHolder del jugador (en PF_Player): ahí aparece la botella.")]
    [SerializeField] private BottleHolder bottleHolder;
    [Tooltip("Bocadillo con lo que dice al darte la botella. Vacío = lo busca en este mismo objeto; si no hay, no dice nada.")]
    [SerializeField] private BartenderSpeech speech;

    [Header("Depuración")]
    [Tooltip("Muestra qué lleva el jugador (ticket, botella o nada) sobre el barman en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    /// <summary>Se acaba de dar una botella (con el ticket que se ha cambiado).</summary>
    public event Action<TicketData> BottleGiven;
    /// <summary>El jugador ha pulsado E pero no puede: sin ticket o con una botella aún en la mano.</summary>
    public event Action Refused;

    private void Awake()
    {
        if (speech == null) speech = GetComponent<BartenderSpeech>();
    }

    public string InteractionPrompt
    {
        get
        {
            var inv = TicketInventory.Instance;
            if (bottleHolder != null && bottleHolder.HasBottle) return "Use your bottle first";
            if (inv != null && inv.HasTicket) return "Trade ticket for a drink [E]";
            return "You need a ticket";
        }
    }

    public void Interact(GameObject interactor)
    {
        var inv = TicketInventory.Instance;
        if (inv == null || bottleHolder == null)
        {
            Debug.LogWarning("[Bartender] Falta TicketInventory o BottleHolder.", this);
            return;
        }

        if (bottleHolder.HasBottle || !inv.HasTicket)
        {
            Refused?.Invoke();
            return;
        }

        TicketData ticket = inv.Take();
        bottleHolder.Give(ticket);
        if (speech != null) speech.Say(ticket, bottleHolder);
        BottleGiven?.Invoke(ticket);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        var inv = TicketInventory.Instance;
        string carried = bottleHolder != null && bottleHolder.HasBottle ? "botella en la mano"
            : inv != null && inv.HasTicket ? $"ticket {inv.Ticket.displayName}"
            : "nada";
        DebugLabels.At(transform.position + Vector3.up * 2.2f, 0, $"Barman · el jugador lleva: {carried}", DebugLabels.Casino);
    }
#endif
}
