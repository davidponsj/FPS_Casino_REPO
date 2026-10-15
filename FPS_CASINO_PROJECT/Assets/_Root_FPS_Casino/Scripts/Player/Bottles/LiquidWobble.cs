using UnityEngine;

/// <summary>
/// Hace que el líquido de una botella se mueva como líquido de verdad. No simula fluidos
/// (sería carísimo para un objeto en la mano): mueve un "plano de superficie" que el shader
/// Shader Graph "SG_Liquid" usa para recortar la malla del líquido.
///
/// - La superficie siempre se queda horizontal en el mundo: si inclinas la botella, el líquido
///   no se inclina con ella (como en la realidad).
/// - Al moverte, girar o parar de golpe, la superficie se inclina en contra de la aceleración
///   (inercia) y vuelve a su sitio oscilando con un muelle amortiguado: el típico "chapoteo".
///
/// Va en el objeto del LÍQUIDO (el que tiene el material SG_Liquid), con el pivote en el centro
/// del volumen del líquido. Usa un MaterialPropertyBlock, así que todas las botellas comparten
/// un único material y cada una tiene su propio color, nivel y movimiento.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class LiquidWobble : MonoBehaviour
{
    [Header("Líquido")]
    [SerializeField] private Color liquidColor = new Color(0.2f, 0.8f, 0.3f, 1f);
    [Tooltip("Color de la superficie (la 'tapa' del líquido que se ve desde arriba). Normalmente un poco más claro.")]
    [SerializeField] private Color surfaceColor = new Color(0.4f, 1f, 0.5f, 1f);
    [Tooltip("0 = vacía, 1 = llena hasta arriba de la malla del líquido.")]
    [Range(0f, 1f)]
    [SerializeField] private float fillAmount = 0.7f;

    [Header("Movimiento")]
    [Tooltip("Cuánto se inclina la superficie por cada m/s² de aceleración. Más alto = más chapoteo.")]
    [SerializeField] private float inertia = 0.02f;
    [Tooltip("Cuánto se inclina la superficie al girar la botella (rad/s).")]
    [SerializeField] private float rotationInfluence = 0.015f;
    [Tooltip("Rigidez del muelle: más alto = oscila más rápido.")]
    [SerializeField] private float springStiffness = 60f;
    [Tooltip("Amortiguación: más alto = deja de oscilar antes. Bajo = líquido 'aguado' que se mueve mucho.")]
    [SerializeField] private float springDamping = 3f;
    [Tooltip("Inclinación máxima de la superficie (pendiente). 0.6 ≈ 30 grados.")]
    [SerializeField] private float maxWobble = 0.6f;

    private static readonly int LiquidColorId = Shader.PropertyToID("_LiquidColor");
    private static readonly int SurfaceColorId = Shader.PropertyToID("_SurfaceColor");
    private static readonly int FillHeightId = Shader.PropertyToID("_FillHeight");
    private static readonly int WobbleXId = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZId = Shader.PropertyToID("_WobbleZ");

    private Renderer liquidRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float halfHeight;

    private Vector3 lastPosition;
    private Vector3 lastVelocity;
    private Quaternion lastRotation;

    // Inclinación de la superficie (x, z) y su velocidad: el estado del muelle.
    private Vector2 wobble;
    private Vector2 wobbleVelocity;

    /// <summary>Nivel de llenado 0-1. Útil para cuando el jugador beba.</summary>
    public float FillAmount
    {
        get => fillAmount;
        set => fillAmount = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Cambia el color del líquido. La superficie se calcula sola, un poco más clara.
    /// Lo usan BottleHolder (botella en la mano) y ThrownBottle (botella lanzada).
    /// </summary>
    public void SetLiquidColor(Color color)
    {
        color.a = 1f;
        liquidColor = color;
        surfaceColor = Color.Lerp(color, Color.white, 0.35f);
    }

    private void Awake()
    {
        liquidRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        // La altura del líquido en metros: los bounds de la malla (locales) por la escala.
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        float meshHeight = meshFilter != null ? meshFilter.sharedMesh.bounds.size.y : 1f;
        halfHeight = meshHeight * transform.lossyScale.y * 0.5f;

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void OnEnable()
    {
        // Al aparecer (equipar la botella, por ejemplo) no queremos un chapoteo por el "salto" de posición.
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastVelocity = Vector3.zero;
        wobble = Vector2.zero;
        wobbleVelocity = Vector2.zero;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Velocidad y aceleración de la botella (LateUpdate: ya se ha movido la cámara/mano este frame).
        Vector3 velocity = (transform.position - lastPosition) / dt;
        Vector3 acceleration = (velocity - lastVelocity) / dt;

        // Velocidad angular aproximada a partir del cambio de rotación.
        Quaternion delta = transform.rotation * Quaternion.Inverse(lastRotation);
        delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
        if (angleDegrees > 180f) angleDegrees -= 360f;
        Vector3 angularVelocity = float.IsNaN(axis.x) ? Vector3.zero : axis * (angleDegrees * Mathf.Deg2Rad / dt);

        // El líquido se queda "atrás": si aceleras hacia +X, la superficie sube por el lado -X.
        // En el shader la superficie es "y + x*WobbleX + z*WobbleZ <= nivel", así que un WobbleX
        // positivo baja el lado +X y sube el -X: por eso aquí la aceleración va con signo +.
        // Girar sobre Z empuja la superficie en X y girar sobre X la empuja en Z.
        Vector2 force = new Vector2(
            acceleration.x * inertia - angularVelocity.z * rotationInfluence,
            acceleration.z * inertia + angularVelocity.x * rotationInfluence);

        // Muelle amortiguado (igual que el "dip" de aterrizaje del PlayerController):
        // la fuerza lo empuja, la rigidez lo devuelve al centro y la amortiguación frena la oscilación.
        Vector2 springAccel = -springStiffness * wobble - springDamping * wobbleVelocity;
        wobbleVelocity += (springAccel + force * springStiffness) * dt;
        wobble += wobbleVelocity * dt;
        wobble = Vector2.ClampMagnitude(wobble, maxWobble);

        lastPosition = transform.position;
        lastVelocity = velocity;
        lastRotation = transform.rotation;

        ApplyToMaterial();
    }

    private void ApplyToMaterial()
    {
        liquidRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(LiquidColorId, liquidColor);
        propertyBlock.SetColor(SurfaceColorId, surfaceColor);
        // El shader compara alturas respecto al pivote (centro del líquido): de -halfHeight a +halfHeight.
        propertyBlock.SetFloat(FillHeightId, Mathf.Lerp(-halfHeight, halfHeight, fillAmount));
        propertyBlock.SetFloat(WobbleXId, wobble.x);
        propertyBlock.SetFloat(WobbleZId, wobble.y);
        liquidRenderer.SetPropertyBlock(propertyBlock);
    }
}
