using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tubo de neón con forma de rectángulo de esquinas redondeadas, dibujado en un Canvas: el centro más blanco y un halo
/// suave por fuera y por dentro. Sirve para el borde del cartel de muerte y es la base de NeonButton (los botones).
/// - Neón viejo (opcional): de vez en cuando falla y pega unos parpadeos secos, como un tubo que está muriendo.
///
/// Va en un objeto del Canvas con RectTransform: el marco se dibuja alrededor de su rectángulo (se ve también fuera de
/// Play, para ajustarlo). El color del neón es el Color de este componente. Si es solo decoración, desmarca Raycast
/// Target para que no tape los clics. No usa texturas ni materiales. Va en tiempo real (funciona con el juego parado).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class NeonFrame : MaskableGraphic
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
    [Tooltip("Lo que se extiende el halo hacia dentro. 20 = un poco de luz sobre lo que rodea.")]
    [Min(0f)]
    [SerializeField] private float innerGlow = 20f;
    [Tooltip("Fuerza del halo respecto al tubo. 0.45 = halo suave.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowStrength = 0.45f;
    [Tooltip("Lo blanco que es el centro del tubo (como un neón de verdad). 0 = todo del color, 1 = centro blanco.")]
    [Range(0f, 1f)]
    [SerializeField] private float coreWhiteness = 0.55f;
    [Tooltip("Cuánto sale el marco del rectángulo: positivo = por fuera, negativo = por dentro.")]
    [SerializeField] private float padding = 0f;
    [Tooltip("Relleno de dentro del marco. Negro con un poco de alfa = oscurece lo de dentro. Alfa 0 = sin relleno (para el borde del cartel, déjalo en 0).")]
    [SerializeField] private Color fillColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Segmentos de cada esquina. Más = curva más suave.")]
    [Range(1, 16)]
    [SerializeField] private int cornerSegments = 8;

    [Header("Brillo")]
    [Tooltip("Brillo normal del neón. 0.55 = encendido a medias. 0.3 = leve.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleBrightness = 0.55f;
    [Tooltip("Rapidez de los cambios de brillo. 12 = rápido pero suave.")]
    [Min(0.1f)]
    [SerializeField] private float fadeSpeed = 12f;

    [Header("Neón viejo")]
    [Tooltip("De vez en cuando el neón falla: unos parpadeos rápidos. Desmarcado = brillo fijo.")]
    [SerializeField] private bool faulty = true;
    [Tooltip("Segundos mínimo y máximo entre fallos.")]
    [SerializeField] private Vector2 faultEverySeconds = new Vector2(2f, 7f);
    [Tooltip("Segundos mínimo y máximo que dura cada fallo.")]
    [SerializeField] private Vector2 faultSeconds = new Vector2(0.15f, 0.5f);
    [Tooltip("Brillo en los bajones del fallo, respecto al normal. 0.1 = casi se apaga.")]
    [Range(0f, 1f)]
    [SerializeField] private float faultDimTo = 0.1f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola lo que le pasa al neón (en los botones: cuándo entra el ratón y cuándo se pulsa).")]
    [SerializeField] protected bool showDebug;

    private float brightness = -1f; // -1 = aún sin calcular
    private float nextFault, faultEnd, nextFaultToggle;
    private bool faultDim;
    private readonly List<Vector2> outline = new List<Vector2>();
    private readonly List<Vector2> normals = new List<Vector2>();

    /// <summary>Brillo normal (sin ratón encima, sin fallos).</summary>
    protected float IdleBrightness => idleBrightness;

    /// <summary>Lo rápido que se suavizan los cambios este frame (0 = nada, 1 = de golpe).</summary>
    protected float Blend => 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);

    /// <summary>Brillo al que tiende ahora mismo. NeonButton lo sube con el ratón encima.</summary>
    protected virtual float TargetBrightness() => idleBrightness;

    /// <summary>Si ahora puede fallar. NeonButton no falla con el ratón encima.</summary>
    protected virtual bool CanFault() => true;

    protected override void OnEnable()
    {
        base.OnEnable();
        brightness = idleBrightness;
        nextFault = Time.unscaledTime + Random.Range(faultEverySeconds.x, faultEverySeconds.y);
        SetVerticesDirty();
    }

    protected virtual void Update()
    {
        float next = Mathf.Lerp(brightness, TargetBrightness(), Blend);

        // Neón viejo: parpadeos secos (sin transición).
        float now = Time.unscaledTime;
        if (faulty && CanFault() && Application.isPlaying)
        {
            if (now >= nextFault)
            {
                faultEnd = now + Random.Range(faultSeconds.x, faultSeconds.y);
                nextFault = faultEnd + Random.Range(faultEverySeconds.x, faultEverySeconds.y);
                nextFaultToggle = now;
                if (showDebug) Debug.Log($"{name}: el neón falla.", this);
            }
            if (now < faultEnd)
            {
                if (now >= nextFaultToggle)
                {
                    faultDim = !faultDim;
                    nextFaultToggle = now + Random.Range(0.03f, 0.09f);
                }
                next = faultDim ? idleBrightness * faultDimTo : idleBrightness;
            }
            else faultDim = false;
        }

        if (Mathf.Abs(next - brightness) > 0.002f)
        {
            brightness = next;
            SetVerticesDirty();
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
