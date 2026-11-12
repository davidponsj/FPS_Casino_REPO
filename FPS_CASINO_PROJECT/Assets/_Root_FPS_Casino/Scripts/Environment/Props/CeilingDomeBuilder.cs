using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Constructor del techo con la cúpula. Pon este objeto en el suelo, justo debajo de donde quieres el centro de la
/// cúpula (p. ej. encima de la ruleta), y pulsa "Construir techo y cúpula":
/// - Techo: cubre todo el mapa (lo mide con las paredes de Walls Root) a la altura de las paredes, con M_Techo y
///   WorldTextureTiling, dejando un hueco donde va la cúpula. Lleva collider (las granadas y los saltos no se escapan).
/// - Cúpula: PF_CupulaTecho escalada al diámetro que digas, apoyada en el techo, con los cristales transparentes (usa la
///   transparencia que ya trae su textura: cristal transparente, marcos opacos) y visible desde abajo.
///   Alrededor, una pieza de techo con el agujero redondo justo de la cúpula (sin huecos en las esquinas).
/// - Luz natural: un "rayo de sol" que entra en diagonal por la cúpula y deja en el suelo una mancha de luz con el
///   borde suave (un foco muy lejano y estrecho, del tamaño justo de la abertura: los rayos llegan casi paralelos,
///   como los del sol). No toca la luz direccional ni el resto de la iluminación del casino.
/// - Opcional: un foco extra, un haz de luz visible y polvo (desactivados por defecto: quedan menos naturales).
/// Todo queda como hijo "Generado"; se puede reconstruir (borra lo anterior) y deshacer con Ctrl+Z.
/// En el juego no hace nada: solo sirve para montarlo en el editor.
/// </summary>
public class CeilingDomeBuilder : MonoBehaviour
{
#pragma warning disable 0414, 0169
    [Header("Techo")]
    [Tooltip("Objeto con todas las paredes (Walls). El techo cubre lo que ocupan, con un margen.")]
    [SerializeField] private Transform wallsRoot;
    [Tooltip("Altura del techo. 0 = la de lo más alto de las paredes.")]
    [Min(0f)]
    [SerializeField] private float ceilingHeight = 0f;
    [Tooltip("Grosor del techo.")]
    [Min(0.05f)]
    [SerializeField] private float ceilingThickness = 0.3f;
    [Tooltip("Metros de más alrededor de las paredes, para que no se vea el borde.")]
    [Min(0f)]
    [SerializeField] private float ceilingMargin = 1f;
    [Tooltip("Material del techo (M_Techo).")]
    [SerializeField] private Material ceilingMaterial;
    [Tooltip("Metros por repetición de la textura del techo.")]
    [Min(0.1f)]
    [SerializeField] private float ceilingMetersPerTile = 3f;

