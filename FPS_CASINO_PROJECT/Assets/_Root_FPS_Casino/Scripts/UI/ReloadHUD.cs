using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Círculo que se va rellenando como un reloj mientras recargas, sincronizado 1:1 con el
/// tiempo real de recarga del arma (ReloadProgress01). Requiere una Image con Image Type =
/// Filled y Fill Method = Radial 360 (Origin: Top suele quedar bien, como un reloj normal).
///
/// La visibilidad se controla con Image.enabled, NO con GameObject.SetActive: si este script
/// vive en el mismo GameObject que quieres ocultar, desactivarlo con SetActive detendría su
/// propio Update() y se quedaría apagado para siempre. Image.enabled solo apaga el dibujado,
/// el script sigue vivo y comprobando el estado cada frame.
///
/// El arma la saca sola del WeaponManager del jugador (evento OnWeaponChanged): no hay que asignarla.
/// </summary>
public class ReloadHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Image con Image Type = Filled y Fill Method = Radial 360. Se rellena mientras recargas.")]
    [SerializeField] private Image radialImage;

    private WeaponManager weaponManager;
    private WeaponBase weapon;

    private void Start()
    {
        weaponManager = FindAnyObjectByType<WeaponManager>();
        if (weaponManager == null)
        {
            Debug.LogWarning($"[{name}] No hay ningún WeaponManager en la escena: el círculo de recarga no se mostrará.", this);
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
        weapon = newWeapon;
    }

    private void Update()
    {
        if (radialImage == null) return;

        if (weapon == null)
        {
            radialImage.enabled = false; // sin arma equipada, no hay nada que recargar
            return;
        }

        bool reloading = weapon.IsReloading;

        radialImage.enabled = reloading;
        radialImage.fillAmount = weapon.ReloadProgress01;
    }
}