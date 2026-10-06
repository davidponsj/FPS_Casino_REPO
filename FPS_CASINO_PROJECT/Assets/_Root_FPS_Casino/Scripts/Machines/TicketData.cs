using UnityEngine;

[CreateAssetMenu(menuName = "Casino/Ticket", fileName = "NuevoTicket")]
public class TicketData : ScriptableObject
{
    public Color ticketColor = Color.white;
    public string displayName = "Ticket";
    public Sprite icon;
    public GameObject worldPrefab;
    public string bottleId;
}
