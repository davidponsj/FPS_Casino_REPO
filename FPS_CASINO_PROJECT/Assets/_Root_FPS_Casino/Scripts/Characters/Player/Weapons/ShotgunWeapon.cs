using UnityEngine;

/// <summary>
/// Escopeta: semi-automática como la pistola (un disparo por pulsación), pero cada disparo
/// suelta varios perdigones a la vez. Cada perdigón es un FireHitscanShot independiente, así
/// que cada uno coge su propia dirección aleatoria dentro de la dispersión actual: los perdigones
/// se reparten por el círculo que marca la retícula, y si te mueves o saltas, el círculo
/// (y el grupo de perdigones) se abre igual que con el resto de armas.
///
/// Munición, recarga, cadencia, dispersión y efectos vienen de WeaponBase; aquí solo se decide
/// CUÁNTOS raycasts salen por disparo.
///
/// Importante: para que haya "abanico" incluso quieto, en el Inspector hay que darle un
/// minSpreadDegrees mayor que 0 (la pistola y el subfusil lo tienen a 0 para ser precisos quietos).
/// </summary>
public class ShotgunWeapon : WeaponBase
{
    [Header("Escopeta")]
    [Tooltip("Perdigones por disparo. Cada perdigón hace 'damage' de daño por separado, así que el daño máximo es damage x pelletCount.")]
    [Min(1)]
    [SerializeField] private int pelletCount = 7;

    protected override void Fire()
    {
        // Todos los perdigones salen en el mismo frame y con la misma dispersión: el recoil
        // del disparo se suma DESPUÉS de Fire() (en TryFire), así que no abre entre perdigón y perdigón.
        for (int i = 0; i < pelletCount; i++)
        {
            FireHitscanShot(damage);
        }
    }
}
