using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Monta la iluminación de las paredes y las sondas de luz del casino. Pon este objeto en el suelo (en cualquier sitio)
/// y pulsa "Construir iluminación":
/// - Apliques: recorre cada pared de Walls Root y pone PF_LamparaPared, con su luz, cada pocos metros por la cara de
///   DENTRO (solo donde hay NavMesh delante; por fuera del mapa no pone). Se salta los huecos ocupados (tragaperras
///   pegadas a la pared, puertas, esquinas).
/// - Light Probes: una rejilla de sondas sobre todo el NavMesh a varias alturas, para que el jugador, los robots y los
///   objetos que se mueven reciban la luz horneada (si no, se ven planos y oscuros).
/// Necesita el NavMesh horneado. Todo queda como hijo "Generado"; se puede reconstruir y deshacer con Ctrl+Z.
/// En el juego no hace nada: solo sirve para montarlo en el editor.
/// </summary>
public class CasinoLightingBuilder : MonoBehaviour
{
#pragma warning disable 0414, 0169
    [Header("Apliques de pared")]
    [Tooltip("Objeto con todas las paredes (Walls).")]
    [SerializeField] private Transform wallsRoot;
    [Tooltip("Prefab del aplique (PF_LamparaPared). Vacío = solo pone las luces.")]
    [SerializeField] private GameObject sconcePrefab;
    [Tooltip("Metros entre aplique y aplique. 5-7 = bien iluminado sin pasarse.")]
    [Min(1f)]
    [SerializeField] private float sconceSpacing = 6f;
    [Tooltip("Altura del centro del aplique sobre el suelo (metros). Por encima de la cabeza: 2.4-2.8.")]
    [Min(0.5f)]
    [SerializeField] private float sconceHeight = 2.6f;
    [Tooltip("Metros sin apliques en los extremos de cada pared (esquinas).")]
    [Min(0f)]
    [SerializeField] private float cornerMargin = 1.5f;
    [Tooltip("Paredes más cortas que esto no llevan aplique.")]
    [Min(0f)]
    [SerializeField] private float minWallLength = 3f;
    [Tooltip("Separación del aplique respecto a la pared (metros).")]
    [SerializeField] private float wallOffset = 0.02f;
    [Tooltip("Giro del modelo (grados) por si el aplique sale mirando hacia la pared o de lado. PF_LamparaPared: 90. Si sale al revés, 270.")]
    [SerializeField] private float modelYaw = 90f;
    [Tooltip("No pone aplique donde hay algo pegado a la pared (tragaperras, puertas, cortinas).")]
    [SerializeField] private bool skipBlocked = true;
    [Tooltip("Objetos delante de los que no quieres apliques aunque no tengan collider: carteles (Luxury Casino), la pantalla del cine, cuadros... Arrastra aquí el objeto (o su padre).")]
    [SerializeField] private Transform[] avoid;
    [Tooltip("Metros de margen alrededor de los objetos de Avoid.")]
    [Min(0f)]
    [SerializeField] private float avoidMargin = 0.75f;

    [Header("Luz de los apliques")]
    [Tooltip("Color cálido de bombilla (unos 2700 K).")]
    [SerializeField] private Color sconceColor = new Color(1f, 0.78f, 0.55f);
    [Tooltip("Intensidad. 2-3 = luz de ambiente; más = manchas quemadas en la pared.")]
    [Min(0f)]
    [SerializeField] private float sconceIntensity = 2.5f;
    [Tooltip("Alcance de la luz en metros.")]
    [Min(0.5f)]
    [SerializeField] private float sconceRange = 5f;
    [Tooltip("Baked = horneada (no cuesta nada en el juego, la mejor opción para muchos apliques). Realtime = sin hornear.")]
    [SerializeField] private LightmapBakeType sconceMode = LightmapBakeType.Baked;

    [Header("Light Probes")]
    [Tooltip("Crea la rejilla de sondas de luz sobre el NavMesh.")]
    [SerializeField] private bool lightProbes = true;
    [Tooltip("Metros entre sondas. 3 = buena calidad; 4-5 = menos sondas en mapas muy grandes.")]
    [Min(1f)]
    [SerializeField] private float probeSpacing = 3f;
    [Tooltip("Alturas de cada capa de sondas sobre el suelo: pies, cabeza y cerca del techo.")]
    [SerializeField] private float[] probeHeights = { 0.4f, 1.7f, 3.6f };

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuántos apliques y sondas pone y cuántos huecos se salta.")]
    [SerializeField] private bool showDebug = true;

