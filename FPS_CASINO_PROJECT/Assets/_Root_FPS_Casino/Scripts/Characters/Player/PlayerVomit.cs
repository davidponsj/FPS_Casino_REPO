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
    [Tooltip("Colores del vómito (sale uno al azar por gota). Verde asqueroso.")]
    [SerializeField]
    private Color[] vomitColors =
    {
        new Color(0.42f, 0.58f, 0.1f), new Color(0.58f, 0.66f, 0.14f), new Color(0.3f, 0.42f, 0.06f), new Color(0.66f, 0.62f, 0.2f)
    };
    [Tooltip("Tamaño de las gotas (mín, máx), en metros.")]
    [SerializeField] private Vector2 dropSize = new Vector2(0.025f, 0.065f);
    [Tooltip("Velocidad del chorro (mín, máx), en m/s.")]
    [SerializeField] private Vector2 spewSpeed = new Vector2(2.2f, 3.6f);
    [Tooltip("Material de las gotas. Vacío = verde húmedo (URP Particles/Lit) creado por código.")]
    [SerializeField] private Material dropMaterial;
    [Tooltip("Material de la neblina. Vacío = se crea por código.")]
    [SerializeField] private Material mistMaterial;

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
    private ParticleSystem drops, mist;
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

        if (drops == null) Build();
        drops.Play();
        mist.Play();

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
        var emission = drops.emission;
        float rate = spewRate.Evaluate(t);
        emission.rateOverTime = rate;
        var mistEmission = mist.emission;
        mistEmission.rateOverTime = rate * 0.6f;

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
        if (drops != null)
        {
            var emission = drops.emission; emission.rateOverTime = 0f;
            var mistEmission = mist.emission; mistEmission.rateOverTime = 0f;
        }
        if (showDebug) Debug.Log(finishedNormally ? "[Vómito] Acaba: ahora mueres." : "[Vómito] Cortado: te han matado.", this);

        Action finished = onFinished;
        onFinished = null;
        if (finishedNormally) finished?.Invoke();
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
        renderer.sharedMaterial = puddleMaterial != null ? puddleMaterial : CreateTransparentMaterial("VomitPuddle (auto)", 0.7f);
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

        drops = CreateSystem("VomitDrops", out ParticleSystemRenderer dropRenderer);
        var main = drops.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spewSpeed.x, spewSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(dropSize.x, dropSize.y);
        main.gravityModifier = 1.1f;
        main.maxParticles = 700;
        main.startColor = RandomColors();
        var shape = drops.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.02f;
        // Se aplastan contra el suelo y desaparecen poco después.
        var collision = drops.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.05f;
        collision.dampen = 0.85f;
        collision.lifetimeLoss = 0.5f;
        collision.radiusScale = 0.5f;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        var size = drops.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
        dropRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        dropRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        dropRenderer.alignment = ParticleSystemRenderSpace.World;
        dropRenderer.sharedMaterial = dropMaterial != null ? dropMaterial : CreateDropMaterial();

        mist = CreateSystem("VomitMist", out ParticleSystemRenderer mistRenderer);
        var mistMain = mist.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        mistMain.gravityModifier = 0.4f;
        mistMain.maxParticles = 400;
        Color mistColor = vomitColors.Length > 1 ? vomitColors[1] : new Color(0.6f, 0.7f, 0.2f);
        mistColor.a = 0.55f;
        mistMain.startColor = mistColor;
        var mistShape = mist.shape;
        mistShape.shapeType = ParticleSystemShapeType.Cone;
        mistShape.angle = 18f;
        mistShape.radius = 0.03f;
        var fadeOut = mist.colorOverLifetime;
        fadeOut.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fadeOut.color = gradient;
        mistRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        mistRenderer.sharedMaterial = mistMaterial != null ? mistMaterial : CreateTransparentMaterial("VomitMist (auto)", 1f, true);
        mistRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        BuildTint();
    }

    private ParticleSystem CreateSystem(string systemName, out ParticleSystemRenderer renderer)
    {
        var go = new GameObject(systemName);
        go.transform.SetParent(mouth, false);
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

    private static Material CreateDropMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) { Debug.LogWarning("[Vómito] No encuentro URP/Particles/Lit: asigna Drop Material."); return null; }
        var material = new Material(shader) { name = "VomitDrops (auto)" };
        material.SetColor("_BaseColor", Color.white); // el color sale de cada gota
        material.SetFloat("_Smoothness", 0.85f);       // húmedo y brillante
        material.SetFloat("_Metallic", 0f);
        return material;
    }

    /// <summary>Transparente con mezcla normal (alpha). softDot = textura de punto suave para la neblina.</summary>
    private static Material CreateTransparentMaterial(string materialName, float smoothness, bool softDot = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) { Debug.LogWarning("[Vómito] No encuentro URP/Particles/Lit: asigna los materiales."); return null; }
        var material = new Material(shader) { name = materialName };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Smoothness", smoothness);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (softDot) material.SetTexture("_BaseMap", SoftDot());
        return material;
    }

    private static Texture2D SoftDot()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
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
