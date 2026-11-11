using System.Collections;
using UnityEngine;

/// <summary>
/// Partículas de la tragaperras al ganar, a la vez que el squash &amp; stretch (SlotWinSquash). Escucha el evento onWin de
/// SlotMachine, así que no hay que conectar nada: se pone en la máquina y listo.
///
/// Escala con el premio (el multiplicador del símbolo). Con Auto Tiers (por defecto) lo decide solo con los símbolos de
/// la máquina, así que si se cambian los multiplicadores se ajusta sin tocar nada:
/// - Jackpot: el símbolo que más paga (el más difícil). Fuente de monedas durante unos segundos, lluvia de confeti y
///   la luz dorada parpadeando.
/// - Premio gordo: los que pagan al menos Big Win Fraction del jackpot. Más monedas, destellos y confeti.
/// - Premio normal: el resto. Ráfaga de monedas de oro que saltan, giran y rebotan, destellos y un fogonazo de luz.
///
/// Las partículas se crean por código al empezar (no hay que montar Particle Systems a mano). Los materiales son
/// opcionales: si se dejan vacíos se crean solos (monedas URP Lit doradas, destellos aditivos, confeti de dos caras).
/// </summary>
[RequireComponent(typeof(SlotMachine))]
public class SlotWinParticles : MonoBehaviour
{
    private enum Tier { Normal, Big, Jackpot }
    public enum FrontMode { Auto, Forward, Back, Right, Left }

    [Header("Dónde salen")]
    [Tooltip("Punto del que salen (p. ej. la bandeja o la pantalla de la máquina). Vacío = 1.2 m por encima de la máquina y un poco hacia delante.")]
    [SerializeField] private Transform emitPoint;
    [Tooltip("Hacia dónde mira la máquina (hacia el jugador). Auto = lo saca de la marca \"de frente\" de la tragaperras (la del primer símbolo). Si aun así salen torcidas, elige a mano el eje del objeto que apunta al jugador.")]
    [SerializeField] private FrontMode front = FrontMode.Auto;

    [Header("Premios")]
    [Tooltip("Marcado = jackpot es el símbolo que más paga de la máquina y gordo lo que pague al menos Big Win Fraction de él. Desmarcado = se usan los dos multiplicadores de abajo.")]
    [SerializeField] private bool autoTiers = true;
    [Tooltip("Con Auto Tiers: parte del multiplicador del jackpot a partir de la que es premio gordo. 0.5 = la mitad o más.")]
    [Range(0f, 1f)]
    [SerializeField] private float bigWinFraction = 0.5f;
    [Tooltip("Sin Auto Tiers: a partir de este multiplicador es premio gordo (sale confeti).")]
    [Min(0f)]
    [SerializeField] private float bigWinMultiplier = 10f;
    [Tooltip("Sin Auto Tiers: a partir de este multiplicador es jackpot (fuente de monedas y lluvia de confeti).")]
    [Min(0f)]
    [SerializeField] private float jackpotMultiplier = 50f;
    [Tooltip("Segundos de espera antes de soltarlo todo, para que vaya a la vez que el rebote de la máquina.")]
    [Min(0f)]
    [SerializeField] private float delay = 0.05f;

    [Header("Monedas")]
    [Tooltip("Monedas de la ráfaga (normal, gordo, jackpot).")]
    [SerializeField] private Vector3Int coins = new Vector3Int(18, 45, 40);
    [Tooltip("Jackpot: monedas por segundo de la fuente y segundos que dura.")]
    [SerializeField] private Vector2 jackpotFountain = new Vector2(60f, 2.5f);
    [Tooltip("Diámetro de las monedas en metros.")]
    [Min(0.01f)]
    [SerializeField] private float coinSize = 0.1f;
    [Tooltip("Velocidad de salida de las monedas (mín, máx), en m/s.")]
    [SerializeField] private Vector2 coinSpeed = new Vector2(3.5f, 6f);
    [Tooltip("Material de las monedas. Vacío = dorado metálico (URP Lit) creado por código.")]
    [SerializeField] private Material coinMaterial;

