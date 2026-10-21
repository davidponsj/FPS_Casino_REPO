using UnityEngine;

/// <summary>
/// Lanzagranadas: semi-automático (un disparo por pulsación). En vez de un raycast instantáneo
/// como el resto de armas, cada disparo crea un proyectil físico (GrenadeProjectile) que cae
/// con la gravedad, rebota las veces que le digas y explota haciendo daño en área.
///
/// Munición, recarga, cadencia y dispersión vienen de WeaponBase igual que en las demás armas;
/// aquí solo cambia QUÉ sale al disparar: un proyectil en lugar de FireHitscanShot.
/// Los rebotes, el daño y el radio de la explosión se configuran en el prefab de la granada.
/// </summary>
public class GrenadeLauncherWeapon : WeaponBase
{
    [Header("Lanzagranadas")]
    [Tooltip("Prefab de la granada (con GrenadeProjectile, Rigidbody y Collider).")]
    [SerializeField] private GrenadeProjectile projectilePrefab;
    [Tooltip("Velocidad (m/s) con la que sale la granada.")]
    [SerializeField] private float launchSpeed = 25f;
    [Tooltip("Grados extra hacia arriba al disparar, para que la granada haga parábola y no caiga enseguida.")]
    [SerializeField] private float launchUpAngle = 3f;
    [Tooltip("Cuánto de tu propia velocidad hereda la granada (0 = nada, 1 = toda). Si disparas corriendo, la granada va más lejos.")]
    [Range(0f, 1f)]
    [SerializeField] private float inheritPlayerVelocity = 0.5f;

    private Collider ownerCollider;

    protected override void Awake()
    {
        base.Awake();
        CacheOwnerCollider();
    }

    protected override void Fire()
    {
        if (projectilePrefab == null || muzzlePoint == null || playerCamera == null) return;

        // Por si el arma se equipó por código después del Awake (WeaponManager llama a Initialize).
        if (ownerCollider == null) CacheOwnerCollider();

        // La dirección sale de la cámara (donde de verdad miras) con la dispersión actual,
        // y se inclina un poco hacia arriba para que haga parábola.
        Vector3 direction = ApplySpread(playerCamera.transform.forward);
        direction = Quaternion.AngleAxis(-launchUpAngle, playerCamera.transform.right) * direction;

        Vector3 velocity = direction * launchSpeed;
        if (playerController != null)
            velocity += playerController.Velocity * inheritPlayerVelocity;

        GrenadeProjectile grenade = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.LookRotation(direction));
        grenade.Launch(velocity, ownerCollider);

        muzzleFlash?.Flash();
    }

    private void CacheOwnerCollider()
    {
        // El CharacterController del jugador también es un Collider: lo guardamos para que
        // la granada lo ignore y no te explote en la cara al salir del cañón.
        if (playerController != null)
            ownerCollider = playerController.GetComponent<Collider>();
    }
}