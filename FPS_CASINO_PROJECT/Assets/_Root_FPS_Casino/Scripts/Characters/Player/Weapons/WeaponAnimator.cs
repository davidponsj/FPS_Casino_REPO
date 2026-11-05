using System;
using UnityEngine;

/// <summary>
/// Animaciones y sonidos del arma, sin Animator. Escucha los eventos de su WeaponBase (disparo, sin balas,
/// recarga) y:
/// - Animación de recarga: en cada frame pone el clip en el punto que toca (SampleAnimation), sincronizado con
///   la recarga real (ReloadProgress01). Dura siempre lo mismo que la recarga, también con la botella azul.
/// - Animación de disparo: cada disparo reproduce el clip desde el principio. Si recargas, manda la recarga.
/// - Animaciones del cargador (opcionales): al gastar cierta parte del cargador suena y se ve un clip, y el arma
///   se QUEDA en la pose final de ese clip hasta recargar. Ej.: el subfusil de 3 tubos gira los tubos al gastar
///   un tercio y otra vez al gastar dos tercios. Van encima del disparo, así que puedes seguir disparando.
/// - Sonidos: disparo (uno al azar de la lista, con un poco de variación de tono), uno opcional después del
///   disparo (el corredero de la escopeta), el "clic" sin balas y los de la recarga, cada uno en su momento
///   de la recarga (0 = empieza, 1 = acaba). Como la animación va por ese mismo progreso, van siempre a la vez.
///
/// Dónde va: en la raíz del modelo importado (el FBX con las animaciones), dentro del prefab del arma.
/// Si el arma no tiene animaciones, puede ir en la raíz del arma y solo hará los sonidos.
/// El Animator del modelo tiene que estar sin Controller: si no, se pelearía con este script.
/// Los sonidos salen por el AudioSource de la raíz del arma (el del WeaponBase).
/// </summary>
public class WeaponAnimator : MonoBehaviour
{
    [Serializable]
    public class ReloadSound
    {
        [Tooltip("Sonido de este momento de la recarga (sacar cargador, meter cartucho, montar...).")]
        public AudioClip clip;
        [Tooltip("En qué momento de la recarga suena. 0 = al empezar, 0.5 = a la mitad, 1 = al acabar.")]
        [Range(0f, 1f)] public float atProgress;
        [Tooltip("Volumen de este sonido. 1 = normal.")]
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Serializable]
    public class MagazineStep
    {
        [Tooltip("Clip que se reproduce al llegar a este punto del cargador (p. ej. AC_RotateTubes1_Subfusil). El arma se queda en su último frame hasta recargar.")]
        public AnimationClip clip;
        [Tooltip("Parte del cargador que queda cuando se lanza. 0.67 = al gastar un tercio; 0.34 = al gastar dos tercios. Con 50 balas: 0.67 salta al quedar 33.")]
        [Range(0f, 1f)] public float atAmmoFraction = 0.67f;
        [Tooltip("Sonido del giro, del cambio de tubo... Vacío = sin sonido.")]
        public AudioClip sound;
        [Tooltip("Volumen de este sonido. 1 = normal.")]
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("Animaciones (opcionales)")]
    [Tooltip("Clip de recarga del FBX (p. ej. AC_Shotgun_Reload). Debe empezar y acabar en la pose de reposo. Vacío = sin animación de recarga.")]
    [SerializeField] private AnimationClip reloadClip;
    [Tooltip("Clip de disparo del FBX (p. ej. AC_Shotgun_Shoot). Debe empezar y acabar en la pose de reposo. Vacío = sin animación de disparo.")]
    [SerializeField] private AnimationClip shootClip;
    [Tooltip("Velocidad del clip de disparo. 1 = como en el FBX; 2 = el doble de rápido, para armas que disparan muy seguido.")]
    [Min(0.01f)]
    [SerializeField] private float shootSpeed = 1f;

    [Header("Animaciones del cargador (opcionales)")]
    [Tooltip("Animaciones que salen al gastar parte del cargador, de la más llena a la más vacía. Subfusil de 3 tubos: dos pasos, en 0.67 y en 0.34. Vacío = ninguna.")]
    [SerializeField] private MagazineStep[] magazineSteps;

