using System.Collections;
using UnityEngine;

public class TicketSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float slideDistance = 0.25f;
    [SerializeField] private float slideDuration = 0.6f;

    public void Spawn(RouletteMachine.Segment segment)
    {
        if (segment == null || segment.ticket == null || segment.ticket.worldPrefab == null)
            return;

        Transform point = spawnPoint ? spawnPoint : transform;
        Vector3 end = point.position;
        Vector3 start = end - point.forward * slideDistance;

        GameObject obj = Instantiate(segment.ticket.worldPrefab, start, point.rotation);

        var pickup = obj.GetComponent<TicketPickup>();
        if (pickup != null)
            pickup.SetData(segment.ticket);

        var rend = obj.GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = segment.ticket.ticketColor;

        StartCoroutine(Slide(obj.transform, start, end));
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