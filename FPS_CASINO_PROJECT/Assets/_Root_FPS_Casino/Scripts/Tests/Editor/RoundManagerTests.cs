using NUnit.Framework;

/// <summary>
/// Tests de la curva de rondas de RoundManager. Se ejecutan en Window > General > Test Runner > EditMode,
/// o con tasks/test.sh (Unity cerrado).
/// </summary>
public class RoundManagerTests
{
    // Valores conocidos de CoD Zombies en solitario.
    [TestCase(1, 6)]
    [TestCase(2, 8)]
    [TestCase(3, 13)]
    [TestCase(4, 18)]
    [TestCase(5, 24)]
    [TestCase(6, 27)]
    [TestCase(10, 33)]
    [TestCase(20, 60)]
    [TestCase(30, 105)]
    public void EnemiesForRound_SigueLaCurvaDeCoD(int round, int expected)
    {
        Assert.AreEqual(expected, RoundManager.EnemiesForRound(round));
    }
}
