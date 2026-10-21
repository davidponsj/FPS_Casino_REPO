using UnityEngine;
using UnityEngine.UI;

public class TicketHUD : MonoBehaviour
{
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