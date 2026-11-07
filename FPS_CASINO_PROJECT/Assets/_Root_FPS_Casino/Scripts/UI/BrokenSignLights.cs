using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Luces de cartel roto: cada bombilla (o cada círculo de YOU LOST) va por su cuenta. Unas están fundidas (apagadas,
/// con algún chispazo de vez en cuando), otras fallan a ratos (una ráfaga corta de parpadeos y luego se quedan quietas
/// unos segundos, como un contacto flojo) y el resto están encendidas y de vez en cuando pegan un parpadeo corto.
///
/// Al aparecer el cartel todo empieza APAGADO y se enciende como un cartel viejo al darle a la corriente: las bombillas
/// (modo Aleatorio) arrancan cada una en un momento al azar dentro de Power On Seconds; las fundidas solo chisporrotean
/// un instante y se quedan apagadas.
/// Los grupos en modo Secuencia (las letras de YOU LOST y sus círculos) empiezan apagados y se encienden UNA A UNA al
/// aparecer el cartel, de izquierda a derecha y de arriba abajo, con un chisporroteo al arrancar como un neón. Si dos
/// grupos en Secuencia tienen las mismas piezas (7 letras y 7 círculos), cada letra se enciende a la vez que su círculo.
///
/// En el FBX todas las bombillas son UNA sola malla, así que al empezar este script la separa en sus piezas sueltas
/// (cada bombilla, cada círculo) y les da vida por separado. Para poder leer la malla, el FBX necesita
/// Read/Write activado (pestaña Model). Las piezas que se tocan (bombilla + casquillo) se tratan como una sola.
///
/// Va en el modelo del cartel (o en cualquier padre). Usa el tiempo real: parpadea aunque el juego esté parado.
/// Respeta el color del material (p. ej. cristal azul): apagada lo oscurece, encendida le suma brillo con la emisión
/// (el material tiene que tener Emission activada). Se cambia con un MaterialPropertyBlock: no crea materiales nuevos.
/// </summary>
public class BrokenSignLights : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Serializable]
    public class LightGroup
    {
        [Tooltip("Malla con las luces (BOLAS, CIRCULOS...). Se separa en sus piezas sueltas.")]
        public MeshRenderer renderer;
        [Tooltip("Parte de las luces que están fundidas (casi siempre apagadas). 0.2 = una de cada cinco.")]
        [Range(0f, 1f)] public float deadChance = 0.2f;
        [Tooltip("Parte de las luces que fallan a ratos (contacto flojo). 0.15 = una de cada siete.")]
        [Range(0f, 1f)] public float flickerChance = 0.15f;
        [Tooltip("Brillo de las luces encendidas de este grupo (color HDR: la intensidad hace que brillen con el Bloom). Negro = sin brillo extra.")]
        [ColorUsage(false, true)] public Color glowColor = new Color(0.2f, 0.6f, 1.6f);
        [Tooltip("Aleatorio = cada luz va a su aire desde el principio (bombillas). Secuencia = empiezan apagadas y se encienden una a una al aparecer el cartel (letras).")]
        public LightMode mode = LightMode.Random;
        [Tooltip("Si las luces buenas pegan de vez en cuando un parpadeo corto. Desmárcalo en las letras para que se queden fijas.")]
        public bool steadyBlinks = true;
    }

    public enum LightMode { Random, Sequence }

    private enum LightState { Steady, Flicker, Dead, Waiting, Igniting }

    private class LightPart
    {
        public Renderer Renderer;
        public LightGroup Group;
        public LightState State;
        public LightState FinalState;  // lo que hace después de encenderse (solo en Secuencia)
        public float IgnitionEnd;      // cuándo acaba el chisporroteo de arranque
        public bool On;
        public float NextChange;
        public bool InBurst;       // está en plena ráfaga de parpadeos (solo las que fallan a ratos)
        public float BurstEnd;     // cuándo acaba esa ráfaga
        public Color BaseColor;    // color del material, para oscurecerlo sin perder el tono
        public Color Glow;
    }

    [Header("Luces")]
    [SerializeField] private LightGroup[] groups = new LightGroup[0];
    [Tooltip("Piezas a menos de esta distancia (en metros del modelo) cuentan como la misma luz (bombilla + casquillo). 0.02 = 2 cm.")]
    [Min(0f)]
    [SerializeField] private float mergeDistance = 0.02f;

    [Header("Brillo")]
    [Tooltip("Cuánto se oscurece una luz apagada (sobre el color de su material). 0.35 = apagada pero se ve el cristal.")]
    [Range(0f, 1f)]
    [SerializeField] private float offBrightness = 0.35f;

    [Header("Encendido al aparecer")]
    [Tooltip("Segundos desde que aparece el cartel hasta que empieza a encenderse (bombillas y primera letra).")]
    [Min(0f)]
    [SerializeField] private float sequenceDelay = 0.35f;
    [Tooltip("Segundos en los que se van encendiendo las bombillas (cada una en un momento al azar). 1 = en un segundo está todo arrancado.")]
    [Min(0f)]
    [SerializeField] private float powerOnSeconds = 1f;
    [Tooltip("Segundos entre una letra y la siguiente (grupos en Secuencia). 0.22 = Y... O... U...")]
    [Min(0f)]
    [SerializeField] private float sequenceStepSeconds = 0.22f;
    [Tooltip("Segundos que chisporrotea cada letra al encenderse, como un neón que arranca. 0 = se enciende de golpe.")]
    [Min(0f)]
    [SerializeField] private float ignitionSeconds = 0.3f;

    [Header("Ritmo")]
    [Tooltip("Segundos mínimo y máximo entre parpadeos dentro de una ráfaga. (0.05, 0.15) = chisporroteo rápido.")]
    [SerializeField] private Vector2 flickerSeconds = new Vector2(0.05f, 0.15f);
    [Tooltip("Segundos mínimo y máximo que dura una ráfaga de parpadeos.")]
    [SerializeField] private Vector2 burstSeconds = new Vector2(0.3f, 0.8f);
    [Tooltip("Segundos mínimo y máximo de calma (encendida) entre ráfagas.")]
    [SerializeField] private Vector2 calmSeconds = new Vector2(2f, 6f);
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
            if (group.mode == LightMode.Sequence) SortForReading(pieces);
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
        Material material = piece.sharedMaterial;
        Color baseColor = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
        var part = new LightPart { Renderer = piece, Group = group, State = state, FinalState = state, On = state != LightState.Dead, BaseColor = baseColor, Glow = group.glowColor };
        part.NextChange = Time.unscaledTime + UnityEngine.Random.Range(0f, 1f); // cada una empieza a su ritmo
        Apply(part);
        return part;
    }

    /// <summary>
    /// Cada vez que aparece el cartel, todo se apaga y vuelve a encenderse: las luces en Secuencia una a una y las demás
    /// cada una en un momento al azar. Después cada una hace lo que le tocó (buena, fallando o fundida).
    /// </summary>
    private void OnEnable()
    {
        float start = Time.unscaledTime + sequenceDelay;
        var indexInGroup = new Dictionary<LightGroup, int>();
        foreach (LightPart part in parts)
        {
            float delay;
            if (part.Group.mode == LightMode.Sequence)
            {
                indexInGroup.TryGetValue(part.Group, out int index);
                indexInGroup[part.Group] = index + 1;
                delay = index * sequenceStepSeconds;
            }
            else delay = UnityEngine.Random.Range(0f, powerOnSeconds);

            part.State = LightState.Waiting;
            part.InBurst = false;
            part.On = false;
            part.NextChange = start + delay;
            Apply(part);
        }
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        foreach (LightPart part in parts)
        {
            if (now < part.NextChange) continue;

            switch (part.State)
            {
                case LightState.Waiting:
                    // Le toca: empieza a chisporrotear.
                    part.State = LightState.Igniting;
                    part.IgnitionEnd = now + ignitionSeconds;
                    part.On = true;
                    part.NextChange = now + RandomIn(flickerSeconds) * 0.6f;
                    break;
                case LightState.Igniting:
                    if (now >= part.IgnitionEnd)
                    {
                        // Ya ha arrancado: encendida (o se funde, si le tocó estar fundida) y sigue como las demás.
                        part.State = part.FinalState;
                        part.On = part.State != LightState.Dead;
                        part.NextChange = now + (part.State == LightState.Dead ? RandomIn(deadSeconds)
                                               : part.State == LightState.Flicker ? RandomIn(calmSeconds)
                                               : SteadyWait(part));
                    }
                    else
                    {
                        part.On = !part.On;
                        part.NextChange = now + RandomIn(flickerSeconds) * 0.6f;
                    }
                    break;
                case LightState.Flicker:
                    if (!part.InBurst)
                    {
                        // Fin de la calma: empieza una ráfaga de parpadeos.
                        part.InBurst = true;
                        part.BurstEnd = now + RandomIn(burstSeconds);
                        part.On = false;
                        part.NextChange = now + RandomIn(flickerSeconds);
                    }
                    else if (now >= part.BurstEnd)
                    {
                        // Fin de la ráfaga: se queda encendida un rato.
                        part.InBurst = false;
                        part.On = true;
                        part.NextChange = now + RandomIn(calmSeconds);
                    }
                    else
                    {
                        part.On = !part.On;
                        part.NextChange = now + RandomIn(flickerSeconds);
                    }
                    break;
                case LightState.Steady:
                    // Encendida mucho rato, apagada un instante (o siempre encendida, si el grupo no parpadea).
                    part.On = !part.On || !part.Group.steadyBlinks;
                    part.NextChange = now + (part.On ? SteadyWait(part) : blinkSeconds);
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
        Color color = part.On ? part.BaseColor : part.BaseColor * offBrightness;
        color.a = part.BaseColor.a; // la transparencia del cristal no cambia
        part.Renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionColorId, part.On ? part.Glow : Color.black);
        part.Renderer.SetPropertyBlock(block);
    }

    private static float RandomIn(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

    /// <summary>Lo que aguanta encendida una luz buena antes del parpadeo corto. Sin parpadeos = para siempre.</summary>
    private float SteadyWait(LightPart part) => part.Group.steadyBlinks ? RandomIn(steadySeconds) : float.PositiveInfinity;

    /// <summary>
    /// Ordena las piezas como se lee el cartel desde la cámara: por filas de arriba abajo y cada fila de izquierda a
    /// derecha. Dos piezas están en la misma fila si sus centros están a menos de media pieza de altura.
    /// </summary>
    private void SortForReading(List<Renderer> pieces)
    {
        if (pieces.Count < 2) return;
        Camera viewer = GetComponentInParent<Camera>();
        Transform view = viewer != null ? viewer.transform : transform;

        var centers = new Dictionary<Renderer, Vector3>();
        float averageHalfHeight = 0f;
        foreach (Renderer piece in pieces)
        {
            centers[piece] = view.InverseTransformPoint(piece.bounds.center);
            averageHalfHeight += piece.bounds.extents.y;
        }
        averageHalfHeight /= pieces.Count;

        pieces.Sort((a, b) => centers[b].y.CompareTo(centers[a].y)); // de arriba abajo
        var sorted = new List<Renderer>(pieces.Count);
        int rowStart = 0;
        for (int i = 1; i <= pieces.Count; i++)
        {
            bool newRow = i == pieces.Count || centers[pieces[rowStart]].y - centers[pieces[i]].y > averageHalfHeight;
            if (!newRow) continue;
            List<Renderer> row = pieces.GetRange(rowStart, i - rowStart);
            row.Sort((a, b) => centers[a].x.CompareTo(centers[b].x)); // de izquierda a derecha
            sorted.AddRange(row);
            rowStart = i;
        }
        pieces.Clear();
        pieces.AddRange(sorted);
    }

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
