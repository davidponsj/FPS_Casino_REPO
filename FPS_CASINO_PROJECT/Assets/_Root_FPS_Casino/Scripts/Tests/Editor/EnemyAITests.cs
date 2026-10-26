using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EnemyAI.FlatDistance: la distancia que usan la IA y HordeAttackSlots ignora la altura.
/// </summary>
public class EnemyAITests
{
    [Test]
    public void FlatDistance_IgnoraLaAltura()
    {
        Assert.AreEqual(5f, EnemyAI.FlatDistance(new Vector3(0f, 0f, 0f), new Vector3(3f, 10f, 4f)), 0.0001f);
    }

    [Test]
    public void FlatDistance_MismoPuntoEnPlanta_EsCero()
    {
        Assert.AreEqual(0f, EnemyAI.FlatDistance(new Vector3(1f, -2f, 1f), new Vector3(1f, 7f, 1f)), 0.0001f);
    }
}
