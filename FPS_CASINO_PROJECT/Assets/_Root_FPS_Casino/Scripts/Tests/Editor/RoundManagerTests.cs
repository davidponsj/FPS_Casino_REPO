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

    // Vida de CoD Zombies: +100 por ronda hasta la 9, luego ×1,1 (valores de la wiki).
    // El margen de 1 es porque la wiki redondea y Mathf.Pow con float tiene algo de error.
    [TestCase(1, 150f)]
    [TestCase(2, 250f)]
    [TestCase(9, 950f)]
    [TestCase(10, 1045f)]
    [TestCase(11, 1149.5f)]
    [TestCase(15, 1683f)]
    [TestCase(30, 7030f)]
    public void HealthForRound_SigueLaCurvaDeCoD(int round, float expected)
    {
        Assert.AreEqual(expected, RoundManager.HealthForRound(round, 150f, 100f, 10, 1.1f), 1f);
    }
}