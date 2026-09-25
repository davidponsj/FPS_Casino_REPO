using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base común para todas las armas del juego (pistola, subfusil, escopeta, lanzagranadas, sniper).
/// Gestiona munición por cargador + reserva, cadencia de fuego, recarga y la dispersión
/// dinámica (estilo Counter-Strike): quieto = precisión perfecta al centro de la cámara,
/// moviéndote = el cono de disparo se abre según tu velocidad actual.
///
/// Cada arma concreta solo tiene que heredar de esta clase e implementar Fire(), que es
/// donde vive su patrón de disparo real (un raycast, varios perdigones, un proyectil...).
///
/// Conecta OnFire y OnReload desde el Player Input (Events > Player > Fire / Reload),
/// igual que hicimos con Move/Look/Jump/Slide.
///
/// Expone eventos (OnAmmoChanged, OnReloadStarted, OnReloadFinished) y ReloadProgress01
/// para que el HUD (munición + indicador circular de recarga) se entere sin tener que
/// leer el estado cada frame salvo cuando de verdad necesita algo continuo (el propio
/// progreso de la recarga).
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

    [Header("Alcance")]
    [SerializeField] protected float range = 100f;
    [SerializeField] protected LayerMask hitMask = ~0; // por defecto, choca con todo

    [Header("Dispersión (estilo CS)")]
    [Tooltip("Grados de dispersión cuando estás completamente quieto. 0 = precisión perfecta al centro.")]
    [SerializeField] protected float minSpreadDegrees = 0f;
    [Tooltip("Grados de dispersión cuando te mueves a máxima velocidad (normalmente tu sprintSpeed).")]
    [SerializeField] protected float maxSpreadDegrees = 4.5f;
    [Tooltip("Velocidad (m/s) a partir de la cual se alcanza la dispersión máxima. Usa el sprintSpeed del PlayerController como referencia.")]
    [SerializeField] protected float speedForMaxSpread = 8.5f;
    [Tooltip("Qué tan rápido se abre/cierra la dispersión visible y real al cambiar de velocidad.")]
    [SerializeField] protected float spreadSmoothing = 12f;

    protected int currentAmmo;
    protected int reserveAmmo;
    protected float nextFireTime;
    protected bool isReloading;
    protected float reloadStartTime;
    protected float currentSpreadDegrees;

    protected AudioSource audioSource;

    /// <summary>Se dispara cada vez que cambia currentAmmo o reserveAmmo (disparo, recarga, RefillAmmo).</summary>
    public event Action OnAmmoChanged;
    /// <summary>Se dispara justo al empezar a recargar.</summary>
    public event Action OnReloadStarted;
    /// <summary>Se dispara al terminar la recarga (con éxito).</summary>
    public event Action OnReloadFinished;

    /// <summary>Dispersión actual en grados, ya usable directamente para el raycast.</summary>
    public float CurrentSpreadDegrees => currentSpreadDegrees;
    /// <summary>Dispersión normalizada 0-1, pensada para la retícula en pantalla.</summary>
    public float NormalizedSpread => maxSpreadDegrees <= minSpreadDegrees
        ? 0f
        : Mathf.InverseLerp(minSpreadDegrees, maxSpreadDegrees, currentSpreadDegrees);

    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;
    /// <summary>Progreso de la recarga actual, de 0 a 1, a velocidad real (mapeado 1:1 con reloadTime). 0 si no estás recargando.</summary>
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

    protected virtual void Update()
    {
        UpdateSpread();
    }

    private void UpdateSpread()
    {
        float speed = playerController != null ? playerController.CurrentHorizontalSpeed : 0f;
        float speedT = speedForMaxSpread > 0f ? Mathf.Clamp01(speed / speedForMaxSpread) : 0f;
        float targetSpread = Mathf.Lerp(minSpreadDegrees, maxSpreadDegrees, speedT);

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

        OnAmmoChanged?.Invoke();
    }

    /// <summary>
    /// El patrón de disparo concreto de cada arma: un raycast, varios perdigones, un proyectil, etc.
    /// La dispersión ya está calculada en currentSpreadDegrees; usa ApplySpread(direction) para aplicarla.
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