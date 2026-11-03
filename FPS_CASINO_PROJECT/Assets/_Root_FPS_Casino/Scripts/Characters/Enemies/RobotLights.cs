using UnityEngine;

/// <summary>
/// Luces emisivas del robot de seguridad: el pinganillo parpadea seco como una sirena (cada robot a su ritmo)
/// y la boca brilla con el volumen de la voz. Solo cambia la intensidad de la emisión con un MaterialPropertyBlock
/// por hueco de material, así que no crea materiales y el color sigue siendo el de la textura.
///
/// Al morir se apagan solas: EnemyHealth apaga todos los Behaviour de la raíz y aquí OnDisable pone las luces a 0.
/// </summary>
public class RobotLights : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const int SampleCount = 256;
    private const float MinChange = 0.001f; // por debajo no merece la pena volver a mandar el PropertyBlock

    [Header("Referencias")]
    [Tooltip("Renderer del robot (el SkinnedMeshRenderer del modelo). Vacío = el primero que encuentre en los hijos.")]
    [SerializeField] private Renderer bodyRenderer;
    [Tooltip("AudioSource de la voz del robot. La boca brilla con su volumen. Vacío = la boca no se enciende.")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Pinganillo")]
    [Tooltip("Hueco del material del pinganillo en el renderer. 1 = M_RobotPinganillo.")]
    [Min(0)]
    [SerializeField] private int pinganilloMaterialIndex = 1;
    [Tooltip("Segundos encendido en cada parpadeo. 0.4 = destello de sirena.")]
    [Min(0.01f)]
    [SerializeField] private float blinkOnSeconds = 0.4f;
    [Tooltip("Segundos apagado entre parpadeos. 0.6 = un parpadeo por segundo.")]
    [Min(0.01f)]
    [SerializeField] private float blinkOffSeconds = 0.6f;
    [Tooltip("Brillo encendido respecto al material. 1 = como el material, 3 = el triple.")]
    [Min(0f)]
    [SerializeField] private float pinganilloIntensity = 1f;

    [Header("Boca")]
    [Tooltip("Hueco del material de la boca en el renderer. 2 = M_RobotBoca.")]
    [Min(0)]
    [SerializeField] private int mouthMaterialIndex = 2;
    [Tooltip("Brillo de la boca cuando habla fuerte, respecto al material. 1.5 = algo más que el material.")]
    [Min(0f)]
    [SerializeField] private float mouthMaxIntensity = 1.5f;
    [Tooltip("Cuánto se enciende con poco volumen. Alto = brilla con voz baja. 8 = una voz normal la enciende entera.")]
    [Min(0f)]
    [SerializeField] private float mouthSensitivity = 8f;
    [Tooltip("Rapidez con la que sigue a la voz. Alto = nervioso, bajo = suave. 20 = sigue las sílabas.")]
    [Min(0.1f)]
    [SerializeField] private float mouthSmoothing = 20f;

    [Header("Depuración")]
    [Tooltip("Muestra sobre el robot el brillo del pinganillo y de la boca, y si está hablando.")]
    [SerializeField] private bool showDebug;

    private MaterialPropertyBlock block;
    private readonly float[] samples = new float[SampleCount];
    private Color pinganilloBaseColor;
    private Color mouthBaseColor;
    private float blinkOffset;
    private float mouthIntensity;
    private float appliedPinganillo = -1f; // -1 = aún sin mandar, para que el primer frame siempre lo aplique
    private float appliedMouth = -1f;
    private bool speaking;

    private void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        if (bodyRenderer == null)
        {
            Debug.LogWarning($"{name}: RobotLights no encuentra el Renderer del robot. Luces desactivadas.", this);
            enabled = false;
            return;
        }

        Material[] materials = bodyRenderer.sharedMaterials;
        if (!HasMaterial(materials, pinganilloMaterialIndex) || !HasMaterial(materials, mouthMaterialIndex))
        {
            Debug.LogWarning($"{name}: RobotLights no encuentra los huecos de material {pinganilloMaterialIndex} (pinganillo) y {mouthMaterialIndex} (boca) en {bodyRenderer.name}. Luces desactivadas.", this);
            enabled = false;
            return;
        }

        block = new MaterialPropertyBlock();
        pinganilloBaseColor = materials[pinganilloMaterialIndex].GetColor(EmissionColorId);
        mouthBaseColor = materials[mouthMaterialIndex].GetColor(EmissionColorId);
        blinkOffset = Random.value * (blinkOnSeconds + blinkOffSeconds); // desincroniza a los robots
    }

    private void Update()
    {
        float blink = IsBlinkOn(Time.time, blinkOnSeconds, blinkOffSeconds, blinkOffset) ? pinganilloIntensity : 0f;
        SetEmission(pinganilloMaterialIndex, pinganilloBaseColor, blink, ref appliedPinganillo);

        speaking = voiceSource != null && voiceSource.isPlaying;
        float target = 0f;
        if (speaking)
        {
            voiceSource.GetOutputData(samples, 0);
            target = Mathf.Clamp01(Loudness(samples, SampleCount) * mouthSensitivity) * mouthMaxIntensity;
        }

        mouthIntensity = Mathf.Lerp(mouthIntensity, target, 1f - Mathf.Exp(-mouthSmoothing * Time.deltaTime));
        SetEmission(mouthMaterialIndex, mouthBaseColor, mouthIntensity, ref appliedMouth);
    }

    private void OnDisable()
    {
        if (block == null || bodyRenderer == null) return; // sin Awake válido, o el renderer ya destruido

        mouthIntensity = 0f;
        SetEmission(pinganilloMaterialIndex, pinganilloBaseColor, 0f, ref appliedPinganillo);
        SetEmission(mouthMaterialIndex, mouthBaseColor, 0f, ref appliedMouth);
    }

    /// <summary>¿Está encendido el pinganillo en este momento? Ciclo = encendido onSeconds y luego apagado offSeconds.</summary>
    public static bool IsBlinkOn(float time, float onSeconds, float offSeconds, float offset) =>
        Mathf.Repeat(time + offset, onSeconds + offSeconds) < onSeconds;

    /// <summary>Volumen (RMS) de las primeras "count" muestras. 0 = silencio, 1 = a tope.</summary>
    public static float Loudness(float[] samples, int count)
    {
        if (count <= 0) return 0f;

        float sum = 0f;
        for (int i = 0; i < count; i++) sum += samples[i] * samples[i];
        return Mathf.Sqrt(sum / count);
    }

    private static bool HasMaterial(Material[] materials, int index) =>
        index < materials.Length && materials[index] != null;

    /// <summary>Pone la emisión de un hueco a "intensidad × color del material", solo si ha cambiado.</summary>
    private void SetEmission(int materialIndex, Color baseColor, float intensity, ref float applied)
    {
        if (Mathf.Abs(intensity - applied) < MinChange) return;

        applied = intensity;
        bodyRenderer.GetPropertyBlock(block, materialIndex);
        block.SetColor(EmissionColorId, baseColor * intensity);
        bodyRenderer.SetPropertyBlock(block, materialIndex);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string blinkText = appliedPinganillo > 0f ? "on" : "off";
        string voiceText = speaking ? "hablando" : "callado";
        DebugLabels.Above(transform, DebugLabels.RobotLightsLine,
            $"Luces · pinganillo {blinkText} · boca {mouthIntensity:0.00} · {voiceText}", DebugLabels.Casino);
    }
#endif
}
