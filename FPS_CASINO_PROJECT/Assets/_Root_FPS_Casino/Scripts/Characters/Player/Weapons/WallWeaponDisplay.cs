using TMPro;
using UnityEngine;

/// <summary>
/// Vitrina de premio para las armas de pared: al empezar la partida crea, a la medida de cada arma, un marco de neón
/// dorado pegado a la pared con el nombre del arma debajo, y un foco que la ilumina desde arriba. De lejos brilla
/// suave; al acercarte se enciende del todo, para que se vea que ahí hay algo que coger.
///
/// Va en el arma de pared (junto a WallWeaponPickup). No hay que colocar nada a mano: mide el modelo, busca la pared
/// que tiene detrás (con un rayo) y pone la placa encima, de cara al jugador. El nombre sale de WallWeaponPickup.
/// La placa se crea al darle a Play; con Show Debug ves en la vista Scene el tamaño que tendrá.
/// </summary>
[RequireComponent(typeof(WallWeaponPickup))]
public class WallWeaponDisplay : MonoBehaviour
{
    private const float CanvasScale = 0.001f; // 1 unidad del Canvas = 1 mm

    [Header("Marco")]
    [Tooltip("Color del neón.")]
    [SerializeField] private Color frameColor = new Color(1f, 0.78f, 0.35f);
    [Tooltip("Margen entre el arma y el marco, en metros. 0.12 = 12 cm alrededor.")]
    [Min(0f)]
    [SerializeField] private float padding = 0.12f;
    [Tooltip("Grosor del tubo de neón, en metros. 0.012 = 1,2 cm.")]
    [Min(0f)]
    [SerializeField] private float lineWidth = 0.012f;
    [Tooltip("Radio de las esquinas, en metros.")]
    [Min(0f)]
    [SerializeField] private float cornerRadius = 0.04f;
    [Tooltip("Halo del neón hacia fuera, en metros.")]
    [Min(0f)]
    [SerializeField] private float glow = 0.05f;
    [Tooltip("Separación de la pared, en metros (para que no parpadee pegado a ella).")]
    [Min(0.001f)]
    [SerializeField] private float wallOffset = 0.01f;

    [Header("Nombre")]
    [Tooltip("Texto debajo del marco. Vacío = el nombre del arma de WallWeaponPickup.")]
    [SerializeField] private string labelOverride = "";
    [Tooltip("Fuente del nombre (Font_Montserrat SemiBold SDF...). Vacío = la de TextMeshPro por defecto.")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Material de la fuente (Font_Montserrat Lux...). Vacío = el de la fuente.")]
    [SerializeField] private Material fontMaterial;
    [Tooltip("Color del nombre.")]
    [SerializeField] private Color textColor = new Color(0.95f, 0.9f, 0.82f);
    [Tooltip("Alto de las letras, en metros. 0.08 = 8 cm.")]
    [Min(0.01f)]
    [SerializeField] private float textHeight = 0.08f;
    [Tooltip("Espacio entre letras (como Character Spacing de TextMeshPro). 15 = espaciado elegante.")]
    [SerializeField] private float characterSpacing = 15f;

    [Header("Al acercarse")]
    [Tooltip("Metros a los que la placa se enciende del todo.")]
    [Min(0.1f)]
    [SerializeField] private float nearDistance = 4f;
    [Tooltip("Brillo de la placa de lejos (0 = invisible, 1 = como de cerca). 0.45 = se ve pero no llama la atención.")]
    [Range(0f, 1f)]
    [SerializeField] private float farAlpha = 0.45f;
    [Tooltip("Rapidez del cambio de brillo. 6 = suave pero rápido.")]
    [Min(0.1f)]
    [SerializeField] private float fadeSpeed = 6f;

    [Header("Foco")]
    [Tooltip("Crear un foco encima del arma.")]
    [SerializeField] private bool addSpotLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.88f, 0.7f);
    [Tooltip("Intensidad del foco. 4 = ilumina el arma sin quemar.")]
    [Min(0f)]
    [SerializeField] private float lightIntensity = 4f;
    [Tooltip("Alcance del foco en metros.")]
    [Min(0.1f)]
    [SerializeField] private float lightRange = 4f;
    [Tooltip("Apertura del foco en grados.")]
    [Range(1f, 179f)]
    [SerializeField] private float spotAngle = 55f;
    [Tooltip("Metros por encima del arma a los que está el foco.")]
    [SerializeField] private float lightHeight = 1.2f;
    [Tooltip("Metros que sale el foco de la pared.")]
    [SerializeField] private float lightForward = 0.6f;

