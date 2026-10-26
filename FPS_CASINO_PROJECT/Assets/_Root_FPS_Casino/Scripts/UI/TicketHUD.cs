using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Icono del ticket que llevas (TicketInventory). Aparece al coger el ticket de la ruleta y
/// desaparece al dárselo al barman. Sin icono en el ticket, sale un cuadrado de su color.
/// </summary>
public class TicketHUD : MonoBehaviour
{
    [Tooltip("Image que muestra el ticket. Se oculta cuando no llevas ninguno.")]
    [SerializeField] private Image ticketIcon;

    private TicketInventory inventory;

    private void Start()
    {
        inventory = TicketInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[TicketHUD] No hay TicketInventory en la escena.", this);
            return;
        }

        inventory.OnTicketAdded += ShowTicket;
        inventory.OnTicketRemoved += HideTicket;

        ticketIcon.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (inventory == null) return;
        inventory.OnTicketAdded -= ShowTicket;
        inventory.OnTicketRemoved -= HideTicket;
    }

    private void ShowTicket(TicketData ticket)
    {
        // Si el ticket tiene icono, se usa; si no, un cuadrado liso del color del ticket
        ticketIcon.sprite = ticket.icon;
        ticketIcon.color = ticket.icon != null ? Color.white : ticket.ticketColor;
        ticketIcon.gameObject.SetActive(true);
    }

    private void HideTicket(TicketData ticket)
    {
        ticketIcon.gameObject.SetActive(false);
    }
}