using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botella del HUD dibujada por código (no necesita sprites): cristal oscuro con contorno, líquido del color de la botella
/// que baja según Fill01, superficie que se mece, burbujas que suben por dentro y otras que se escapan por la boca,
/// un reflejo en el cristal y un anillo dorado en la boca. El cristal se tiñe un poco del color del líquido en los
/// bordes, como el vidrio de verdad. Con poco tiempo (Warning) el líquido late y se agita.
///
/// Va en un objeto del Canvas con RectTransform más alto que ancho (p. ej. 90 x 150): la botella ocupa todo el
/// rectángulo. La controla BottleEffectHUD (color, nivel y aviso). Raycast Target desmarcado: es solo decoración.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class BottleGauge : MaskableGraphic
{
    [Header("Forma (fracciones del rectángulo)")]
    [Tooltip("Ancho del cuerpo respecto al ancho del rectángulo. 0.9 = casi todo.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float bodyWidth = 0.9f;
    [Tooltip("Ancho del cuello respecto al ancho del rectángulo.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float neckWidth = 0.32f;
    [Tooltip("Altura (0 abajo, 1 arriba) donde empieza a estrecharse el hombro de la botella.")]
    [Range(0.2f, 0.9f)]
    [SerializeField] private float shoulderStart = 0.5f;
    [Tooltip("Altura donde acaba el hombro y empieza el cuello.")]
    [Range(0.3f, 0.95f)]
    [SerializeField] private float shoulderEnd = 0.7f;
    [Tooltip("Altura de la boca de la botella. Por encima queda hueco para las burbujas que se escapan.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float mouthHeight = 0.86f;
    [Tooltip("Redondeo de las esquinas de abajo, respecto al alto.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float bottomRounding = 0.1f;

    [Header("Cristal")]
    [Tooltip("Color del cristal en el centro (se ve detrás del líquido). Oscuro y algo transparente.")]
    [SerializeField] private Color glassColor = new Color(0.055f, 0.05f, 0.07f, 0.6f);
    [Tooltip("Cuánto se tiñen los bordes del cristal del color del líquido. 0.25 = un reflejo de color suave.")]
    [Range(0f, 1f)]
    [SerializeField] private float glassEdgeTint = 0.25f;
    [Tooltip("Opacidad de los bordes del cristal.")]
    [Range(0f, 1f)]
    [SerializeField] private float glassEdgeAlpha = 0.72f;
    [Tooltip("Color del contorno.")]
    [SerializeField] private Color outlineColor = new Color(0.05f, 0.03f, 0.02f, 0.95f);
    [Tooltip("Grosor del contorno, en unidades del Canvas.")]
    [Min(0f)]
    [SerializeField] private float outlineWidth = 4f;
    [Tooltip("Grosor del cristal (separación entre el borde y el líquido), en unidades del Canvas.")]
    [Min(0f)]
    [SerializeField] private float glassThickness = 4f;
    [Tooltip("Color del anillo de la boca.")]
    [SerializeField] private Color trimColor = new Color(0.83f, 0.66f, 0.29f, 1f);
    [Tooltip("Brillo del reflejo del cristal (la raya blanca de la izquierda). 0 = sin reflejo.")]
    [Range(0f, 1f)]
    [SerializeField] private float highlight = 0.35f;

    [Header("Líquido")]
    [Tooltip("Lo que se oscurece el líquido abajo (1 = igual que arriba). 0.55 = degradado de profundidad.")]
    [Range(0f, 1f)]
    [SerializeField] private float bottomShade = 0.55f;
    [Tooltip("Altura de las olas de la superficie, respecto al alto.")]
    [Range(0f, 0.05f)]
    [SerializeField] private float waveHeight = 0.012f;
    [Tooltip("Velocidad del vaivén de la superficie.")]
    [Min(0f)]
    [SerializeField] private float waveSpeed = 3f;
    [Tooltip("Burbujas que suben por dentro.")]
    [Range(0, 16)]
    [SerializeField] private int bubbles = 7;
    [Tooltip("Burbujas que se escapan por la boca.")]
    [Range(0, 8)]
    [SerializeField] private int escapingBubbles = 3;
    [Tooltip("Velocidad de las burbujas.")]
    [Min(0f)]
    [SerializeField] private float bubbleSpeed = 0.45f;

    [Header("Aviso de poco tiempo")]
    [Tooltip("Latidos por segundo del líquido cuando queda poco.")]
    [Min(0f)]
    [SerializeField] private float warningPulseSpeed = 4f;

    private const int Slices = 64;
    private const int Columns = 28;

    private Color liquidColor = new Color(0.3f, 0.9f, 0.4f);
    private float fill01 = 1f;
    private bool warning;
    private readonly List<float> sliceY = new List<float>(Slices + 1);
    private readonly List<float> sliceHalf = new List<float>(Slices + 1);

    /// <summary>Color del líquido (el de la botella que te has bebido).</summary>
    public Color LiquidColor
    {
        get => liquidColor;
        set { liquidColor = value; liquidColor.a = 1f; SetVerticesDirty(); }
    }

    /// <summary>Nivel del líquido: 1 = llena, 0 = vacía.</summary>
    public float Fill01
    {
        get => fill01;
        set { fill01 = Mathf.Clamp01(value); SetVerticesDirty(); }
    }

    /// <summary>Queda poco: el líquido late y se agita.</summary>
    public bool Warning
    {
        get => warning;
        set => warning = value;
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        // Está animada (olas y burbujas): se redibuja cada frame mientras se ve.
        if (Application.isPlaying && canvasRenderer.GetAlpha() > 0f) SetVerticesDirty();
    }

    // ---------------- FORMA ----------------

    /// <summary>Medio ancho de la botella a la altura y (en unidades del rectángulo, desde abajo).</summary>
    private float HalfAt(float y, float height, float width)
    {
        float body = width * bodyWidth * 0.5f;
        float neck = width * neckWidth * 0.5f;
        float corner = Mathf.Min(body, height * bottomRounding);
        float y01 = y / height;

        if (y < corner)
        {
            float dy = corner - y;
            return body - corner + Mathf.Sqrt(Mathf.Max(0f, corner * corner - dy * dy));
        }
        if (y01 <= shoulderStart) return body;
        if (y01 < shoulderEnd)
        {
            float t = (y01 - shoulderStart) / Mathf.Max(0.0001f, shoulderEnd - shoulderStart);
            return Mathf.Lerp(body, neck, 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI));
        }
        return neck;
    }

    private void BuildSlices(float height, float width)
    {
        sliceY.Clear();
        sliceHalf.Clear();
        float top = height * mouthHeight;
        for (int i = 0; i <= Slices; i++)
        {
            float y = top * i / Slices;
            sliceY.Add(y);
            sliceHalf.Add(HalfAt(y, height, width));
        }
    }

    /// <summary>Hasta dónde llega el líquido en la columna x: de "bottom" a "top" (dentro del cristal).</summary>
    private bool ColumnRange(float x, out float bottom, out float top)
    {
        bottom = top = 0f;
        float ax = Mathf.Abs(x);
        int first = -1;
        for (int i = 0; i <= Slices; i++)
        {
            bool inside = sliceHalf[i] - glassThickness >= ax && sliceY[i] >= glassThickness;
            if (inside && first < 0) first = i;
            else if (!inside && first >= 0) { top = sliceY[i - 1]; bottom = sliceY[first]; return true; }
        }
        if (first < 0) return false;
        bottom = sliceY[first];
        top = sliceY[Slices];
        return true;
    }

    // ---------------- DIBUJO ----------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float w = r.width, h = r.height;
        if (w <= 1f || h <= 1f) return;

        BuildSlices(h, w);
        Vector2 origin = new Vector2(r.center.x, r.yMin); // abajo en el centro
        float time = Application.isPlaying ? Time.unscaledTime : 0f;

        // 1) Contorno y cristal: la silueta, primero algo más grande en oscuro y encima el cristal.
        AddSilhouette(vh, origin, outlineWidth, outlineColor, outlineColor);
        Color glassEdge = new Color(0.16f + liquidColor.r * glassEdgeTint, 0.16f + liquidColor.g * glassEdgeTint,
                                    0.16f + liquidColor.b * glassEdgeTint, glassEdgeAlpha);
        AddSilhouette(vh, origin, 0f, glassColor, glassEdge);

        // 2) Líquido.
        float innerBottom = glassThickness;
        float maxLevel = h * Mathf.Lerp(shoulderStart, shoulderEnd, 0.6f);
        float level = Mathf.Lerp(innerBottom, maxLevel, fill01);
        float pulse = warning ? 0.5f + 0.5f * Mathf.Sin(time * warningPulseSpeed * Mathf.PI * 2f) : 0f;
        float waves = waveHeight * h * (warning ? 2f : 1f) * (fill01 > 0.001f ? 1f : 0f);

        Color top = Brighten(liquidColor, 1.1f + 0.35f * pulse);
        Color bottom = Brighten(liquidColor, bottomShade);
        float bodyInner = w * bodyWidth * 0.5f - glassThickness;

        if (fill01 > 0.001f)
        {
            for (int c = 0; c < Columns; c++)
            {
                float x0 = Mathf.Lerp(-bodyInner, bodyInner, c / (float)Columns);
                float x1 = Mathf.Lerp(-bodyInner, bodyInner, (c + 1) / (float)Columns);
                if (!ColumnRange(x0, out float b0, out float t0) || !ColumnRange(x1, out float b1, out float t1)) continue;

                float s0 = Mathf.Min(t0, Surface(x0, level, waves, time, w));
                float s1 = Mathf.Min(t1, Surface(x1, level, waves, time, w));
                if (s0 <= b0 && s1 <= b1) continue;
                s0 = Mathf.Max(s0, b0);
                s1 = Mathf.Max(s1, b1);

                float span = Mathf.Max(1f, maxLevel - innerBottom);
                AddQuad(vh,
                    origin + new Vector2(x0, b0), origin + new Vector2(x1, b1),
                    origin + new Vector2(x1, s1), origin + new Vector2(x0, s0),
                    Color.Lerp(bottom, top, (b0 - innerBottom) / span), Color.Lerp(bottom, top, (b1 - innerBottom) / span),
                    Color.Lerp(bottom, top, (s1 - innerBottom) / span), Color.Lerp(bottom, top, (s0 - innerBottom) / span));

                // Borde brillante de la superficie (el menisco).
                float band = Mathf.Max(1.5f, h * 0.012f);
                Color shine = Brighten(liquidColor, 1.6f);
                shine.a = 0.9f;
                if (s0 - band > b0 && s1 - band > b1)
                    AddQuad(vh, origin + new Vector2(x0, s0 - band), origin + new Vector2(x1, s1 - band),
                        origin + new Vector2(x1, s1), origin + new Vector2(x0, s0), shine, shine, shine, shine);
            }

            // 3) Burbujas que suben por dentro (no salen del líquido).
            for (int i = 0; i < bubbles; i++)
            {
                float seed = i * 12.9898f;
                float speed = bubbleSpeed * (0.6f + Hash(seed) * 0.8f);
                float t = Mathf.Repeat(time * speed + Hash(seed + 1f), 1f);
                float y = Mathf.Lerp(innerBottom, level, t);
                float radius = Mathf.Lerp(w * 0.025f, w * 0.06f, Hash(seed + 2f)) * Mathf.Lerp(0.6f, 1f, t);
                float xBase = (Hash(seed + 3f) * 2f - 1f) * 0.7f;
                float half = HalfAt(y, h, w) - glassThickness - radius;
                if (half <= 0f || y + radius > Surface(xBase * half, level, waves, time, w)) continue;
                float x = Mathf.Clamp(xBase * half + Mathf.Sin(time * 4f + seed) * radius, -half, half);
                AddBubble(vh, origin + new Vector2(x, y), radius, 0.55f);
            }
        }

        // 4) Anillo dorado de la boca.
        float mouth = h * mouthHeight;
        float neckHalf = w * neckWidth * 0.5f;
        AddRect(vh, origin, -neckHalf - outlineWidth, mouth - h * 0.045f, neckHalf + outlineWidth, mouth + outlineWidth, outlineColor);
        AddRect(vh, origin, -neckHalf - outlineWidth * 0.5f, mouth - h * 0.04f, neckHalf + outlineWidth * 0.5f, mouth, trimColor);

        // 5) Reflejo del cristal: una raya a la izquierda del cuerpo y un punto en el hombro.
        if (highlight > 0f)
        {
            float bodyHalf = w * bodyWidth * 0.5f;
            float sx = -bodyHalf * 0.62f;
            float sw = w * 0.055f;
            float y0 = h * 0.12f, y1 = h * (shoulderStart - 0.02f);
            const int streakSteps = 8;
            for (int i = 0; i < streakSteps; i++)
            {
                // Raya que aparece y desaparece suave por los extremos, y se estrecha hacia arriba.
                float fa = i / (float)streakSteps, fb = (i + 1) / (float)streakSteps;
                Color ca = new Color(1f, 1f, 1f, highlight * Mathf.Sin(fa * Mathf.PI));
                Color cb = new Color(1f, 1f, 1f, highlight * Mathf.Sin(fb * Mathf.PI));
                float wa = sw * Mathf.Lerp(1f, 0.6f, fa), wb = sw * Mathf.Lerp(1f, 0.6f, fb);
                float ya = Mathf.Lerp(y0, y1, fa), yb = Mathf.Lerp(y0, y1, fb);
                AddQuad(vh, origin + new Vector2(sx, ya), origin + new Vector2(sx + wa, ya),
                    origin + new Vector2(sx + wb, yb), origin + new Vector2(sx, yb), ca, ca, cb, cb);
            }
            AddBubble(vh, origin + new Vector2(sx + sw * 0.5f, h * (shoulderStart + 0.06f)), sw * 0.55f, highlight);
        }

        // 6) Burbujas que se escapan por la boca y se desvanecen al subir.
        if (fill01 > 0.001f)
        {
            float room = h * (1f - mouthHeight);
            for (int i = 0; i < escapingBubbles; i++)
            {
                float seed = 100f + i * 7.31f;
                float t = Mathf.Repeat(time * (0.5f + Hash(seed) * 0.4f) + Hash(seed + 1f), 1f);
                float y = mouth + t * room;
                float x = Mathf.Sin(time * 2.5f + seed) * neckHalf * 0.7f;
                float radius = Mathf.Lerp(w * 0.04f, w * 0.015f, t);
                AddBubble(vh, origin + new Vector2(x, y), radius, 0.6f * (1f - t));
            }
        }
    }

    private float Surface(float x, float level, float waves, float time, float width)
    {
        float k = Mathf.PI * 2f / Mathf.Max(1f, width);
        return level + waves * (Mathf.Sin(x * k * 1.3f + time * waveSpeed) + 0.5f * Mathf.Sin(x * k * 2.7f - time * waveSpeed * 1.7f));
    }

    /// <summary>Silueta de la botella con degradado de lado a lado: "center" en medio y "edge" en los bordes.</summary>
    private void AddSilhouette(VertexHelper vh, Vector2 origin, float grow, Color center, Color edge)
    {
        const int across = 8;
        for (int i = 0; i < Slices; i++)
        {
            float y0 = sliceY[i] - (i == 0 ? grow : 0f);
            float y1 = sliceY[i + 1];
            float h0 = sliceHalf[i] + grow, h1 = sliceHalf[i + 1] + grow;
            for (int k = 0; k < across; k++)
            {
                float a = -1f + 2f * k / across, b = -1f + 2f * (k + 1) / across;
                Color ca = Color.Lerp(center, edge, a * a), cb = Color.Lerp(center, edge, b * b);
                AddQuad(vh, origin + new Vector2(a * h0, y0), origin + new Vector2(b * h0, y0),
                    origin + new Vector2(b * h1, y1), origin + new Vector2(a * h1, y1), ca, cb, cb, ca);
            }
        }
    }

    private static void AddRect(VertexHelper vh, Vector2 origin, float x0, float y0, float x1, float y1, Color color)
    {
        AddQuad(vh, origin + new Vector2(x0, y0), origin + new Vector2(x1, y0),
            origin + new Vector2(x1, y1), origin + new Vector2(x0, y1), color, color, color, color);
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, ca, Vector2.zero);
        vh.AddVert(b, cb, Vector2.zero);
        vh.AddVert(c, cc, Vector2.zero);
        vh.AddVert(d, cd, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    /// <summary>Burbuja: un círculo claro con el borde algo más marcado.</summary>
    private static void AddBubble(VertexHelper vh, Vector2 center, float radius, float alpha)
    {
        if (radius <= 0.1f || alpha <= 0.01f) return;
        const int segments = 12;
        Color inner = new Color(1f, 1f, 1f, alpha * 0.25f);
        Color rim = new Color(1f, 1f, 1f, alpha);
        int start = vh.currentVertCount;
        vh.AddVert(center, inner, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, rim, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(start, start + 1 + i, start + 2 + i);
    }

    private static Color Brighten(Color c, float factor)
    {
        return new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), 1f);
    }

    /// <summary>Número "al azar" fijo a partir de una semilla (0 a 1): cada burbuja tiene siempre su ritmo.</summary>
    private static float Hash(float seed)
    {
        return Mathf.Repeat(Mathf.Sin(seed * 78.233f) * 43758.5453f, 1f);
    }
}
