using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hitmarker de neón: 4 rayas en diagonal alrededor del centro de la pantalla que saltan al dar a un enemigo
/// (aparecen grandes, se encogen de golpe y se desvanecen abriéndose un poco). Al matar salen más grandes,
/// de otro color y duran más, como en CoD Zombies.
///
/// Se entera de los impactos por WeaponBase.OnAnyEnemyHit, así que vale para todas las armas sin asignarlas.
/// Los perdigones de un escopetazo llegan en el mismo frame: cuentan como un solo impacto, y si alguno mata, sale el de baja.
///
/// Las rayas las crea el propio script al empezar (no hay que montarlas a mano). Cada una es un núcleo fino y claro
/// con un halo más ancho y transparente detrás: eso es lo que le da el aspecto de neón.
///
/// Va en un objeto vacío del Canvas, anclado al centro de la pantalla y colocado el último (para que se pinte encima).
/// Usa el tiempo real (no le afecta Time.timeScale), así sigue funcionando con cámara lenta o una micro‑pausa al matar.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class HitMarkerHUD : MonoBehaviour
{
    private const int BarCount = 4;

    [Header("Forma (en unidades del Canvas: 1920x1080 de referencia)")]
    [Tooltip("Distancia del centro al principio de cada raya. 18 = deja libre el centro de la retícula.")]
    [Min(0f)]
    [SerializeField] private float gap = 18f;
    [Tooltip("Largo de cada raya. 16 = se ve bien sin tapar.")]
    [Min(1f)]
    [SerializeField] private float length = 16f;
    [Tooltip("Grosor del núcleo de cada raya. 3 = fino y nítido.")]
    [Min(1f)]
    [SerializeField] private float thickness = 3f;
    [Tooltip("Grosor del halo respecto al núcleo. 3 = el halo es el triple de ancho que el núcleo.")]
    [Min(1f)]
    [SerializeField] private float glowThicknessMultiplier = 3f;
    [Tooltip("Sprite del halo (opcional). Uno difuminado queda más suave. Vacío = rectángulo transparente.")]
    [SerializeField] private Sprite glowSprite;

    [Header("Al dar")]
    [Tooltip("Color del núcleo de las rayas al dar.")]
    [SerializeField] private Color hitCoreColor = Color.white;
    [Tooltip("Color del halo al dar. La transparencia (A) es la fuerza del halo: 0.45 = brillo suave.")]
    [SerializeField] private Color hitGlowColor = new Color(0.2f, 0.9f, 1f, 0.45f);
    [Tooltip("Segundos que se queda entero antes de empezar a desvanecerse. 0.05 = un golpe seco.")]
    [Min(0f)]
    [SerializeField] private float hitHoldSeconds = 0.05f;
    [Tooltip("Segundos que tarda en desvanecerse. 0.18 = rápido, para no tapar con el subfusil.")]
    [Min(0f)]
    [SerializeField] private float hitFadeSeconds = 0.18f;

    [Header("Al matar")]
    [Tooltip("Color del núcleo al matar.")]
    [SerializeField] private Color killCoreColor = new Color(1f, 0.9f, 0.95f, 1f);
    [Tooltip("Color del halo al matar. Magenta neón = baja.")]
    [SerializeField] private Color killGlowColor = new Color(1f, 0.1f, 0.45f, 0.6f);
    [Tooltip("Tamaño respecto al de dar. 1.4 = un 40% más grande.")]
    [Min(0.1f)]
    [SerializeField] private float killScale = 1.4f;
    [Tooltip("Segundos que se queda entero al matar. 0.12 = se nota que ha caído.")]
    [Min(0f)]
    [SerializeField] private float killHoldSeconds = 0.12f;
    [Tooltip("Segundos que tarda en desvanecerse al matar. 0.3 = algo más lento que al dar.")]
    [Min(0f)]
    [SerializeField] private float killFadeSeconds = 0.3f;

    [Header("Animación")]
    [Tooltip("Tamaño con el que aparece antes de encogerse. 1.5 = salta grande y vuelve a su tamaño.")]
    [Min(1f)]
    [SerializeField] private float popScale = 1.5f;
    [Tooltip("Segundos que tarda en encogerse a su tamaño. 0.06 = golpe rápido.")]
    [Min(0f)]
    [SerializeField] private float popSeconds = 0.06f;
    [Tooltip("Cuánto se abren las rayas hacia fuera mientras se desvanecen. 6 = se dispersan un poco.")]
    [Min(0f)]
    [SerializeField] private float expandOnFade = 6f;

    [Header("Sonido (opcional)")]
    [Tooltip("AudioSource 2D (Spatial Blend 0) para el sonido. Vacío = sin sonido.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Sonido al dar (un 'tic' metálico corto). Vacío = sin sonido.")]
    [SerializeField] private AudioClip hitSound;
    [Tooltip("Sonido al matar. Vacío = usa el de dar.")]
    [SerializeField] private AudioClip killSound;
    [Tooltip("Volumen de los sonidos. 1 = normal.")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private readonly Image[] cores = new Image[BarCount];
    private readonly Image[] glows = new Image[BarCount];

    private float showTime = -1f; // cuándo saltó el último (-1 = oculto)
    private bool isKill;
    private int killFrame = -1;   // frame de la última baja (para que un perdigón que no mata no la tape)
    private int soundFrame = -1;  // frame del último sonido (para no sonar 6 veces con la escopeta)
    private bool soundWasKill;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        BuildBars();
        LayoutBars(gap);
    }

    private void OnEnable() => WeaponBase.OnAnyEnemyHit += HandleEnemyHit;
    private void OnDisable() => WeaponBase.OnAnyEnemyHit -= HandleEnemyHit;

    private void HandleEnemyHit(bool killed)
    {
        // En el mismo frame, si un perdigón ya ha matado, otro que solo da no lo cambia a "dar".
        if (!killed && killFrame == Time.frameCount) return;
        if (killed) killFrame = Time.frameCount;

        isKill = killed;
        showTime = Time.unscaledTime;
        ApplyColors();
        PlaySound(killed);
    }

    private void Update()
    {
        if (showTime < 0f) return;

        float age = Time.unscaledTime - showTime;
        float hold = isKill ? killHoldSeconds : hitHoldSeconds;
        float fade = isKill ? killFadeSeconds : hitFadeSeconds;

        if (age >= hold + fade)
        {
            canvasGroup.alpha = 0f;
            showTime = -1f;
            return;
        }

        float fade01 = fade > 0f ? Mathf.Clamp01((age - hold) / fade) : 0f;
        canvasGroup.alpha = 1f - fade01;

        // Salta grande y se encoge de golpe (ease out), luego se queda a su tamaño.
        float pop01 = popSeconds > 0f ? Mathf.Clamp01(age / popSeconds) : 1f;
        float easeOut = 1f - (1f - pop01) * (1f - pop01) * (1f - pop01);
        float scale = (isKill ? killScale : 1f) * Mathf.Lerp(popScale, 1f, easeOut);
        rect.localScale = new Vector3(scale, scale, 1f);

        LayoutBars(gap + expandOnFade * fade01);
    }

    // ---------------- RAYAS ----------------

    /// <summary>Crea las 4 rayas en diagonal (45°, 135°, 225° y 315°), cada una con su halo detrás.</summary>
    private void BuildBars()
    {
        for (int i = 0; i < BarCount; i++)
        {
            // Un "brazo" girado en la diagonal: dentro, las rayas solo se colocan en su eje X.
            var arm = new GameObject($"Arm{i}", typeof(RectTransform));
            var armRect = (RectTransform)arm.transform;
            armRect.SetParent(rect, false);
            armRect.sizeDelta = Vector2.zero;
            armRect.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);

            glows[i] = CreateBar(armRect, "Glow", glowSprite); // primero el halo, para que el núcleo quede encima
            cores[i] = CreateBar(armRect, "Core", null);
        }

        ApplyColors();
    }

    private static Image CreateBar(RectTransform parent, string barName, Sprite sprite)
    {
        var bar = new GameObject(barName, typeof(RectTransform), typeof(Image));
        var barRect = (RectTransform)bar.transform;
        barRect.SetParent(parent, false);
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0f, 0.5f); // crece desde el centro hacia fuera

        Image image = bar.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false; // que no bloquee clics de la UI
        return image;
    }

    /// <summary>Coloca cada raya a 'distance' del centro. El halo sobresale lo mismo por cada lado.</summary>
    private void LayoutBars(float distance)
    {
        float glowThickness = thickness * glowThicknessMultiplier;
        float glowExtra = glowThickness - thickness;

        for (int i = 0; i < BarCount; i++)
        {
            RectTransform core = cores[i].rectTransform;
            core.anchoredPosition = new Vector2(distance, 0f);
            core.sizeDelta = new Vector2(length, thickness);

            RectTransform glow = glows[i].rectTransform;
            glow.anchoredPosition = new Vector2(distance - glowExtra * 0.5f, 0f);
            glow.sizeDelta = new Vector2(length + glowExtra, glowThickness);
        }
    }

    private void ApplyColors()
    {
        Color core = isKill ? killCoreColor : hitCoreColor;
        Color glow = isKill ? killGlowColor : hitGlowColor;
        for (int i = 0; i < BarCount; i++)
        {
            cores[i].color = core;
            glows[i].color = glow;
        }
    }

    // ---------------- SONIDO ----------------

    private void PlaySound(bool killed)
    {
        if (audioSource == null) return;

        // Una vez por frame (la escopeta da con varios perdigones a la vez); la baja sí puede sonar encima de un "dar".
        if (soundFrame == Time.frameCount && (soundWasKill || !killed)) return;

        AudioClip clip = killed && killSound != null ? killSound : hitSound;
        if (clip == null) return;

        audioSource.PlayOneShot(clip, volume);
        soundFrame = Time.frameCount;
        soundWasKill = killed;
    }
}
