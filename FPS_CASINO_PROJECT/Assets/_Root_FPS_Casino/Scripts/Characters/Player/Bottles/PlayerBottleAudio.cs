using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sonidos de la botella del jugador. Escucha al BottleHolder (no hay que tocarlo):
/// - Al coger una botella (del barman): Take.
/// - Al beberla: Drink.
/// El sonido de romperse al lanzarla va en PF_BottleBreak (un AudioSource que suena al aparecer).
/// Suenan en primera persona, sin posición 3D.
/// </summary>
public class PlayerBottleAudio : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("BottleHolder del jugador. Vacío = lo busca en este objeto, sus hijos o sus padres.")]
    [SerializeField] private BottleHolder holder;
    [Tooltip("Grupo SFX del Audio Mixer (AM_Main).")]
    [SerializeField] private AudioMixerGroup output;

    [Header("Sonidos")]
    [Tooltip("Al coger la botella (SFX_Bottle_Take).")]
    [SerializeField] private AudioClip takeClip;
    [SerializeField, Range(0f, 1f)] private float takeVolume = 0.8f;
    [Tooltip("Al beberla (SFX_Bottle_Drink).")]
    [SerializeField] private AudioClip drinkClip;
    [SerializeField, Range(0f, 1f)] private float drinkVolume = 0.9f;
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.05f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cada sonido de botella.")]
    [SerializeField] private bool showDebug;

    private AudioSource source;
    private bool hadBottle;
#pragma warning disable 0414 // solo se muestra en el editor
    private string lastSound = "-";
#pragma warning restore 0414

    private void Awake()
    {
        if (holder == null) holder = GetComponentInChildren<BottleHolder>();
        if (holder == null) holder = GetComponentInParent<BottleHolder>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = output;
    }

    private void OnEnable()
    {
        if (holder != null) holder.OnEffectStarted += HandleDrink;
    }

    private void OnDisable()
    {
        if (holder != null) holder.OnEffectStarted -= HandleDrink;
    }

    private void Update()
    {
        if (holder == null) return;
        bool has = holder.HasBottle;
        if (has && !hadBottle) Play(takeClip, takeVolume, "coger");
        hadBottle = has;
    }

    private void HandleDrink(TicketData ticket) => Play(drinkClip, drinkVolume, "beber");

    private void Play(AudioClip clip, float volume, string label)
    {
        if (clip == null) return;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume);
        lastSound = label;
        if (showDebug) Debug.Log($"[Botella] Sonido: {label}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        DebugLabels.At(transform.position + Vector3.up * 2.4f, 0, $"Audio botella · último: {lastSound}", DebugLabels.Casino);
    }
#endif
}
