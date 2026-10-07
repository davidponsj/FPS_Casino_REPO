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
    [Tooltip("Grados de dispersión cuando estás completamente quieto y sin disparar. 0 = precisión perfecta al centro.")]
    [SerializeField] protected float minSpreadDegrees = 0f;
    [Tooltip("Techo ABSOLUTO de dispersión. Movimiento + salto + recoil se suman, pero el total nunca pasa de aquí.")]
    [SerializeField] protected float maxSpreadDegrees = 6f;
    [Tooltip("Qué tan rápido se ABRE la dispersión (al empezar a moverte, saltar...). Alto = casi instantáneo.")]
    [SerializeField] protected float spreadOpenSpeed = 20f;
    [Tooltip("Qué tan rápido se CIERRA la dispersión al volver a estar quieto. Más bajo que el de abrir: se abre de golpe y se cierra poco a poco.")]
    [SerializeField] protected float spreadCloseSpeed = 8f;

    [Header("Dispersión por movimiento")]
    [Tooltip("Grados que suma moverte a speedForMaxSpread (o más rápido).")]
    [SerializeField] protected float movementSpreadDegrees = 3f;
    [Tooltip("Por debajo de esta velocidad (m/s) moverse no penaliza: puedes corregir la posición sin perder precisión.")]
    [SerializeField] protected float speedSpreadDeadzone = 1f;
    [Tooltip("Velocidad (m/s) a partir de la cual se alcanza toda la dispersión por movimiento. Usa el sprintSpeed del PlayerController como referencia.")]
    [SerializeField] protected float speedForMaxSpread = 8.5f;
    [Tooltip("Grados que suma estar en el aire (saltando, cayendo, wallrun). Pon 0 para que saltar no penalice.")]
    [SerializeField] protected float airborneSpreadDegrees = 3f;

    [Header("Dispersión por disparo (recoil)")]
    [Tooltip("Grados que suma el PRIMER disparo de una ráfaga a la dispersión, por encima de la dispersión por movimiento.")]
    [SerializeField] protected float spreadPerShot = 0.6f;
    [Tooltip("Cuánto más fuerte pega cada bala seguida sin soltar el gatillo (0.15 = un 15% más que la anterior). Pon 0 para que todas las balas abran siempre lo mismo.")]
    [SerializeField] protected float consecutiveShotGrowth = 0f;
    [Tooltip("Si pasa más de este tiempo sin disparar, la racha de 'balas seguidas' se reinicia.")]
    [SerializeField] protected float burstResetTime = 0.3f;
    [Tooltip("Tope de cuánto puede acumular el recoil, independientemente del movimiento.")]
    [SerializeField] protected float maxRecoilSpread = 3f;
    [Tooltip("Segundos sin disparar antes de que el recoil empiece a recuperarse. Así, disparando seguido, la dispersión se va acumulando.")]
    [SerializeField] protected float recoilRecoveryDelay = 0.1f;
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
    protected int shotsInBurst;   // disparos seguidos sin soltar el gatillo (se reinicia tras burstResetTime sin disparar)
    protected float lastShotTime;

    protected AudioSource audioSource;

    /// <summary>Se dispara cada vez que cambia currentAmmo o reserveAmmo (disparo, recarga, RefillAmmo).</summary>
    public event Action OnAmmoChanged;
    /// <summary>Se dispara justo al empezar a recargar.</summary>
    public event Action OnReloadStarted;
    /// <summary>Se dispara al terminar la recarga (con éxito).</summary>
    public event Action OnReloadFinished;

    public float CurrentSpreadDegrees => currentSpreadDegrees;
    /// <summary>Cámara desde la que dispara el arma. La retícula la usa para pasar grados a píxeles.</summary>
    public Camera PlayerCamera => playerCamera;
    /// <summary>
    /// Dispersión normalizada 0-1, pensada para la retícula en pantalla. Usa el MISMO techo
    /// (maxSpreadDegrees) que el propio disparo, así que cuando la bala puede desviarse al
    /// máximo, la retícula se ve también al 100% abierta — nunca van desincronizadas.
    /// </summary>
    public float NormalizedSpread => maxSpreadDegrees <= minSpreadDegrees
        ? 0f
        : Mathf.InverseLerp(minSpreadDegrees, maxSpreadDegrees, currentSpreadDegrees);

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
        // El recoil solo empieza a recuperarse cuando llevas un momento sin disparar.
        // Si se recuperase también mientras disparas, una automática nunca llegaría a abrirse.
        if (Time.time - lastShotTime > recoilRecoveryDelay)
            recoilSpread = Mathf.MoveTowards(recoilSpread, 0f, spreadRecoveryRate * Time.deltaTime);

        // Cada fuente de imprecisión suma por separado (movimiento, aire, disparos), pero el
        // total nunca pasa del techo real (maxSpreadDegrees). Así la retícula, que usa ese
        // mismo techo, siempre refleja el 100% real.
        float targetSpread = minSpreadDegrees + GetMovementSpread() + GetAirborneSpread() + recoilSpread;
        targetSpread = Mathf.Clamp(targetSpread, minSpreadDegrees, maxSpreadDegrees);

        // Se abre rápido y se cierra despacio: al saltar o arrancar a correr notas el castigo
        // al momento, pero al pararte tienes que esperar un poco a que se estabilice.
        float lerpSpeed = targetSpread > currentSpreadDegrees ? spreadOpenSpeed : spreadCloseSpeed;
        currentSpreadDegrees = Mathf.Lerp(currentSpreadDegrees, targetSpread, 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime));
    }

    private float GetMovementSpread()
    {
        if (playerController == null) return 0f;

        // InverseLerp ya devuelve 0..1 limitado: por debajo de la deadzone 0, a speedForMaxSpread o más, 1.
        float speedT = Mathf.InverseLerp(speedSpreadDeadzone, speedForMaxSpread, playerController.CurrentHorizontalSpeed);
        return speedT * movementSpreadDegrees;
    }

    private float GetAirborneSpread()
    {
        if (playerController == null || playerController.IsGrounded) return 0f;
        return airborneSpreadDegrees;
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

        // Ángulo aleatorio y distancia al centro aleatoria SIN raíz cuadrada: así hay más balas
        // cerca del centro que en el borde (como un arma de verdad), en vez de repartirse igual
        // por todo el círculo como hace insideUnitCircle. El borde sigue siendo el de la retícula.
        float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
        float distance = UnityEngine.Random.value * spreadRadius;
        Vector2 randomPoint = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

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

        // Si llevas un rato sin disparar, la racha se reinicia; si no, este disparo
        // cuenta como "uno más seguido" y pega más fuerte que el anterior.
        if (Time.time - lastShotTime > burstResetTime)
            shotsInBurst = 0;
        lastShotTime = Time.time;

        float scaledSpreadThisShot = spreadPerShot * (1f + shotsInBurst * consecutiveShotGrowth);
        float previousRecoil = recoilSpread;
        recoilSpread = Mathf.Min(recoilSpread + scaledSpreadThisShot, maxRecoilSpread);
        shotsInBurst++;

        // El "golpe" del disparo se aplica al instante (sin suavizado), para que la retícula
        // salte con cada bala y la siguiente ya salga con la dispersión abierta.
        currentSpreadDegrees = Mathf.Min(currentSpreadDegrees + (recoilSpread - previousRecoil), maxSpreadDegrees);

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