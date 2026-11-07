using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Luces de cartel roto: cada bombilla (o cada círculo de YOU LOST) parpadea por su cuenta. Unas están fundidas (apagadas,
/// con algún chispazo de vez en cuando), otras parpadean sin parar como si fallara el contacto, y el resto están
/// encendidas y de vez en cuando pegan un parpadeo corto.
///
/// En el FBX todas las bombillas son UNA sola malla, así que al empezar este script la separa en sus piezas sueltas
/// (cada bombilla, cada círculo) y les da vida por separado. Para poder leer la malla, el FBX necesita
/// Read/Write activado (pestaña Model). Las piezas que se tocan (bombilla + casquillo) se tratan como una sola.
///
/// Va en el modelo del cartel (o en cualquier padre). Usa el tiempo real: parpadea aunque el juego esté parado.
/// El brillo se cambia con un MaterialPropertyBlock (_BaseColor), así que no crea materiales nuevos.
/// </summary>
public class BrokenSignLights : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Serializable]
    public class LightGroup
    {
        [Tooltip("Malla con las luces (BOLAS, CIRCULOS...). Se separa en sus piezas sueltas.")]
        public MeshRenderer renderer;
        [Tooltip("Parte de las luces que están fundidas (casi siempre apagadas). 0.2 = una de cada cinco.")]
        [Range(0f, 1f)] public float deadChance = 0.2f;
        [Tooltip("Parte de las luces que parpadean sin parar (contacto roto). 0.25 = una de cada cuatro.")]
        [Range(0f, 1f)] public float flickerChance = 0.25f;
    }

    private enum LightState { Steady, Flicker, Dead }

    private class LightPart
    {
        public Renderer Renderer;
        public LightState State;
        public bool On;
        public float NextChange;
    }

    [Header("Luces")]
    [SerializeField] private LightGroup[] groups = new LightGroup[0];
    [Tooltip("Piezas a menos de esta distancia (en metros del modelo) cuentan como la misma luz (bombilla + casquillo). 0.02 = 2 cm.")]
    [Min(0f)]
    [SerializeField] private float mergeDistance = 0.02f;

    [Header("Brillo")]
    [Tooltip("Brillo de una luz encendida respecto a su textura. 1.5 = más clara que la textura; con Bloom, brilla.")]
    [Min(0f)]
    [SerializeField] private float onBrightness = 1.5f;
    [Tooltip("Brillo de una luz apagada. 0.25 = oscura pero se ve la bombilla.")]
    [Min(0f)]
    [SerializeField] private float offBrightness = 0.25f;

    [Header("Ritmo")]
    [Tooltip("Segundos mínimo y máximo entre cambios de una luz que parpadea. (0.04, 0.25) = nervioso, como un contacto roto.")]
    [SerializeField] private Vector2 flickerSeconds = new Vector2(0.04f, 0.25f);
    [Tooltip("Segundos mínimo y máximo que una luz buena se queda encendida antes de un parpadeo corto.")]
    [SerializeField] private Vector2 steadySeconds = new Vector2(1.5f, 5f);
    [Tooltip("Segundos mínimo y máximo que una luz fundida se queda apagada antes de un chispazo.")]
    [SerializeField] private Vector2 deadSeconds = new Vector2(3f, 9f);
    [Tooltip("Segundos que dura un parpadeo corto o un chispazo. 0.06 = un destello.")]
    [Min(0.01f)]
    [SerializeField] private float blinkSeconds = 0.06f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuántas luces ha encontrado en cada malla.")]
    [SerializeField] private bool showDebug;

    private readonly List<LightPart> parts = new List<LightPart>();
    private MaterialPropertyBlock block;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        foreach (LightGroup group in groups)
        {
            if (group == null || group.renderer == null) continue;
            List<Renderer> pieces = SplitIntoPieces(group.renderer);
            foreach (Renderer piece in pieces)
                parts.Add(CreatePart(piece, group));
            if (showDebug) Debug.Log($"{name}: {group.renderer.name} separado en {pieces.Count} luces.", this);
        }
    }

    private LightPart CreatePart(Renderer piece, LightGroup group)
    {
        float roll = UnityEngine.Random.value;
        var state = roll < group.deadChance ? LightState.Dead
                  : roll < group.deadChance + group.flickerChance ? LightState.Flicker
                  : LightState.Steady;
        var part = new LightPart { Renderer = piece, State = state, On = state != LightState.Dead };
        part.NextChange = Time.unscaledTime + UnityEngine.Random.Range(0f, 1f); // cada una empieza a su ritmo
        Apply(part);
        return part;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        foreach (LightPart part in parts)
        {
            if (now < part.NextChange) continue;

            switch (part.State)
            {
                case LightState.Flicker:
                    part.On = !part.On;
                    part.NextChange = now + RandomIn(flickerSeconds);
                    break;
                case LightState.Steady:
                    // Encendida mucho rato, apagada un instante.
                    part.On = !part.On;
                    part.NextChange = now + (part.On ? RandomIn(steadySeconds) : blinkSeconds);
                    break;
                case LightState.Dead:
                    // Apagada mucho rato, chispazo de un instante.
                    part.On = !part.On;
                    part.NextChange = now + (part.On ? blinkSeconds : RandomIn(deadSeconds));
                    break;
            }
            Apply(part);
        }
    }

    private void Apply(LightPart part)
    {
        float brightness = part.On ? onBrightness : offBrightness;
        part.Renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(brightness, brightness, brightness, 1f));
        part.Renderer.SetPropertyBlock(block);
    }

    private static float RandomIn(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

    // ---------------- SEPARAR LA MALLA EN PIEZAS ----------------

    /// <summary>
    /// Crea un objeto hijo por cada pieza suelta de la malla (con su mismo material) y oculta la malla original.
    /// Dos triángulos son de la misma pieza si comparten un vértice (por posición). Las piezas muy juntas se unen.
    /// </summary>
    private List<Renderer> SplitIntoPieces(MeshRenderer source)
    {
        var result = new List<Renderer>();
        MeshFilter filter = source.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null) return result;
        if (!mesh.isReadable)
        {
            Debug.LogWarning($"{name}: la malla de {source.name} no se puede leer. Activa Read/Write en la pestaña Model del FBX.", this);
            return result;
        }

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles;

        // 1) Vértices en la misma posición = mismo punto (las mallas duplican vértices en las costuras).
        var parent = new int[vertices.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        var byPosition = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000f);
            if (byPosition.TryGetValue(key, out int other)) Union(parent, i, other);
            else byPosition.Add(key, i);
        }

        // 2) Los tres vértices de cada triángulo son de la misma pieza.
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Union(parent, triangles[t], triangles[t + 1]);
            Union(parent, triangles[t], triangles[t + 2]);
        }

        // 3) Triángulos agrupados por pieza, y su caja para unir piezas pegadas.
        var pieceTriangles = new Dictionary<int, List<int>>();
        var pieceBounds = new Dictionary<int, Bounds>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int root = Find(parent, triangles[t]);
            if (!pieceTriangles.TryGetValue(root, out List<int> list))
            {
                list = new List<int>();
                pieceTriangles.Add(root, list);
                pieceBounds.Add(root, new Bounds(vertices[triangles[t]], Vector3.zero));
            }
            Bounds bounds = pieceBounds[root];
            for (int k = 0; k < 3; k++)
            {
                list.Add(triangles[t + k]);
                bounds.Encapsulate(vertices[triangles[t + k]]);
            }
            pieceBounds[root] = bounds;
        }

        // 4) Piezas que se tocan (o casi) = una sola luz.
        var roots = new List<int>(pieceTriangles.Keys);
        var lightOf = new Dictionary<int, int>();
        foreach (int root in roots) lightOf[root] = root;
        for (int a = 0; a < roots.Count; a++)
        {
            Bounds boundsA = pieceBounds[roots[a]];
            boundsA.Expand(mergeDistance * 2f);
            for (int b = a + 1; b < roots.Count; b++)
                if (boundsA.Intersects(pieceBounds[roots[b]]))
                    UnionLights(lightOf, roots[a], roots[b]);
        }

        var lights = new Dictionary<int, List<int>>();
        foreach (int root in roots)
        {
            int light = FindLight(lightOf, root);
            if (!lights.TryGetValue(light, out List<int> merged)) lights.Add(light, merged = new List<int>());
            merged.AddRange(pieceTriangles[root]);
        }

        // 5) Un objeto hijo por luz, con su trozo de malla.
        int index = 0;
        foreach (List<int> lightTriangles in lights.Values)
        {
            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUvs = new List<Vector2>();
            var newTriangles = new int[lightTriangles.Count];

            for (int i = 0; i < lightTriangles.Count; i++)
            {
                int old = lightTriangles[i];
                if (!remap.TryGetValue(old, out int fresh))
                {
                    fresh = newVertices.Count;
                    remap.Add(old, fresh);
                    newVertices.Add(vertices[old]);
                    if (normals.Length > old) newNormals.Add(normals[old]);
                    if (uvs.Length > old) newUvs.Add(uvs[old]);
                }
                newTriangles[i] = fresh;
            }

            var pieceMesh = new Mesh { name = $"{mesh.name}_Light{index}" };
            pieceMesh.SetVertices(newVertices);
            if (newNormals.Count == newVertices.Count) pieceMesh.SetNormals(newNormals);
            if (newUvs.Count == newVertices.Count) pieceMesh.SetUVs(0, newUvs);
            pieceMesh.SetTriangles(newTriangles, 0);
            pieceMesh.RecalculateBounds();

            var piece = new GameObject($"Light{index}", typeof(MeshFilter), typeof(MeshRenderer));
            piece.layer = source.gameObject.layer;
            piece.transform.SetParent(source.transform, false);
            piece.GetComponent<MeshFilter>().sharedMesh = pieceMesh;
            MeshRenderer pieceRenderer = piece.GetComponent<MeshRenderer>();
            pieceRenderer.sharedMaterials = source.sharedMaterials;
            pieceRenderer.shadowCastingMode = source.shadowCastingMode;
            result.Add(pieceRenderer);
            index++;
        }

        source.enabled = false; // la malla entera ya no se pinta: se pintan sus piezas
        return result;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int rootA = Find(parent, a), rootB = Find(parent, b);
        if (rootA != rootB) parent[rootA] = rootB;
    }

    private static int FindLight(Dictionary<int, int> lightOf, int i)
    {
        while (lightOf[i] != i)
        {
            lightOf[i] = lightOf[lightOf[i]];
            i = lightOf[i];
        }
        return i;
    }

    private static void UnionLights(Dictionary<int, int> lightOf, int a, int b)
    {
        int rootA = FindLight(lightOf, a), rootB = FindLight(lightOf, b);
        if (rootA != rootB) lightOf[rootA] = rootB;
    }
}
