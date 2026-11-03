using NUnit.Framework;

/// <summary>
/// Tests del precio de la munición de las armas de pared (parte del precio del arma).
/// Se ejecutan en Window > General > Test Runner > EditMode, o con tasks/test.sh (Unity cerrado).
/// </summary>
public class WallWeaponPickupTests
{
    [TestCase(1500, 0.5f, 750)]
    [TestCase(500, 0.5f, 250)]
    [TestCase(0, 0.5f, 0)]
    [TestCase(1500, 1f, 1500)]
    public void AmmoPrice_EsLaParteDelPrecio(int price, float fraction, int expected)
    {
        Assert.AreEqual(expected, WallWeaponPickup.AmmoPrice(price, fraction));
    }
}
