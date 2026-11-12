using NUnit.Framework;

/// <summary>
/// Tests de la subida de la apuesta de la tragaperras con la ronda.
/// Se ejecutan en Window > General > Test Runner > EditMode, o con tasks/test.sh (Unity cerrado).
/// </summary>
public class SlotBetButtonTests
{
    [TestCase(100, 1, 100)]
    [TestCase(100, 0, 100)]  // antes de empezar la ronda 1
    [TestCase(100, 2, 110)]
    [TestCase(100, 10, 190)]
    [TestCase(50, 2, 55)]
    [TestCase(250, 4, 325)]
    [TestCase(50, 4, 65)]    // 65 justo
    [TestCase(50, 3, 60)]
    public void ScaledBet_SubeUnDiezPorCientoPorRondaRedondeadoA5(int baseBet, int round, int expected)
    {
        Assert.AreEqual(expected, SlotBetButton.ScaledBet(baseBet, 10f, round));
    }

    [Test]
    public void ScaledBet_SinSubida_SiempreLaBase()
    {
        Assert.AreEqual(250, SlotBetButton.ScaledBet(250, 0f, 20));
    }
}
