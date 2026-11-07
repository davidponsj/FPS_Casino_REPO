using UnityEngine;

/// <summary>
/// Lanzagranadas tragaperras: semi-automático (un disparo por pulsación). En vez de un raycast instantáneo
/// como el resto de armas, cada disparo crea un proyectil físico (GrenadeProjectile) que cae
/// con la gravedad, rebota las veces que le digas y explota haciendo daño en área.
///
/// Con un rodillo de tragaperras (SlotReel) en el arma:
/// - Al cogerlo, el rodillo gira y para en una fruta al azar: esa es la granada cargada.
/// - Al disparar sale la granada de ESA fruta y, justo después, el rodillo gira a la siguiente (desde la que estaba).
/// - Con la última bala no gira: recarga, y al acabar la recarga gira.
/// - No se puede disparar mientras el rodillo gira.
/// Sin SlotReel, dispara siempre Projectile Prefab (como el lanzagranadas de antes).
///
/// Munición, recarga, cadencia y dispersión vienen de WeaponBase igual que en las demás armas;
/// aquí solo cambia QUÉ sale al disparar: un proyectil en lugar de FireHitscanShot.
/// Los rebotes, el daño y el radio de la explosión se configuran en el prefab de la granada.
/// </summary>
public class GrenadeLauncherWeapon : WeaponBase
{
    [Header("Lanzagranadas")]
    [Tooltip("Granada por defecto (con GrenadeProjectile, Rigidbody y Collider). Se usa si no hay rodillo o si la fruta no tiene granada.")]
    [SerializeField] private GrenadeProjectile projectilePrefab;
    [Tooltip("Velocidad (m/s) con la que sale la granada.")]
    [SerializeField] private float launchSpeed = 25f;
    [Tooltip("Grados extra hacia arriba al disparar, para que la granada haga parábola y no caiga enseguida.")]
    [SerializeField] private float launchUpAngle = 3f;
    [Tooltip("Cuánto de tu propia velocidad hereda la granada (0 = nada, 1 = toda). Si disparas corriendo, la granada va más lejos.")]
    [Range(0f, 1f)]
    [SerializeField] private float inheritPlayerVelocity = 0.5f;

    [Header("Rodillo de tragaperras (opcional)")]
    [Tooltip("Rodillo que elige la granada de cada disparo. Vacío = lo busca en el arma; si no hay, dispara siempre Projectile Prefab.")]
    [SerializeField] private SlotReel reel;

    private Collider ownerCollider;

    protected override void Awake()
    {
        base.Awake();
        CacheOwnerCollider();

        if (reel == null)
            reel = GetComponentInChildren<SlotReel>();
    }

    private void OnEnable()
    {
        OnReloadFinished += SpinReel; // después de recargar, el rodillo gira para cargar fruta nueva
    }

    private void OnDisable()
    {
        OnReloadFinished -= SpinReel;
    }

    private void Start()
    {
        // Al coger el arma de la pared (se crea nueva cada vez), el rodillo gira para cargar la primera fruta.
        SpinReel();
    }

    protected override void TryFire()
    {
        // Mientras el rodillo gira no hay fruta cargada todavía: no dispara (ni hace "clic").
        if (reel != null && reel.IsSpinning) return;
        base.TryFire();
    }

    protected override void Fire()
    {
        GrenadeProjectile prefab = GetLoadedGrenade();
        if (prefab == null || muzzlePoint == null || playerCamera == null) return;

        // Por si el arma se equipó por código después del Awake (WeaponManager llama a Initialize).
        if (ownerCollider == null) CacheOwnerCollider();

        // La dirección sale de la cámara (donde de verdad miras) con la dispersión actual,
        // y se inclina un poco hacia arriba para que haga parábola.
        Vector3 direction = ApplySpread(playerCamera.transform.forward);
        direction = Quaternion.AngleAxis(-launchUpAngle, playerCamera.transform.right) * direction;

        Vector3 velocity = direction * launchSpeed;
        if (playerController != null)
            velocity += playerController.Velocity * inheritPlayerVelocity;

        GrenadeProjectile grenade = Instantiate(prefab, muzzlePoint.position, Quaternion.LookRotation(direction));
        grenade.Launch(velocity, ownerCollider);

        muzzleFlash?.Flash();

        // WeaponBase ya ha gastado la bala antes de llamar a Fire. Si queda alguna, el rodillo gira a la siguiente fruta;
        // si era la última, no gira: toca recargar (y al acabar la recarga girará).
        if (currentAmmo > 0)
            SpinReel();
    }

    /// <summary>La granada de la fruta cargada; si no hay rodillo o la fruta no tiene granada, la de por defecto.</summary>
    private GrenadeProjectile GetLoadedGrenade()
    {
        if (reel != null && reel.CurrentFruit != null && reel.CurrentFruit.grenadePrefab != null)
            return reel.CurrentFruit.grenadePrefab;
        return projectilePrefab;
    }

    private void SpinReel()
    {
        if (reel != null) reel.Spin();
    }

    private void CacheOwnerCollider()
    {
        // El CharacterController del jugador también es un Collider: lo guardamos para que
        // la granada lo ignore y no te explote en la cara al salir del cañón.
        if (playerController != null)
            ownerCollider = playerController.GetComponent<Collider>();
    }
}
