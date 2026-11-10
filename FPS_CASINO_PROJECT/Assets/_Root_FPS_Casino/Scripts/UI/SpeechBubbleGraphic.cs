using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bocadillo de cómic dibujado por código (no necesita sprites), con el estilo del barman: relleno claro, contorno
/// grueso oscuro, sombra dura desplazada y una cola que apunta a quien habla. Opcionalmente, una franja de color en el
/// borde izquierdo (el color de la botella).
/// Sin cola y con el radio a la mitad del alto sirve también para pastillas (etiquetas) y círculos (marco del retrato).
///
/// El rectángulo del RectTransform es el CUERPO del bocadillo; la cola y la sombra se dibujan por fuera.
/// La usa BartenderSpeech, que la configura por código.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SpeechBubbleGraphic : MaskableGraphic
{
    public enum TailSide { None, Bottom, Left }

    [Header("Forma")]
    [Tooltip("Radio de las esquinas en píxeles del Canvas. La mitad del alto = pastilla; la mitad del lado de un cuadrado = círculo.")]
    [Min(0f)]
    [SerializeField] private float cornerRadius = 22f;
    [Tooltip("Lado por el que sale la cola.")]
    [SerializeField] private TailSide tailSide = TailSide.Bottom;
    [Tooltip("Dónde sale la cola a lo largo de ese lado (0 = izquierda/abajo, 1 = derecha/arriba).")]
    [Range(0f, 1f)]
    [SerializeField] private float tailPosition = 0.15f;
    [Tooltip("Ancho de la base de la cola.")]
    [Min(0f)]
    [SerializeField] private float tailWidth = 34f;
    [Tooltip("Largo de la cola.")]
    [Min(0f)]
    [SerializeField] private float tailLength = 30f;
    [Tooltip("Cuánto se tuerce la punta de la cola (negativo = hacia atrás). Torcida queda más de cómic.")]
    [SerializeField] private float tailSkew = -14f;

    [Header("Contorno y sombra")]
    [Tooltip("Color del contorno.")]
    [SerializeField] private Color outlineColor = new Color(0.11f, 0.09f, 0.15f, 1f);
    [Tooltip("Grosor del contorno. 0 = sin contorno.")]
    [Min(0f)]
    [SerializeField] private float outlineWidth = 5f;
    [Tooltip("Color de la sombra dura.")]
    [SerializeField] private Color shadowColor = new Color(0.36f, 0.16f, 0.52f, 0.95f);
    [Tooltip("Desplazamiento de la sombra. (0, 0) = sin sombra.")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(7f, -7f);

    [Header("Franja de color")]
    [Tooltip("Ancho de la franja del borde izquierdo. 0 = sin franja.")]
    [Min(0f)]
    [SerializeField] private float accentWidth = 0f;
    [Tooltip("Color de la franja (lo cambia BartenderSpeech con el color de la botella).")]
    [SerializeField] private Color accentColor = Color.white;

    private const int CornerSegments = 8;

    /// <summary>Configura todo de una vez (lo usa BartenderSpeech al construir el bocadillo).</summary>
    public void Configure(float radius, TailSide side, float position, float width, float length, float skew,
        Color outline, float outlineSize, Color shadow, Vector2 shadowShift)
    {
        cornerRadius = radius;
        tailSide = side;
        tailPosition = position;
        tailWidth = width;
        tailLength = length;
        tailSkew = skew;
        outlineColor = outline;
        outlineWidth = outlineSize;
        shadowColor = shadow;
        shadowOffset = shadowShift;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public float TailLength => tailSide == TailSide.None ? 0f : tailLength;
    public float TailPosition => tailPosition;
    public float OutlineWidth => outlineWidth;

    public void SetAccent(float width, Color color)
    {
        accentWidth = width;
        accentColor = color;
        SetVerticesDirty();
    }

    public void SetTailPosition(float position)
    {
        tailPosition = Mathf.Clamp01(position);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float radius = Mathf.Min(cornerRadius, r.width * 0.5f, r.height * 0.5f);
        float alpha = color.a;

        // De atrás hacia delante: sombra, contorno, relleno. El relleno de la cola tapa el contorno del cuerpo en la
        // unión, así la cola sale "pegada" sin raya.
        if (shadowOffset != Vector2.zero && shadowColor.a > 0f)
        {
            Color shadow = shadowColor;
            shadow.a *= alpha;
            AddShape(vh, r, radius, outlineWidth, shadowOffset, shadow);
        }
        if (outlineWidth > 0f)
        {
            Color outline = outlineColor;
            outline.a *= alpha;
            AddShape(vh, r, radius, outlineWidth, Vector2.zero, outline);
        }
        AddShape(vh, r, radius, 0f, Vector2.zero, color);

        if (accentWidth > 0f)
        {
            // Franja pegada al borde izquierdo, dentro del cuerpo, con las esquinas redondeadas del propio bocadillo.
            Color accent = accentColor;
            accent.a *= alpha;
            float w = Mathf.Min(accentWidth, r.width * 0.5f);
            AddRoundedRect(vh, new Rect(r.xMin, r.yMin, w + radius, r.height), radius, 0f, Vector2.zero, accent, w);
        }
    }

    private void AddShape(VertexHelper vh, Rect r, float radius, float grow, Vector2 offset, Color c)
    {
        AddRoundedRect(vh, r, radius, grow, offset, c, -1f);
        AddTail(vh, r, grow, offset, c);
    }

    /// <summary>Rectángulo redondeado. clipRight >= 0 recorta por la derecha a esa distancia del borde izquierdo
    /// (para la franja de color).</summary>
    private static void AddRoundedRect(VertexHelper vh, Rect r, float radius, float grow, Vector2 offset, Color c, float clipRight)
    {
        float xMin = r.xMin - grow + offset.x, xMax = r.xMax + grow + offset.x;
        float yMin = r.yMin - grow + offset.y, yMax = r.yMax + grow + offset.y;
        float rad = radius + grow;
        float clip = clipRight >= 0f ? xMin + clipRight + grow : float.MaxValue;

        // Abanico desde el centro con el contorno completo (esquinas incluidas): una sola pieza, sin costuras.
        int start = vh.currentVertCount;
        Vector2 center = new Vector2((xMin + Mathf.Min(xMax, clip)) * 0.5f, (yMin + yMax) * 0.5f);
        vh.AddVert(center, c, Vector4.zero);

        Vector2[] corners =
        {
            new Vector2(xMax - rad, yMax - rad), // arriba-derecha
            new Vector2(xMin + rad, yMax - rad), // arriba-izquierda
            new Vector2(xMin + rad, yMin + rad), // abajo-izquierda
            new Vector2(xMax - rad, yMin + rad)  // abajo-derecha
        };
        int count = 0;
        for (int k = 0; k < 4; k++)
        {
            for (int s = 0; s <= CornerSegments; s++)
            {
                float a = (k * 90f + s * 90f / CornerSegments) * Mathf.Deg2Rad;
                Vector2 p = corners[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                p.x = Mathf.Min(p.x, clip);
                vh.AddVert(p, c, Vector4.zero);
                count++;
            }
        }
        for (int i = 0; i < count; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
    }

    private void AddTail(VertexHelper vh, Rect r, float grow, Vector2 offset, Color c)
    {
        if (tailSide == TailSide.None || tailLength <= 0f) return;

        // La base entra un poco en el cuerpo (más que el contorno) para que el relleno tape la unión.
        float inset = outlineWidth + 2f;
        float half = tailWidth * 0.5f + grow;
        Vector2 a, b, tip;
        if (tailSide == TailSide.Bottom)
        {
            float x = Mathf.Lerp(r.xMin + tailWidth, r.xMax - tailWidth, tailPosition);
            a = new Vector2(x - half, r.yMin + inset);
            b = new Vector2(x + half, r.yMin + inset);
            tip = new Vector2(x + tailSkew, r.yMin - tailLength - grow * 1.6f);
        }
        else
        {
            float y = Mathf.Lerp(r.yMin + tailWidth, r.yMax - tailWidth, tailPosition);
            a = new Vector2(r.xMin + inset, y + half);
            b = new Vector2(r.xMin + inset, y - half);
            tip = new Vector2(r.xMin - tailLength - grow * 1.6f, y + tailSkew);
        }

        // Lados un poco curvados (curva cuadrática): queda más de dibujo que un triángulo recto.
        const int steps = 6;
        Vector2 midA = Vector2.Lerp(a, tip, 0.5f) + Perp(a, tip) * (tailWidth * 0.12f);
        Vector2 midB = Vector2.Lerp(b, tip, 0.5f) - Perp(b, tip) * (tailWidth * 0.12f);
        int start = vh.currentVertCount;
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            vh.AddVert(Bezier(a, midA, tip, t) + offset, c, Vector4.zero);
            vh.AddVert(Bezier(b, midB, tip, t) + offset, c, Vector4.zero);
        }
        for (int s = 0; s < steps; s++)
        {
            int i = start + s * 2;
            vh.AddTriangle(i, i + 1, i + 3);
            vh.AddTriangle(i, i + 3, i + 2);
        }
    }

    private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static Vector2 Perp(Vector2 from, Vector2 to)
    {
        Vector2 d = (to - from).normalized;
        return new Vector2(-d.y, d.x);
    }
}