    [Header("Destellos y confeti")]
    [Tooltip("Destellos de la ráfaga (normal, gordo, jackpot).")]
    [SerializeField] private Vector3Int sparkles = new Vector3Int(30, 60, 120);
    [Tooltip("Confeti (normal, gordo, jackpot). 0 = nada.")]
    [SerializeField] private Vector3Int confetti = new Vector3Int(0, 60, 220);
    [Tooltip("Colores del confeti (sale uno al azar por pieza).")]
    [SerializeField]
    private Color[] confettiColors =
    {
        new Color(0.95f, 0.78f, 0.3f), new Color(0.85f, 0.15f, 0.2f), new Color(0.55f, 0.2f, 0.75f),
        new Color(1f, 1f, 1f), new Color(0.2f, 0.75f, 0.45f)
    };
    [Tooltip("Material de los destellos. Vacío = brillo aditivo creado por código.")]
    [SerializeField] private Material sparkleMaterial;
    [Tooltip("Material del confeti. Vacío = sin luz y de dos caras, creado por código.")]
    [SerializeField] private Material confettiMaterial;

    [Header("Luz")]
    [Tooltip("Fogonazo de luz dorada al ganar. 0 = sin luz.")]
    [Min(0f)]
    [SerializeField] private float lightIntensity = 6f;
    [Tooltip("Alcance de la luz en metros.")]
    [Min(0.1f)]
    [SerializeField] private float lightRange = 5f;
    [SerializeField] private Color lightColor = new Color(1f, 0.8f, 0.4f);

    [Header("Depuración")]
    [Tooltip("Escribe en la consola el tipo de premio y dibuja el punto de salida en la vista Scene.")]
    [SerializeField] private bool showDebug;

    private SlotMachine machine;
    private ParticleSystem coinSystem, sparkleSystem, confettiSystem;
    private Light flash;
    private Coroutine routine;
    private string lastTier = "-";

    private Vector3 frontCache;
    private bool frontCached;

    /// <summary>Hacia el jugador, en horizontal. Se calcula una vez al empezar (con los cilindros en reposo).</summary>
    private Vector3 Front
    {
        get
        {
            if (frontCached && Application.isPlaying) return frontCache;
            Vector3 dir;
            switch (front)
            {
                case FrontMode.Forward: dir = transform.forward; break;
                case FrontMode.Back: dir = -transform.forward; break;
                case FrontMode.Right: dir = transform.right; break;
                case FrontMode.Left: dir = -transform.right; break;
                default:
                    SlotMachine slot = machine != null ? machine : GetComponent<SlotMachine>();
                    dir = slot != null ? slot.FacingDirection : transform.forward;
                    break;
            }
            dir = Vector3.ProjectOnPlane(dir, Vector3.up);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            if (Application.isPlaying) { frontCache = dir; frontCached = true; }
            return dir;
        }
    }

    private void Awake()
    {
        machine = GetComponent<SlotMachine>();

        Transform root = new GameObject("WinParticles").transform;
        root.SetParent(emitPoint != null ? emitPoint : transform, false);
        if (emitPoint == null) root.position = transform.position + Vector3.up * 1.2f + Front * 0.35f;
        // Las partículas salen hacia arriba y hacia el jugador.
        root.rotation = Quaternion.LookRotation(Vector3.up, Front);

        coinSystem = CreateCoins(root);
        sparkleSystem = CreateSparkles(root);
        confettiSystem = CreateConfetti(root);

        if (lightIntensity > 0f)
        {
            flash = new GameObject("WinLight").AddComponent<Light>();
            flash.transform.SetParent(root, false);
            flash.transform.position = root.position + Front * 0.5f;
            flash.type = LightType.Point;
            flash.color = lightColor;
            flash.range = lightRange;
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;
        }
    }

    private void OnEnable() => GetComponent<SlotMachine>().onWin.AddListener(HandleWin);
    private void OnDisable() => GetComponent<SlotMachine>().onWin.RemoveListener(HandleWin);

