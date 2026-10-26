using UnityEngine;

/// <summary>
/// El barman. Al pulsar E con un ticket en el TicketInventory, se lo quita y te da su botella
/// (BottleHolder.Give). Con una botella ya en la mano no hace nada: primero hay que gastarla.
/// </summary>
public class Bartender : MonoBehaviour, IInteractable
{
    [Header("Referencias")]
    [Tooltip("BottleHolder del jugador (en PF_Player): ahí aparece la botella.")]
    [SerializeField] private BottleHolder bottleHolder;

    [Header("Depuración")]
    [Tooltip("Muestra qué lleva el jugador (ticket, botella o nada) sobre el barman en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    public string InteractionPrompt
    {
        get
        {
            var inv = TicketInventory.Instance;
            if (bottleHolder != null && bottleHolder.HasBottle) return "Usa la botella";
            if (inv != null && inv.HasTicket) return "Cambiar ticket por bebida [E]";
            return "Necesitas un ticket";
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

        if (bottleHolder.HasBottle || !inv.HasTicket) return;

        bottleHolder.Give(inv.Take());
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
