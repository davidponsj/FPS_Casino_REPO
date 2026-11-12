using System;
using UnityEngine;

/// <summary>
/// El barman, en dos pasos para aprovechar sus animaciones:
/// 1. Pulsas E con un ticket en el TicketInventory: se lo queda y empieza a sacar la botella (AC_BOTTLE). Mientras la
///    saca, la E no hace nada.
/// 2. Cuando ya la tiene en la mano (AC_BOTTLE_idle, esperando), vuelves a pulsar E: te la da (BottleHolder.Give), dice
///    su frase y vuelve a la pose normal. Si no vienes a por ella, te espera con la botella en la mano.
/// Sin ticket, o con una botella aún en la mano, pulsar E es un "no" (AC_NOTICKET).
/// Avisa con TicketTaken, BottleGiven y Refused para que BartenderAnimation ponga la animación que toca.
/// </summary>
public class Bartender : MonoBehaviour, IInteractable
{
    [Header("Referencias")]
    [Tooltip("BottleHolder del jugador (en PF_Player): ahí aparece la botella.")]
    [SerializeField] private BottleHolder bottleHolder;
    [Tooltip("Bocadillo con lo que dice al darte la botella. Vacío = lo busca en este mismo objeto; si no hay, no dice nada.")]
    [SerializeField] private BartenderSpeech speech;

    [Header("Botella")]
    [Tooltip("Segundos que tarda en sacar la botella antes de que puedas cogerla. Si hay BartenderAnimation, se usa lo que dura AC_BOTTLE.")]
    [Min(0f)]
    [SerializeField] private float prepareSeconds = 2.5f;

    [Header("Depuración")]
    [Tooltip("Muestra qué lleva el jugador y qué está haciendo el barman sobre él en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    /// <summary>Se ha quedado el ticket y empieza a sacar la botella.</summary>
    public event Action<TicketData> TicketTaken;
    /// <summary>Te ha dado la botella (segunda E).</summary>
    public event Action<TicketData> BottleGiven;
    /// <summary>El jugador ha pulsado E pero no puede: sin ticket o con una botella aún en la mano.</summary>
    public event Action Refused;

    private TicketData pendingTicket;   // la botella que está sacando o que tiene en la mano esperando
    private float readyTime;

    /// <summary>Tiene una botella a medio sacar o esperando a que la cojas.</summary>
    public bool IsBusy => pendingTicket != null;
    /// <summary>Ya tiene la botella en la mano: la siguiente E te la da.</summary>
    public bool IsBottleReady => pendingTicket != null && Time.time >= readyTime;

    /// <summary>Lo que tarda en sacar la botella (BartenderAnimation lo ajusta a la duración de AC_BOTTLE).</summary>
    public float PrepareSeconds
    {
        get => prepareSeconds;
        set => prepareSeconds = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        if (speech == null) speech = GetComponent<BartenderSpeech>();
    }

    public string InteractionPrompt
    {
        get
        {
            if (pendingTicket != null)
            {
                if (!IsBottleReady) return "Getting your drink...";
                if (bottleHolder != null && bottleHolder.HasBottle) return "Use your bottle first";
                return "Take your drink [E]";
            }
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

        // Segunda E: coger la botella que ya tiene en la mano.
        if (pendingTicket != null)
        {
            if (!IsBottleReady) return;                 // aún la está sacando
            if (bottleHolder.HasBottle)
            {
                Refused?.Invoke();
                return;
            }
            TicketData ticket = pendingTicket;
            pendingTicket = null;
            bottleHolder.Give(ticket);
            if (speech != null) speech.Say(ticket, bottleHolder);
            BottleGiven?.Invoke(ticket);
            return;
        }

        // Primera E: darle el ticket.
        if (bottleHolder.HasBottle || !inv.HasTicket)
        {
            Refused?.Invoke();
            return;
        }
        pendingTicket = inv.Take();
        readyTime = Time.time + prepareSeconds;
        TicketTaken?.Invoke(pendingTicket);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        var inv = TicketInventory.Instance;
        string carried = bottleHolder != null && bottleHolder.HasBottle ? "botella en la mano"
            : inv != null && inv.HasTicket ? $"ticket {inv.Ticket.displayName}"
            : "nada";
        string state = pendingTicket == null ? "libre"
            : IsBottleReady ? $"esperando con {pendingTicket.displayName}"
            : $"sacando {pendingTicket.displayName} ({readyTime - Time.time:0.0} s)";
        DebugLabels.At(transform.position + Vector3.up * 2.2f, 0, $"Barman · {state} · el jugador lleva: {carried}", DebugLabels.Casino);
    }
#endif
}
