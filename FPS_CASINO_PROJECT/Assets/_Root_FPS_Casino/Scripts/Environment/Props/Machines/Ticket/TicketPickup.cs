using UnityEngine;

public class TicketPickup : MonoBehaviour, IInteractable
{
    public void SetData(TicketData newData) => data = newData;

    [SerializeField] private TicketData data;

    public string InteractionPrompt =>
        data ? $"Coger {data.displayName} [E]" : "Coger ticket [E]";

    public void Interact(GameObject interactor)
    {
        if (data == null || TicketInventory.Instance == null)
        {
            Debug.LogWarning("[Ticket] Falta TicketData o TicketInventory en la escena.", this);
            return;
        }

        if (TicketInventory.Instance.TryAdd(data))
            Destroy(gameObject);
    }
}
