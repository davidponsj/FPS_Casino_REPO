using UnityEngine;

public class TicketSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    public void Spawn(RouletteMachine.Segment segment)
    {
        if (segment == null || segment.ticket == null || segment.ticket.worldPrefab == null)
            return;

        Transform point = spawnPoint ? spawnPoint : transform;
        GameObject obj = Instantiate(segment.ticket.worldPrefab, point.position, point.rotation);

        var pickup = obj.GetComponent<TicketPickup>();
        if (pickup != null)
            pickup.SetData(segment.ticket);
    }
}
