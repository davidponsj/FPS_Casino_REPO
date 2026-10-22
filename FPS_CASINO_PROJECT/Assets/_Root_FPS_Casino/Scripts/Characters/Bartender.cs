using UnityEngine;

public class Bartender : MonoBehaviour, IInteractable
{
    [SerializeField] private BottleHolder bottleHolder;

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

        if (bottleHolder.HasBottle) return;

        if (!inv.HasTicket)
        {
            Debug.Log("[Bartender] No tienes ticket.");
            return;
        }

        TicketData ticket = inv.Tickets[0];
        inv.Consume();   // quita el ticket (esto desbloquea la ruleta...)
        inv.Lock();      // ...así que la volvemos a bloquear hasta beber la botella
        bottleHolder.Give(ticket);
    }
}