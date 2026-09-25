using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Círculo que se va rellenando como un reloj mientras recargas, sincronizado 1:1 con el
/// tiempo real de recarga del arma (ReloadProgress01). Requiere una Image con Image Type =
/// Filled y Fill Method = Radial 360 (Origin: Top suele quedar bien, como un reloj normal).
///
/// A diferencia del AmmoUI, este SÍ se actualiza cada frame: el progreso de la recarga es
/// un valor continuo, no discreto, así que no hay evento que valga — hay que leerlo en Update.
/// </summary>
public class ReloadRadialUI : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El arma actualmente equipada. Cuando montéis cambio de arma, llamad a SetWeapon() al equipar una nueva.")]
    [SerializeField] private WeaponBase weapon;
    [SerializeField] private Image radialImage;
    [Tooltip("Objeto raíz del círculo (para ocultarlo del todo cuando no estás recargando). Puede ser el mismo GameObject que lleva la Image.")]
    [SerializeField] private GameObject root;

    public void SetWeapon(WeaponBase newWeapon)
    {
        weapon = newWeapon;
    }

    private void Update()
    {
        if (weapon == null) return;

        bool reloading = weapon.IsReloading;

        if (root != null)
            root.SetActive(reloading);

        if (radialImage != null)
            radialImage.fillAmount = weapon.ReloadProgress01;
    }
}