    [Header("Depuración")]
    [Tooltip("Dibuja en la vista Scene el marco que se va a crear y la dirección hacia la que mira (aunque no estés en Play).")]
    [SerializeField] private bool showDebug;

    private CanvasGroup plaqueGroup;
    private Transform viewer;
    private float alpha;

    private struct Layout
    {
        public Vector3 center;   // centro de la placa, sobre la pared
        public Vector3 facing;   // hacia dónde mira la placa (hacia fuera de la pared)
        public Vector3 up;       // arriba de la placa
        public Vector2 size;     // ancho y alto del marco, en metros
    }

    private void Start()
    {
        if (!TryGetLayout(out Layout layout))
        {
            Debug.LogWarning($"{name}: WallWeaponDisplay no encuentra el modelo del arma (ningún Renderer en sus hijos).", this);
            return;
        }

        CreatePlaque(layout);
        if (addSpotLight) CreateSpotLight(layout);
        alpha = farAlpha;
        if (plaqueGroup != null) plaqueGroup.alpha = alpha;
    }

    private void Update()
    {
        if (plaqueGroup == null) return;

        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        bool near = viewer != null && (viewer.position - plaqueGroup.transform.position).sqrMagnitude <= nearDistance * nearDistance;

        alpha = Mathf.MoveTowards(alpha, near ? 1f : farAlpha, fadeSpeed * Time.deltaTime);
        plaqueGroup.alpha = alpha;
    }

    // ---------------- MEDIR Y COLOCAR ----------------

    /// <summary>
    /// Mide el modelo en los ejes del arma: el eje más fino es el que apunta a la pared; de los otros dos, el que más
    /// se parece a "arriba" es el alto. Luego busca la pared con un rayo a cada lado para pegar la placa a ella.
    /// </summary>
    private bool TryGetLayout(out Layout layout)
    {
        layout = default(Layout);
        bool any = false;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds local = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            any = true;
        }
        if (!any) return false;

        // Tamaño real (en metros) de cada eje del arma.
        Vector3 localSize = max - min;
        Vector3[] axes = { transform.right, transform.up, transform.forward };
        float[] meters =
        {
            localSize.x * transform.lossyScale.x, localSize.y * transform.lossyScale.y, localSize.z * transform.lossyScale.z
        };
        for (int i = 0; i < 3; i++) meters[i] = Mathf.Abs(meters[i]);

        int depthAxis = 0;
        for (int i = 1; i < 3; i++) if (meters[i] < meters[depthAxis]) depthAxis = i;
        int a = (depthAxis + 1) % 3, b = (depthAxis + 2) % 3;
        int heightAxis = Mathf.Abs(Vector3.Dot(axes[a], Vector3.up)) >= Mathf.Abs(Vector3.Dot(axes[b], Vector3.up)) ? a : b;
        int widthAxis = heightAxis == a ? b : a;

        Vector3 center = transform.TransformPoint((min + max) * 0.5f);
        Vector3 normal = axes[depthAxis].normalized;

