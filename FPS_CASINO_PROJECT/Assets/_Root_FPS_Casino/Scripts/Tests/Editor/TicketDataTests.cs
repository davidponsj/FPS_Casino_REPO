using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Tests de los SO_Ticket_*: cada ticket da la botella de su mismo color
/// (SO_Ticket_Red → SO_BottleEffect_Red), igual que hacía la antigua BottleEffectLibrary.
/// </summary>
public class TicketDataTests
{
    [Test]
    public void CadaTicket_TieneLaBotellaDeSuColor()
    {
        string[] guids = AssetDatabase.FindAssets("t:TicketData SO_Ticket_");
        Assert.AreEqual(6, guids.Length, "Tendría que haber 6 tickets.");

        foreach (string guid in guids)
        {
            TicketData ticket = AssetDatabase.LoadAssetAtPath<TicketData>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsNotNull(ticket.bottleEffect, $"{ticket.name} no tiene BottleEffect.");
            string color = ticket.name.Substring("SO_Ticket_".Length);
            Assert.AreEqual("SO_BottleEffect_" + color, ticket.bottleEffect.name, ticket.name);
        }
    }
}
