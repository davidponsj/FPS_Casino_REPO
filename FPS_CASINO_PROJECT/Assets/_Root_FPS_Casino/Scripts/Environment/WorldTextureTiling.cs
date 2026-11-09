using UnityEngine;

/// <summary>
/// Repite la textura del material según el tamaño REAL del objeto, para que todas las paredes (o suelos, techos...)
/// tengan el dibujo al mismo tamaño aunque cada una esté escalada distinto. Sin esto, un mismo material se estira:
/// en una pared de 9 m el dibujo sale estrecho y en una de 89 m, enorme.
///
/// Va en cada objeto con el material repetible (las paredes: Cubes escalados). Calcula cuántas veces cabe la textura
/// (Meters Per Tile) en el ancho y el alto de la cara principal y se lo da al material de ese objeto con un
/// MaterialPropertyBlock (no crea materiales nuevos; todas las paredes siguen compartiendo el mismo).
/// Funciona también fuera de Play: al escalar la pared en el editor, la textura se reajusta sola.
/// Solo vale para shaders URP que usan _BaseMap_ST (Lit, Simple Lit, Unlit): el Normal y el Metallic se repiten igual.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class WorldTextureTiling : MonoBehaviour
{
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    public enum Face { WidthHeight, WidthDepth, DepthHeight }

    [Tooltip("Metros que ocupa una repetición de la textura. 2 = el dibujo se repite cada 2 m, en todas las paredes igual.")]
    [Min(0.01f)]
    [SerializeField] private float metersPerTile = 2f;
    [Tooltip("Qué medidas del objeto usa la textura. Ancho y alto = paredes (X e Y). Ancho y fondo = suelos y techos (X y Z).")]
    [SerializeField] private Face face = Face.WidthHeight;
    [Tooltip("Desplaza el dibujo, en repeticiones. (0, 0.5) = media repetición hacia arriba (p. ej. para que el zócalo quede abajo).")]
    [SerializeField] private Vector2 offset = Vector2.zero;
    [Tooltip("Repetir solo en horizontal: el alto se ve UNA vez entero (para texturas con zócalo o moldura que no deben repetirse hacia arriba).")]
    [SerializeField] private bool stretchVertically;

    private Renderer cachedRenderer;
    private MeshFilter meshFilter;
    private MaterialPropertyBlock block;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Update()
    {
        // En el editor, si escalas la pared, la textura se reajusta. En el juego las paredes no cambian: no hace nada.
        if (!transform.hasChanged) return;
        transform.hasChanged = false;
        Apply();
    }

    private void Apply()
    {
        if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (cachedRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        // Tamaño real: el de la malla (un Cube mide 1, un Plane 10) por la escala del objeto.
        Vector3 meshSize = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.bounds.size : Vector3.one;
        Vector3 size = Vector3.Scale(meshSize, transform.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        Vector2 meters;
        switch (face)
        {
            case Face.WidthDepth: meters = new Vector2(size.x, size.z); break;
            case Face.DepthHeight: meters = new Vector2(size.z, size.y); break;
            default: meters = new Vector2(size.x, size.y); break;
        }

        float tilesX = meters.x / metersPerTile;
        float tilesY = stretchVertically ? 1f : meters.y / metersPerTile;

        cachedRenderer.GetPropertyBlock(block);
        block.SetVector(BaseMapStId, new Vector4(tilesX, tilesY, offset.x, offset.y));
        cachedRenderer.SetPropertyBlock(block);
    }
}
