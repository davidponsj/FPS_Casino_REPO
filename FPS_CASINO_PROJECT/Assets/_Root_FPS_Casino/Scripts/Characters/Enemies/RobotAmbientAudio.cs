using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sonido de los robots de seguridad (motor, radio...). Para que no canse con una horda entera:
/// - No suena seguido: cada pocos segundos suena un trozo al azar del sonido, con fundidos.
/// - En 3D y flojo: solo se oye de cerca.
/// - Como mucho suenan unos pocos robots a la vez (Max Simultaneous), aunque haya muchos.
/// Se calla al morir el robot.
/// </summary>
public class RobotAmbientAudio : MonoBehaviour
{
    private static int playingCount;

    // Por si "Enter Play Mode Options" deja los static de la partida anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() => playingCount = 0;

    [Header("Sonido")]
    [Tooltip("Sonido del robot (SFX_Robot_Security). Suenan trozos al azar.")]
    [SerializeField] private AudioClip clip;
    [Tooltip("Grupo SFX del Audio Mixer (AM_Main).")]
    [SerializeField] private AudioMixerGroup output;
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.06f;

    [Header("Cuándo suena")]
    [Tooltip("Segundos de silencio entre trozo y trozo (mín, máx).")]
    [SerializeField] private Vector2 gapSeconds = new Vector2(5f, 12f);
    [Tooltip("Segundos que dura cada trozo (mín, máx).")]
    [SerializeField] private Vector2 chunkSeconds = new Vector2(2f, 3.5f);
    [Tooltip("Robots que pueden sonar a la vez como mucho.")]
    [Min(1)]
    [SerializeField] private int maxSimultaneous = 2;
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 0.3f;

    [Header("Sonido 3D")]
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 12f;

#pragma warning disable 0414, 0169 // showDebug solo se usa en el editor
    [Header("Depuración")]
    [Tooltip("Muestra sobre el robot si está sonando y cuánto falta para el siguiente trozo.")]
    [SerializeField] private bool showDebug;
#pragma warning restore 0414, 0169

    private AudioSource source;
    private EnemyHealth health;
    private float nextStart;
    private float chunkEnd = -1f;
    private bool counted;

    private void Awake()
    {
        health = GetComponentInParent<EnemyHealth>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.clip = clip;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = output;
    }

    private void OnEnable()
    {
        nextStart = Time.time + Random.Range(0.5f, gapSeconds.y); // que no empiecen todos a la vez
        chunkEnd = -1f;
    }

    private void OnDisable() => StopChunk();

    private void Update()
    {
        if (clip == null) return;
        bool dead = health != null && health.IsDead;

        if (chunkEnd < 0f)
        {
            if (!dead && Time.time >= nextStart)
            {
                if (playingCount < maxSimultaneous) StartChunk();
                else nextStart = Time.time + Random.Range(1f, 3f);
            }
            return;
        }

        bool ending = dead || Time.time >= chunkEnd - fadeSeconds;
        float target = ending ? 0f : volume;
        source.volume = Mathf.MoveTowards(source.volume, target, volume * Time.deltaTime / fadeSeconds);
        if (ending && source.volume <= 0f) StopChunk();
    }

    private void StartChunk()
    {
        float length = Random.Range(chunkSeconds.x, chunkSeconds.y);
        source.time = Random.Range(0f, Mathf.Max(0f, clip.length - length));
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.volume = 0f;
        source.Play();
        chunkEnd = Time.time + length;
        playingCount++;
        counted = true;
    }

    private void StopChunk()
    {
        if (source != null) { source.Stop(); source.volume = 0f; }
        if (counted) { playingCount = Mathf.Max(0, playingCount - 1); counted = false; }
        chunkEnd = -1f;
        nextStart = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        string state = chunkEnd > 0f ? $"sonando ({source.volume:0.00})" : $"siguiente en {Mathf.Max(0f, nextStart - Time.time):0.0} s";
        DebugLabels.At(transform.position + Vector3.up * 2.2f, 2, $"Audio robot · {state} · a la vez {playingCount}", DebugLabels.Casino);
    }
#endif
}
