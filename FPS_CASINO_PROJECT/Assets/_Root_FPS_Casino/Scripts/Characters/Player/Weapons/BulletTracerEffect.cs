using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Trazador de bala: una estela de luz que sale del cañón y viaja muy rápido hasta el punto de impacto
/// (o hasta el alcance máximo si no golpea nada), y se apaga al llegar. Es solo visual: el daño ya lo ha
/// hecho el raycast del arma en el momento del disparo.
///
/// Dos capas, como en los shooters actuales:
/// - Halo: la línea de este objeto, ancha y del color del arma.
/// - Núcleo: la línea del hijo Core Line, fina y casi blanca (el "metal caliente").
/// El color es HDR: con Bloom en la escena brilla con halo de luz.
///
/// Un color por arma: cada arma tiene su variante de este prefab (PF_BulletTracer_Shotgun...) con su
/// color, y se asigna en el Tracer Prefab de su HitscanWeapon.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BulletTracerEffect : MonoBehaviour
{
    [Header("Color")]
    [Tooltip("Color del arma. Es HDR: sube la Intensity (2-4) para que brille con Bloom. Naranja = Lanzacartas, rojo = escopeta...")]
    [ColorUsage(true, true)]
    [SerializeField] private Color tracerColor = new Color(2f, 1.5f, 0.5f, 1f); // amarillo cálido brillante
    [Tooltip("Cuánto se acerca el núcleo al blanco. 0 = del mismo color que el halo, 0.8 = casi blanco (recomendado).")]
    [Range(0f, 1f)]
    [SerializeField] private float coreWhiteness = 0.8f;

    [Header("Forma")]
    [Tooltip("Grosor del halo en metros, en la cabeza de la estela. Entre 0.02 y 0.05 queda bien.")]
    [FormerlySerializedAs("width")]
    [Min(0f)]
    [SerializeField] private float widthMeters = 0.03f;
    [Tooltip("Grosor del núcleo respecto al halo. 0.3 = un tercio.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float coreWidthFraction = 0.3f;
    [Tooltip("Largo de la estela en metros. 2 = bala de pistola; 1 = perdigón; 4 = sniper.")]
    [Min(0.05f)]
    [SerializeField] private float streakLengthMeters = 2f;

    [Header("Movimiento")]
    [Tooltip("Velocidad de la estela en metros por segundo. 250 = rápida pero se ve viajar; 600 = casi instantánea.")]
    [Min(1f)]
    [SerializeField] private float speedMetersPerSecond = 250f;
    [Tooltip("Segundos que tarda en apagarse al llegar al final. 0.05 = se apaga casi al momento.")]
    [FormerlySerializedAs("visibleSeconds")]
    [FormerlySerializedAs("duration")]
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 0.05f;

    [Header("Referencias")]
    [Tooltip("Line Renderer hijo para el núcleo blanco. Vacío = solo halo (como el trazador antiguo).")]
    [SerializeField] private LineRenderer coreLine;

    private LineRenderer glowLine;
    private Vector3 start;
    private Vector3 direction;
    private float totalDistance;
    private float travelled;
    private float fadeTimer = -1f; // -1 = todavía viajando

    private void Awake()
    {
        glowLine = GetComponent<LineRenderer>();
        Setup(glowLine, tracerColor);

        if (coreLine != null)
            Setup(coreLine, Color.Lerp(tracerColor, Color.white * Mathf.Max(1f, tracerColor.maxColorComponent), coreWhiteness));
    }

    /// <summary>Las dos líneas: dos puntos en el mundo, de la cola (transparente) a la cabeza (opaca).</summary>
    private static void Setup(LineRenderer line, Color hdrColor)
    {
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startColor = new Color(1f, 1f, 1f, 0f); // cola: se desvanece
        line.endColor = Color.white;                 // cabeza: opaca

        // El color HDR va en el material (los colores de los vértices no pasan de 1 y no brillarían).
        // Al usar .material cada trazador tiene su copia: así cada arma puede tener su color.
        line.material.color = hdrColor;
    }

    public void Play(Vector3 from, Vector3 to)
    {
        start = from;
        Vector3 path = to - from;
        totalDistance = path.magnitude;
        direction = totalDistance > 0.0001f ? path / totalDistance : Vector3.forward;
        travelled = 0f;
        fadeTimer = -1f;

        UpdateLines(1f);
    }

    private void Update()
    {
        if (fadeTimer < 0f)
        {
            travelled += speedMetersPerSecond * Time.deltaTime;
            if (travelled >= totalDistance)
            {
                travelled = totalDistance;
                fadeTimer = fadeSeconds; // ha llegado: empieza a apagarse
            }
            UpdateLines(1f);
            return;
        }

        fadeTimer -= Time.deltaTime;
        UpdateLines(Mathf.Clamp01(fadeTimer / fadeSeconds));

        if (fadeTimer <= 0f)
            Destroy(gameObject);
    }

    /// <summary>Coloca la estela: la cabeza donde va la bala y la cola Streak Length metros por detrás.</summary>
    private void UpdateLines(float thickness01)
    {
        float head = travelled;
        float tail = Mathf.Max(0f, head - streakLengthMeters);
        Vector3 headPoint = start + direction * head;
        Vector3 tailPoint = start + direction * tail;

        SetLine(glowLine, tailPoint, headPoint, widthMeters * thickness01);
        if (coreLine != null)
            SetLine(coreLine, tailPoint, headPoint, widthMeters * coreWidthFraction * thickness01);
    }

    private static void SetLine(LineRenderer line, Vector3 tail, Vector3 head, float width)
    {
        line.SetPosition(0, tail);
        line.SetPosition(1, head);
        line.startWidth = width * 0.3f; // la cola, más fina
        line.endWidth = width;
    }
}
