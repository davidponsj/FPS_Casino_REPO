using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base común para todas las armas del juego (pistola, subfusil, escopeta, lanzagranadas, sniper).
/// Gestiona munición por cargador + reserva, cadencia de fuego, recarga, dispersión dinámica
/// (estilo Counter-Strike) y el patrón de disparo hitscan compartido (raycast + daño + trazador
/// + impacto + fogonazo), para que cada arma concreta solo tenga que decidir CUÁNDO dispara
/// (semi-auto, automática, a ráfagas, varios perdigones...), no CÓMO se ve/hace daño un disparo.
///
/// Conecta OnFire y OnReload desde el Player Input (Events > Player > Fire / Reload),
/// igual que hicimos con Move/Look/Jump/Slide.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public abstract class WeaponBase : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punto en la punta del cañón. Desde aquí salen los efectos visuales (no el raycast, que sale de la cámara para que apunte donde de verdad miras).")]
    [SerializeField] protected Transform muzzlePoint;
    [SerializeField] protected Camera playerCamera;
    [SerializeField] protected PlayerController playerController;

    [Header("Munición")]
    [SerializeField] protected int magazineSize = 12;
    [SerializeField] protected int startingReserveAmmo = 60;
    [SerializeField] protected float reloadTime = 1.4f;

    [Header("Cadencia")]
    [Tooltip("Disparos por segundo.")]
    [SerializeField] protected float fireRate = 4f;

    [Header("Alcance y daño")]
    [SerializeField] protected float range = 100f;
    [SerializeField] protected LayerMask hitMask = ~0; // por defecto, choca con todo
    [SerializeField] protected float damage = 25f;

    [Header("Dispersión (estilo CS)")]
    [Tooltip("Grados de dispersión cuando estás completamente quieto. 0 = precisión perfecta al centro.")]
    [SerializeField] protected float minSpreadDegrees = 0f;
    [Tooltip("Grados de dispersión cuando te mueves a máxima velocidad (normalmente tu sprintSpeed).")]
    [SerializeField] protected float maxSpreadDegrees = 4.5f;
    [Tooltip("Velocidad (m/s) a partir de la cual se alcanza la dispersión máxima. Usa el sprintSpeed del PlayerController como referencia.")]
    [SerializeField] protected float speedForMaxSpread = 8.5f;
    [Tooltip("Qué tan rápido se abre/cierra la dispersión visible y real al cambiar de velocidad.")]
    [SerializeField] protected float spreadSmoothing = 12f;
    [Tooltip("Si estás en el aire (saltando, cayendo), la dispersión se va a la máxima aunque no te muevas apenas en horizontal — como saltar en sitio.")]
    [SerializeField] protected bool maxSpreadWhileAirborne = true;

    [Header("Dispersión por disparo (recoil)")]
    [Tooltip("Grados que se suman a la dispersión cada vez que disparas, por encima de la dispersión por movimiento.")]
    [SerializeField] protected float spreadPerShot = 0.6f;
    [Tooltip("Tope de cuánto puede acumular el recoil, independientemente del movimiento.")]
    [SerializeField] protected float maxRecoilSpread = 3f;
    [Tooltip("Grados por segundo que se recupera el recoil cuando dejas de disparar.")]
    [SerializeField] protected float spreadRecoveryRate = 3f;

    [Header("Efectos (opcionales, compartidos por todas las armas)")]
    [SerializeField] protected BulletTracerEffect tracerPrefab;
    [SerializeField] protected GameObject impactEffectPrefab;
    [SerializeField] protected MuzzleFlash muzzleFlash;

    protected int currentAmmo;
    protected int reserveAmmo;
    protected float nextFireTime;
    protected bool isReloading;
    protected float reloadStartTime;
    protected float currentSpreadDegrees;
    protected float recoilSpread; // dispersión extra acumulada por disparos recientes, se recupera sola

    protected AudioSource audioSource;

    /// <summary>Se dispara cada vez que cambia currentAmmo o reserveAmmo (disparo, recarga, RefillAmmo).</summary>
    public event Action OnAmmoChanged;
    /// <summary>Se dispara justo al empezar a recargar.</summary>
    public event Action OnReloadStarted;
    /// <summary>Se dispara al terminar la recarga (con éxito).</summary>
    public event Action OnReloadFinished;

    public float CurrentSpreadDegrees => currentSpreadDegrees;
    /// <summary>Tope absoluto de dispersión: el de movimiento más lo que puede aportar el recoil.</summary>
    private float EffectiveMaxSpreadDegrees => maxSpreadDegrees + maxRecoilSpread;
    /// <summary>Dispersión normalizada 0-1 (ya contando el recoil), pensada para la retícula en pantalla.</summary>
    public float NormalizedSpread => EffectiveMaxSpreadDegrees <= minSpreadDegrees
        ? 0f
        : Mathf.InverseLerp(minSpreadDegrees, EffectiveMaxSpreadDegrees, currentSpreadDegrees);

    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;
    public float ReloadProgress01 => isReloading
        ? Mathf.Clamp01((Time.time - reloadStartTime) / Mathf.Max(reloadTime, 0.0001f))
        : 0f;

    protected virtual void Awake()
    {
        currentAmmo = magazineSize;
        reserveAmmo = startingReserveAmmo;
        audioSource = GetComponent<AudioSource>();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        if (playerCamera == null)
            playerCamera = GetComponentInParent<Camera>();
    }

    /// <summary>
    /// Inyecta las referencias de cámara y controller al equipar el arma por código
    /// (WeaponManager la llama justo después de Instantiate). Necesario porque un prefab
    /// no puede guardar de fábrica una referencia a la cámara de tu escena concreta.
    /// </summary>
    public void Initialize(PlayerController controller, Camera camera)
    {
        playerController = controller;
        playerCamera = camera;
    }

    protected virtual void Update()
    {
        UpdateSpread();
    }

    private void UpdateSpread()
    {
        float speed = playerController != null ? playerController.CurrentHorizontalSpeed : 0f;
        float speedT = speedForMaxSpread > 0f ? Mathf.Clamp01(speed / speedForMaxSpread) : 0f;

        if (maxSpreadWhileAirborne && playerController != null && !playerController.IsGrounded)
            speedT = 1f;

        float movementSpread = Mathf.Lerp(minSpreadDegrees, maxSpreadDegrees, speedT);

        // El recoil se va recuperando solo con el tiempo, dispares o no.
        recoilSpread = Mathf.MoveTowards(recoilSpread, 0f, spreadRecoveryRate * Time.deltaTime);

        float targetSpread = movementSpread + recoilSpread;
        currentSpreadDegrees = Mathf.Lerp(currentSpreadDegrees, targetSpread, spreadSmoothing * Time.deltaTime);
    }

    /// <summary>
    /// Aplica la dispersión actual a una dirección base, devolviendo una dirección aleatoria
    /// dentro de un cono. Si la dispersión es 0 (quieto), devuelve la dirección exacta de la cámara.
    /// </summary>
    protected Vector3 ApplySpread(Vector3 forward)
    {
        if (currentSpreadDegrees <= 0.001f)
            return forward;

        float spreadRadius = Mathf.Tan(currentSpreadDegrees * Mathf.Deg2Rad);
        Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * spreadRadius;

        Vector3 spreadDirection = forward
            + playerCamera.transform.right * randomPoint.x
            + playerCamera.transform.up * randomPoint.y;

        return spreadDirection.normalized;
    }

    // ---------------- DISPARO HITSCAN COMPARTIDO ----------------

    /// <summary>
    /// Un disparo hitscan completo: raycast con dispersión, daño si golpea algo, impacto,
    /// trazador y fogonazo. Pensado para que pistola/subfusil/escopeta (un perdigón)/sniper
    /// solo tengan que llamar a esto con el daño que les corresponda, en vez de reescribir
    /// el raycast cada vez. Devuelve el RaycastHit si impactó contra algo, o null si no.
    /// </summary>
    protected RaycastHit? FireHitscanShot(float shotDamage)
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = ApplySpread(playerCamera.transform.forward);

        RaycastHit? result = null;
        Vector3 tracerEndPoint;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(shotDamage);

            if (impactEffectPrefab != null)
                Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));

            tracerEndPoint = hit.point;
            result = hit;
        }
        else
        {
            tracerEndPoint = origin + direction * range;
        }

        SpawnTracer(tracerEndPoint);
        muzzleFlash?.Flash();

        return result;
    }

    protected void SpawnTracer(Vector3 endPoint)
    {
        if (tracerPrefab == null || muzzlePoint == null) return;

        BulletTracerEffect tracer = Instantiate(tracerPrefab, muzzlePoint.position, Quaternion.identity);
        tracer.Play(muzzlePoint.position, endPoint);
    }

    // ---------------- INPUT (Player Input > Invoke Unity Events) ----------------

    public virtual void OnFire(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryFire();
    }

    public virtual void OnReload(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryReload();
    }

    // ---------------- DISPARO ----------------

    protected virtual void TryFire()
    {
        if (isReloading) return;
        if (Time.time < nextFireTime) return;

        if (currentAmmo <= 0)
        {
            OnDryFire();
            return;
        }

        currentAmmo--;
        nextFireTime = Time.time + (1f / fireRate);
        Fire();

        recoilSpread = Mathf.Min(recoilSpread + spreadPerShot, maxRecoilSpread);

        OnAmmoChanged?.Invoke();
    }

    /// <summary>
    /// El patrón de disparo concreto de cada arma. La mayoría de armas hitscan solo necesitan
    /// llamar a FireHitscanShot(damage); la escopeta llamará varias veces con perdigones,
    /// el lanzagranadas instanciará un proyectil en vez de usar esto.
    /// </summary>
    protected abstract void Fire();

    /// <summary>Se llama cuando pulsas disparo con el cargador vacío. Por defecto no hace nada; cada arma puede sonar "click".</summary>
    protected virtual void OnDryFire() { }

    // ---------------- RECARGA ----------------

    protected virtual void TryReload()
    {
        if (isReloading) return;
        if (currentAmmo >= magazineSize) return;
        if (reserveAmmo <= 0) return; // sin reserva: hay que ir a la pared a por otra arma

        StartCoroutine(ReloadRoutine());
    }

    protected virtual IEnumerator ReloadRoutine()
    {
        isReloading = true;
        reloadStartTime = Time.time;
        OnReloadStarted?.Invoke();

        yield return new WaitForSeconds(reloadTime);

        int needed = magazineSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;

        isReloading = false;
        OnAmmoChanged?.Invoke();
        OnReloadFinished?.Invoke();
    }

    /// <summary>Rellena cargador y reserva al máximo. Lo usará el sistema de "coger arma de la pared".</summary>
    public virtual void RefillAmmo(int reserveAmount)
    {
        currentAmmo = magazineSize;
        reserveAmmo = reserveAmount;
        OnAmmoChanged?.Invoke();
    }
}