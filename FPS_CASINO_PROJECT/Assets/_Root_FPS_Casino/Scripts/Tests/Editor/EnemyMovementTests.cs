using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EnemyMovement como IMovementModifiable: cada charco pone y quita su multiplicador,
/// y manda el más lento.
/// </summary>
public class EnemyMovementTests
{
    private GameObject enemy;
    private EnemyMovement movement;
    private readonly object puddleA = new object();
    private readonly object puddleB = new object();

    [SetUp]
    public void SetUp()
    {
        enemy = new GameObject("Enemy");
        movement = enemy.AddComponent<EnemyMovement>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(enemy);
    }

    [Test]
    public void SinCharcos_VaANormal()
    {
        Assert.AreEqual(1f, movement.SlowMultiplier);
    }

    [Test]
    public void DosCharcos_MandaElMasLento()
    {
        movement.SetSpeedMultiplier(puddleA, 0.5f);
        movement.SetSpeedMultiplier(puddleB, 0.2f);
        Assert.AreEqual(0.2f, movement.SlowMultiplier, 0.0001f);
    }

    [Test]
    public void SaleDeUnCharco_QuedaElOtro()
    {
        movement.SetSpeedMultiplier(puddleA, 0.5f);
        movement.SetSpeedMultiplier(puddleB, 0.2f);
        movement.ClearSpeedMultiplier(puddleB);
        Assert.AreEqual(0.5f, movement.SlowMultiplier, 0.0001f);

        movement.ClearSpeedMultiplier(puddleA);
        Assert.AreEqual(1f, movement.SlowMultiplier);
    }
}
