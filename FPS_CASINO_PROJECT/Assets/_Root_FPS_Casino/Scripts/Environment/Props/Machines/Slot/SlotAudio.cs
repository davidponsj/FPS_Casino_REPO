using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sonidos de la tragaperras. Escucha los eventos de SlotMachine, así que suena desde la propia
/// máquina sin tocar ningún otro script: botón, palanca, rodillos girando, parada de cada rodillo,
/// premio y derrota.
/// </summary>
[RequireComponent(typeof(SlotMachine))]
public class SlotAudio : MonoBehaviour
{
    [Header("Salida")]
    [Tooltip("Canal del Audio Mixer por el que sale el sonido (SFX de AM_Main). Vacío = sin mixer.")]
    [SerializeField] private AudioMixerGroup output;

    [Header("Sonidos")]
    [Tooltip("Clic al elegir apuesta con un botón.")]
    [SerializeField] private AudioClip buttonClip;
    [Tooltip("Tirón de la palanca al empezar a girar.")]
    [SerializeField] private AudioClip leverClip;
    [Tooltip("Bucle de los rodillos girando. Se corta cuando para el último.")]
    [SerializeField] private AudioClip spinLoop;
    [Tooltip("Golpe seco de cada rodillo al pararse.")]
    [SerializeField] private AudioClip reelStopClip;
    [Tooltip("Musiquita de premio.")]
    [SerializeField] private AudioClip winClip;
    [Tooltip("Sonido de derrota. Vacío = no suena nada.")]
    [SerializeField] private AudioClip loseClip;

    [Header("Volumen")]
    [SerializeField, Range(0f, 1f)] private float buttonVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float leverVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float spinVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float reelStopVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float winVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float loseVolume = 0.5f;
    [Tooltip("Variación de tono de cada parada para que no suenen las tres iguales.")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.06f;

    [Header("Sonido 3D")]
    [Tooltip("Hasta esta distancia se oye a todo volumen.")]
    [SerializeField] private float minDistance = 1f;
    [Tooltip("A partir de esta distancia ya no se oye.")]
    [SerializeField] private float maxDistance = 15f;

    private SlotMachine machine;
    private AudioSource spinSource;
    private AudioSource sfxSource;
    private int reelsStopped;

    private void Awake()
    {
        machine = GetComponent<SlotMachine>();
        spinSource = CreateSource(true);
        sfxSource = CreateSource(false);
    }

    private void OnEnable()
    {
        machine.onBetSelected.AddListener(HandleBetSelected);
        machine.onSpinStart.AddListener(HandleSpinStart);
        machine.onReelStopped.AddListener(HandleReelStopped);
        machine.onWin.AddListener(HandleWin);
        machine.onLose.AddListener(HandleLose);
    }

    private void OnDisable()
    {
        machine.onBetSelected.RemoveListener(HandleBetSelected);
        machine.onSpinStart.RemoveListener(HandleSpinStart);
        machine.onReelStopped.RemoveListener(HandleReelStopped);
        machine.onWin.RemoveListener(HandleWin);
        machine.onLose.RemoveListener(HandleLose);
    }

    private AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.outputAudioMixerGroup = output;
        source.spatialBlend = 1f;   // 3D: suena desde la máquina
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        return source;
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
    }

    private void HandleBetSelected() => Play(buttonClip, buttonVolume);

    private void HandleSpinStart()
    {
        reelsStopped = 0;
        Play(leverClip, leverVolume);

        if (spinLoop == null) return;
        spinSource.clip = spinLoop;
        spinSource.volume = spinVolume;
        spinSource.Play();
    }

    private void HandleReelStopped(int reel)
    {
        Play(reelStopClip, reelStopVolume, 1f + Random.Range(-pitchVariation, pitchVariation));

        // Cuando para el último rodillo se acaba el sonido de girar
        reelsStopped++;
        if (reelsStopped >= machine.ReelCount) spinSource.Stop();
    }

    private void HandleWin(int points) => Play(winClip, winVolume);

    private void HandleLose() => Play(loseClip, loseVolume);
}