    private void HandleWin(int points)
    {
        float multiplier = machine.LastWinMultiplier;
        float jackpotAt = autoTiers ? machine.HighestMultiplier : jackpotMultiplier;
        float bigAt = autoTiers ? machine.HighestMultiplier * bigWinFraction : bigWinMultiplier;
        Tier tier = multiplier >= jackpotAt ? Tier.Jackpot : multiplier >= bigAt ? Tier.Big : Tier.Normal;
        lastTier = $"{tier} (x{multiplier}, {points} pts)";
        if (showDebug) Debug.Log($"[Tragaperras] Premio {lastTier}.", this);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Celebrate(tier));
    }

    private IEnumerator Celebrate(Tier tier)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        int t = (int)tier;
        Burst(coinSystem, Pick(coins, t));
        Burst(sparkleSystem, Pick(sparkles, t));
        Burst(confettiSystem, Pick(confetti, t));

        float lightSeconds = tier == Tier.Jackpot ? jackpotFountain.y : tier == Tier.Big ? 1f : 0.6f;
        if (flash != null) StartCoroutine(Flash(lightSeconds, tier == Tier.Jackpot));

        if (tier != Tier.Jackpot) yield break;

        // Jackpot: fuente de monedas y destellos mientras dura, más confeti a mitad.
        float elapsed = 0f, coinDebt = 0f;
        bool secondConfetti = false;
        while (elapsed < jackpotFountain.y)
        {
            coinDebt += jackpotFountain.x * Time.deltaTime;
            int n = Mathf.FloorToInt(coinDebt);
            if (n > 0)
            {
                Burst(coinSystem, n);
                Burst(sparkleSystem, n);
                coinDebt -= n;
            }
            if (!secondConfetti && elapsed > jackpotFountain.y * 0.5f)
            {
                Burst(confettiSystem, Pick(confetti, t) / 2);
                secondConfetti = true;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Flash(float seconds, bool blink)
    {
        flash.enabled = true;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            float fade = 1f - k * k;
            float pulse = blink ? 0.55f + 0.45f * Mathf.Cos(t * Mathf.PI * 2f * 4f) : 1f;
            flash.intensity = lightIntensity * fade * pulse;
            yield return null;
        }
        flash.intensity = 0f;
        flash.enabled = false;
    }

    /// <summary>Suelta n partículas de golpe (el sistema tiene que estar en marcha para que se muevan).</summary>
    private static void Burst(ParticleSystem system, int n)
    {
        if (n <= 0) return;
        if (!system.isPlaying) system.Play();
        system.Emit(n);
    }

    private static int Pick(Vector3Int values, int tier) => tier == 0 ? values.x : tier == 1 ? values.y : values.z;

    // ---------------- CREACIÓN ----------------

    private static ParticleSystem NewSystem(string systemName, Transform parent, out ParticleSystemRenderer renderer)
    {
        var go = new GameObject(systemName);
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission;
        emission.enabled = false; // todo sale con Emit()
        renderer = go.GetComponent<ParticleSystemRenderer>();
        return system;
    }

    private ParticleSystem CreateCoins(Transform parent)
    {
        ParticleSystem system = NewSystem("Coins", parent, out ParticleSystemRenderer renderer);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(coinSpeed.x, coinSpeed.y);
        main.startSize3D = true;
        // Cilindro de Unity: 1 de diámetro y 2 de alto → moneda de coinSize de ancho y un 8% de grosor.
        main.startSizeX = coinSize;
        main.startSizeY = coinSize * 0.04f;
        main.startSizeZ = coinSize;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.6f;
        main.maxParticles = 500;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.15f;
        // El sistema mira hacia arriba; se inclina 25° hacia el jugador (X negativa) para que las monedas caigan delante.
        shape.rotation = new Vector3(-25f, 0f, 0f);

        // Dan vueltas en el aire.
        var spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-12f, 12f);
        spin.y = new ParticleSystem.MinMaxCurve(-12f, 12f);
        spin.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

        // Rebotan en el suelo y en lo que pillen.
        var collision = system.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.35f;
        collision.dampen = 0.45f;
        collision.radiusScale = 0.5f;
        collision.quality = ParticleSystemCollisionQuality.Medium;

        // Se encogen al final en vez de desaparecer de golpe.
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = coinMaterial != null ? coinMaterial : CreateCoinMaterial();
        return system;
    }

    private ParticleSystem CreateSparkles(Transform parent)
    {
        ParticleSystem system = NewSystem("Sparkles", parent, out ParticleSystemRenderer renderer);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), Color.white);
        main.gravityModifier = 0.35f;
        main.maxParticles = 600;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50f;
        shape.radius = 0.25f;
        shape.rotation = new Vector3(-20f, 0f, 0f);

        // Parpadean y se apagan.
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.1f, 1f), new Keyframe(0.35f, 0.5f), new Keyframe(0.55f, 1f), new Keyframe(1f, 0f)));

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = sparkleMaterial != null ? sparkleMaterial : CreateParticleMaterial(true);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return system;
    }

    private ParticleSystem CreateConfetti(Transform parent)
    {
        ParticleSystem system = NewSystem("Confetti", parent, out ParticleSystemRenderer renderer);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
        main.startSizeZ = 1f;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.25f;
        main.maxParticles = 800;

        // Un color al azar de la lista por pieza.
        var gradient = new Gradient();
        int count = Mathf.Clamp(confettiColors.Length, 1, 8);
        var keys = new GradientColorKey[count];
        for (int i = 0; i < count; i++)
            keys[i] = new GradientColorKey(confettiColors.Length > 0 ? confettiColors[i] : Color.white, (i + 1) / (float)count);
        gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        gradient.mode = GradientMode.Fixed; // en Fixed cada color ocupa su tramo: todos salen igual de a menudo
        main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.3f;
        shape.rotation = new Vector3(-15f, 0f, 0f);

        // Revolotean al caer (frenados por el aire y con ruido).
        var drag = system.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 1.6f;
        drag.multiplyDragByParticleSize = false;
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.6f;
        var spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
        spin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
        spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f)));

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = confettiMaterial != null ? confettiMaterial : CreateParticleMaterial(false);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return system;
    }

    // ---------------- MATERIALES DE RESPALDO ----------------

    private static Material CreateCoinMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[Tragaperras] No encuentro URP/Lit: asigna Coin Material."); return null; }
        var material = new Material(shader) { name = "SlotCoin (auto)" };
        material.SetColor("_BaseColor", new Color(1f, 0.76f, 0.28f));
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 0.78f);
        material.enableInstancing = true;
        return material;
    }

    /// <summary>additive = destellos (brillo que suma luz, con textura de punto suave); si no, confeti opaco de dos caras.</summary>
    private static Material CreateParticleMaterial(bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) { Debug.LogWarning("[Tragaperras] No encuentro URP/Particles/Unlit: asigna los materiales de destellos y confeti."); return null; }
        var material = new Material(shader) { name = additive ? "SlotSparkle (auto)" : "SlotConfetti (auto)" };
        material.SetFloat("_Cull", 0f); // dos caras
        if (!additive) return material;

        material.SetTexture("_BaseMap", SoftDot());
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f); // aditivo
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    /// <summary>Punto suave blanco (centro brillante que se difumina) para los destellos.</summary>
    private static Texture2D SoftDot()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftDot (auto)", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // núcleo brillante + halo, con una cruz fina de destello
                float core = Mathf.Clamp01(1f - d);
                float star = Mathf.Clamp01(1f - Mathf.Abs(dx) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.4f)
                           + Mathf.Clamp01(1f - Mathf.Abs(dy) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.4f);
                float a = Mathf.Clamp01(core * core * core + star * 0.6f);
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
        Vector3 p = emitPoint != null ? emitPoint.position : transform.position + Vector3.up * 1.2f + Front * 0.35f;
        Gizmos.color = new Color(1f, 0.8f, 0.3f);
        Gizmos.DrawWireSphere(p, 0.15f);
        Gizmos.DrawLine(p, p + (Vector3.up + Front * 0.45f).normalized * 0.8f);
        DebugLabels.At(p + Vector3.up * 0.3f, 0, $"Premio · último: {lastTier}", DebugLabels.Casino);
    }
#endif
}
