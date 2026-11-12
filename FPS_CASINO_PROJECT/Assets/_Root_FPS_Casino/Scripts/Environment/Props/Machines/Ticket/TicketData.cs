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
    [Tooltip("Modelo del ticket que sale de la ruleta (PF_Ticket).")]
    public GameObject worldPrefab;
    [Tooltip("Material de este color para el modelo (M_Ticket_Verde...). Vacío = el modelo se tiñe con Ticket Color.")]
    public Material worldMaterial;
    [Tooltip("Botella que da el barman por este ticket (SO_BottleEffect_*).")]
    public BottleEffect bottleEffect;

    [Header("Barman")]
    [Tooltip("Lo que dice el barman al darte esta botella (una frase al azar, sin repetir la anterior). Que cuente qué hace al beberla y al lanzarla, corto: se lee en plena partida. Lo que vaya entre *asteriscos* sale resaltado con el color de la botella.")]
    [TextArea(2, 4)]
    public string[] bartenderLines;
}
