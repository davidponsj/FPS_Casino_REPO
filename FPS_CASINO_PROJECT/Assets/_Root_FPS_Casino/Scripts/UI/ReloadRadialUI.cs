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
/// </summary>
public class ReloadRadialUI : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El arma actualmente equipada. Cuando montéis cambio de arma, llamad a SetWeapon() al equipar una nueva.")]
    [SerializeField] private WeaponBase weapon;
    [SerializeField] private Image radialImage;

    public void SetWeapon(WeaponBase newWeapon)
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