        // ¿A qué lado está la pared? El primero que choque con algo que no sea el propio arma.
        Vector3 facing = normal;
        Vector3 surface = center - normal * (meters[depthAxis] * 0.5f);
        float best = float.PositiveInfinity;
        foreach (Vector3 dir in new[] { normal, -normal })
        {
            foreach (RaycastHit hit in Physics.RaycastAll(center, dir, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance;
                facing = -dir;
                surface = hit.point;
            }
        }

        Vector3 up = Vector3.ProjectOnPlane(axes[heightAxis], facing).normalized;
        if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;

        layout.facing = facing;
        layout.up = up;
        // Centro del arma llevado al plano de la pared, un pelín por delante de ella.
        layout.center = center + facing * (Vector3.Dot(surface - center, facing) + wallOffset);
        layout.size = new Vector2(meters[widthAxis] + padding * 2f, meters[heightAxis] + padding * 2f);
        return true;
    }

    // ---------------- CREAR ----------------

    private void CreatePlaque(Layout layout)
    {
        var root = new GameObject("WeaponPlaque", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        var rect = (RectTransform)root.transform;

        // El Canvas se ve de frente mirando por su eje Z: su Z apunta hacia la pared.
        rect.SetPositionAndRotation(layout.center, Quaternion.LookRotation(-layout.facing, layout.up));
        Vector3 parentScale = transform.lossyScale;
        rect.localScale = new Vector3(CanvasScale / parentScale.x, CanvasScale / parentScale.y, CanvasScale / parentScale.z);
        rect.sizeDelta = layout.size / CanvasScale;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        plaqueGroup = root.GetComponent<CanvasGroup>();
        plaqueGroup.interactable = false;
        plaqueGroup.blocksRaycasts = false;

        // Marco de neón, a todo el tamaño de la placa.
        var frameObject = new GameObject("Frame", typeof(RectTransform));
        frameObject.layer = gameObject.layer;
        var frameRect = (RectTransform)frameObject.transform;
        frameRect.SetParent(rect, false);
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        NeonFrame frame = frameObject.AddComponent<NeonFrame>();
        frame.color = frameColor;
        frame.raycastTarget = false;
        frame.Configure(lineWidth / CanvasScale, cornerRadius / CanvasScale, glow / CanvasScale, glow * 0.4f / CanvasScale, 1f, false);

        // Nombre debajo del marco.
        string label = !string.IsNullOrEmpty(labelOverride) ? labelOverride : GetComponent<WallWeaponPickup>().DisplayName;
        if (string.IsNullOrEmpty(label)) return;

        var textObject = new GameObject("Name", typeof(RectTransform));
        textObject.layer = gameObject.layer;
        var textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -glow / CanvasScale);
        textRect.sizeDelta = new Vector2(Mathf.Max(layout.size.x, 1f) / CanvasScale, textHeight * 1.6f / CanvasScale);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        if (fontMaterial != null) text.fontSharedMaterial = fontMaterial;
        text.text = label.ToUpperInvariant();
        text.fontSize = textHeight / CanvasScale;
        text.characterSpacing = characterSpacing;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Top;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private void CreateSpotLight(Layout layout)
    {
        var lightObject = new GameObject("WeaponSpot");
        lightObject.transform.SetParent(transform, true);
        Vector3 position = layout.center + Vector3.up * lightHeight + layout.facing * lightForward;
        lightObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(layout.center - position));

        Light spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = lightColor;
        spot.intensity = lightIntensity;
        spot.range = lightRange;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = spotAngle * 0.5f;
        spot.shadows = LightShadows.None;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !TryGetLayout(out Layout layout)) return;

        Vector3 right = Vector3.Cross(layout.up, layout.facing).normalized;
        Vector3 hx = right * layout.size.x * 0.5f, hy = layout.up * layout.size.y * 0.5f;
        Gizmos.color = frameColor;
        Gizmos.DrawLine(layout.center - hx - hy, layout.center + hx - hy);
        Gizmos.DrawLine(layout.center + hx - hy, layout.center + hx + hy);
        Gizmos.DrawLine(layout.center + hx + hy, layout.center - hx + hy);
        Gizmos.DrawLine(layout.center - hx + hy, layout.center - hx - hy);
        Gizmos.DrawLine(layout.center, layout.center + layout.facing * 0.3f);
    }
#endif
}
