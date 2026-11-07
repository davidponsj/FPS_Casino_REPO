using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Marco de neón para los botones del cartel de muerte (RETRY, MENÚ): un tubo de luz con las esquinas redondeadas,
/// el centro más blanco y un halo suave por fuera y por dentro, como los carteles de casino.
/// - Quieto: brilla a medias.
/// - Ratón encima: se enciende del todo, late suave y el texto crece un poco.
/// - Al pulsar: el texto se hunde un instante.
///
/// Va en el objeto del botón, EN LUGAR del Image: es el que recibe el ratón (todo el rectángulo del botón es clicable)
/// y el que dibuja el marco alrededor de ese rectángulo. En el Button pon Target Graphic = este componente y
/// Transition = None (el brillo ya lo hace este script). El color del neón es el Color de este componente.
/// No usa texturas ni materiales: dibuja el marco con su propia malla. Va en tiempo real (funciona con el juego parado).
/// </summary>
public class NeonButton : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Marco (en unidades del Canvas)")]
    [Tooltip("Grosor del tubo de neón. 12 = línea fina pero visible.")]
    [Min(0f)]
    [SerializeField] private float lineWidth = 12f;
    [Tooltip("Radio de las esquinas. 45 = redondeadas como una placa de máquina recreativa.")]
    [Min(0f)]
    [SerializeField] private float cornerRadius = 45f;
    [Tooltip("Lo que se extiende el halo hacia fuera. 50 = resplandor amplio.")]
    [Min(0f)]
    [SerializeField] private float outerGlow = 50f;
    [Tooltip("Lo que se extiende el halo hacia dentro del botón. 20 = un poco de luz sobre la placa.")]
    [Min(0f)]
    [SerializeField] private float innerGlow = 20f;
    [Tooltip("Fuerza del halo respecto al tubo. 0.45 = halo suave.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowStrength = 0.45f;
    [Tooltip("Lo blanco que es el centro del tubo (como un neón de verdad). 0 = todo del color, 1 = centro blanco.")]
    [Range(0f, 1f)]
    [SerializeField] private float coreWhiteness = 0.55f;
    [Tooltip("Cuánto sale el marco del rectángulo del botón: positivo = por fuera, negativo = por dentro.")]
    [SerializeField] private float padding = 0f;
    [Tooltip("Relleno de dentro del marco. Negro con un poco de alfa = la placa se oscurece y el texto resalta. Alfa 0 = sin relleno.")]
    [SerializeField] private Color fillColor = new Color(0f, 0f, 0f, 0.2f);
    [Tooltip("Segmentos de cada esquina. Más = curva más suave.")]
    [Range(1, 16)]
    [SerializeField] private int cornerSegments = 8;

    [Header("Estados")]
    [Tooltip("Brillo del neón con el ratón fuera. 0.55 = encendido a medias.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleBrightness = 0.55f;
    [Tooltip("Brillo con el ratón encima.")]
    [Range(0f, 1f)]
    [SerializeField] private float hoverBrightness = 1f;
    [Tooltip("Cuánto late el neón con el ratón encima. 0 = no late.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pulseAmount = 0.15f;
    [Tooltip("Latidos por segundo.")]
    [Min(0f)]
    [SerializeField] private float pulseSpeed = 2.5f;
    [Tooltip("Rapidez de los cambios de brillo y tamaño. 12 = rápido pero suave.")]
    [Min(0.1f)]
    [SerializeField] private float fadeSpeed = 12f;

    [Header("Texto")]
    [Tooltip("Texto del botón, para que crezca y se hunda. Vacío = lo busca en los hijos.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Tamaño del texto con el ratón encima. 1.06 = crece un poco.")]
    [Min(0.1f)]
    [SerializeField] private float hoverScale = 1.06f;
    [Tooltip("Tamaño del texto al pulsar. 0.94 = se hunde.")]
    [Min(0.1f)]
    [SerializeField] private float pressScale = 0.94f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuándo entra/sale el ratón y cuándo se pulsa.")]
    [SerializeField] private bool showDebug;

    private bool hovered;
    private bool pressed;
    private float brightness = -1f; // -1 = aún sin calcular
    private Selectable selectable;
    private readonly List<Vector2> outline = new List<Vector2>();
    private readonly List<Vector2> normals = new List<Vector2>();

    protected override void Awake()
    {
        base.Awake();
        selectable = GetComponent<Selectable>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        hovered = pressed = false;
        brightness = idleBrightness;
        if (label != null) label.rectTransform.localScale = Vector3.one;
        SetVerticesDirty();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (showDebug) Debug.Log($"{name}: ratón encima.", this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        if (showDebug) Debug.Log($"{name}: pulsado.", this);
    }

    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    private void Update()
    {
        bool active = selectable == null || selectable.IsInteractable();
        bool lit = hovered && active;

        float target = lit ? hoverBrightness : idleBrightness;
        if (lit && pulseAmount > 0f)
            target -= pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));

        float blend = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
        float next = Mathf.Lerp(brightness, target, blend);
        if (Mathf.Abs(next - brightness) > 0.002f)
        {
            brightness = next;
            SetVerticesDirty();
        }

        if (label != null)
        {
            float scale = pressed && active ? pressScale : lit ? hoverScale : 1f;
            float current = label.rectTransform.localScale.x;
            label.rectTransform.localScale = Vector3.one * Mathf.Lerp(current, scale, blend);
        }
    }

    // ---------------- MALLA DEL MARCO ----------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float bright = brightness < 0f ? idleBrightness : brightness;

        Rect box = rectTransform.rect;
        box.xMin -= padding; box.yMin -= padding;
        box.xMax += padding; box.yMax += padding;
        float radius = Mathf.Min(cornerRadius, box.width * 0.5f, box.height * 0.5f);
        BuildOutline(box, radius);

        float half = lineWidth * 0.5f;
        Color neon = color;
        Color core = Color.Lerp(neon, Color.white, coreWhiteness);

        // Anillos de dentro a fuera: halo interior, tubo (borde, centro blanco, borde) y halo exterior.
        if (fillColor.a > 0f) AddFill(vh, -half, radius, fillColor);
        AddStrip(vh, -half - innerGlow, WithAlpha(neon, 0f), -half, WithAlpha(neon, glowStrength * bright), radius);
        AddStrip(vh, -half, WithAlpha(neon, bright), 0f, WithAlpha(core, bright), radius);
        AddStrip(vh, 0f, WithAlpha(core, bright), half, WithAlpha(neon, bright), radius);
        AddStrip(vh, half, WithAlpha(neon, glowStrength * bright), half + outerGlow, WithAlpha(neon, 0f), radius);
    }

    /// <summary>Puntos del borde del rectángulo redondeado (sobre la línea central del tubo) y hacia dónde mira cada uno.</summary>
    private void BuildOutline(Rect box, float radius)
    {
        outline.Clear();
        normals.Clear();
        Vector2[] centers =
        {
            new Vector2(box.xMax - radius, box.yMax - radius), // arriba derecha
            new Vector2(box.xMin + radius, box.yMax - radius), // arriba izquierda
            new Vector2(box.xMin + radius, box.yMin + radius), // abajo izquierda
            new Vector2(box.xMax - radius, box.yMin + radius)  // abajo derecha
        };

        for (int corner = 0; corner < 4; corner++)
        {
            for (int step = 0; step <= cornerSegments; step++)
            {
                float angle = (corner + step / (float)cornerSegments) * Mathf.PI * 0.5f;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                outline.Add(centers[corner] + normal * radius);
                normals.Add(normal);
            }
        }
    }

    /// <summary>Una banda alrededor de todo el marco, entre dos distancias al borde, con un color en cada lado.</summary>
    private void AddStrip(VertexHelper vh, float fromOffset, Color fromColor, float toOffset, Color toColor, float radius)
    {
        int start = vh.currentVertCount;
        int count = outline.Count;
        for (int i = 0; i < count; i++)
        {
            vh.AddVert(OffsetPoint(i, fromOffset, radius), fromColor, Vector2.zero);
            vh.AddVert(OffsetPoint(i, toOffset, radius), toColor, Vector2.zero);
        }
        for (int i = 0; i < count; i++)
        {
            int a = start + i * 2;
            int b = start + (i + 1) % count * 2;
            vh.AddTriangle(a, b, b + 1);
            vh.AddTriangle(a, b + 1, a + 1);
        }
    }

    /// <summary>Relleno de dentro del marco: un abanico desde el centro hasta el borde interior del tubo.</summary>
    private void AddFill(VertexHelper vh, float edgeOffset, float radius, Color fill)
    {
        int start = vh.currentVertCount;
        vh.AddVert(rectTransform.rect.center, fill, Vector2.zero);
        for (int i = 0; i < outline.Count; i++)
            vh.AddVert(OffsetPoint(i, edgeOffset, radius), fill, Vector2.zero);
        for (int i = 0; i < outline.Count; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % outline.Count);
    }

    /// <summary>Punto del borde desplazado hacia fuera (+) o hacia dentro (-). Hacia dentro no pasa del centro de la esquina.</summary>
    private Vector2 OffsetPoint(int index, float offset, float radius)
    {
        float distance = Mathf.Max(offset, -radius);
        return outline[index] + normals[index] * distance;
    }

    private static Color WithAlpha(Color c, float alpha)
    {
        c.a *= alpha;
        return c;
    }
}
