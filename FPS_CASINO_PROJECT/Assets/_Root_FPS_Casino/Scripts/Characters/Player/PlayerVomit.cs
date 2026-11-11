using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Vómito antes de morir por blackout. PlayerBlackout lo lanza en vez de matarte directamente y, al acabar, te mata
/// (y PlayerDeathAnimation hace la caída de borracho de siempre, empezando desde la postura en la que te deja esto).
///
/// Secuencia (unos 2.8 s, ajustable con las curvas del Inspector):
/// 1. Mareo: la cabeza se balancea y empieza a mirar hacia abajo.
/// 2. Primera arcada: la cabeza da un golpe hacia delante y abajo, tiembla y sale un chorro corto.
/// 3. Respiro: se recupera un poco.
/// 4. Arcada fuerte: mira casi al suelo, tiembla más y sale el chorro largo; el vómito cae y forma un charco.
/// 5. Goteo con la cabeza colgando.
/// Mientras dura no te puedes mover ni disparar, el arma y la botella se ocultan y los bordes de la pantalla se tiñen de
/// verde en cada arcada. Si un enemigo te mata a mitad, se corta y muere normal.
///
/// Va en la Main Camera del jugador (junto a PlayerDeathAnimation). Todo se crea por código; los materiales son
/// opcionales (vacíos = se crean solos).
/// </summary>
[RequireComponent(typeof(Camera))]
public class PlayerVomit : MonoBehaviour
{
    [Header("Referencias (vacías = las busca en el jugador)")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerInput playerInput;
    [Tooltip("Objetos que se ocultan al empezar (WeaponHolder y LeftHandHolder): así ves bien el vómito.")]
    [SerializeField] private GameObject[] hideWhileVomiting;

    [Header("Duración y cámara")]
    [Tooltip("Segundos que dura todo el vómito antes de la animación de muerte.")]
    [Min(0.5f)]
    [SerializeField] private float duration = 2.8f;
    [Tooltip("Grados que mira hacia abajo a lo largo del vómito (0 = inicio, 1 = final).")]
    [SerializeField]
    private AnimationCurve pitchDegrees = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.18f, 8f), new Keyframe(0.22f, 10f), new Keyframe(0.27f, 40f),
        new Keyframe(0.34f, 34f), new Keyframe(0.45f, 18f), new Keyframe(0.52f, 55f), new Keyframe(0.75f, 52f),
        new Keyframe(1f, 48f));
    [Tooltip("Metros que la cabeza se echa hacia delante (las arcadas).")]
    [SerializeField]
    private AnimationCurve lungeMeters = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.21f, 0f), new Keyframe(0.27f, 0.12f), new Keyframe(0.36f, 0.05f),
        new Keyframe(0.46f, 0.03f), new Keyframe(0.52f, 0.18f), new Keyframe(0.75f, 0.15f), new Keyframe(1f, 0.12f));
    [Tooltip("Temblor de la cabeza en grados.")]
    [SerializeField]
    private AnimationCurve shakeDegrees = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.21f, 0f), new Keyframe(0.25f, 2.5f), new Keyframe(0.34f, 0.3f),
        new Keyframe(0.48f, 0f), new Keyframe(0.52f, 4f), new Keyframe(0.75f, 1.2f), new Keyframe(1f, 0.3f));
    [Tooltip("Balanceo de mareo en grados.")]
    [Min(0f)]
    [SerializeField] private float swayDegrees = 4f;

    [Header("Vómito")]
    [Tooltip("Gotas de vómito por segundo a lo largo del vómito (los chorros).")]
    [SerializeField]
    private AnimationCurve spewRate = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.24f, 0f), new Keyframe(0.25f, 250f), new Keyframe(0.3f, 0f),
        new Keyframe(0.505f, 0f), new Keyframe(0.52f, 320f), new Keyframe(0.72f, 220f), new Keyframe(0.78f, 20f),
        new Keyframe(0.95f, 8f), new Keyframe(1f, 0f));
    [Tooltip("Colores del vómito (sale uno al azar por gota o grumo). Verde asqueroso.")]
    [SerializeField]
    private Color[] vomitColors =
    {
        new Color(0.38f, 0.52f, 0.08f), new Color(0.55f, 0.62f, 0.12f), new Color(0.45f, 0.42f, 0.12f), new Color(0.62f, 0.68f, 0.2f)
    };
    [Tooltip("Grosor del chorro (mín, máx), en metros. Las gotas se estiran con la velocidad, así que parece líquido.")]
    [SerializeField] private Vector2 dropSize = new Vector2(0.025f, 0.065f);
    [Tooltip("Velocidad del chorro (mín, máx), en m/s.")]
    [SerializeField] private Vector2 spewSpeed = new Vector2(2.2f, 3.6f);
    [Tooltip("Parte del vómito que son grumos sólidos (el resto es líquido). 0.25 = uno de cada cuatro.")]
    [Range(0f, 1f)]
    [SerializeField] private float chunkShare = 0.25f;
    [Tooltip("Tamaño de los grumos (mín, máx), en metros.")]
    [SerializeField] private Vector2 chunkSize = new Vector2(0.02f, 0.045f);
    [Tooltip("Material del chorro líquido. Vacío = se crea por código (sin luz ni reflejos, así nunca sale azul).")]
    [SerializeField] private Material dropMaterial;
    [Tooltip("Material de los grumos. Vacío = se crea por código (con luz pero mate, sin reflejar el cielo).")]
    [SerializeField] private Material chunkMaterial;
    [Tooltip("Material de la neblina. Vacío = se crea por código.")]
    [SerializeField] private Material mistMaterial;

    [Header("Efecto de partículas (recomendado)")]
    [Tooltip("Prefab con el efecto de vómito hecho en el editor de partículas (p. ej. PF_VomitFX, a partir de las partículas de líquido de PF_BottleBreak). Sale de la boca: su eje Z es hacia dónde sale el chorro. Sus Particle Systems tienen que estar en Looping; este script sube y baja su emisión con Spew Rate. Vacío = partículas por código.")]
    [SerializeField] private GameObject effectPrefab;
    [Tooltip("Pinta las partículas del prefab con Vomit Colors (para poder reutilizar un efecto blanco o de otro color).")]
    [SerializeField] private bool tintEffect = true;

    [Header("Charco")]
    [Tooltip("Diámetro del charco de vómito en el suelo, en metros. 0 = sin charco.")]
    [Min(0f)]
    [SerializeField] private float puddleSize = 0.9f;
    [Tooltip("Material del charco. Vacío = uno transparente creado por código (sin brillo, que es vómito, no una botella).")]
    [SerializeField] private Material puddleMaterial;

    [Header("Pantalla")]
    [Tooltip("Tinte verde de los bordes de la pantalla a lo largo del vómito (0 = nada, 1 = máximo).")]
    [SerializeField]
    private AnimationCurve greenTint = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.25f, 0.35f), new Keyframe(0.36f, 0.12f), new Keyframe(0.52f, 0.5f),
        new Keyframe(1f, 0.25f));
    [SerializeField] private Color tintColor = new Color(0.35f, 0.55f, 0.05f, 1f);

    [Header("Sonido")]
    [Tooltip("Grupo SFX del Audio Mixer (AM_Main).")]
    [SerializeField] private AudioMixerGroup output;
    [Tooltip("Arcadas (SFX_Vomit_Heave_01 y _02): la primera suena en la primera arcada y la segunda en la fuerte.")]
    [SerializeField] private AudioClip[] heaveClips;
    [Tooltip("Chorro y salpicadura (SFX_Vomit_Splash).")]
    [SerializeField] private AudioClip splashClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuándo empieza y acaba, y dibuja la boca y el chorro en la vista Scene.")]
    [SerializeField] private bool showDebug;

    // Momentos de la secuencia (0-1) en los que suenan las cosas y sale el charco.
    private const float FirstHeaveAt = 0.22f, BigHeaveAt = 0.5f, SplashAt = 0.53f, PuddleAt = 0.55f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public bool IsPlaying { get; private set; }

    private Action onFinished;
    private float elapsed;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private ParticleSystem drops, chunks, mist;
    private ParticleSystem[] effectSystems;   // las del prefab (si hay)
    private float[] effectBaseRates;          // su emisión tal cual está en el prefab
    private float spewPeak = 1f;              // el máximo de Spew Rate, para pasar la curva a 0-1 con el prefab
    private Transform mouth;
    private Image tint;
    private AudioSource audioSource;
    private bool firstHeavePlayed, bigHeavePlayed, splashPlayed, puddleSpawned;
    private Transform puddle;
    private float puddleGrow;
    private float noiseSeed;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandleDied;
    }

    /// <summary>Empieza a vomitar. onFinished se llama al acabar (PlayerBlackout te mata ahí).</summary>
    public void Play(Action finished)
    {
        if (IsPlaying) return;
        onFinished = finished;
        IsPlaying = true;
        elapsed = 0f;
        firstHeavePlayed = bigHeavePlayed = splashPlayed = puddleSpawned = false;
        noiseSeed = UnityEngine.Random.value * 100f;

        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;

        if (playerInput != null) playerInput.DeactivateInput();
        if (hideWhileVomiting != null)
            foreach (GameObject hidden in hideWhileVomiting)
                if (hidden != null) hidden.SetActive(false);

        if (mouth == null) Build();
        if (effectSystems != null)
        {
            foreach (ParticleSystem system in effectSystems) system.Play();
        }
        else
        {
            drops.Play();
            chunks.Play();
            mist.Play();
        }

        if (showDebug) Debug.Log("[Vómito] Empieza.", this);
    }

    private void HandleDied()
    {
        // Si te matan a mitad (un enemigo), se corta y la muerte normal sigue desde aquí.
        if (IsPlaying) Stop(false);
    }

    private void LateUpdate()
    {
        if (puddle != null && puddleGrow < 1f)
        {
            puddleGrow = Mathf.Min(1f, puddleGrow + Time.deltaTime / 1.1f);
            float s = puddleSize * (1f - (1f - puddleGrow) * (1f - puddleGrow));
            puddle.localScale = new Vector3(s, 1f, s);
        }

        if (!IsPlaying) return;

        // El menú de pausa vuelve a activar los controles al cerrarse: mientras vomitas, siguen apagados.
        if (playerInput != null && playerInput.inputIsActive) playerInput.DeactivateInput();

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // ---- Cámara ----
        float time = elapsed;
        float shake = shakeDegrees.Evaluate(t);
        float sway = Mathf.Sin(time * 2.1f) * swayDegrees * (1f - t * 0.5f);
        float nx = (Mathf.PerlinNoise(noiseSeed, time * 22f) - 0.5f) * 2f * shake;
        float ny = (Mathf.PerlinNoise(noiseSeed + 10f, time * 22f) - 0.5f) * 2f * shake;
        float nz = (Mathf.PerlinNoise(noiseSeed + 20f, time * 18f) - 0.5f) * 2f * shake;
        float lunge = lungeMeters.Evaluate(t);
        transform.localPosition = startLocalPosition + new Vector3(0f, -lunge * 0.6f, lunge);
        transform.localRotation = startLocalRotation * Quaternion.Euler(pitchDegrees.Evaluate(t) + nx, sway * 0.5f + ny, sway * 0.6f + nz);

        // ---- Vómito ----
        SetSpew(spewRate.Evaluate(t));

        if (tint != null)
        {
            Color c = tintColor;
            c.a = greenTint.Evaluate(t);
            tint.color = c;
        }

        // ---- Sonido y charco ----
        if (!firstHeavePlayed && t >= FirstHeaveAt) { firstHeavePlayed = true; PlayClip(Clip(0)); }
        if (!bigHeavePlayed && t >= BigHeaveAt) { bigHeavePlayed = true; PlayClip(Clip(1)); }
        if (!splashPlayed && t >= SplashAt) { splashPlayed = true; PlayClip(splashClip); }
        if (!puddleSpawned && t >= PuddleAt) { puddleSpawned = true; SpawnPuddle(); }

        if (t >= 1f) Stop(true);
    }

    private void Stop(bool finishedNormally)
    {
        IsPlaying = false;
        if (mouth != null) SetSpew(0f);
        if (showDebug) Debug.Log(finishedNormally ? "[Vómito] Acaba: ahora mueres." : "[Vómito] Cortado: te han matado.", this);

        Action finished = onFinished;
        onFinished = null;
        if (finishedNormally) finished?.Invoke();
    }

    /// <summary>Cuánto vómito sale ahora (gotas por segundo de Spew Rate).</summary>
    private void SetSpew(float rate)
    {
        if (effectSystems != null)
        {
            // Prefab: se respeta su emisión y se escala con la curva (0 = nada, el pico de la curva = lo del prefab).
            float k = Mathf.Clamp01(rate / spewPeak);
            for (int i = 0; i < effectSystems.Length; i++)
            {
                var emission = effectSystems[i].emission;
                emission.rateOverTimeMultiplier = effectBaseRates[i] * k;
            }
            return;
        }
        var dropEmission = drops.emission; dropEmission.rateOverTime = rate;
        var chunkEmission = chunks.emission; chunkEmission.rateOverTime = rate * chunkShare;
        var mistEmission = mist.emission; mistEmission.rateOverTime = rate * 0.35f;
    }

    private AudioClip Clip(int index) =>
        heaveClips != null && heaveClips.Length > 0 ? heaveClips[Mathf.Min(index, heaveClips.Length - 1)] : null;

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.outputAudioMixerGroup = output;
        }
        audioSource.pitch = 1f + UnityEngine.Random.Range(-0.05f, 0.05f);
        audioSource.PlayOneShot(clip, volume);
    }

    // ---------------- CHARCO ----------------

    private void SpawnPuddle()
    {
        if (puddleSize <= 0f) return;

        // Donde cae el chorro: un poco por delante de los pies.
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f) flatForward = transform.parent != null ? transform.parent.forward : Vector3.forward;
        flatForward.Normalize();
        Vector3 origin = transform.position + flatForward * 0.75f;

        Transform player = playerHealth != null ? playerHealth.transform : transform.root;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit floor = default;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(player) || hit.normal.y < 0.5f) continue;
            if (hit.distance < best) { best = hit.distance; floor = hit; found = true; }
        }
        if (!found) return;

        var go = new GameObject("VomitPuddle");
        go.transform.SetPositionAndRotation(floor.point + floor.normal * 0.012f,
            Quaternion.FromToRotation(Vector3.up, floor.normal) * Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        go.transform.localScale = new Vector3(0.01f, 1f, 0.01f);
        go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = puddleMaterial != null ? puddleMaterial : CreateTransparentMaterial("VomitPuddle (auto)", 0.3f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // PuddleVisual (el de los charcos de las botellas) lee este color al empezar y dibuja la mancha irregular.
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, vomitColors.Length > 0 ? vomitColors[0] : Color.green);
        renderer.SetPropertyBlock(block);
        go.AddComponent<PuddleVisual>();

        puddle = go.transform;
        puddleGrow = 0f;
    }

    // ---------------- CREACIÓN ----------------

    private void Build()
    {
        mouth = new GameObject("VomitMouth").transform;
        mouth.SetParent(transform, false);
        mouth.localPosition = new Vector3(0f, -0.12f, 0.2f);
        mouth.localRotation = Quaternion.Euler(20f, 0f, 0f); // un poco hacia abajo

        BuildTint();
        foreach (Keyframe key in spewRate.keys) spewPeak = Mathf.Max(spewPeak, key.value);

        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, mouth);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            effectSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            effectBaseRates = new float[effectSystems.Length];
            for (int i = 0; i < effectSystems.Length; i++)
            {
                ParticleSystem system = effectSystems[i];
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var systemMain = system.main;
                systemMain.loop = true;
                systemMain.playOnAwake = false;
                if (tintEffect) systemMain.startColor = RandomColors();
                effectBaseRates[i] = system.emission.rateOverTimeMultiplier;
            }
            return; // con prefab no se crean las partículas por código
        }

        Material liquid = dropMaterial != null ? dropMaterial : CreateUnlitMaterial("VomitLiquid (auto)", Blob(0.55f));
        Material misty = mistMaterial != null ? mistMaterial : CreateUnlitMaterial("VomitMist (auto)", Blob(0f));

        // ---- Chorro líquido: gotas estiradas con la velocidad (parece un chorro, no bolitas) ----
        drops = CreateSystem("VomitStream", mouth, out ParticleSystemRenderer dropRenderer);
        var main = drops.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spewSpeed.x, spewSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(dropSize.x, dropSize.y);
        main.gravityModifier = 1.1f;
        main.maxParticles = 700;
        main.startColor = RandomColors();
        var shape = drops.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 7f;
        shape.radius = 0.015f;
        var collision = drops.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0f;
        collision.dampen = 1f;
        collision.lifetimeLoss = 1f;          // al tocar el suelo desaparece... y salpica (sub-emisor de abajo)
        collision.radiusScale = 0.4f;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        dropRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        dropRenderer.velocityScale = 0.06f;
        dropRenderer.lengthScale = 1.4f;
        dropRenderer.sharedMaterial = liquid;
        dropRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ---- Salpicadura: cada gota que toca el suelo suelta unas gotitas hacia los lados ----
        ParticleSystem splat = CreateSystem("VomitSplat", drops.transform, out ParticleSystemRenderer splatRenderer);
        var splatMain = splat.main;
        splatMain.loop = false;
        splatMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        splatMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        splatMain.startSize = new ParticleSystem.MinMaxCurve(dropSize.x * 0.4f, dropSize.x);
        splatMain.gravityModifier = 1.4f;
        splatMain.maxParticles = 500;
        var splatEmission = splat.emission;
        splatEmission.rateOverTime = 0f;
        splatEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 4) });
        var splatShape = splat.shape;
        splatShape.shapeType = ParticleSystemShapeType.Sphere;
        splatShape.radius = 0.02f;
        // Hacia arriba en el mundo (la gota choca con el suelo y salpica para arriba), da igual cómo esté la cabeza.
        var splatUp = splat.velocityOverLifetime;
        splatUp.enabled = true;
        splatUp.space = ParticleSystemSimulationSpace.World;
        splatUp.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        splatUp.y = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        splatUp.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        splatRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        splatRenderer.velocityScale = 0.05f;
        splatRenderer.lengthScale = 1.2f;
        splatRenderer.sharedMaterial = liquid;
        splatRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var subEmitters = drops.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(splat, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritColor);

        // ---- Grumos: trozos irregulares con luz (no bolas perfectas) ----
        chunks = CreateSystem("VomitChunks", mouth, out ParticleSystemRenderer chunkRenderer);
        var chunkMain = chunks.main;
        chunkMain.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        chunkMain.startSpeed = new ParticleSystem.MinMaxCurve(spewSpeed.x * 0.85f, spewSpeed.y * 0.9f);
        chunkMain.startSize3D = true;
        chunkMain.startSizeX = new ParticleSystem.MinMaxCurve(chunkSize.x, chunkSize.y);
        chunkMain.startSizeY = new ParticleSystem.MinMaxCurve(chunkSize.x * 0.6f, chunkSize.y * 0.8f);
        chunkMain.startSizeZ = new ParticleSystem.MinMaxCurve(chunkSize.x, chunkSize.y);
        chunkMain.startRotation3D = true;
        chunkMain.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        chunkMain.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        chunkMain.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        chunkMain.gravityModifier = 1.2f;
        chunkMain.maxParticles = 250;
        chunkMain.startColor = RandomColors();
        var chunkShape = chunks.shape;
        chunkShape.shapeType = ParticleSystemShapeType.Cone;
        chunkShape.angle = 10f;
        chunkShape.radius = 0.015f;
        var chunkCollision = chunks.collision;
        chunkCollision.enabled = true;
        chunkCollision.type = ParticleSystemCollisionType.World;
        chunkCollision.mode = ParticleSystemCollisionMode.Collision3D;
        chunkCollision.bounce = 0.1f;
        chunkCollision.dampen = 0.9f;
        chunkCollision.radiusScale = 0.5f;
        chunkCollision.quality = ParticleSystemCollisionQuality.Medium;
        var chunkSizeLife = chunks.sizeOverLifetime;
        chunkSizeLife.enabled = true;
        chunkSizeLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
        chunkRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        chunkRenderer.mesh = LumpMesh();
        chunkRenderer.alignment = ParticleSystemRenderSpace.World;
        chunkRenderer.sharedMaterial = chunkMaterial != null ? chunkMaterial : CreateMatteMaterial();

        // ---- Neblina fina alrededor del chorro ----
        mist = CreateSystem("VomitMist", mouth, out ParticleSystemRenderer mistRenderer);
        var mistMain = mist.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        mistMain.gravityModifier = 0.5f;
        mistMain.maxParticles = 300;
        Color mistColor = vomitColors.Length > 1 ? vomitColors[1] : new Color(0.6f, 0.7f, 0.2f);
        mistColor.a = 0.4f;
        mistMain.startColor = mistColor;
        var mistShape = mist.shape;
        mistShape.shapeType = ParticleSystemShapeType.Cone;
        mistShape.angle = 16f;
        mistShape.radius = 0.02f;
        var fadeOut = mist.colorOverLifetime;
        fadeOut.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fadeOut.color = gradient;
        mistRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        mistRenderer.sharedMaterial = misty;
        mistRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static ParticleSystem CreateSystem(string systemName, Transform parent, out ParticleSystemRenderer renderer)
    {
        var go = new GameObject(systemName);
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // el chorro cae aunque la cabeza se mueva
        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        renderer = go.GetComponent<ParticleSystemRenderer>();
        return system;
    }

    private ParticleSystem.MinMaxGradient RandomColors()
    {
        var gradient = new Gradient();
        int count = Mathf.Clamp(vomitColors.Length, 1, 8);
        var keys = new GradientColorKey[count];
        for (int i = 0; i < count; i++)
            keys[i] = new GradientColorKey(vomitColors.Length > 0 ? vomitColors[i] : Color.green, (i + 1) / (float)count);
        gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        gradient.mode = GradientMode.Fixed;
        return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
    }

    private void BuildTint()
    {
        var cam = GetComponent<Camera>();
        var canvasObject = new GameObject("VomitTint", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.03f;
        canvas.sortingOrder = 90; // por debajo de los párpados de la muerte (100)

        var imageObject = new GameObject("Tint", typeof(RectTransform), typeof(Image));
        imageObject.layer = gameObject.layer;
        var rect = (RectTransform)imageObject.transform;
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        tint = imageObject.GetComponent<Image>();
        tint.raycastTarget = false;
        tint.sprite = Sprite.Create(EdgeTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        tint.color = new Color(tintColor.r, tintColor.g, tintColor.b, 0f);
    }

    /// <summary>Textura transparente en el centro y opaca hacia los bordes (viñeta).</summary>
    private static Texture2D EdgeTexture()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.75f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(d * d * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // ---------------- MATERIALES DE RESPALDO ----------------

    /// <summary>Grumo irregular: esfera de pocos polígonos con los vértices desplazados al azar.</summary>
    private static Mesh LumpMesh()
    {
        // Icosaedro subdividido una vez (42 vértices) y "abollado".
        float p = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new System.Collections.Generic.List<Vector3>
        {
            new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
            new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
            new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
        };
        int[] faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        var mid = new System.Collections.Generic.Dictionary<long, int>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < faces.Length; i += 3)
        {
            int a = faces[i], b = faces[i + 1], c = faces[i + 2];
            int ab = Midpoint(verts, mid, a, b), bc = Midpoint(verts, mid, b, c), ca = Midpoint(verts, mid, c, a);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        var rng = new System.Random(7);
        for (int i = 0; i < verts.Count; i++)
            verts[i] = verts[i].normalized * (0.5f * (0.72f + 0.5f * (float)rng.NextDouble()));
        var mesh = new Mesh { name = "VomitLump" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Vértice a mitad de la arista a-b (lo reutiliza si otra cara ya lo creó).</summary>
    private static int Midpoint(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.Dictionary<long, int> mid, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (mid.TryGetValue(key, out int index)) return index;
        verts.Add((verts[a] + verts[b]) * 0.5f);
        mid[key] = verts.Count - 1;
        return verts.Count - 1;
    }

    /// <summary>Sin luz ni reflejos: el color sale tal cual (el verde de cada gota). Transparente con borde suave.</summary>
    private static Material CreateUnlitMaterial(string materialName, Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) { Debug.LogWarning("[Vómito] No encuentro URP/Particles/Unlit: asigna los materiales."); return null; }
        var material = new Material(shader) { name = materialName };
        MakeTransparent(material);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", texture);
        return material;
    }

    /// <summary>Con luz pero mate y sin reflejos del entorno (los reflejos del cielo eran lo que lo ponía azul).</summary>
    private static Material CreateMatteMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) { Debug.LogWarning("[Vómito] No encuentro URP/Particles/Lit: asigna Chunk Material."); return null; }
        var material = new Material(shader) { name = "VomitChunks (auto)" };
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_EnvironmentReflections", 0f);
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        return material;
    }

    /// <summary>Transparente con mezcla normal (alpha), para el charco.</summary>
    private static Material CreateTransparentMaterial(string materialName, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) { Debug.LogWarning("[Vómito] No encuentro URP/Particles/Lit: asigna Puddle Material."); return null; }
        var material = new Material(shader) { name = materialName };
        MakeTransparent(material);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_EnvironmentReflections", 0f);
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        return material;
    }

    private static void MakeTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    /// <summary>Gota redonda: core = parte central totalmente opaca (0 = todo difuminado, para la neblina).</summary>
    private static Texture2D Blob(float core)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d <= core ? 1f : Mathf.Clamp01(1f - (d - core) / Mathf.Max(0.01f, 1f - core));
                a = core > 0f ? a * a * (3f - 2f * a) : a * a;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        Vector3 m = transform.TransformPoint(new Vector3(0f, -0.12f, 0.2f));
        Vector3 dir = transform.rotation * Quaternion.Euler(20f, 0f, 0f) * Vector3.forward;
        Gizmos.color = new Color(0.5f, 0.75f, 0.1f);
        Gizmos.DrawWireSphere(m, 0.03f);
        Gizmos.DrawLine(m, m + dir * 0.6f);
        if (Application.isPlaying && IsPlaying)
            DebugLabels.Above(transform, 0, $"Vomitando · {elapsed:0.0}/{duration:0.0} s", DebugLabels.Health);
    }
#endif
}
