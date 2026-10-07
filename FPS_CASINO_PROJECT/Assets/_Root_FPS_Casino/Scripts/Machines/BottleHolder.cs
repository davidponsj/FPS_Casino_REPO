using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BottleHolder : MonoBehaviour
{
    [SerializeField] private GameObject bottleModel;
    [SerializeField] private Key drinkKey = Key.Q;
    [Tooltip("Duración del efecto en segundos (de momento solo se muestra en la Console)")]
    [SerializeField] private float effectDuration = 10f;

    private TicketData current;

    public bool HasBottle => current != null;

    private void Awake()
    {
        if (bottleModel != null) bottleModel.SetActive(false);
    }

    public void Give(TicketData ticket)
    {
        current = ticket;

        // Pinta el cilindro con el color del ticket
        var rend = bottleModel.GetComponentInChildren<Renderer>(true);
        if (rend != null)
        {
            Color c = ticket.ticketColor;
            c.a = 1f;
            rend.material.color = c;
        }

        bottleModel.SetActive(true);
        Debug.Log($"[Botella] Recibes botella {ticket.displayName}. Pulsa {drinkKey} para beber.");
    }

    private void Update()
    {
        if (!HasBottle || Keyboard.current == null) return;

        if (Keyboard.current[drinkKey].wasPressedThisFrame)
            Drink();
    }

    private void Drink()
    {
        TicketData drunk = current;
        current = null;
        bottleModel.SetActive(false);

        Debug.Log($"[Botella] Has bebido botella {drunk.displayName}");

        if (TicketInventory.Instance != null)
            TicketInventory.Instance.Unlock();   // ahora sí se puede volver a jugar

        StartCoroutine(EffectRoutine(drunk));
    }

    private IEnumerator EffectRoutine(TicketData drunk)
    {
        Debug.Log($"[Botella] Efecto de {drunk.displayName} activo durante {effectDuration}s");
        yield return new WaitForSeconds(effectDuration);
        Debug.Log($"[Botella] Efecto de {drunk.displayName} terminado");
    }
}