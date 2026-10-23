using UnityEngine;

/// <summary>
/// Un ticket de la ruleta (Create → Casino → Ticket). El barman lo cambia por la botella
/// de bottleEffect, que dice qué pasa al beberla o lanzarla.
/// </summary>
[CreateAssetMenu(menuName = "Casino/Ticket", fileName = "NuevoTicket")]
public class TicketData : ScriptableObject
{
    public Color ticketColor = Color.white;
    public string displayName = "Ticket";
    public Sprite icon;
    public GameObject worldPrefab;
    [Tooltip("Botella que da el barman por este ticket (SO_BottleEffect_*).")]
    public BottleEffect bottleEffect;
}