    [Header("Cúpula")]
    [Tooltip("Prefab de la cúpula (PF_CupulaTecho).")]
    [SerializeField] private GameObject domePrefab;
    [Tooltip("Diámetro de la cúpula en metros.")]
    [Min(1f)]
    [SerializeField] private float domeDiameter = 11f;
    [Tooltip("Da la vuelta a la cúpula (el modelo viene hacia abajo). Marcado = la bóveda sube por encima del techo y el borde queda apoyado en él.")]
    [SerializeField] private bool flipDome = true;
    [Tooltip("Sube o baja la cúpula respecto al techo (metros).")]
    [SerializeField] private float domeHeightOffset = 0f;
    [Tooltip("Tamaño del agujero redondo respecto al diámetro de la cúpula. Algo menos de 1 para que el borde de la cúpula tape el corte.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float holeFraction = 0.97f;
    [Tooltip("Cristal de verdad: pone el material de la cúpula (M_Cupula) en transparente, usando la transparencia de su textura (cristales transparentes, marcos opacos), y muy liso para que brille.")]
    [SerializeField] private bool glass = true;
    [Tooltip("Lo transparente que es el cristal. 1 = el de la textura; menos = más transparente.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float glassOpacity = 0.8f;
    [Tooltip("Brillo propio del cristal (como si le diera el sol por fuera). 0 = nada, que suele ser lo más natural.")]
    [ColorUsage(false, true)]
    [SerializeField] private Color glassGlow = Color.black;
    [Tooltip("Que la cúpula se vea desde abajo aunque el modelo tenga las caras hacia fuera (Render Face: Both en M_Cupula).")]
    [SerializeField] private bool doubleSided = true;

    [Header("Sol (luz natural)")]
    [Tooltip("Rayo de sol que entra por la cúpula.")]
    [SerializeField] private bool sunlight = true;
    [Tooltip("Altura del sol en grados (90 = cae vertical, 55 = entra en diagonal y la mancha de luz queda desplazada).")]
    [Range(30f, 90f)]
    [SerializeField] private float sunElevation = 65f;
    [Tooltip("Desde dónde viene el sol (grados). Gíralo para mover la mancha de luz por el suelo.")]
    [Range(0f, 360f)]
    [SerializeField] private float sunAzimuth = 30f;
    [Tooltip("Color del sol. Blanco un poco cálido = mediodía; más naranja = tarde.")]
    [SerializeField] private Color sunColor = new Color(1f, 0.95f, 0.86f);
    [Tooltip("Lo fuerte que ilumina la mancha de luz en el suelo. 1.5-3 = natural; más = quemado.")]
    [Min(0f)]
    [SerializeField] private float sunBrightness = 2f;
    [Tooltip("Lo difuminado del borde de la mancha. 0 = borde duro; 0.5 = muy suave.")]
    [Range(0f, 0.8f)]
    [SerializeField] private float sunSoftness = 0.3f;
    [Tooltip("Sombras del sol (los objetos bajo la cúpula hacen sombra). Necesita las sombras de luces adicionales activadas en el URP Asset.")]
    [SerializeField] private bool sunShadows = true;

    [Header("Foco extra (opcional)")]
    [Tooltip("Añade además un foco dentro de la cúpula. Normalmente no hace falta con el sol.")]
    [SerializeField] private bool extraSpot = false;
    [Tooltip("Color de la luz que entra. Blanco un poco cálido = luz de día natural.")]
    [SerializeField] private Color lightColor = new Color(1f, 0.96f, 0.9f);
    [Min(0f)]
    [SerializeField] private float lightIntensity = 3f;
    [Tooltip("Apertura del foco en grados. Muy abierto = luz que baña la sala sin un círculo marcado.")]
    [Range(20f, 170f)]
    [SerializeField] private float spotAngle = 130f;
    [SerializeField] private bool shadows = true;

    [Header("Haz de luz y polvo")]
    [Tooltip("Cono de luz visible que baja de la cúpula.")]
    [SerializeField] private bool lightShaft = false;
    [Tooltip("Color del haz (la transparencia la da Shaft Strength).")]
    [SerializeField] private Color shaftColor = new Color(0.9f, 0.95f, 1f);
    [Tooltip("Lo visible que es el haz. 0.05 = casi imperceptible (natural); 0.15 = marcado. 0 = sin haz.")]
    [Range(0f, 1f)]
    [SerializeField] private float shaftStrength = 0f;
    [Tooltip("Cuánto se abre el haz al llegar al suelo. 1.3 = un 30% más ancho abajo.")]
    [Range(1f, 2f)]
    [SerializeField] private float shaftSpread = 1.15f;
    [Tooltip("Motas de polvo flotando en el haz. 0 = sin polvo.")]
    [Min(0)]
    [SerializeField] private int dustAmount = 0;

    [Tooltip("Carpeta donde se guardan el material, la textura y la malla del haz que se creen.")]
    [SerializeField] private string assetFolder = "Assets/_Root_FPS_Casino/Art/3D/Materials/Props/Cupula";

    [Header("Depuración")]
    [Tooltip("Dibuja en la vista Scene el círculo de la cúpula y la altura del techo.")]
    [SerializeField] private bool showDebug = true;

    private const string GeneratedName = "Generado";
#pragma warning restore 0414, 0169

#if UNITY_EDITOR
    [ContextMenu("Construir techo y cúpula")]
    public void Build()
    {
        Undo.SetCurrentGroupName("Construir techo y cúpula");
        int group = Undo.GetCurrentGroup();
        Clear();

        var root = new GameObject(GeneratedName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Techo y cúpula");
        root.SetParent(transform, false);
        root.SetPositionAndRotation(transform.position, Quaternion.identity);

        // ---- Medidas del mapa ----
        Bounds map = MapBounds();
        float floorY = transform.position.y;
        float topY = ceilingHeight > 0f ? floorY + ceilingHeight : map.max.y;
        Vector3 center = transform.position;
        float hole = domeDiameter; // el hueco cuadrado; las esquinas las tapa la pieza con agujero redondo

        // ---- Techo: 4 piezas alrededor del hueco ----
        float minX = map.min.x - ceilingMargin, maxX = map.max.x + ceilingMargin;
        float minZ = map.min.z - ceilingMargin, maxZ = map.max.z + ceilingMargin;
        float hx0 = center.x - hole * 0.5f, hx1 = center.x + hole * 0.5f;
        float hz0 = center.z - hole * 0.5f, hz1 = center.z + hole * 0.5f;
        float y = topY + ceilingThickness * 0.5f;
        CeilingPiece(root, "Techo_Norte", minX, maxX, hz1, maxZ, y);
        CeilingPiece(root, "Techo_Sur", minX, maxX, minZ, hz0, y);
        CeilingPiece(root, "Techo_Oeste", minX, hx0, hz0, hz1, y);
        CeilingPiece(root, "Techo_Este", hx1, maxX, hz0, hz1, y);

        // ---- Cúpula ----
        float domeTop = topY;
        float domeRim = domeDiameter * 0.5f;
        float holeRadius;
        if (domePrefab != null)
        {
            var dome = (GameObject)PrefabUtility.InstantiatePrefab(domePrefab, root);
            dome.name = "Cupula";
            dome.transform.position = new Vector3(center.x, topY, center.z);
            dome.transform.rotation = flipDome ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
            Bounds b = WorldBounds(dome);
            float width = Mathf.Max(b.size.x, b.size.z);
            if (width > 1e-4f) dome.transform.localScale *= domeDiameter / width;
            // Apoyada en el techo: su punto más bajo justo en la cara de abajo del techo; la bóveda, por encima.
            b = WorldBounds(dome);
            dome.transform.position += new Vector3(center.x - b.center.x, topY + domeHeightOffset - b.min.y, center.z - b.center.z);
            domeTop = WorldBounds(dome).max.y;
            domeRim = RimRadius(dome, center);
            PrepareDomeMaterial(dome);
            foreach (Renderer r in dome.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // el sol pasa a través del cristal
        }

        // Pieza del techo con el agujero redondo: tapa las esquinas del hueco cuadrado.
        holeRadius = Mathf.Min(domeRim, hole * 0.5f) * holeFraction;
        RoundHoleCeiling(root, center, topY, hole, holeRadius);

        // ---- Sol: entra en diagonal por la cúpula ----
        if (sunlight) Sun(root, new Vector3(center.x, topY, center.z), holeRadius, topY - floorY);

        // ---- Foco extra (opcional) ----
        float lightY = Mathf.Lerp(topY, domeTop, 0.5f);
        if (extraSpot) SpotLight(root, center, lightY, floorY);

        // ---- Haz de luz y polvo ----
        float topRadius = domeDiameter * 0.42f;
        if (lightShaft && shaftStrength > 0f)
        {
            var shaft = new GameObject("Haz_de_luz");
            shaft.transform.SetParent(root, false);
            shaft.transform.SetPositionAndRotation(new Vector3(center.x, floorY, center.z), Quaternion.identity);
            shaft.AddComponent<MeshFilter>().sharedMesh = ShaftMesh(topY - floorY - 0.05f, topRadius, topRadius * shaftSpread);
            var renderer = shaft.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShaftMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        if (dustAmount > 0) Dust(root, center, floorY, topY, topRadius);

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(gameObject);
        if (showDebug)
            Debug.Log($"[Techo] Construido: techo a {topY - floorY:0.0} m, cúpula de {domeDiameter:0.#} m, agujero de {holeRadius * 2f:0.0} m.", this);
    }

    /// <summary>
    /// Foco muy lejano en la dirección del sol, apuntando al centro de la abertura y con la apertura justa para cubrirla:
    /// los rayos llegan casi paralelos y la mancha del suelo tiene la forma de la abertura, como la luz del sol.
    /// </summary>
    private void Sun(Transform root, Vector3 opening, float radius, float height)
    {
        const float distance = 40f;
        Quaternion rotation = Quaternion.Euler(sunElevation, sunAzimuth, 0f); // mira hacia abajo, como una luz direccional
        Vector3 forward = rotation * Vector3.forward;
        var go = new GameObject("Luz_Sol_Cupula");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(opening - forward * distance, rotation);
        var sun = go.AddComponent<Light>();
        sun.type = LightType.Spot;
        sun.color = sunColor;
        // Lo que recorre la luz hasta el suelo, para que llegue con el brillo pedido (las luces bajan con la distancia al cuadrado).
        float toFloor = distance + height / Mathf.Max(0.1f, Mathf.Sin(sunElevation * Mathf.Deg2Rad));
        sun.intensity = sunBrightness * toFloor * toFloor;
        sun.range = toFloor * 2f; // de sobra: cerca del final del alcance URP apaga la luz poco a poco
        float angle = 2f * Mathf.Atan(radius / distance) * Mathf.Rad2Deg;
        sun.spotAngle = angle * (1f + sunSoftness * 0.6f);
        sun.innerSpotAngle = angle * (1f - sunSoftness * 0.6f);
        sun.shadows = sunShadows ? LightShadows.Soft : LightShadows.None;
        sun.shadowNearPlane = Mathf.Min(distance - 2f, 10f);
    }

    /// <summary>Radio del borde de abajo de la cúpula (la boca que se apoya en el techo), medido con sus vértices.</summary>
    private float RimRadius(GameObject dome, Vector3 center)
    {
        Bounds b = WorldBounds(dome);
        float band = b.min.y + b.size.y * 0.03f;
        float rim = 0f;
        foreach (MeshFilter mf in dome.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Vector3[] vertices;
            try { vertices = mesh.vertices; }
            catch { continue; }
            foreach (Vector3 v in vertices)
            {
                Vector3 w = mf.transform.TransformPoint(v);
                if (w.y > band) continue;
                rim = Mathf.Max(rim, new Vector2(w.x - center.x, w.z - center.z).magnitude);
            }
        }
        return rim > 0.1f ? rim : domeDiameter * 0.5f;
    }

    private void SpotLight(Transform root, Vector3 center, float lightY, float floorY)
    {
        var lightGo = new GameObject("Luz_Cupula");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.SetPositionAndRotation(new Vector3(center.x, lightY, center.z), Quaternion.Euler(90f, 0f, 0f));
        var spot = lightGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = lightColor;
        spot.intensity = lightIntensity;
        spot.range = (lightY - floorY) + 3f;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = spotAngle * 0.55f;
        spot.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    /// <summary>
    /// Plancha del techo de lado "size" con un agujero redondo de radio "radius" en el centro, a la altura del techo y
    /// mirando hacia abajo. Rellena las esquinas entre el hueco cuadrado y la cúpula redonda.
    /// </summary>
    private void RoundHoleCeiling(Transform root, Vector3 center, float y, float size, float radius)
    {
        const int segments = 64;
        float half = size * 0.5f;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            // Punto del borde del cuadrado en esa dirección.
            float k = half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z));
            Vector3 inner = dir * radius, outer = dir * k;
            verts.Add(inner);
            verts.Add(outer);
            uvs.Add(new Vector2((center.x + inner.x) / ceilingMetersPerTile, (center.z + inner.z) / ceilingMetersPerTile));
            uvs.Add(new Vector2((center.x + outer.x) / ceilingMetersPerTile, (center.z + outer.z) / ceilingMetersPerTile));
        }
        for (int i = 0; i < segments; i++)
        {
            int k = i * 2;
            tris.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 }); // cara hacia abajo
        }
        string path = $"{assetFolder}/SM_DomeCeilingRing.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "SM_DomeCeilingRing" };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);

        var go = new GameObject("Techo_Aro_Cupula");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(new Vector3(center.x, y - 0.005f, center.z), Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        if (ceilingMaterial != null) renderer.sharedMaterial = ceilingMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided; // tapa al sol aunque mire hacia abajo
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
    }

    [ContextMenu("Borrar lo generado")]
    public void Clear()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    private Bounds MapBounds()
    {
        if (wallsRoot != null)
        {
            Renderer[] renderers = wallsRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }
        }
        Debug.LogWarning("[Techo] Sin Walls Root: uso un techo de 100 x 100 m alrededor de este objeto.", this);
        return new Bounds(transform.position + Vector3.up * 2.5f, new Vector3(100f, 5f, 100f));
    }

    private void CeilingPiece(Transform root, string pieceName, float x0, float x1, float z0, float z1, float y)
    {
        if (x1 - x0 < 0.05f || z1 - z0 < 0.05f) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = pieceName;
        go.transform.SetParent(root, true);
        go.transform.SetPositionAndRotation(new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), Quaternion.identity);
        go.transform.localScale = new Vector3(x1 - x0, ceilingThickness, z1 - z0);
        if (ceilingMaterial != null) go.GetComponent<Renderer>().sharedMaterial = ceilingMaterial;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI
                                                   | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic);

        // Que el bake del NavMesh no tome el tejado como suelo por el que andar.
        var modifier = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 1; // Not Walkable

        // Textura del techo al mismo tamaño en todas las piezas (cara de abajo: ancho y fondo).
        var tiling = go.AddComponent<WorldTextureTiling>();
        var so = new SerializedObject(tiling);
        SerializedProperty face = so.FindProperty("face");
        if (face != null) face.enumValueIndex = 1; // Width Depth
        SerializedProperty meters = so.FindProperty("metersPerTile");
        if (meters != null) meters.floatValue = ceilingMetersPerTile;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void PrepareDomeMaterial(GameObject dome)
    {
        foreach (Renderer r in dome.GetComponentsInChildren<Renderer>())
        {
            Material m = r.sharedMaterial;
            if (m == null) continue;
            if (doubleSided) m.SetFloat("_Cull", 0f);
            if (glass)
            {
                // Transparente con la transparencia de la textura: cristales que dejan ver el cielo, marcos opacos.
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetFloat("_BlendModePreserveSpecular", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                Color baseColor = m.GetColor("_BaseColor");
                baseColor.a = glassOpacity;
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Smoothness", 0.92f);
                m.SetFloat("_Metallic", 0f);
            }
            if (glassGlow.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                m.SetColor("_EmissionColor", glassGlow);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
        }
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ---------------- HAZ DE LUZ ----------------

    /// <summary>Cono abierto (sin tapas) del suelo al techo. Transparencia: 1 arriba, 0 abajo (en el color de los vértices).</summary>
    private Mesh ShaftMesh(float height, float topRadius, float bottomRadius)
    {
        const int segments = 48;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var tris = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts.Add(dir * bottomRadius + Vector3.up * 0.05f);
            verts.Add(dir * topRadius + Vector3.up * height);
            uvs.Add(new Vector2(i / (float)segments * 6f, 0f));
            uvs.Add(new Vector2(i / (float)segments * 6f, 1f));
            colors.Add(new Color(1f, 1f, 1f, 0f));
            colors.Add(new Color(1f, 1f, 1f, 1f));
        }
        for (int i = 0; i < segments; i++)
        {
            int k = i * 2;
            tris.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
        }
        string path = $"{assetFolder}/SM_DomeLightShaft.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "SM_DomeLightShaft" };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private Material ShaftMaterial()
    {
        Material m = AdditiveMaterial("M_DomeLightShaft", StreakTexture());
        Color c = shaftColor;
        c.a = shaftStrength;
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    private Material AdditiveMaterial(string materialName, Texture2D texture)
    {
        string path = $"{assetFolder}/{materialName}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = materialName };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", texture);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // aditivo: suma luz
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    /// <summary>Rayas verticales suaves e irregulares (los "rayos" del haz).</summary>
    private Texture2D StreakTexture()
    {
        string path = $"{assetFolder}/T_DomeLightShaft.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int w = 256, h = 16;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "T_DomeLightShaft", wrapMode = TextureWrapMode.Repeat };
        var rng = new System.Random(3);
        var columns = new float[w];
        // Suma de bandas de distintas anchuras, normalizada: rayas más y menos brillantes.
        for (int k = 0; k < 9; k++)
        {
            float freq = 1 + rng.Next(6), phase = (float)rng.NextDouble() * Mathf.PI * 2f, amp = 0.3f + (float)rng.NextDouble() * 0.7f;
            for (int x = 0; x < w; x++) columns[x] += amp * Mathf.Max(0f, Mathf.Sin(x / (float)w * Mathf.PI * 2f * freq + phase));
        }
        float max = 0f;
        foreach (float v in columns) max = Mathf.Max(max, v);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0.35f + 0.65f * columns[x] / Mathf.Max(0.001f, max);
                px[y * w + x] = new Color(1f, 1f, 1f, v);
            }
        t.SetPixels(px);
        t.Apply();
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    private void Dust(Transform root, Vector3 center, float floorY, float topY, float radius)
    {
        var go = new GameObject("Polvo");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(new Vector3(center.x, (floorY + topY) * 0.5f, center.z), Quaternion.identity);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        main.startColor = new Color(1f, 0.95f, 0.85f, 0.7f);
        main.maxParticles = Mathf.Max(10, dustAmount * 2);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = dustAmount / 10f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 1.6f, topY - floorY - 0.5f, radius * 1.6f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.06f;
        noise.frequency = 0.2f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AdditiveMaterial("M_DomeDust", DotTexture());
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private Texture2D DotTexture()
    {
        string path = $"{assetFolder}/T_DomeDust.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int size = 32;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "T_DomeDust", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        t.SetPixels(px);
        t.Apply();
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        Vector3 c = transform.position;
        float h = ceilingHeight > 0f ? ceilingHeight : 5f;
        Gizmos.color = new Color(0.6f, 0.85f, 1f);
        const int n = 40;
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            float r = domeDiameter * 0.5f;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0) * r, h, Mathf.Sin(a0) * r), c + new Vector3(Mathf.Cos(a1) * r, h, Mathf.Sin(a1) * r));
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r), c + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r));
        }
        Gizmos.DrawLine(c, c + Vector3.up * h);
        if (sunlight)
        {
            // Rayo de sol: de la abertura al sitio del suelo donde cae la mancha de luz.
            Vector3 forward = Quaternion.Euler(sunElevation, sunAzimuth, 0f) * Vector3.forward;
            Vector3 top = c + Vector3.up * h;
            Vector3 patch = top + forward * (h / Mathf.Max(0.1f, Mathf.Sin(sunElevation * Mathf.Deg2Rad)));
            Gizmos.color = new Color(1f, 0.9f, 0.5f);
            Gizmos.DrawLine(top - forward * 6f, patch);
            Gizmos.DrawWireSphere(patch, 0.4f);
            DebugLabels.At(patch + Vector3.up * 0.6f, 0, "Mancha de sol", DebugLabels.Casino);
        }
    }
#endif
}

#if UNITY_EDITOR
/// <summary>Botones en el Inspector para construir y borrar.</summary>
[CustomEditor(typeof(CeilingDomeBuilder))]
public class CeilingDomeBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Pon este objeto en el suelo, justo debajo del centro de la cúpula. Arrastra Walls a Walls Root, M_Techo y PF_CupulaTecho, y pulsa Construir. Al reconstruir se pierden los retoques hechos a mano dentro de \"Generado\".", MessageType.Info);
        GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
        if (GUILayout.Button("Construir techo y cúpula", GUILayout.Height(32)))
            foreach (Object t in targets) ((CeilingDomeBuilder)t).Build();
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Borrar lo generado"))
            foreach (Object t in targets) ((CeilingDomeBuilder)t).Clear();
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
#endif
