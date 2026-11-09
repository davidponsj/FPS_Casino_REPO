using TMPro;
using UnityEngine;

/// <summary>
/// Recarga integrada en el contador de munición (sustituye al círculo): mientras recargas, las balas pasan de la reserva
/// al cargador contando, como el contador de una tragaperras ("4 / 60" ? "12 / 52"), con los números algo apagados, y
/// una línea dorada fina se llena debajo de izquierda a derecha. Todo va sincronizado con la recarga real del arma
/// (ReloadProgress01). Al acabar, los números vuelven a su color y la línea desaparece.
///
/// Va en el objeto de la barra (ReloadBar, con CanvasGroup), dentro del HUD de munición. Para que los números se vean
/// durante la recarga, en AmmoHUD desmarca Hide While Reloading.
/// La visibilidad de la barra se hace con el alpha del CanvasGroup (no con SetActive): así este script sigue vivo
/// aunque esté en el mismo objeto que se esconde.
/// El arma la saca sola del WeaponManager del jugador (evento OnWeaponChanged): no hay que asignarla.
/// </summary>
public class ReloadHUD : MonoBehaviour
{
    [Header("Barra")]
    [Tooltip("La parte que se llena (una Image fina). Su Pivot X tiene que ser 0 (a la izquierda): se estira en X de 0 a 1. No necesita sprite.")]
    [SerializeField] private RectTransform fill;
    [Tooltip("CanvasGroup de la barra entera (fondo + relleno): aparece al recargar y se va al acabar. Vacío = el de este objeto.")]
    [SerializeField] private CanvasGroup barGroup;
    [Tooltip("Segundos que tarda la barra en aparecer y desaparecer.")]
    [Min(0f)]
    [SerializeField] private float fadeSeconds = 0.12f;

    [Header("Números")]
    [Tooltip("Texto del cargador (el mismo que usa AmmoHUD). Cuenta hacia arriba mientras recargas. Vacío = no cuenta.")]
    [SerializeField] private TMP_Text magazineText;
    [Tooltip("Texto de la reserva (el mismo que usa AmmoHUD). Cuenta hacia abajo mientras recargas. Vacío = no cuenta.")]
    [SerializeField] private TMP_Text reserveText;
    [Tooltip("Cuánto se apagan los números durante la recarga (multiplica su alfa). 0.55 = se ve que no se puede disparar.")]
    [Range(0f, 1f)]
    [SerializeField] private float reloadingAlpha = 0.55f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuándo empieza y acaba cada recarga y cuántas balas pasan.")]
    [SerializeField] private bool showDebug;

    private WeaponManager weaponManager;
    private WeaponBase weapon;
    private bool wasReloading;
    private int startMagazine, startReserve, targetMagazine;
    private float barAlpha;

    private void Awake()
    {
        if (barGroup == null) barGroup = GetComponent<CanvasGroup>();
        SetBar(0f, 0f);
    }

    private void Start()
    {
        weaponManager = FindAnyObjectByType<WeaponManager>();
        if (weaponManager == null)
        {
            Debug.LogWarning($"[{name}] No hay ningún WeaponManager en la escena: la barra de recarga no se mostrará.", this);
            return;
        }

        weaponManager.OnWeaponChanged += SetWeapon;
        SetWeapon(weaponManager.CurrentWeapon);
    }

    private void OnDestroy()
    {
        if (weaponManager != null) weaponManager.OnWeaponChanged -= SetWeapon;
    }

    private void SetWeapon(WeaponBase newWeapon)
    {
        if (wasReloading) EndReload(); // cambiar de arma a mitad de recarga: deja los números bien
        weapon = newWeapon;
        wasReloading = false;
    }

    private void Update()
    {
        bool reloading = weapon != null && weapon.IsReloading;
        if (reloading && !wasReloading) BeginReload();
        else if (!reloading && wasReloading) EndReload();
        wasReloading = reloading;

        float targetAlpha = reloading ? 1f : 0f;
        barAlpha = fadeSeconds > 0f ? Mathf.MoveTowards(barAlpha, targetAlpha, Time.deltaTime / fadeSeconds) : targetAlpha;

        float progress = reloading ? weapon.ReloadProgress01 : (barAlpha > 0f && fill != null ? fill.localScale.x : 0f);
        SetBar(barAlpha, progress);

        if (reloading)
        {
            // Las balas pasan de la reserva al cargador a la vez que avanza la recarga.
            int moved = Mathf.RoundToInt((targetMagazine - startMagazine) * Mathf.Clamp01(progress));
            if (magazineText != null) magazineText.text = (startMagazine + moved).ToString();
            if (reserveText != null) reserveText.text = (startReserve - moved).ToString();
        }
    }

    private void BeginReload()
    {
        startMagazine = weapon.CurrentAmmo;
        startReserve = weapon.ReserveAmmo;
        targetMagazine = Mathf.Min(weapon.MagazineSize, startMagazine + Mathf.Max(0, startReserve));
        SetNumbersAlpha(reloadingAlpha);
        if (showDebug) Debug.Log($"[{name}] Recarga: {startMagazine} ? {targetMagazine} (reserva {startReserve}).", this);
    }

    private void EndReload()
    {
        SetNumbersAlpha(1f);
        if (weapon != null)
        {
            // Los números de verdad (por si la recarga se cortó a medias o el arma da otra cifra).
            if (magazineText != null) magazineText.text = weapon.CurrentAmmo.ToString();
            if (reserveText != null) reserveText.text = weapon.ReserveAmmo.ToString();
        }
        if (showDebug) Debug.Log($"[{name}] Fin de la recarga.", this);
    }

    private void SetNumbersAlpha(float alpha)
    {
        if (magazineText != null) magazineText.alpha = alpha;
        if (reserveText != null) reserveText.alpha = alpha;
    }

    private void SetBar(float alpha, float progress01)
    {
        if (barGroup != null) barGroup.alpha = alpha;
        if (fill != null) fill.localScale = new Vector3(Mathf.Clamp01(progress01), 1f, 1f);
    }
}
