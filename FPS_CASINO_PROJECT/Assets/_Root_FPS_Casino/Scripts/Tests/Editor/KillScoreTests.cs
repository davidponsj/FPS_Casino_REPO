using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Tests de las cuentas de KillScore (multiplicador del combo, 360° y orden de las líneas).
/// Se ejecutan en Window > General > Test Runner > EditMode, o con tasks/test.sh (Unity cerrado).
/// </summary>
public class KillScoreTests
{
    [TestCase(1, 1f)]
    [TestCase(2, 1.1f)]
    [TestCase(11, 2f)]
    [TestCase(21, 3f)]
    [TestCase(50, 3f)] // tope ×3
    public void GetComboMultiplier_SubePorBajaHastaElTope(int kills, float expected)
    {
        Assert.AreEqual(expected, KillScore.GetComboMultiplier(kills, 0.1f, 3f), 0.001f);
    }

    [Test]
    public void SpinTracker_VueltaRapida_Cuenta360()
    {
        var spin = new KillScore.SpinTracker(64);
        for (int i = 0; i < 4; i++) spin.Add(10f + i * 0.1f, 95f);

        Assert.GreaterOrEqual(spin.NetDegrees(10.3f, 1f), 360f);
    }

    [Test]
    public void SpinTracker_VueltaLenta_NoCuenta()
    {
        var spin = new KillScore.SpinTracker(64);
        for (int i = 0; i < 4; i++) spin.Add(10f + i * 0.6f, 95f); // 2 s en dar la vuelta

        Assert.Less(spin.NetDegrees(11.8f, 1f), 360f);
    }

    [Test]
    public void SpinTracker_IdaYVuelta_NoCuenta()
    {
        var spin = new KillScore.SpinTracker(64);
        spin.Add(10f, 200f);
        spin.Add(10.1f, -200f);

        Assert.AreEqual(0f, spin.NetDegrees(10.1f, 1f), 0.001f);
    }

    [Test]
    public void SortByPoints_DeMenosAMas_YLosEmpatesMantienenSuOrden()
    {
        var lines = new List<KillScore.ScoreLine>
        {
            new KillScore.ScoreLine { Label = "Baja", Points = 100 },
            new KillScore.ScoreLine { Label = "360°", Points = 300 },
            new KillScore.ScoreLine { Label = "A quemarropa", Points = 50 },
            new KillScore.ScoreLine { Label = "Aéreo", Points = 100 },
        };

        KillScore.SortByPoints(lines);

        CollectionAssert.AreEqual(
            new[] { "A quemarropa", "Baja", "Aéreo", "360°" },
            lines.ConvertAll(line => line.Label));
    }
}