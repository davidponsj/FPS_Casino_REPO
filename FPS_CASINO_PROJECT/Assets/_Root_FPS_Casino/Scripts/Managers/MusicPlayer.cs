using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Música y ambiente de la escena. Suena en bucle desde que empieza, sin posición 3D:
/// - Music: la música de la escena (MUS_Menu en el menú, MUS_Gameplay en la partida), por el grupo Music del mixer
///   (la controla el slider de música).
/// - Ambience: fondo opcional (AMB_Casino_Loop: murmullo del casino), por el grupo SFX.
/// - Death Music: al morir el jugador, la música pasa a esta (MUS_Lost) con un fundido, y el ambiente baja.
/// - En la partida, la música espera a que el jugador termine de despertarse (PlayerWakeUpIntro); el ambiente suena
///   desde el principio.
/// Los fundidos usan tiempo real, así que funcionan aunque el juego esté en pausa.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [Header("Música")]
    [Tooltip("Música de esta escena, en bucle.")]
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;
    [Tooltip("Música al morir el jugador (MUS_Lost). Vacío = sigue la misma.")]
    [SerializeField] private AudioClip deathMusic;
    [SerializeField, Range(0f, 1f)] private float deathMusicVolume = 0.5f;
    [Tooltip("Espera a que el jugador termine de despertarse (abra los ojos y se levante) para empezar la música.")]
    [SerializeField] private bool waitForWakeUp = true;
    [Tooltip("Segundos de más tras despertarse antes de que entre la música.")]
    [Min(0f)]
    [SerializeField] private float delayAfterWakeUp = 0.3f;

    [Header("Ambiente")]
    [Tooltip("Fondo en bucle (murmullo del casino). Vacío = sin ambiente.")]
    [SerializeField] private AudioClip ambience;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;
    [Tooltip("Cuánto se queda el ambiente al morir (0 = se apaga, 1 = igual).")]
    [SerializeField, Range(0f, 1f)] private float ambienceOnDeath = 0.3f;

    [Header("Salidas (AM_Main)")]
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Fundidos")]
    [Tooltip("Segundos de los fundidos al empezar y al cambiar de música.")]
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 1.5f;

#pragma warning disable 0414, 0169 // showDebug solo se usa en el editor
    [Header("Depuración")]
    [Tooltip("Muestra qué suena y a qué volumen.")]
    [SerializeField] private bool showDebug;
#pragma warning restore 0414, 0169

    private AudioSource musicA, musicB, ambienceSource;
    private AudioSource currentMusic;
    private float musicTarget, ambienceTarget;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        musicA = CreateSource(musicGroup);
        musicB = CreateSource(musicGroup);
        ambienceSource = CreateSource(sfxGroup);
    }

    private void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) playerHealth.OnDied += HandleDied;

        if (music != null) StartCoroutine(StartMusicWhenAwake());
        if (ambience != null)
        {
            ambienceSource.clip = ambience;
            ambienceSource.time = Random.Range(0f, ambience.length * 0.9f); // cada partida empieza en otro punto
            ambienceSource.Play();
            ambienceTarget = ambienceVolume;
        }
    }

    private IEnumerator StartMusicWhenAwake()
    {
        if (waitForWakeUp && FindFirstObjectByType<PlayerWakeUpIntro>() != null)
        {
            yield return null; // la intro empieza en su Start: un frame para que IsPlaying ya esté puesto
            while (PlayerWakeUpIntro.IsPlaying) yield return null;
            yield return new WaitForSecondsRealtime(delayAfterWakeUp);
        }
        if (currentMusic == null) StartMusic(musicA, music, musicVolume); // si ha muerto antes, ya suena la de muerte
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandleDied;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / fadeSeconds;
        foreach (AudioSource source in new[] { musicA, musicB })
        {
            float target = source == currentMusic ? musicTarget : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, step * Mathf.Max(target, 0.5f));
            if (source != currentMusic && source.isPlaying && source.volume <= 0f) source.Stop();
        }
        ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceTarget, step * 0.5f);
    }

    private void HandleDied()
    {
        if (deathMusic != null) StartMusic(currentMusic == musicA ? musicB : musicA, deathMusic, deathMusicVolume);
        ambienceTarget = ambienceVolume * ambienceOnDeath;
    }

    private void StartMusic(AudioSource source, AudioClip clip, float volume)
    {
        source.clip = clip;
        source.volume = 0f;
        source.Play();
        currentMusic = source;
        musicTarget = volume;
    }

    private AudioSource CreateSource(AudioMixerGroup group)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.ignoreListenerPause = true; // la música no se corta en el menú de pausa
        source.outputAudioMixerGroup = group;
        return source;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || currentMusic == null) return;
        string clip = currentMusic.clip != null ? currentMusic.clip.name : "-";
        DebugLabels.At(transform.position + Vector3.up, 0, $"Música · {clip} {currentMusic.volume:0.00} · ambiente {ambienceSource.volume:0.00}", DebugLabels.Casino);
    }
#endif
}
