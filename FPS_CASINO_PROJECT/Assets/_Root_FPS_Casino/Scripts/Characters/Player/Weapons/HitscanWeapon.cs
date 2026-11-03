using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Arma de bala instantánea (hitscan): un raycast por bala. Sirve para el Lanzacartas, el sniper,
/// la Tommy Gun y la escopeta; lo que las diferencia son sus valores en el Inspector.
/// - Automatic: dispara sin parar mientras mantienes el gatillo (Tommy). Si no, un disparo por pulsación.
/// - Pellets Per Shot: balas por disparo (1 = pistola; 6 = escopeta). Cada una coge su propia
///   dirección dentro de la dispersión actual, así que se reparten por el círculo de la retícula.
///
/// Munición, recarga, cadencia, dispersión, daño y efectos vienen de WeaponBase; aquí solo se decide
/// CUÁNDO se dispara y CUÁNTAS balas salen. Para que la escopeta abra en abanico incluso quieta,
/// su minSpreadDegrees tiene que ser mayor que 0.
/// </summary>
public class HitscanWeapon : WeaponBase
{
    [Header("Tipo de disparo")]
    [Tooltip("Marcado: dispara sin parar mientras mantienes el gatillo (como la Tommy Gun). Desmarcado: un disparo por pulsación.")]
    [SerializeField] private bool automatic;
    [Tooltip("Balas que salen en cada disparo. Cada una hace 'damage' por separado. 1 = pistola, 6 = escopeta.")]
    [Min(1)]
    [FormerlySerializedAs("pelletCount")]
    [SerializeField] private int pelletsPerShot = 1;

    private bool triggerHeld;

    public override void OnFire(InputAction.CallbackContext context)
    {
        if (!automatic)
        {
            base.OnFire(context);
            return;
        }

        // Automática: OnFire solo marca el gatillo; Update dispara en bucle respetando la cadencia.
        if (context.performed) triggerHeld = true;
        else if (context.canceled) triggerHeld = false;
    }

    protected override void Update()
    {
        base.Update(); // sigue calculando la dispersión como siempre

        if (automatic && triggerHeld)
            TryFire();
    }

    protected override void Fire()
    {
        // Todas las balas salen en el mismo frame y con la misma dispersión: el retroceso del disparo
        // se suma DESPUÉS de Fire() (en TryFire), así que no se abre entre bala y bala.
        for (int i = 0; i < pelletsPerShot; i++)
            FireHitscanShot(damage);
    }
}
