using NUnit.Framework;

/// <summary>
/// Tests de las cuentas de RobotLights (parpadeo del pinganillo y volumen de la voz).
/// Se ejecutan en Window > General > Test Runner > EditMode, o con tasks/test.sh (Unity cerrado).
/// </summary>
public class RobotLightsTests
{
    [TestCase(0.1f, 0f, true)]
    [TestCase(0.5f, 0f, false)]
    [TestCase(1.1f, 0f, true)]   // segundo ciclo
    [TestCase(0.1f, 0.5f, false)] // desfasado medio ciclo
    public void IsBlinkOn_EncendidoYApagadoPorCiclo(float time, float offset, bool expected)
    {
        Assert.AreEqual(expected, RobotLights.IsBlinkOn(time, 0.4f, 0.6f, offset));
    }

    [Test]
    public void Loudness_Silencio_EsCero()
    {
        Assert.AreEqual(0f, RobotLights.Loudness(new float[8], 8), 0.0001f);
    }

    [Test]
    public void Loudness_SenalConstante_EsSuValor()
    {
        float[] samples = { 0.5f, -0.5f, 0.5f, -0.5f };
        Assert.AreEqual(0.5f, RobotLights.Loudness(samples, 4), 0.0001f);
    }

    [Test]
    public void Loudness_SoloCuentaLasPrimerasCount()
    {
        float[] samples = { 0.5f, 0.5f, 1f, 1f };
        Assert.AreEqual(0.5f, RobotLights.Loudness(samples, 2), 0.0001f);
    }
}