    private const string GeneratedName = "Generado";
    private int lastSconces, lastSkipped, lastProbes;
    private readonly List<Bounds> avoidBounds = new List<Bounds>();
#pragma warning restore 0414, 0169

#if UNITY_EDITOR
    [ContextMenu("Construir iluminación")]
    public void Build()
    {
        Undo.SetCurrentGroupName("Construir iluminación");
        int group = Undo.GetCurrentGroup();
        Clear();

        var root = new GameObject(GeneratedName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Iluminación");
        root.SetParent(transform, false);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        lastSconces = lastSkipped = lastProbes = 0;
        float floorY = transform.position.y;
        CollectAvoidBounds();
        if (wallsRoot != null)
        {
            var sconces = new GameObject("Apliques").transform;
            sconces.SetParent(root, false);
            foreach (Transform wall in wallsRoot)
                WallSconces(sconces, wall, floorY);
        }
        if (lightProbes) Probes(root);

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(gameObject);
        if (showDebug)
            Debug.Log($"[Iluminación] {lastSconces} apliques ({lastSkipped} huecos ocupados saltados), {lastProbes} light probes.", this);
    }

    [ContextMenu("Borrar lo generado")]
    public void Clear()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    private void WallSconces(Transform parent, Transform wall, float floorY)
    {
        var filter = wall.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        Bounds local = filter.sharedMesh.bounds;
        Vector3 scale = wall.lossyScale;
        float sizeX = Mathf.Abs(local.size.x * scale.x), sizeZ = Mathf.Abs(local.size.z * scale.z);

        // El lado largo es la pared; el corto, su grosor.
        bool alongX = sizeX >= sizeZ;
        float length = alongX ? sizeX : sizeZ;
        float halfThickness = (alongX ? sizeZ : sizeX) * 0.5f;
        Vector3 along = (alongX ? wall.right : wall.forward);
        Vector3 across = (alongX ? wall.forward : wall.right);
        along.y = 0f; across.y = 0f;
        along.Normalize(); across.Normalize();
        Vector3 center = wall.TransformPoint(local.center);
        if (length < minWallLength) return;
        var wallRenderer = wall.GetComponent<Renderer>();
        if (wallRenderer != null && wallRenderer.bounds.max.y < floorY + sconceHeight + 0.2f) return; // pared demasiado baja

        float usable = Mathf.Max(0f, length - cornerMargin * 2f);
        int count = Mathf.Max(1, Mathf.FloorToInt(usable / sconceSpacing) + 1);
        float step = count > 1 ? usable / (count - 1) : 0f;

        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 normal = across * side;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? -usable * 0.5f + i * step : 0f;
                Vector3 face = center + along * t + normal * (halfThickness + wallOffset);
                face.y = floorY + sconceHeight;

                // Solo por dentro del casino: delante tiene que haber suelo andable.
                Vector3 front = new Vector3(face.x, floorY, face.z) + normal * 1f;
                if (!NavMesh.SamplePosition(front, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - floorY) > 1f) continue;

                // Hueco libre: nada pegado a la pared delante del aplique.
                if (skipBlocked && Physics.CheckBox(face + normal * 0.35f + Vector3.down * 0.3f, new Vector3(0.3f, 0.6f, 0.3f),
                        Quaternion.LookRotation(normal), ~0, QueryTriggerInteraction.Ignore))
                {
                    lastSkipped++;
                    continue;
                }

                if (InAvoidZone(face))
                {
                    lastSkipped++;
                    continue;
                }

                Quaternion rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, modelYaw, 0f);
                Transform lamp;
                if (sconcePrefab != null)
                {
                    lamp = ((GameObject)PrefabUtility.InstantiatePrefab(sconcePrefab, parent)).transform;
                    lamp.SetPositionAndRotation(face, rotation);
                }
                else
                {
                    lamp = new GameObject("Aplique").transform;
                    lamp.SetParent(parent, false);
                    lamp.SetPositionAndRotation(face, rotation);
                }
                lamp.name = $"Aplique_{wall.name}_{(side > 0 ? "A" : "B")}{i + 1}";

                // Pegado a la pared por detrás y con su centro a la altura pedida, mida lo que mida el modelo.
                float depth = 0f;
                if (LampExtents(lamp, face, normal, out float back, out float frontEdge, out float centerY))
                {
                    lamp.position += normal * -back + Vector3.up * (face.y - centerY);
                    depth = frontEdge - back;
                }
                GameObjectUtility.SetStaticEditorFlags(lamp.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);

                var lightGo = new GameObject("Luz");
                lightGo.transform.SetParent(lamp, false);
                lightGo.transform.position = face + normal * (depth + 0.15f); // justo delante del aplique
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = sconceColor;
                light.intensity = sconceIntensity;
                light.range = sconceRange;
                light.lightmapBakeType = sconceMode;
                light.shadows = sconceMode == LightmapBakeType.Baked ? LightShadows.Soft : LightShadows.None;
                lastSconces++;
            }
        }
    }

    private void CollectAvoidBounds()
    {
        avoidBounds.Clear();
        if (avoid == null) return;
        foreach (Transform t in avoid)
        {
            if (t == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                Bounds b = r.bounds;
                b.Expand(avoidMargin * 2f);
                avoidBounds.Add(b);
            }
        }
    }

    private bool InAvoidZone(Vector3 point)
    {
        foreach (Bounds b in avoidBounds)
            if (b.Contains(point)) return true;
        return false;
    }

    /// <summary>
    /// Mide el aplique: cuánto sobresale por detrás (back) y por delante (front) de la cara de la pared, y la altura de su
    /// centro. Usa las esquinas reales de cada malla, así funciona aunque la pared esté girada.
    /// </summary>
    private static bool LampExtents(Transform lamp, Vector3 face, Vector3 normal, out float back, out float front, out float centerY)
    {
        back = float.MaxValue; front = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        foreach (MeshFilter mf in lamp.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                Vector3 w = mf.transform.TransformPoint(corner);
                float d = Vector3.Dot(w - face, normal);
                back = Mathf.Min(back, d); front = Mathf.Max(front, d);
                minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
                any = true;
            }
        }
        centerY = (minY + maxY) * 0.5f;
        return any;
    }

    private void Probes(Transform root)
    {
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        if (mesh.vertices == null || mesh.vertices.Length == 0)
        {
            Debug.LogWarning("[Iluminación] No hay NavMesh horneado: hornéalo primero para colocar las light probes.", this);
            return;
        }
        Vector3 min = mesh.vertices[0], max = mesh.vertices[0];
        foreach (Vector3 v in mesh.vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }

        var points = new List<Vector3>();
        var used = new HashSet<Vector3Int>();
        for (float x = min.x; x <= max.x; x += probeSpacing)
            for (float z = min.z; z <= max.z; z += probeSpacing)
            {
                Vector3 sample = new Vector3(x, (min.y + max.y) * 0.5f, z);
                if (!NavMesh.SamplePosition(sample, out NavMeshHit hit, probeSpacing * 0.5f + (max.y - min.y), NavMesh.AllAreas)) continue;
                var key = Vector3Int.RoundToInt(hit.position / (probeSpacing * 0.5f));
                if (!used.Add(key)) continue;
                foreach (float h in probeHeights)
                {
                    // Sin sondas metidas dentro de mesas o por encima del techo.
                    if (Physics.Raycast(hit.position + Vector3.up * 0.1f, Vector3.up, h, ~0, QueryTriggerInteraction.Ignore)) break;
                    points.Add(hit.position + Vector3.up * h);
                }
            }

        var go = new GameObject("Light_Probes");
        go.transform.SetParent(root, false);
        var group = go.AddComponent<LightProbeGroup>();
        for (int i = 0; i < points.Count; i++) points[i] = go.transform.InverseTransformPoint(points[i]);
        group.probePositions = points.ToArray();
        lastProbes = points.Count;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        DebugLabels.At(transform.position + Vector3.up * 2f, 0,
            $"Iluminación · {lastSconces} apliques · {lastSkipped} saltados · {lastProbes} probes", DebugLabels.Casino);
    }
#endif
}

#if UNITY_EDITOR
/// <summary>Botones en el Inspector para construir y borrar.</summary>
[CustomEditor(typeof(CasinoLightingBuilder))]
public class CasinoLightingBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Hornea el NavMesh antes. Arrastra Walls a Walls Root y PF_LamparaPared, y pulsa Construir. Al reconstruir se pierden los retoques hechos a mano dentro de \"Generado\".", MessageType.Info);
        GUI.backgroundColor = new Color(1f, 0.85f, 0.55f);
        if (GUILayout.Button("Construir iluminación", GUILayout.Height(32)))
            foreach (Object t in targets) ((CasinoLightingBuilder)t).Build();
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Borrar lo generado"))
            foreach (Object t in targets) ((CasinoLightingBuilder)t).Clear();
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
#endif
