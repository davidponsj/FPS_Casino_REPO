using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Por donde sale el ticket que gana la ruleta. RouletteMachine llama a Spawn(segmento): el ticket
/// queda "esperando" y, si tiene worldPrefab, sale deslizándose desde spawnPoint. Al pulsar E sobre
/// la ruleta, esta llama a TryCollect() y el ticket pasa al TicketInventory.
/// </summary>
public class RouletteTicketDispenser : MonoBehaviour
{
    [Header("Salida del ticket")]
    [Tooltip("Dónde acaba el ticket; sale desde detrás (eje Z azul) hacia aquí. Vacío = este objeto.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Metros que recorre el ticket al salir. 0.25 = asoma un palmo.")]
    [FormerlySerializedAs("slideDistance")]
    [Min(0f)]
    [SerializeField] private float slideDistanceMeters = 0.25f;
    [Tooltip("Segundos que tarda en salir. 0.6 = salida suave.")]
    [FormerlySerializedAs("slideDuration")]
    [Min(0.01f)]
    [SerializeField] private float slideSeconds = 0.6f;

    [Header("Depuración")]
    [Tooltip("Muestra el ticket que está esperando (o \"vacío\") en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private GameObject spawnedObject;
    private TicketData waitingTicket;

    public bool HasTicketWaiting => waitingTicket != null;
    public string WaitingTicketName => waitingTicket != null ? waitingTicket.displayName : "ticket";

    public void Spawn(RouletteMachine.Segment segment)
    {
        if (segment == null || segment.ticket == null)
            return;

        // El ticket queda "esperando" aunque no haya prefab, para poder cogerlo desde la ruleta
        waitingTicket = segment.ticket;

        if (segment.ticket.worldPrefab == null)
            return;

        Transform point = spawnPoint ? spawnPoint : transform;
        Vector3 end = point.position;
        Vector3 start = end - point.forward * slideDistanceMeters;

        spawnedObject = Instantiate(segment.ticket.worldPrefab, start, point.rotation);

        // Sin colliders: el ticket ya no se coge mirándolo, sino desde la ruleta
        foreach (var col in spawnedObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        var rend = spawnedObject.GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = segment.ticket.ticketColor;

        StartCoroutine(Slide(spawnedObject.transform, start, end));
    }

    // Lo llama la ruleta cuando el jugador pulsa E sobre ella
    public bool TryCollect()
    {
        if (waitingTicket == null) return false;

        if (TicketInventory.Instance == null)
        {
            Debug.LogWarning("[Dispensador] No hay TicketInventory en la escena: no se puede coger el ticket.", this);
            return false;
        }

        if (!TicketInventory.Instance.TryAdd(waitingTicket))
            return false;

        if (spawnedObject != null)
            Destroy(spawnedObject);

        spawnedObject = null;
        waitingTicket = null;
        return true;
    }

    private IEnumerator Slide(Transform t, Vector3 from, Vector3 to)
    {
        float elapsed = 0f;
        while (elapsed < slideSeconds)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / slideSeconds));
            yield return null;
        }
        if (t != null) t.position = to;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        Transform point = spawnPoint ? spawnPoint : transform;
        DebugLabels.At(point.position + Vector3.up * 0.3f, 0, $"Ticket esperando: {(waitingTicket != null ? waitingTicket.displayName : "vacío")}", DebugLabels.Casino);
    }
#endif
}
