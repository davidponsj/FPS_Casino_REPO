using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Reparte carritos de limpieza por el mapa como obstáculos, cada uno con productos al azar encima (escoba, cubo,
/// lejía...). Pon este objeto en la escena y pulsa "Repartir carritos":
/// - Los carritos van solo donde hay NavMesh (suelo andable), con hueco libre alrededor y separados entre sí.
/// - Giro al azar y un número de productos al azar en cada uno, colocados encima sin salirse.
/// - Cada carrito lleva un NavMeshObstacle que corta el NavMesh: los robots lo rodean.
/// Cambia la semilla (Seed) para otra distribución. Todo queda en "Generado" y se deshace con Ctrl+Z.
/// </summary>
public class CleaningCartScatter : MonoBehaviour
{
#pragma warning disable 0414, 0169
    [Header("Prefabs")]
    [Tooltip("Carrito (PF_CleaningCart).")]
    [SerializeField] private GameObject cartPrefab;
    [Tooltip("Productos que pueden ir encima (PF_Escoba, PF_Cubo, PF_Lejia, PF_Fairy, PF_BoteLimpieza, PF_RolloPapel, PF_Desatascador, PF_Recogedor...).")]
    [SerializeField] private GameObject[] itemPrefabs;
    [Tooltip("Cartel de suelo mojado que puede aparecer junto al carrito (PF_CartelMojado). Vacío = nunca.")]
    [SerializeField] private GameObject wetSignPrefab;
    [Range(0f, 1f)]
    [SerializeField] private float wetSignChance = 0.35f;

    [Header("Reparto")]
    [Tooltip("Paredes del mapa (Walls): los carritos se reparten dentro de lo que ocupan.")]
    [SerializeField] private Transform wallsRoot;
    [Min(1)]
    [SerializeField] private int cartCount = 8;
    [Tooltip("Distancia mínima entre carritos (metros).")]
    [Min(0f)]
    [SerializeField] private float minSpacing = 8f;
    [Tooltip("Hueco libre alrededor del carrito (metros), para no taponar pasillos ni pegarse a máquinas.")]
    [Min(0f)]
    [SerializeField] private float clearance = 0.6f;
    [Tooltip("Productos por carrito (mín, máx).")]
    [SerializeField] private Vector2Int itemsPerCart = new Vector2Int(2, 5);
    [Tooltip("Misma semilla = mismo reparto. Cámbiala para otro.")]
    [SerializeField] private int seed = 1;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuántos carritos ha puesto y cuántos intentos ha necesitado.")]
    [SerializeField] private bool showDebug = true;

    private const string GeneratedName = "Generado";
    private int lastPlaced;
#pragma warning restore 0414, 0169

#if UNITY_EDITOR
    [ContextMenu("Repartir carritos")]
    public void Build()
    {
        if (cartPrefab == null) { Debug.LogWarning("[Carritos] Falta Cart Prefab.", this); return; }
        Undo.SetCurrentGroupName("Repartir carritos");
        int group = Undo.GetCurrentGroup();
        Clear();

        var root = new GameObject(GeneratedName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Carritos");
        root.SetParent(transform, false);

        Random.State oldState = Random.state;
        Random.InitState(seed);
        Bounds area = MapBounds();
        var placed = new List<Vector3>();
        int attempts = 0;
        while (placed.Count < cartCount && attempts < cartCount * 200)
        {
            attempts++;
            var p = new Vector3(Random.Range(area.min.x, area.max.x), area.center.y, Random.Range(area.min.z, area.max.z));
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, area.extents.y + 2f, NavMesh.AllAreas)) continue;
            if (placed.Exists(o => (o - hit.position).sqrMagnitude < minSpacing * minSpacing)) continue;

            Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject cart = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, root);
            cart.transform.SetPositionAndRotation(hit.position, Quaternion.identity); // recto para colocar los productos
            Bounds b = GetBounds(cart);
            cart.transform.position += Vector3.up * (hit.position.y - b.min.y); // apoyado en el suelo
            Fill(cart.transform);
            cart.transform.rotation = yaw; // los productos giran con él
            b = GetBounds(cart);

            // Hueco libre: nada alrededor (se ignora el suelo subiendo la caja un poco).
            Vector3 half = b.extents + new Vector3(clearance, 0f, clearance);
            half.y = Mathf.Max(0.05f, half.y - 0.1f);
            Collider[] own = cart.GetComponentsInChildren<Collider>();
            foreach (Collider c in own) c.enabled = false; // que no se choque consigo mismo
            Physics.SyncTransforms();
            bool blocked = Physics.CheckBox(b.center + Vector3.up * 0.1f, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in own) c.enabled = true;
            if (blocked)
            {
                Object.DestroyImmediate(cart);
                continue;
            }

            if (wetSignPrefab != null && Random.value < wetSignChance) WetSign(cart.transform, b, hit.position.y);
            var obstacle = cart.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = cart.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.center = cart.transform.InverseTransformPoint(b.center);
            obstacle.size = Vector3.Scale(b.size, new Vector3(0.9f, 1f, 0.9f));
            placed.Add(hit.position);
        }
        Random.state = oldState;
        lastPlaced = placed.Count;

        Undo.CollapseUndoOperations(group);
        if (showDebug) Debug.Log($"[Carritos] {placed.Count}/{cartCount} carritos en {attempts} intentos.", this);
    }

    [ContextMenu("Borrar lo generado")]
    public void Clear()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    private struct Level { public float y, clearance; public Vector2 min, max; public List<Rect> used; }

    /// <summary>
    /// Productos al azar, sin repetir, repartidos por las bandejas y las cajas del carrito. Las superficies se sacan de la
    /// propia malla (caras que miran hacia arriba con bastante área), así vale para cualquier carrito. Un producto solo va
    /// en una bandeja si cabe de alto (la de arriba no tiene límite) y nunca encima de otro.
    /// </summary>
    private void Fill(Transform cart)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;
        List<Level> levels = FindLevels(cart);
        if (levels.Count == 0) { Debug.LogWarning("[Carritos] No encuentro bandejas en el carrito: va sin productos.", this); return; }
        var pool = new List<GameObject>(itemPrefabs);
        pool.RemoveAll(p => p == null);
        int count = Mathf.Min(pool.Count, Random.Range(itemsPerCart.x, itemsPerCart.y + 1));
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(pool[index], cart);
            pool.RemoveAt(index);
            item.transform.rotation = Quaternion.Euler(0f, (Random.value < 0.5f ? 0f : 90f) + Random.Range(-15f, 15f), 0f);
            if (!PlaceOnLevel(item, levels)) Object.DestroyImmediate(item); // no cabe en ningún sitio
        }
    }

    private bool PlaceOnLevel(GameObject item, List<Level> levels)
    {
        Bounds ib = GetBounds(item);
        Vector3 offset = ib.center - item.transform.position;
        Vector2 half = new Vector2(ib.extents.x, ib.extents.z);
        for (int attempt = 0; attempt < 25; attempt++)
        {
            Level level = levels[Random.Range(0, levels.Count)];
            if (ib.size.y > level.clearance) continue;
            Vector2 min = level.min + half, max = level.max - half;
            if (min.x > max.x || min.y > max.y) continue; // no cabe de ancho
            var c = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            var rect = new Rect(c - half, half * 2f);
            if (level.used.Exists(r => r.Overlaps(rect))) continue;
            level.used.Add(rect);
            item.transform.position = new Vector3(c.x, level.y + ib.extents.y, c.y) - offset;
            return true;
        }
        return false;
    }

    /// <summary>Alturas del carrito donde se puede apoyar algo (en el mundo, con el carrito recto).</summary>
    private List<Level> FindLevels(Transform cart)
    {
        var area = new Dictionary<int, float>();
        var lo = new Dictionary<int, Vector2>();
        var hi = new Dictionary<int, Vector2>();
        foreach (MeshFilter mf in cart.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Vector3[] v = mf.sharedMesh.vertices;
            int[] t = mf.sharedMesh.triangles;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                float twice = n.magnitude;
                if (twice < 1e-6f || Mathf.Abs(n.y / twice) < 0.95f) continue; // solo caras horizontales
                int key = Mathf.RoundToInt((a.y + b.y + c.y) / 3f / 0.02f);
                area[key] = (area.TryGetValue(key, out float s0) ? s0 : 0f) + twice * 0.5f;
                Vector2 mn = Vector2.Min(Vector2.Min(new Vector2(a.x, a.z), new Vector2(b.x, b.z)), new Vector2(c.x, c.z));
                Vector2 mx = Vector2.Max(Vector2.Max(new Vector2(a.x, a.z), new Vector2(b.x, b.z)), new Vector2(c.x, c.z));
                lo[key] = lo.TryGetValue(key, out Vector2 l0) ? Vector2.Min(l0, mn) : mn;
                hi[key] = hi.TryGetValue(key, out Vector2 h0) ? Vector2.Max(h0, mx) : mx;
            }
        }
        var levels = new List<Level>();
        if (area.Count == 0) return levels;
        float biggest = 0f;
        foreach (float a in area.Values) biggest = Mathf.Max(biggest, a);
        float floorY = GetBounds(cart.gameObject).min.y + 0.1f;
        var keys = new List<int>(area.Keys);
        keys.Sort();
        foreach (int key in keys)
        {
            float y = key * 0.02f;
            if (area[key] < biggest * 0.08f || y < floorY) continue; // ruedas, bordes y el suelo no cuentan
            Vector2 size = hi[key] - lo[key];
            Vector2 shrink = size * 0.08f; // sin pegarse al borde
            levels.Add(new Level { y = y, min = lo[key] + shrink, max = hi[key] - shrink, used = new List<Rect>() });
        }
        // Hueco de alto: hasta la siguiente bandeja que pise su planta (la de más arriba, sin límite).
        for (int i = 0; i < levels.Count; i++)
        {
            Level l = levels[i];
            l.clearance = float.MaxValue;
            for (int j = i + 1; j < levels.Count; j++)
            {
                Level u = levels[j];
                bool overlaps = u.min.x < l.max.x && u.max.x > l.min.x && u.min.y < l.max.y && u.max.y > l.min.y;
                if (overlaps && u.y - l.y > 0.05f) { l.clearance = u.y - l.y - 0.03f; break; }
            }
            levels[i] = l;
        }
        if (showDebug) Debug.Log($"[Carritos] {levels.Count} superficies en el carrito: " + string.Join(", ", levels.ConvertAll(l => $"{l.y:0.00} m")), this);
        return levels;
    }

    private void WetSign(Transform cart, Bounds b, float floorY)
    {
        GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(wetSignPrefab, cart);
        Vector3 side = cart.right * (b.extents.magnitude + 0.5f) * (Random.value < 0.5f ? 1f : -1f);
        sign.transform.SetPositionAndRotation(new Vector3(b.center.x, floorY, b.center.z) + side, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        sign.transform.position += Vector3.up * (floorY - GetBounds(sign).min.y);
    }

    private Bounds MapBounds()
    {
        if (wallsRoot != null)
        {
            Renderer[] rs = wallsRoot.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                return b;
            }
        }
        return new Bounds(transform.position, new Vector3(60f, 4f, 60f));
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebug) return;
        Bounds a = MapBounds();
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireCube(a.center, a.size);
        DebugLabels.At(transform.position + Vector3.up * 2f, 0, $"Carritos · {lastPlaced} puestos · semilla {seed}", DebugLabels.Casino);
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(CleaningCartScatter))]
public class CleaningCartScatterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.6f, 1f, 0.8f);
        if (GUILayout.Button("Repartir carritos", GUILayout.Height(30)))
            foreach (Object t in targets) ((CleaningCartScatter)t).Build();
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Borrar lo generado"))
            foreach (Object t in targets) ((CleaningCartScatter)t).Clear();
    }
}
#endif
