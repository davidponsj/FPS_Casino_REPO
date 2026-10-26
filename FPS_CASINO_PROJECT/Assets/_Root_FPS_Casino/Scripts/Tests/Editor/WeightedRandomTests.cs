using NUnit.Framework;

/// <summary>
/// Tests de WeightedRandom (elección por pesos de la ruleta y la tragaperras).
/// La tirada va de 0 a 1 y se pasa desde fuera para que el resultado sea fijo.
/// </summary>
public class WeightedRandomTests
{
    private static float Identity(float w) => w;

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void PesoCero_NuncaSale(float roll)
    {
        Assert.AreEqual(1, WeightedRandom.PickIndex(new[] { 0f, 1f, 0f }, Identity, roll));
    }

    [TestCase(0f)]
    [TestCase(1f)]
    public void UnSoloElemento_SaleSiempre(float roll)
    {
        Assert.AreEqual(0, WeightedRandom.PickIndex(new[] { 5f }, Identity, roll));
    }

    [TestCase(0.24f, 0)]
    [TestCase(0.26f, 1)]
    [TestCase(0.99f, 1)]
    public void Tirada_CaeEnElTramoDeSuPeso(float roll, int expected)
    {
        // Pesos 1 y 3: el primero ocupa el 25 % de la tirada.
        Assert.AreEqual(expected, WeightedRandom.PickIndex(new[] { 1f, 3f }, Identity, roll));
    }

    [Test]
    public void PesosNegativos_CuentanComoCero()
    {
        Assert.AreEqual(1, WeightedRandom.PickIndex(new[] { -5f, 1f }, Identity, 0f));
    }

    [Test]
    public void TodosLosPesosCero_DevuelveElPrimero()
    {
        Assert.AreEqual(0, WeightedRandom.PickIndex(new[] { 0f, 0f }, Identity, 0.5f));
    }

    [Test]
    public void ListaVacia_DevuelveMenosUno()
    {
        Assert.AreEqual(-1, WeightedRandom.PickIndex(new float[0], Identity, 0.5f));
    }

    [Test]
    public void Reparto_SigueLosPesos()
    {
        UnityEngine.Random.InitState(1234);
        float[] weights = { 1f, 2f, 1f };
        int[] counts = new int[3];
        const int rolls = 10000;
        for (int i = 0; i < rolls; i++) counts[WeightedRandom.PickIndex(weights, Identity)]++;

        Assert.AreEqual(0.25f, counts[0] / (float)rolls, 0.03f);
        Assert.AreEqual(0.50f, counts[1] / (float)rolls, 0.03f);
        Assert.AreEqual(0.25f, counts[2] / (float)rolls, 0.03f);
    }
}
