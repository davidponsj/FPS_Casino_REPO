using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de TicketInventory: guarda un solo ticket, y Take() lo saca y lo devuelve.
/// </summary>
public class TicketInventoryTests
{
    private GameObject go;
    private TicketInventory inventory;
    private TicketData red, blue;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TicketInventory");
        inventory = go.AddComponent<TicketInventory>();
        red = ScriptableObject.CreateInstance<TicketData>();
        blue = ScriptableObject.CreateInstance<TicketData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(red);
        Object.DestroyImmediate(blue);
    }

    [Test]
    public void SoloCabeUnTicket()
    {
        Assert.IsTrue(inventory.TryAdd(red));
        Assert.IsFalse(inventory.TryAdd(blue));
        Assert.AreSame(red, inventory.Ticket);
    }

    [Test]
    public void Take_DevuelveElTicketYDejaVacio()
    {
        inventory.TryAdd(red);
        Assert.AreSame(red, inventory.Take());
        Assert.IsFalse(inventory.HasTicket);
        Assert.IsNull(inventory.Take());
    }

    [Test]
    public void TryAdd_NullNoCuenta()
    {
        Assert.IsFalse(inventory.TryAdd(null));
        Assert.IsFalse(inventory.HasTicket);
    }
}
