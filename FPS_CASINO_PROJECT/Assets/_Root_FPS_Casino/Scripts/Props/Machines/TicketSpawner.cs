using System.Collections;
using UnityEngine;

public class TicketSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float slideDistance = 0.25f;
    [SerializeField] private float slideDuration = 0.6f;

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
        Vector3 start = end - point.forward * slideDistance;

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
        if (waitingTicket == null || TicketInventory.Instance == null)
            return false;

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
        while (elapsed < slideDuration)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / slideDuration));
            yield return null;
        }
        if (t != null) t.position = to;
    }
}