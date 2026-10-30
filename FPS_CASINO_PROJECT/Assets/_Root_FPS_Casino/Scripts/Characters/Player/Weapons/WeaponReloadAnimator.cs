using UnityEngine;

/// <summary>
/// Reproduce el clip de recarga del arma sincronizado con la recarga real de WeaponBase:
/// en cada frame pone el clip en el mismo punto que la recarga (ReloadProgress01), así que
/// dura siempre lo mismo que la recarga, también con el multiplicador de las botellas.
/// Va en la raíz del modelo importado (el FBX con la animación), dentro del prefab del arma.
/// </summary>
public class WeaponReloadAnimator : MonoBehaviour
{
    [Header("Animación")]
    [Tooltip("Clip de recarga del FBX (p. ej. AC_Reload). Debe empezar y acabar en la pose de reposo del arma.")]
    [SerializeField] private AnimationClip reloadClip;

    private WeaponBase weapon;
    private bool wasReloading;

    private void Awake()
    {
        weapon = GetComponentInParent<WeaponBase>();
        if (weapon == null) Debug.LogWarning($"{name}: WeaponReloadAnimator no encuentra un WeaponBase en sus padres.", this);
        if (reloadClip == null) Debug.LogWarning($"{name}: WeaponReloadAnimator no tiene asignado el clip de recarga.", this);
    }

    private void LateUpdate()
    {
        if (weapon == null || reloadClip == null) return;

        bool reloading = weapon.IsReloading;
        if (reloading)
            reloadClip.SampleAnimation(gameObject, weapon.ReloadProgress01 * reloadClip.length);
        else if (wasReloading)
            reloadClip.SampleAnimation(gameObject, 0f); // vuelve a la pose de reposo al acabar
        wasReloading = reloading;
    }
}
