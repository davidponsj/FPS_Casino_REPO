using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hitmarkers de neón que salen JUSTO donde ha dado la bala y siguen al robot mientras se mueve: 4 rayas en diagonal
/// que aparecen grandes, se encogen de golpe y se desvanecen abriéndose. Al matar salen más grandes, de otro color
/// y duran más. Puede haber muchos a la vez (el subfusil y la escopeta llenan la pantalla).
///
/// Se entera de los impactos por WeaponBase.OnAnyEnemyHit (qué enemigo, dónde y si lo ha matado), así que vale
/// para todas las armas sin asignarlas. Si un perdigón mata, los demás perdigones de ese disparo al mismo enemigo
/// también se ponen en modo "baja".
///
/// Los hitmarkers los crea el propio script al empezar (no hay que montarlos a mano). Cada raya es un núcleo fino
/// y claro con un halo más ancho y transparente detrás: eso es lo que le da el aspecto de neón.
///
/// Va en un objeto vacío del Canvas, anclado al centro de la pantalla y colocado el último (para que se pinte encima).
/// Usa el tiempo real (no le afecta Time.timeScale), así sigue funcionando con cámara lenta o una micro‑pausa al matar.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HitMarkerHUD : MonoBehaviour
{
    private const int BarCount = 4;

    [Header("Referencias")]
    [Tooltip("Cámara del jugador, para pasar el punto del impacto a la pantalla. Vacío = Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Header("Posición")]
    [Tooltip("Marcado: sale donde ha dado la bala y sigue al robot. Desmarcado: siempre en el centro de la pantalla.")]
    [SerializeField] private bool showAtImpactPoint = true;
    [Tooltip("Hitmarkers que puede haber a la vez. 24 = sobra para el subfusil y la escopeta. Si se llenan, se reutiliza el más antiguo.")]
    [Min(1)]
    [SerializeField] private int maxMarkers = 24;
    [Tooltip("Giro aleatorio de cada hitmarker, en grados. 10 = cada uno sale un poco torcido y se ve más vivo. 0 = todos rectos.")]
    [Range(0f, 45f)]
    [SerializeField] private float randomTiltDegrees = 10f;

    [Header("Forma (en unidades del Canvas: 1920x1080 de referencia)")]
    [Tooltip("Distancia del centro del hitmarker al principio de cada raya. 18 = deja ver el punto del impacto.")]
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

    /// <summary>Un hitmarker en pantalla: sus rayas y a qué punto del mundo está pegado.</summary>
    private class Marker
    {
        public RectTransform rect;
        public CanvasGroup group;
        public readonly Image[] cores = new Image[BarCount];
        public readonly Image[] glows = new Image[BarCount];
        public EnemyHealth enemy;    // a quién ha dado (para pasar a "baja" los perdigones del mismo disparo)
        public Transform target;     // lo que sigue: el enemigo (vacío = punto fijo)
        public Vector3 localPoint;   // punto del impacto relativo al enemigo
        public Vector3 worldPoint;   // último punto conocido en el mundo (si el enemigo desaparece, se queda aquí)
        public float showTime = -1f; // -1 = libre
        public bool isKill;
        public int frame;
        public bool IsActive => showTime >= 0f;
    }

    private RectTransform rect;
    private Canvas canvas;
    private Marker[] markers;

    private int soundFrame = -1; // frame del último sonido (para no sonar 6 veces con la escopeta)
    private bool soundWasKill;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();

        // La versión anterior apagaba un CanvasGroup en este objeto. Si sigue ahí, que no oculte nada.
        if (TryGetComponent(out CanvasGroup rootGroup))
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = false;
        }
        rect.localScale = Vector3.one;

        markers = new Marker[maxMarkers];
        for (int i = 0; i < maxMarkers; i++)
            markers[i] = BuildMarker(i);
    }

    private void OnEnable() => WeaponBase.OnAnyEnemyHit += HandleEnemyHit;
    private void OnDisable() => WeaponBase.OnAnyEnemyHit -= HandleEnemyHit;

    private void HandleEnemyHit(EnemyHealth enemy, Vector3 point, bool killed)
    {
        // Si este perdigón mata, los otros del mismo disparo a este enemigo también pasan a "baja".
        if (killed)
        {
            foreach (Marker other in markers)
            {
                if (!other.IsActive || other.enemy != enemy || other.frame != Time.frameCount) continue;
                other.isKill = true;
                ApplyColors(other);
            }
        }

        Marker marker = GetFreeMarker();
        marker.enemy = enemy;
        marker.target = enemy != null ? enemy.transform : null;
        marker.localPoint = marker.target != null ? marker.target.InverseTransformPoint(point) : point;
        marker.worldPoint = point;
        marker.isKill = killed;
        marker.frame = Time.frameCount;
        marker.showTime = Time.unscaledTime;
        marker.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-randomTiltDegrees, randomTiltDegrees));
        marker.rect.SetAsLastSibling(); // el más nuevo, encima
        marker.rect.gameObject.SetActive(true);
        ApplyColors(marker);
        UpdateMarker(marker); // colocado ya en este frame, sin un frame en el centro

        PlaySound(killed);
    }

    // LateUpdate: después de que la cámara y los robots se hayan movido en este frame, así no va con retraso.
    private void LateUpdate()
    {
        foreach (Marker marker in markers)
            if (marker.IsActive) UpdateMarker(marker);
    }

    private void UpdateMarker(Marker marker)
    {
        float age = Time.unscaledTime - marker.showTime;
        float hold = marker.isKill ? killHoldSeconds : hitHoldSeconds;
        float fade = marker.isKill ? killFadeSeconds : hitFadeSeconds;

        if (age >= hold + fade)
        {
            Hide(marker);
            return;
        }

        if (!PlaceMarker(marker))
        {
            marker.group.alpha = 0f; // el impacto está detrás de la cámara: no se dibuja, pero sigue su tiempo
            return;
        }

        float fade01 = fade > 0f ? Mathf.Clamp01((age - hold) / fade) : 0f;
        marker.group.alpha = 1f - fade01;

        // Salta grande y se encoge de golpe (ease out), luego se queda a su tamaño.
        float pop01 = popSeconds > 0f ? Mathf.Clamp01(age / popSeconds) : 1f;
        float easeOut = 1f - (1f - pop01) * (1f - pop01) * (1f - pop01);
        float scale = (marker.isKill ? killScale : 1f) * Mathf.Lerp(popScale, 1f, easeOut);
        marker.rect.localScale = new Vector3(scale, scale, 1f);

        LayoutBars(marker, gap + expandOnFade * fade01);
    }

    /// <summary>Pone el hitmarker en pantalla encima del punto del impacto. False si ese punto está detrás de la cámara.</summary>
    private bool PlaceMarker(Marker marker)
    {
        if (!showAtImpactPoint)
        {
            marker.rect.anchoredPosition = Vector2.zero;
            return true;
        }

        // Sigue al robot: el punto se mueve con él (si el robot ya no existe, se queda en el último sitio).
        if (marker.target != null)
            marker.worldPoint = marker.target.TransformPoint(marker.localPoint);

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return false;

        Vector3 screenPoint = cam.WorldToScreenPoint(marker.worldPoint);
        if (screenPoint.z <= 0f) return false;

        // Con un Canvas "Screen Space - Overlay" no hay cámara de UI (null); con "Screen Space - Camera", la suya.
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, uiCamera, out Vector2 localPoint))
            return false;

        marker.rect.anchoredPosition = localPoint;
        return true;
    }

    private Marker GetFreeMarker()
    {
        Marker oldest = markers[0];
        foreach (Marker marker in markers)
        {
            if (!marker.IsActive) return marker;
            if (marker.showTime < oldest.showTime) oldest = marker;
        }
        return oldest; // todos ocupados: se reutiliza el más antiguo
    }

    private static void Hide(Marker marker)
    {
        marker.showTime = -1f;
        marker.enemy = null;
        marker.target = null;
        marker.rect.gameObject.SetActive(false);
    }

    // ---------------- CONSTRUCCIÓN ----------------

    /// <summary>Crea un hitmarker: 4 rayas en diagonal (45°, 135°, 225° y 315°), cada una con su halo detrás.</summary>
    private Marker BuildMarker(int index)
    {
        var marker = new Marker();

        var root = new GameObject($"Marker{index}", typeof(RectTransform), typeof(CanvasGroup));
        marker.rect = (RectTransform)root.transform;
        marker.rect.SetParent(rect, false);
        marker.rect.anchorMin = marker.rect.anchorMax = new Vector2(0.5f, 0.5f);
        marker.rect.sizeDelta = Vector2.zero;

        marker.group = root.GetComponent<CanvasGroup>();
        marker.group.interactable = false;
        marker.group.blocksRaycasts = false;

        for (int i = 0; i < BarCount; i++)
        {
            // Un "brazo" girado en la diagonal: dentro, las rayas solo se colocan en su eje X.
            var arm = new GameObject($"Arm{i}", typeof(RectTransform));
            var armRect = (RectTransform)arm.transform;
            armRect.SetParent(marker.rect, false);
            armRect.sizeDelta = Vector2.zero;
            armRect.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);

            marker.glows[i] = CreateBar(armRect, "Glow", glowSprite); // primero el halo, para que el núcleo quede encima
            marker.cores[i] = CreateBar(armRect, "Core", null);
        }

        LayoutBars(marker, gap);
        root.SetActive(false);
        return marker;
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

    /// <summary>Coloca cada raya a 'distance' del centro del hitmarker. El halo sobresale lo mismo por cada lado.</summary>
    private void LayoutBars(Marker marker, float distance)
    {
        float glowThickness = thickness * glowThicknessMultiplier;
        float glowExtra = glowThickness - thickness;

        for (int i = 0; i < BarCount; i++)
        {
            RectTransform core = marker.cores[i].rectTransform;
            core.anchoredPosition = new Vector2(distance, 0f);
            core.sizeDelta = new Vector2(length, thickness);

            RectTransform glow = marker.glows[i].rectTransform;
            glow.anchoredPosition = new Vector2(distance - glowExtra * 0.5f, 0f);
            glow.sizeDelta = new Vector2(length + glowExtra, glowThickness);
        }
    }

    private void ApplyColors(Marker marker)
    {
        Color core = marker.isKill ? killCoreColor : hitCoreColor;
        Color glow = marker.isKill ? killGlowColor : hitGlowColor;
        for (int i = 0; i < BarCount; i++)
        {
            marker.cores[i].color = core;
            marker.glows[i].color = glow;
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
