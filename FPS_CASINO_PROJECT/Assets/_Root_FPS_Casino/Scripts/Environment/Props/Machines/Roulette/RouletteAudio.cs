using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sonidos de la ruleta. Escucha los eventos de RouletteMachine (igual que SlotAudio con la tragaperras), así que no
/// hay que tocar ningún otro script:
/// - Al tirar: el golpecito de lanzar la bola.
/// - Mientras la bola corre por el borde: el rodar en bucle, cuyo volumen y tono siguen la velocidad REAL de la bola
///   (se mide cada frame), así se oye cómo va frenando.
/// - Cada rebote entre las casillas: un "clac" que va perdiendo fuerza rebote a rebote.
/// - Al caer en su casilla: el golpe final y, si da ticket, unas campanitas de premio.
/// - Sin puntos o con un ticket pendiente: un aviso grave.
/// </summary>
[RequireComponent(typeof(RouletteMachine))]
public class RouletteAudio : MonoBehaviour
{
    [Header("Salida")]
    [Tooltip("Grupo SFX del Audio Mixer (AM_Main), para que lo controle el slider de efectos.")]
    [SerializeField] private AudioMixerGroup output;
    [Tooltip("La bola de la ruleta (el mismo objeto que Ball en Roulette Machine). Sirve para que el rodar siga su velocidad.")]
    [SerializeField] private Transform ball;

    [Header("Sonidos")]
    [SerializeField] private AudioClip launchClip;
    [Tooltip("Bucle de la bola rodando (SFX_Roulette_Roll_Loop).")]
    [SerializeField] private AudioClip rollLoop;
    [Tooltip("Rebotes entre casillas. Sale uno al azar cada vez.")]
    [SerializeField] private AudioClip[] bounceClips;
    [SerializeField] private AudioClip landClip;
    [Tooltip("Suena al caer en una casilla que da ticket.")]
    [SerializeField] private AudioClip winClip;
    [Tooltip("Sin puntos suficientes o con un ticket esperando.")]
    [SerializeField] private AudioClip errorClip;

    [Header("Volumen")]
    [SerializeField, Range(0f, 1f)] private float launchVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float rollVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float bounceVolume = 0.9f;
    [Tooltip("Cada rebote suena a esta parte del anterior. 0.75 = se van apagando.")]
    [SerializeField, Range(0.3f, 1f)] private float bounceFalloff = 0.75f;
    [SerializeField, Range(0f, 1f)] private float landVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float winVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float errorVolume = 0.5f;
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.06f;

    [Header("Rodar")]
    [Tooltip("Tono del rodar con la bola casi parada y a toda velocidad.")]
    [SerializeField] private Vector2 rollPitch = new Vector2(0.65f, 1.15f);
    [Tooltip("Segundos del fundido del rodar al empezar y al acabar.")]
    [Min(0.01f)]
    [SerializeField] private float rollFade = 0.15f;

    [Header("Sonido 3D")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 15f;

    [Header("Depuración")]
    [Tooltip("Muestra sobre la ruleta la velocidad de la bola y el volumen del rodar.")]
    [SerializeField] private bool showDebug;

    private RouletteMachine roulette;
    private AudioSource rollSource;
    private AudioSource sfxSource;
    private bool rolling;
    private int bounces;
    private Vector3 lastBallPosition;
    private float topSpeed;
    private float ballSpeed;
    private float rollTarget;
    private int ignoreFrames;   // al tirar, la bola se coloca de golpe en el borde: esos frames no cuentan como velocidad

    private void Awake()
    {
        roulette = GetComponent<RouletteMachine>();
        rollSource = CreateSource(true);
        sfxSource = CreateSource(false);
        if (rollLoop != null) rollSource.clip = rollLoop;
    }

    private void OnEnable()
    {
        roulette.onSpinStart.AddListener(HandleSpinStart);
        roulette.onBallBounce.AddListener(HandleBounce);
        roulette.onSpinFinished.AddListener(HandleFinished);
        roulette.onNotEnoughPoints.AddListener(HandleError);
        roulette.onBlockedByTicket.AddListener(HandleError);
    }

    private void OnDisable()
    {
        roulette.onSpinStart.RemoveListener(HandleSpinStart);
        roulette.onBallBounce.RemoveListener(HandleBounce);
        roulette.onSpinFinished.RemoveListener(HandleFinished);
        roulette.onNotEnoughPoints.RemoveListener(HandleError);
        roulette.onBlockedByTicket.RemoveListener(HandleError);
        if (rollSource != null) rollSource.Stop();
    }

    private void Update()
    {
        if (ball != null && Time.deltaTime > 0f)
        {
            Vector3 p = ball.position;
            if (ignoreFrames > 0) ignoreFrames--;
            else
            {
                ballSpeed = Vector3.Distance(p, lastBallPosition) / Time.deltaTime;
                if (rolling) topSpeed = Mathf.Max(topSpeed, ballSpeed);
            }
            lastBallPosition = p;
        }

        if (rollSource.clip == null) return;

        // Volumen y tono según lo rápido que va la bola respecto a su máximo de esta tirada.
        float speed01 = topSpeed > 0.001f ? Mathf.Clamp01(ballSpeed / topSpeed) : (rolling ? 1f : 0f);
        float target = rolling ? rollVolume * Mathf.Lerp(0.35f, 1f, speed01) : 0f;
        rollTarget = Mathf.MoveTowards(rollTarget, target, Time.deltaTime / rollFade);
        rollSource.volume = rollTarget;
        rollSource.pitch = Mathf.Lerp(rollPitch.x, rollPitch.y, speed01);

        if (rollTarget <= 0f && rollSource.isPlaying) rollSource.Stop();
    }

    private void HandleSpinStart()
    {
        Play(launchClip, launchVolume);
        bounces = 0;
        topSpeed = 0f;
        ignoreFrames = 2;

        rolling = true;
        if (rollSource.clip != null && !rollSource.isPlaying)
        {
            rollSource.time = Random.Range(0f, rollSource.clip.length);  // cada tirada empieza en otro punto del bucle
            rollSource.Play();
        }
    }

    private void HandleBounce()
    {
        // El primer rebote es la bola cayendo del borde: ahí deja de rodar.
        rolling = false;
        float volume = bounceVolume * Mathf.Pow(bounceFalloff, bounces);
        bounces++;
        if (bounceClips != null && bounceClips.Length > 0)
            Play(bounceClips[Random.Range(0, bounceClips.Length)], volume);
    }

    private void HandleFinished(RouletteMachine.Segment result)
    {
        rolling = false;
        Play(landClip, landVolume);
        if (result != null && result.ticket != null) Play(winClip, winVolume, false);
    }

    private void HandleError() => Play(errorClip, errorVolume, false);

    private void Play(AudioClip clip, float volume, bool vary = true)
    {
        if (clip == null) return;
        sfxSource.pitch = vary ? 1f + Random.Range(-pitchVariation, pitchVariation) : 1f;
        sfxSource.PlayOneShot(clip, volume);
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = loop ? 0f : 1f;
        source.outputAudioMixerGroup = output;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        return source;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        string state = rolling ? $"rodando · bola {ballSpeed:0.0} m/s (máx {topSpeed:0.0}) · vol {rollTarget:0.00}" : $"quieta · rebotes {bounces}";
        DebugLabels.At(transform.position + Vector3.up * 1.6f, 1, $"Audio ruleta · {state}", DebugLabels.Casino);
    }
#endif
}
