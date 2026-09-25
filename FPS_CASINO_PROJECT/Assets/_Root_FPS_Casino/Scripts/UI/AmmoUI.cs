using TMPro;
using UnityEngine;

/// <summary>
/// Texto de munición estilo "12 / 60" (cargador / reserva). Se actualiza solo cuando
/// cambia la munición (evento OnAmmoChanged), no cada frame.
///
/// Mientras recargas, el número del cargador se oculta (para que el círculo de recarga,
/// colocado en el mismo sitio, lo "sustituya" visualmente) y vuelve a aparecer al terminar.
/// La reserva se queda siempre visible.
/// </summary>
public class AmmoUI : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El arma actualmente equipada. Cuando montéis cambio de arma, llamad a SetWeapon() al equipar una nueva.")]
    [SerializeField] private WeaponBase weapon;
    [SerializeField] private TMP_Text magazineText;
    [SerializeField] private TMP_Text reserveText;
    [Tooltip("Opcional: el símbolo '/' entre cargador y reserva, si lo tienes como texto aparte. También se ocultará durante la recarga.")]
    [SerializeField] private GameObject separator;

    /// <summary>Llamar a esto al cambiar de arma equipada.</summary>
    public void SetWeapon(WeaponBase newWeapon)
    {
        Unsubscribe();
        weapon = newWeapon;
        Subscribe();

        if (weapon != null)
            UpdateUI();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (weapon == null) return;

        weapon.OnAmmoChanged += UpdateUI;
        weapon.OnReloadStarted += HideAmmoNumbers;
        weapon.OnReloadFinished += ShowAmmoNumbers;

        UpdateUI();
        // Por si te suscribes a mitad de una recarga ya en curso
        if (weapon.IsReloading) HideAmmoNumbers();
        else ShowAmmoNumbers();
    }

    private void Unsubscribe()
    {
        if (weapon == null) return;

        weapon.OnAmmoChanged -= UpdateUI;
        weapon.OnReloadStarted -= HideAmmoNumbers;
        weapon.OnReloadFinished -= ShowAmmoNumbers;
    }

    private void UpdateUI()
    {
        if (weapon == null) return;

        if (magazineText != null)
            magazineText.text = weapon.CurrentAmmo.ToString();

        if (reserveText != null)
            reserveText.text = weapon.ReserveAmmo.ToString();
    }

    private void HideAmmoNumbers()
    {
        if (magazineText != null) magazineText.gameObject.SetActive(false);
        if (reserveText != null) reserveText.gameObject.SetActive(false);
        if (separator != null) separator.SetActive(false);
    }

    private void ShowAmmoNumbers()
    {
        if (magazineText != null) magazineText.gameObject.SetActive(true);
        if (reserveText != null) reserveText.gameObject.SetActive(true);
        if (separator != null) separator.SetActive(true);
    }
}