using UnityEngine;

/// <summary>
/// Sniper: semi-automático como la pistola, una bala por pulsación que hace mucho daño.
/// No tiene lógica propia: munición, recarga, cadencia, dispersión y efectos vienen de
/// WeaponBase. Lo que lo hace "sniper" son sus valores en el Inspector:
/// - damage muy alto y range largo.
/// - Cargador pequeño y fireRate bajo (cerrojo): disparas, recargas y vuelves a disparar.
/// - La dispersión que MÁS se abre de todas las armas al moverte o saltar, pero quieto
///   se cierra casi del todo (minSpreadDegrees pequeño, pero mayor que 0).
/// </summary>
public class SniperWeapon : WeaponBase
{
    protected override void Fire()
    {
        FireHitscanShot(damage);
    }
}