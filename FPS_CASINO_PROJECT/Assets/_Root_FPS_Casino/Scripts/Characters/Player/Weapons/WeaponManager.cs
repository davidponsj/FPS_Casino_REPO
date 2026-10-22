using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dueño real del arma equipada. El Player Input conecta Fire/Reload AQUÍ (no directamente
/// al arma), porque el arma cambia en tiempo real al coger otra de la pared — si el evento
/// apuntara a una instancia concreta, dejaría de funcionar en cuanto esa instancia se
/// destruyera al equipar otra.
///
/// Empiezas sin arma (currentWeapon null): disparar/recargar con las manos vacías
/// simplemente no hace nada, sin necesidad de comprobarlo en ningún otro sitio.
///
/// Al equipar avisa con OnWeaponChanged: la munición, el círculo de recarga y la retícula
/// (AmmoHUD, ReloadHUD, DynamicCrosshair) se suscriben solos, así que aquí no hay que conocerlos.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punto dentro de la cámara donde se instancia el arma equipada (CameraHolder > WeaponHolder).")]
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Camera playerCamera;

    /// <summary>Se dispara al equipar un arma nueva, con esa arma.</summary>
    public event Action<WeaponBase> OnWeaponChanged;

    public WeaponBase CurrentWeapon { get; private set; }

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    /// <summary>Equipa una instancia nueva de weaponPrefab, sustituyendo a la que llevaras antes.</summary>
    public void EquipWeapon(WeaponBase weaponPrefab)
    {
        if (weaponPrefab == null || weaponSocket == null) return;

        if (CurrentWeapon != null)
            Destroy(CurrentWeapon.gameObject);

        // Instantiate(prefab, parent) coloca la copia con la MISMA posición/rotación LOCAL
        // que tenga guardada el prefab — así que, si colocaste el arma a mano dentro de
        // WeaponHolder antes de convertirla en prefab, aparece exactamente donde la dejaste.
        // Ya no forzamos aquí la posición a cero.
        CurrentWeapon = Instantiate(weaponPrefab, weaponSocket);
        CurrentWeapon.Initialize(playerController, playerCamera);

        OnWeaponChanged?.Invoke(CurrentWeapon);
    }

    // ---------------- INPUT (Player Input > Invoke Unity Events) ----------------

    public void OnFire(InputAction.CallbackContext context)
    {
        CurrentWeapon?.OnFire(context);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        CurrentWeapon?.OnReload(context);
    }
}