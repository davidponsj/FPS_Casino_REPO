using UnityEngine;

/// <summary>
/// Lo que comparten las cinco IAs: acceso al movimiento, al objetivo (el jugador)
/// y la etiqueta de depuración. Es abstracta: en el prefab se añade la IA concreta
/// (HunterAI, PatrollerAI...), que hereda de ella.
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public abstract class EnemyBase : MonoBehaviour
{
    private static Transform cachedPlayer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

    [Header("Visual")]
    [Tooltip("Renderer del cuerpo, para los cambios de color por estado. Vacío = el primero que encuentre.")]
    [SerializeField] private Renderer bodyRenderer;

    private MaterialPropertyBlock propertyBlock;
    private Color baseColor = Color.white;

    public EnemyMovement Movement { get; private set; }
    public Transform Target { get; protected set; }

    /// <summary>Nombre del estado actual, para depuración.</summary>
    public abstract string CurrentStateName { get; }

    public float DistanceToTarget =>
        Target == null ? float.PositiveInfinity : FlatDistance(transform.position, Target.position);

    protected virtual void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        SetupVisual();
    }

    protected virtual void Start()
    {
        Target = FindPlayer();

        if (Target == null)
            Debug.LogWarning($"{name}: no se encontró ningún objeto con el tag Player.", this);
    }

    /// <summary>
    /// Busca al jugador una sola vez y lo comparte entre todos los enemigos
    /// (con 30 enemigos, una búsqueda en vez de 30).
    /// </summary>
    public static Transform FindPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) cachedPlayer = player.transform;
        }

        return cachedPlayer;
    }

    // ---------------------------------------------------------------
    //  Color del cuerpo (feedback visual de estado)
    // ---------------------------------------------------------------

    private void SetupVisual()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        if (bodyRenderer == null) return;

        propertyBlock = new MaterialPropertyBlock();

        Material material = bodyRenderer.sharedMaterial;
        if (material != null)
        {
            if (material.HasProperty(BaseColorId)) baseColor = material.GetColor(BaseColorId);
            else if (material.HasProperty(ColorId)) baseColor = material.GetColor(ColorId);
        }
    }

    /// <summary>Cambia el color del cuerpo sin crear materiales nuevos (MaterialPropertyBlock).</summary>
    protected void SetBodyColor(Color color)
    {
        if (bodyRenderer == null) return;

        bodyRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        bodyRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>Vuelve al color base (el del material, o el que haya fijado la IA con SetBaseBodyColor).</summary>
    protected void ResetBodyColor() => SetBodyColor(baseColor);

    /// <summary>Cambia el color base del cuerpo (p. ej. el color de un perfil) y lo aplica.</summary>
    protected void SetBaseBodyColor(Color color)
    {
        baseColor = color;
        ResetBodyColor();
    }

    /// <summary>Distancia en el plano horizontal (ignora la altura).</summary>
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

#if UNITY_EDITOR
    // Muestra el nombre del estado sobre la cabeza del enemigo.
    // En la vista Game hay que activar el botón "Gizmos" para verlo.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.4f, CurrentStateName);
    }
#endif
}