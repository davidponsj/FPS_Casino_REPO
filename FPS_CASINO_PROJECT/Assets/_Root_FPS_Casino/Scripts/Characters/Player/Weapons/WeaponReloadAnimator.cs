using UnityEngine;

/// <summary>
/// Animaciones del arma sin Animator: en cada frame pone el clip del FBX en el punto que toca (SampleAnimation).
/// - Recarga: sincronizada con la recarga real de WeaponBase (ReloadProgress01), así que dura siempre lo mismo
///   que la recarga, también con el multiplicador de las botellas.
/// - Disparo: cada disparo (WeaponBase.OnFired) reproduce el clip de disparo desde el principio.
///   Si recargas, manda la recarga.
/// Va en la raíz del modelo importado (el FBX con las animaciones), dentro del prefab del arma.
/// El Animator del modelo tiene que estar sin Controller: si no, se pelearía con este script.
/// </summary>
public class WeaponAnimator : MonoBehaviour
{
    [Header("Animaciones")]
    [Tooltip("Clip de recarga del FBX (p. ej. AC_Reload). Debe empezar y acabar en la pose de reposo del arma.")]
    [SerializeField] private AnimationClip reloadClip;
    [Tooltip("Clip de disparo del FBX (p. ej. AC_Shoot). Debe empezar y acabar en la pose de reposo. Vacío = sin animación de disparo.")]
    [SerializeField] private AnimationClip shootClip;
    [Tooltip("Velocidad del clip de disparo. 1 = como en el FBX; 2 = el doble de rápido, para armas que disparan muy seguido.")]
    [Min(0.01f)]
    [SerializeField] private float shootSpeed = 1f;

    [Header("Depuración")]
    [Tooltip("Muestra junto al arma qué animación suena y en qué segundo va, en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private WeaponBase weapon;
    private float shootStartTime = float.NegativeInfinity;
    private bool outOfRestPose; // hay que devolver el arma a la pose de reposo cuando acabe la animación

    // Solo para la depuración.
    private string debugState = "reposo";

    private void Awake()
    {
        weapon = GetComponentInParent<WeaponBase>();
        if (weapon == null) Debug.LogWarning($"{name}: WeaponAnimator no encuentra un WeaponBase en sus padres.", this);
        if (reloadClip == null) Debug.LogWarning($"{name}: WeaponAnimator no tiene asignado el clip de recarga.", this);
    }

    private void OnEnable()
    {
        if (weapon != null) weapon.OnFired += HandleFired;
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.OnFired -= HandleFired;
    }

    private void HandleFired()
    {
        shootStartTime = Time.time;
    }

    // En LateUpdate: así va después de cualquier otra cosa que mueva el arma en el frame.
    private void LateUpdate()
    {
        if (weapon == null) return;

        if (weapon.IsReloading && reloadClip != null)
        {
            Pose(reloadClip, weapon.ReloadProgress01 * reloadClip.length, "recarga");
            return;
        }

        float shootTime = (Time.time - shootStartTime) * shootSpeed;
        if (shootClip != null && shootTime < shootClip.length)
        {
            Pose(shootClip, shootTime, "disparo");
            return;
        }

        if (outOfRestPose)
        {
            // Los dos clips empiezan en la pose de reposo: su segundo 0 la recupera.
            (reloadClip != null ? reloadClip : shootClip).SampleAnimation(gameObject, 0f);
            outOfRestPose = false;
            debugState = "reposo";
        }
    }

    private void Pose(AnimationClip clip, float time, string state)
    {
        clip.SampleAnimation(gameObject, time);
        outOfRestPose = true;
        debugState = $"{state} · {time:0.00}/{clip.length:0.00} s";
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        DebugLabels.At(transform.position, 1, $"Animación · {debugState}", DebugLabels.Weapon);
    }
#endif
}