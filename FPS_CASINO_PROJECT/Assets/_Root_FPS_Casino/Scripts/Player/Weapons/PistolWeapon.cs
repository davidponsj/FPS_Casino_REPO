using UnityEngine;

/// <summary>
/// Pistola: semi-automática, un único raycast por pulsación. Toda la lógica de disparo
/// (raycast, daño, trazador, impacto, fogonazo) vive en WeaponBase; aquí solo se decide
/// CUÁNDO se dispara.
/// </summary>
public class PistolWeapon : WeaponBase
{
    protected override void Fire()
    {
        FireHitscanShot(damage);
    }
}