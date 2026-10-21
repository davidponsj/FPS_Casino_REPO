using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tommy Gun: automática. A diferencia de la pistola (que dispara solo al pulsar), aquí
/// OnFire solo marca "gatillo pulsado/soltado" — quien de verdad decide cuándo sale cada
/// bala es Update(), que llama a TryFire() en bucle mientras el gatillo esté pulsado.
/// TryFire() ya respeta el fireRate y la munición por su cuenta (heredado de WeaponBase),
/// así que aquí no hay que limitar nada más: solo "sigue intentando disparar si puedes".
/// </summary>
public class TommyGunWeapon : WeaponBase
{
    private bool triggerHeld;

    public override void OnFire(InputAction.CallbackContext context)
    {
        if (context.performed) triggerHeld = true;
        else if (context.canceled) triggerHeld = false;
    }

    protected override void Update()
    {
        base.Update(); // sigue calculando la dispersión como siempre

        if (triggerHeld)
            TryFire();
    }

    protected override void Fire()
    {
        FireHitscanShot(damage);
    }
}