    [Header("Sonido: disparo")]
    [Tooltip("Sonidos de disparo. Si hay varios, suena uno al azar cada vez (queda más natural que repetir siempre el mismo).")]
    [SerializeField] private AudioClip[] fireClips;
    [Tooltip("Volumen del disparo. 1 = normal.")]
    [Range(0f, 1f)]
    [SerializeField] private float fireVolume = 1f;
    [Tooltip("Variación de tono al azar en cada disparo. 0 = siempre igual, 0.05 = ±5% (recomendado), 0.15 = mucha.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchVariation = 0.05f;

    [Header("Sonido: después del disparo (opcional)")]
    [Tooltip("Sonido que va detrás de cada disparo, como el corredero de la escopeta. Vacío = nada.")]
    [SerializeField] private AudioClip afterFireClip;
    [Tooltip("Segundos desde el disparo hasta este sonido. 0.35 = el corredero de una escopeta.")]
    [Min(0f)]
    [SerializeField] private float afterFireDelaySeconds = 0.35f;
    [Tooltip("Volumen de este sonido. 1 = normal.")]
    [Range(0f, 1f)]
    [SerializeField] private float afterFireVolume = 1f;

    [Header("Sonido: sin balas")]
    [Tooltip("Clic al disparar con el cargador vacío y sin balas en la reserva. Vacío = nada.")]
    [SerializeField] private AudioClip dryFireClip;
    [Tooltip("Volumen del clic. 1 = normal.")]
    [Range(0f, 1f)]
    [SerializeField] private float dryFireVolume = 1f;

    [Header("Sonido: recarga")]
    [Tooltip("Sonidos de la recarga, cada uno en su momento. Ajusta At Progress mirando la animación de recarga.")]
    [SerializeField] private ReloadSound[] reloadSounds;

    [Header("Depuración")]
    [Tooltip("Muestra junto al arma qué animación va y en qué segundo, y el último sonido, en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private WeaponBase weapon;
    private AudioSource audioSource;
    private float basePitch = 1f;

    private float shootStartTime = float.NegativeInfinity;
    private bool outOfRestPose;           // hay que devolver el arma a la pose de reposo cuando acabe la animación
    private float afterFireTime = -1f;     // cuándo toca el sonido de después del disparo (-1 = ninguno pendiente)
    private float lastReloadProgress = -1f; // progreso de la recarga en el frame anterior (-1 = no recargando)

    private int magazineStage;            // cuántos pasos del cargador se han pasado ya (0 = cargador lleno)
    private int playingStep = -1;          // paso cuyo clip se está reproduciendo ahora (-1 = ninguno)
    private float stepStartTime;

    // Solo para la depuración.
    private string debugState = "reposo";
    private string debugLastSound = "—";

    private void Awake()
    {
        weapon = GetComponentInParent<WeaponBase>();

        // De la más llena a la más vacía, por si en el Inspector están desordenadas.
        if (magazineSteps != null)
            Array.Sort(magazineSteps, (a, b) => b.atAmmoFraction.CompareTo(a.atAmmoFraction));

        if (weapon == null)
        {
            Debug.LogWarning($"{name}: WeaponAnimator no encuentra un WeaponBase en este objeto ni en sus padres.", this);
            return;
        }

        audioSource = weapon.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            basePitch = audioSource.pitch;
            audioSource.playOnAwake = false;
        }
        else if (HasAnySound())
        {
            Debug.LogWarning($"{name}: WeaponAnimator tiene sonidos pero la raíz del arma no tiene AudioSource.", this);
        }
    }

    private void OnEnable()
    {
        if (weapon == null) return;
        weapon.OnFired += HandleFired;
        weapon.OnDryFire += HandleDryFire;
        weapon.OnReloadStarted += HandleReloadStarted;
        weapon.OnReloadFinished += HandleReloadFinished;
    }

    private void OnDisable()
    {
        if (weapon == null) return;
        weapon.OnFired -= HandleFired;
        weapon.OnDryFire -= HandleDryFire;
        weapon.OnReloadStarted -= HandleReloadStarted;
        weapon.OnReloadFinished -= HandleReloadFinished;
    }

    // En LateUpdate: así va después de cualquier otra cosa que mueva el arma en el frame.
    private void LateUpdate()
    {
        if (weapon == null) return;

        UpdateSounds();
        UpdateMagazineStage();
        UpdateAnimation();
        UpdateMagazineAnimation();
    }

    // ---------------- EVENTOS DEL ARMA ----------------

    private void HandleFired()
    {
        shootStartTime = Time.time;

        if (fireClips != null && fireClips.Length > 0)
            PlaySound(fireClips[UnityEngine.Random.Range(0, fireClips.Length)], fireVolume, true, "disparo");

        if (afterFireClip != null)
            afterFireTime = Time.time + afterFireDelaySeconds;
    }

    private void HandleDryFire()
    {
        PlaySound(dryFireClip, dryFireVolume, false, "sin balas");
    }

    private void HandleReloadStarted()
    {
        // Si estaba pendiente el corredero de la escopeta, al recargar ya no tiene sentido.
        afterFireTime = -1f;
        lastReloadProgress = -1f; // -1 para que también suenen los que están en At Progress = 0
        PlayReloadSoundsUpTo(0f);
    }

    private void HandleReloadFinished()
    {
        // El último frame de la recarga puede quedarse justo antes del 1: suenan los que falten.
        PlayReloadSoundsUpTo(1f);
        lastReloadProgress = -1f;
    }

    // ---------------- ANIMACIÓN ----------------

    private void UpdateAnimation()
    {
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

    // ---------------- ANIMACIONES DEL CARGADOR ----------------

    /// <summary>
    /// Mira cuánta munición queda y, si se ha cruzado el siguiente paso (p. ej. un tercio gastado), lanza su clip
    /// y su sonido. Durante la recarga no hace nada: la animación de recarga ya devuelve el arma a su sitio.
    /// </summary>
    private void UpdateMagazineStage()
    {
        if (magazineSteps == null || magazineSteps.Length == 0 || weapon.IsReloading) return;

        int newStage = 0;
        float ammo01 = weapon.MagazineSize > 0 ? (float)weapon.CurrentAmmo / weapon.MagazineSize : 1f;
        foreach (MagazineStep step in magazineSteps)
            if (ammo01 <= step.atAmmoFraction) newStage++;

        if (newStage > magazineStage)
        {
            // Si de golpe se pasan dos pasos, se ve el último.
            MagazineStep step = magazineSteps[newStage - 1];
            playingStep = newStage - 1;
            stepStartTime = Time.time;
            PlaySound(step.sound, step.volume, false, $"cargador {newStage}");
        }
        else if (newStage < magazineStage)
        {
            playingStep = -1; // ha recargado: vuelve al cargador lleno sin animación (la recarga ya lo ha hecho)
        }

        magazineStage = newStage;
    }

    /// <summary>
    /// Se aplica DESPUÉS del disparo y del reposo: así el giro de tubos se ve aunque sigas disparando, y el arma
    /// se queda en la pose del último paso (sin esto, cada disparo o el reposo devolverían los tubos a su sitio).
    /// </summary>
    private void UpdateMagazineAnimation()
    {
        if (weapon.IsReloading || magazineSteps == null || magazineSteps.Length == 0) return;

        if (playingStep >= 0)
        {
            AnimationClip clip = magazineSteps[playingStep].clip;
            float time = Time.time - stepStartTime;
            if (clip != null && time < clip.length)
            {
                clip.SampleAnimation(gameObject, time);
                outOfRestPose = true;
                debugState = $"cargador {playingStep + 1} · {time:0.00}/{clip.length:0.00} s";
                return;
            }
            playingStep = -1;
        }

        // Mantener la pose del último paso pasado (su último frame).
        if (magazineStage > 0)
        {
            AnimationClip held = magazineSteps[magazineStage - 1].clip;
            if (held != null)
            {
                held.SampleAnimation(gameObject, held.length);
                outOfRestPose = true;
            }
        }
    }

    // ---------------- SONIDO ----------------

    private void UpdateSounds()
    {
        if (afterFireTime >= 0f && Time.time >= afterFireTime)
        {
            afterFireTime = -1f;
            PlaySound(afterFireClip, afterFireVolume, false, "después del disparo");
        }

        if (weapon.IsReloading)
            PlayReloadSoundsUpTo(weapon.ReloadProgress01);
    }

    /// <summary>Suena cada sonido de recarga cuyo momento se ha pasado desde el frame anterior.</summary>
    private void PlayReloadSoundsUpTo(float progress)
    {
        if (reloadSounds == null) return;

        foreach (ReloadSound sound in reloadSounds)
        {
            if (sound.atProgress > lastReloadProgress && sound.atProgress <= progress)
                PlaySound(sound.clip, sound.volume, false, $"recarga ({sound.atProgress:0.00})");
        }

        lastReloadProgress = progress;
    }

    private void PlaySound(AudioClip clip, float volume, bool varyPitch, string label)
    {
        if (clip == null || audioSource == null) return;

        // PlayOneShot permite que se solapen (disparo + corredero) sin cortar el anterior.
        audioSource.pitch = varyPitch ? basePitch * (1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation)) : basePitch;
        audioSource.PlayOneShot(clip, volume);
        debugLastSound = $"{label} · {clip.name}";
    }

    private bool HasAnySound()
    {
        return (fireClips != null && fireClips.Length > 0) || afterFireClip != null || dryFireClip != null
            || (reloadSounds != null && reloadSounds.Length > 0) || (magazineSteps != null && magazineSteps.Length > 0);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        DebugLabels.At(transform.position, 1, $"Animación · {debugState}", DebugLabels.Weapon);
        DebugLabels.At(transform.position, 2, $"Sonido · {debugLastSound}", DebugLabels.Weapon);
        if (magazineSteps != null && magazineSteps.Length > 0)
            DebugLabels.At(transform.position, 3, $"Cargador · paso {magazineStage}/{magazineSteps.Length}", DebugLabels.Weapon);
    }
#endif
}
