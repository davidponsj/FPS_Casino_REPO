using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Pasos del jugador sobre la moqueta del casino. Lee el PlayerController, así que no hay que conectar nada:
/// - Andando o esprintando por el suelo: un paso cada cierta distancia recorrida (con sprint, zancadas más largas y
///   pasos más fuertes). Nunca repite el mismo sonido dos veces seguidas y varía un poco el tono.
/// - Corriendo por la pared (wallrun): pasos más rápidos y algo más agudos y flojos (opcional).
/// - Al aterrizar: golpe de los dos pies, más fuerte cuanto más fuerte caes.
/// - Haciendo slide: roce de tela en bucle que sigue tu velocidad.
/// Los sonidos son tuyos (en primera persona): suenan "en tu cabeza", sin posición 3D.
/// </summary>
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("PlayerController del jugador. Vacío = lo busca en este objeto o en sus padres.")]
    [SerializeField] private PlayerController controller;
    [Tooltip("Grupo SFX del Audio Mixer (AM_Main).")]
    [SerializeField] private AudioMixerGroup output;

    [Header("Sonidos")]
    [Tooltip("Pasos sobre moqueta (SFX_Step_Carpet_01..06). Cuantos más, menos repetitivo.")]
    [SerializeField] private AudioClip[] stepClips;
    [Tooltip("Aterrizaje (SFX_Step_Carpet_Land).")]
    [SerializeField] private AudioClip landClip;
    [Tooltip("Roce del slide en bucle (SFX_Slide_Carpet_Loop).")]
    [SerializeField] private AudioClip slideLoop;

    [Header("Pasos")]
    [Tooltip("Metros entre paso y paso andando.")]
    [Min(0.1f)]
    [SerializeField] private float walkStepDistance = 1.6f;
    [Tooltip("Metros entre paso y paso esprintando (zancadas más largas).")]
    [Min(0.1f)]
    [SerializeField] private float sprintStepDistance = 2.3f;
    [SerializeField, Range(0f, 1f)] private float walkVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float sprintVolume = 0.35f;
    [Tooltip("Variación de tono de cada paso. 0.08 = natural.")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.08f;

    [Header("Wallrun")]
    [Tooltip("Pasos también al correr por la pared.")]
    [SerializeField] private bool wallRunSteps = true;
    [Tooltip("Metros entre pasos por la pared.")]
    [Min(0.1f)]
    [SerializeField] private float wallStepDistance = 1.9f;
    [SerializeField, Range(0f, 1f)] private float wallVolume = 0.22f;
    [Tooltip("Tono de los pasos en la pared (la pared no es moqueta: suenan algo más secos).")]
    [SerializeField, Range(0.5f, 2f)] private float wallPitch = 1.2f;

    [Header("Aterrizaje")]
    [Tooltip("Velocidad de caída (m/s) a partir de la que suena el aterrizaje. Saltitos más flojos no suenan.")]
    [Min(0f)]
    [SerializeField] private float minLandSpeed = 2.5f;
    [Tooltip("Velocidad de caída (m/s) con la que suena al máximo.")]
    [Min(0.1f)]
    [SerializeField] private float maxLandSpeed = 12f;
    [SerializeField, Range(0f, 1f)] private float landVolume = 0.55f;

    [Header("Slide")]
    [SerializeField, Range(0f, 1f)] private float slideVolume = 0.3f;
    [Tooltip("Velocidad (m/s) a la que el roce suena al máximo.")]
    [Min(0.1f)]
    [SerializeField] private float slideFullSpeed = 12f;

    [Header("Depuración")]
    [Tooltip("Muestra sobre el jugador el estado, los metros para el siguiente paso y el volumen del slide.")]
    [SerializeField] private bool showDebug;

    private AudioSource stepSource;
    private AudioSource slideSource;
    private float distanceSinceStep;
    private int lastClip = -1;
    private float slideLevel;
    private PlayerHealth health;

    private void Awake()
    {
        if (controller == null) controller = GetComponentInParent<PlayerController>();
        health = GetComponentInParent<PlayerHealth>();
        stepSource = CreateSource(false);
        slideSource = CreateSource(true);
        slideSource.clip = slideLoop;
        slideSource.volume = 0f;
    }

    private void OnEnable()
    {
        if (controller != null) controller.OnLanded += HandleLanded;
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnLanded -= HandleLanded;
        if (slideSource != null) slideSource.Stop();
    }

    private void Update()
    {
        if (controller == null) return;
        bool dead = health != null && health.IsDead;
        PlayerController.PlayerMovementState state = controller.CurrentState;
        float speed = controller.CurrentHorizontalSpeed;

        // ---- Pasos ----
        bool onFloor = !dead && controller.IsGrounded &&
                       (state == PlayerController.PlayerMovementState.Walking || state == PlayerController.PlayerMovementState.Sprinting);
        bool onWall = !dead && wallRunSteps && state == PlayerController.PlayerMovementState.WallRunning;

        if (onFloor || onWall)
        {
            distanceSinceStep += speed * Time.deltaTime;
            bool sprint = state == PlayerController.PlayerMovementState.Sprinting;
            float stride = onWall ? wallStepDistance : sprint ? sprintStepDistance : walkStepDistance;
            if (distanceSinceStep >= stride)
            {
                distanceSinceStep = 0f;
                float volume = onWall ? wallVolume : sprint ? sprintVolume : walkVolume;
                PlayStep(volume, onWall ? wallPitch : 1f);
            }
        }
        else
        {
            // Al volver a andar, el primer paso llega pronto (no tienes que recorrer una zancada entera en silencio).
            distanceSinceStep = walkStepDistance * 0.6f;
        }

        // ---- Slide ----
        bool sliding = !dead && state == PlayerController.PlayerMovementState.Sliding && slideLoop != null;
        float target = sliding ? slideVolume * Mathf.Clamp01(speed / slideFullSpeed) : 0f;
        slideLevel = Mathf.MoveTowards(slideLevel, target, Time.deltaTime * (sliding ? 6f : 4f));
        slideSource.volume = slideLevel;
        slideSource.pitch = Mathf.Lerp(0.8f, 1.15f, Mathf.Clamp01(speed / slideFullSpeed));
        if (slideLevel > 0f && !slideSource.isPlaying)
        {
            slideSource.time = Random.Range(0f, slideLoop.length);
            slideSource.Play();
        }
        else if (slideLevel <= 0f && slideSource.isPlaying)
        {
            slideSource.Stop();
        }
    }

    private void HandleLanded(float impact)
    {
        float fall = Mathf.Abs(impact);
        if (fall < minLandSpeed || landClip == null) return;
        float k = Mathf.InverseLerp(minLandSpeed, maxLandSpeed, fall);
        stepSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation) * 0.5f - k * 0.08f; // más grave si caes fuerte
        stepSource.PlayOneShot(landClip, landVolume * Mathf.Lerp(0.5f, 1f, k));
        distanceSinceStep = 0f;
    }

    private void PlayStep(float volume, float basePitch)
    {
        if (stepClips == null || stepClips.Length == 0) return;
        int index = Random.Range(0, stepClips.Length);
        if (stepClips.Length > 1 && index == lastClip) index = (index + 1) % stepClips.Length;
        lastClip = index;
        if (stepClips[index] == null) return;

        stepSource.pitch = basePitch * (1f + Random.Range(-pitchVariation, pitchVariation));
        stepSource.PlayOneShot(stepClips[index], volume * Random.Range(0.85f, 1f));
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.outputAudioMixerGroup = output;
        source.spatialBlend = 0f; // en primera persona
        return source;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || controller == null) return;
        DebugLabels.Above(transform, DebugLabels.PlayerControllerLine + 6,
            $"Pasos · {controller.CurrentState} · {distanceSinceStep:0.0} m · slide {slideLevel:0.00}", DebugLabels.Movement);
    }
#endif
}
