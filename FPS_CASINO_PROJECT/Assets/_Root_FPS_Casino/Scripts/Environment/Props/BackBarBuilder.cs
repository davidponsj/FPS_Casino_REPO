using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Constructor de la contrabarra (lo que hay detrás del barman): mueble bajo, panel del fondo retroiluminado (con un PNG
/// si se le da), baldas con tira LED, botellas repartidas con variación, luces cálidas bajo las baldas y lámparas
/// colgantes sobre la barra. Da profundidad a un fondo plano: el PNG al fondo, las baldas y botellas de verdad delante,
/// y la luz entre medias.
///
/// Con Shelf Image (un PNG de baldas con botellas y fondo transparente), cada tira de botellas de la imagen se pone de
/// pie sobre su balda 3D, a escala real y repetida a lo ancho; delante pueden ir botellas 3D y detrás el panel con luz,
/// así se ven capas a distintas profundidades.
///
/// Uso: objeto vacío contra la pared, en el suelo y en el centro de la barra, con su eje Z (flecha azul) mirando a la
/// sala. En el Inspector, botón "Construir contrabarra". Se puede volver a construir cuantas veces haga falta (borra lo
/// anterior) y con Ctrl+Z se deshace. Lo generado queda en la escena como hijo "Generado": se puede retocar a mano, pero
/// si se vuelve a construir se pierden esos retoques.
/// Los materiales que falten se crean una vez como assets (M_BackBar_*) en la carpeta indicada.
/// En el juego no hace nada: solo sirve para montarlo en el editor.
/// </summary>
public class BackBarBuilder : MonoBehaviour
{
    // Los campos solo se usan en el editor: fuera de él Unity avisaría de que "no se usan".
#pragma warning disable 0414, 0169
    [Header("Medidas (metros)")]
    [Tooltip("Ancho total de la contrabarra.")]
    [Min(1f)]
    [SerializeField] private float width = 11f;
    [Tooltip("Alto del mueble bajo (encima van botellas).")]
    [Min(0.2f)]
    [SerializeField] private float cabinetHeight = 0.95f;
    [Tooltip("Fondo del mueble bajo. Pequeño, para que quepa el barman delante.")]
    [Min(0.1f)]
    [SerializeField] private float cabinetDepth = 0.35f;
    [Tooltip("Alturas de las baldas (desde el suelo).")]
    [SerializeField] private float[] shelfHeights = { 1.3f, 1.85f, 2.4f };
    [Tooltip("Fondo de las baldas.")]
    [Min(0.05f)]
    [SerializeField] private float shelfDepth = 0.25f;
    [Tooltip("Grosor de las baldas.")]
    [Min(0.01f)]
    [SerializeField] private float shelfThickness = 0.04f;
    [Tooltip("Hasta dónde sube el panel del fondo.")]
    [Min(0.5f)]
    [SerializeField] private float panelTop = 3f;

    [Header("Panel del fondo")]
    [Tooltip("Imagen del fondo (el PNG de la barra). Vacío = degradado cálido. Brilla un poco, como retroiluminado.")]
    [SerializeField] private Texture2D backImage;
    [Tooltip("Cuánto brilla el panel del fondo.")]
    [Min(0f)]
    [SerializeField] private float panelGlow = 1.2f;
    [Tooltip("Color de la luz del fondo (y del degradado si no hay imagen).")]
    [SerializeField] private Color panelColor = new Color(1f, 0.62f, 0.28f);

    [Header("Imagen de botellas por baldas (opcional)")]
    [Tooltip("PNG con baldas de botellas y fondo transparente. Cada tira (Strip Bands) se pone de pie sobre una balda 3D, la de arriba en la balda de arriba. Vacío = solo botellas 3D.")]
    [SerializeField] private Texture2D shelfImage;
    [Tooltip("Tiras de la imagen, en altura de 0 (abajo) a 1 (arriba): x = dónde empieza, y = dónde acaba. Los valores por defecto son los de vuestra imagen de 3 baldas (cortadas justo encima de cada balda pintada).")]
    [SerializeField]
    private Vector2[] stripBands = { new Vector2(0.697f, 1f), new Vector2(0.370f, 0.675f), new Vector2(0.043f, 0.348f) };
    [Tooltip("Alto real de cada tira en metros (las botellas de la imagen saldrán de ese tamaño). 0.45 = botellas de ~35 cm.")]
    [Min(0.05f)]
    [SerializeField] private float stripHeight = 0.45f;
    [Tooltip("Cuánto brillan las botellas de la imagen (como si les diera la luz de detrás).")]
    [Min(0f)]
    [SerializeField] private float imageGlow = 0.25f;
    [Tooltip("Con imagen: botellas 3D delante de la imagen en cada balda, más separadas (para dar relieve). Desmarcado = solo en el mueble.")]
    [SerializeField] private bool frontBottlesOnShelves = true;
    [Tooltip("Con imagen: cuánto más separadas van las botellas 3D de las baldas. 3 = una de cada tres.")]
    [Min(1f)]
    [SerializeField] private float frontBottleSparsity = 3f;

    [Header("Botellas 3D")]
    [Tooltip("Prefabs de botella (PF_BotellaAlcohol y las que tengáis). Sale uno al azar cada vez.")]
    [SerializeField] private GameObject[] bottlePrefabs;
    [Tooltip("Texturas de color de las botellas (T_BotellaMesa_A_Verde, _Ambar...): cada botella sale con una al azar. Cambian el color del vidrio, no la etiqueta. Vacío = el color original.")]
    [SerializeField] private Texture2D[] bottleColorTextures;
    [Tooltip("Alto de cada botella respecto al hueco que tiene hasta la balda de arriba (mín, máx). 0.5-0.9 = de medianas a casi tocando la balda.")]
    [SerializeField] private Vector2 heightFill = new Vector2(0.5f, 0.9f);
    [Tooltip("Alto real de las botellas en metros (mín, máx): nunca más pequeñas ni más grandes que esto.")]
    [SerializeField] private Vector2 heightRange = new Vector2(0.16f, 0.42f);
    [Tooltip("Variación del ancho (botellas más gorditas o más finas). 0.2 = ±20%.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float widthVariation = 0.2f;
    [Tooltip("Hueco entre una botella y la siguiente, en metros (mín, máx).")]
    [SerializeField] private Vector2 bottleGap = new Vector2(0.005f, 0.035f);
    [Tooltip("Probabilidad de dejar un hueco más grande (así no parece una fila de fábrica).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float gapChance = 0.1f;
    [Tooltip("Tinte extra (multiplica todo, también la etiqueta). Normalmente a 0: el color ya lo dan las texturas.")]
    [SerializeField]
    private Color[] bottleTints =
    {
        new Color(0.85f, 0.55f, 0.2f), new Color(0.3f, 0.6f, 0.3f), new Color(0.95f, 0.95f, 0.95f)
    };
    [Range(0f, 1f)]
    [SerializeField] private float tintStrength = 0f;
    [Tooltip("Semilla del reparto. Cambia el número para otra colocación de botellas.")]
    [SerializeField] private int seed = 7;

    [Header("Luz")]
    [Tooltip("Color de las tiras LED y de la luz bajo las baldas.")]
    [SerializeField] private Color ledColor = new Color(1f, 0.78f, 0.48f);
    [Tooltip("Brillo de las tiras LED.")]
    [Min(0f)]
    [SerializeField] private float ledGlow = 3f;
    [Tooltip("Metros entre las luces de cada balda. 3.5 = 3 luces por balda en 11 m.")]
    [Min(0.5f)]
    [SerializeField] private float lightSpacing = 3.5f;
    [SerializeField, Min(0f)] private float shelfLightIntensity = 1.6f;
    [Tooltip("Luces que dan el resplandor del panel hacia delante (sobre el barman y las botellas).")]
    [Min(0)]
    [SerializeField] private int glowLights = 3;

    [Header("Lámparas colgantes sobre la barra")]
    [Tooltip("Lámpara de techo (PF_LamparaTecho). Vacío = sin lámparas.")]
    [SerializeField] private GameObject pendantPrefab;
    [Min(0)]
    [SerializeField] private int pendantCount = 4;
    [Tooltip("Metros desde la pared hasta la línea de lámparas (sobre la barra).")]
    [SerializeField] private float pendantDistance = 1.7f;
    [Tooltip("Altura de las lámparas.")]
    [SerializeField] private float pendantHeight = 2.6f;
    [SerializeField, Min(0f)] private float pendantIntensity = 3f;

    [Header("Materiales (vacíos = se crean solos)")]
    [SerializeField] private Material cabinetMaterial;
    [SerializeField] private Material shelfMaterial;
    [Tooltip("Carpeta donde se guardan los materiales que se creen.")]
    [SerializeField] private string materialFolder = "Assets/_Root_FPS_Casino/Art/3D/Materials/Props";

    [Header("Depuración")]
    [Tooltip("Dibuja en la vista Scene el contorno de la contrabarra y la línea de las lámparas.")]
    [SerializeField] private bool showDebug = true;

    private const string GeneratedName = "Generado";
#pragma warning restore 0414, 0169

#if UNITY_EDITOR
    [ContextMenu("Construir contrabarra")]
    public void Build()
    {
        Undo.SetCurrentGroupName("Construir contrabarra");
        int group = Undo.GetCurrentGroup();
        Clear();

        var root = new GameObject(GeneratedName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Contrabarra");
        root.SetParent(transform, false);

        var rng = new System.Random(seed);
        Material cabinet = cabinetMaterial != null ? cabinetMaterial : LitMaterial("M_BackBar_Cabinet", new Color(0.08f, 0.07f, 0.07f), 0.65f);
        Material shelf = shelfMaterial != null ? shelfMaterial : LitMaterial("M_BackBar_Shelf", new Color(0.12f, 0.1f, 0.09f), 0.8f);
        Material led = EmissiveMaterial("M_BackBar_LED", ledColor, ledGlow, null);
        Material panel = EmissiveMaterial("M_BackBar_Panel", backImage != null ? Color.white : panelColor, panelGlow,
            backImage != null ? backImage : null, backImage == null);

        // Las coordenadas locales: X a lo ancho, Y arriba, Z hacia la sala (la pared queda en Z = 0).
        float half = width * 0.5f;

        // Panel del fondo, pegado a la pared, desde el mueble hasta arriba.
        float panelBottom = cabinetHeight - 0.02f;
        Box(root, "Panel", new Vector3(0f, (panelBottom + panelTop) * 0.5f, 0.012f), new Vector3(width, panelTop - panelBottom, 0.02f), panel, false);

        // Mueble bajo y su encimera.
        Box(root, "Mueble", new Vector3(0f, cabinetHeight * 0.5f, cabinetDepth * 0.5f), new Vector3(width, cabinetHeight, cabinetDepth), cabinet, true);
        Box(root, "Encimera", new Vector3(0f, cabinetHeight + 0.015f, cabinetDepth * 0.5f + 0.01f), new Vector3(width + 0.04f, 0.03f, cabinetDepth + 0.04f), shelf, true);
        Box(root, "LED_Mueble", new Vector3(0f, cabinetHeight - 0.03f, cabinetDepth + 0.012f), new Vector3(width - 0.1f, 0.015f, 0.01f), led, false);
        bottleVariants = BuildBottleVariants();
        float firstShelfBottom = shelfHeights.Length > 0 ? shelfHeights[0] - shelfThickness * 0.5f : panelTop;
        PlaceBottles(root, cabinetHeight + 0.03f, firstShelfBottom - (cabinetHeight + 0.03f), cabinetDepth * 0.55f, half - 0.15f, rng, 1f);
        Material strips = shelfImage != null ? StripMaterial() : null;

        // Baldas: tabla, tira LED en el canto de abajo, luces y botellas encima.
        for (int i = 0; i < shelfHeights.Length; i++)
        {
            float y = shelfHeights[i];
            Box(root, $"Balda_{i + 1}", new Vector3(0f, y, shelfDepth * 0.5f), new Vector3(width - 0.3f, shelfThickness, shelfDepth), shelf, true);
            Box(root, $"LED_Balda_{i + 1}", new Vector3(0f, y - shelfThickness * 0.5f - 0.006f, shelfDepth - 0.02f), new Vector3(width - 0.4f, 0.012f, 0.012f), led, false);
            AddShelfLights(root, i, y - shelfThickness * 0.5f - 0.03f, shelfDepth - 0.05f, half);
            float top = y + shelfThickness * 0.5f;
            float above = (i + 1 < shelfHeights.Length ? shelfHeights[i + 1] - shelfThickness * 0.5f : panelTop) - top;
            if (strips != null)
            {
                // La tira de botellas de la imagen, de pie al fondo de la balda; las 3D (pocas) delante.
                int band = shelfHeights.Length - 1 - i;   // la balda de arriba lleva la tira de arriba
                if (band >= 0 && band < stripBands.Length) AddStrip(root, i, band, top, strips, rng);
                if (frontBottlesOnShelves) PlaceBottles(root, top, above * 0.75f, shelfDepth * 0.72f, half - 0.3f, rng, frontBottleSparsity);
            }
            else
            {
                PlaceBottles(root, top, above, shelfDepth * 0.5f, half - 0.3f, rng, 1f);
            }
        }

        // Resplandor del panel hacia delante.
        for (int i = 0; i < glowLights; i++)
        {
            float x = glowLights == 1 ? 0f : Mathf.Lerp(-half * 0.7f, half * 0.7f, i / (float)(glowLights - 1));
            Light glow = NewLight(root, $"Luz_Panel_{i + 1}", new Vector3(x, (cabinetHeight + panelTop) * 0.5f, 0.35f), LightType.Point, panelColor, 1.4f, 3.2f);
            glow.transform.localRotation = Quaternion.identity;
        }

        // Lámparas colgantes sobre la barra, con su foco hacia abajo.
        if (pendantPrefab != null && pendantCount > 0)
        {
            for (int i = 0; i < pendantCount; i++)
            {
                float x = pendantCount == 1 ? 0f : Mathf.Lerp(-half * 0.8f, half * 0.8f, i / (float)(pendantCount - 1));
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(pendantPrefab, root);
                lamp.name = $"Lampara_{i + 1}";
                lamp.transform.localPosition = new Vector3(x, pendantHeight, pendantDistance);
                lamp.transform.localRotation = Quaternion.identity;
                Light spot = NewLight(root, $"Foco_Lampara_{i + 1}", new Vector3(x, pendantHeight - 0.15f, pendantDistance), LightType.Spot, new Color(1f, 0.82f, 0.6f), pendantIntensity, pendantHeight + 1f);
                spot.spotAngle = 70f;
                spot.innerSpotAngle = 35f;
                spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // hacia abajo
                spot.shadows = LightShadows.Soft;
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(gameObject);
        Debug.Log($"[Contrabarra] Construida: {root.childCount} piezas.", this);
    }

    [ContextMenu("Borrar lo generado")]
    public void Clear()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    /// <summary>Una tira de la imagen de pie sobre la balda: repetida a lo ancho y desplazada al azar en cada balda.</summary>
    private void AddStrip(Transform root, int shelf, int band, float surfaceY, Material material, System.Random rng)
    {
        Vector2 v = stripBands[band];
        float vHeight = Mathf.Max(0.01f, v.y - v.x);
        float tileWidth = stripHeight * shelfImage.width / (vHeight * shelfImage.height);   // metros de imagen a lo ancho
        float stripWidth = width - 0.35f;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"Botellas_Imagen_{shelf + 1}";
        DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(root, false);
        quad.transform.localPosition = new Vector3(0f, surfaceY + stripHeight * 0.5f, shelfDepth * 0.3f);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // la cara visible del Quad, hacia la sala
        quad.transform.localScale = new Vector3(stripWidth, stripHeight, 1f);

        var renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetVector("_BaseMap_ST", new Vector4(stripWidth / tileWidth, vHeight, (float)rng.NextDouble(), v.x));
        renderer.SetPropertyBlock(block);
        GameObjectUtility.SetStaticEditorFlags(quad, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    private Material StripMaterial()
    {
        Material m = LoadMaterial("M_BackBar_BottleImage");
        if (m == null) m = SaveMaterial(new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_BackBar_BottleImage" });
        m.SetTexture("_BaseMap", shelfImage);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.55f);
        // Recorte por transparencia: se ven las botellas, no el fondo del PNG.
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", shelfImage);
        m.SetColor("_EmissionColor", Color.white * imageGlow);
        EditorUtility.SetDirty(m);
        return m;
    }

    private Material[] bottleVariants;

    /// <summary>
    /// Fila de botellas sobre una superficie. Cada botella se mide (sus Renderers) y se escala para que su alto sea un
    /// valor al azar que quepa en el hueco hasta la balda de arriba; se apoya justo sobre la superficie y la siguiente
    /// va pegada según su ancho real, así nunca se solapan ni atraviesan las baldas.
    /// sparsity > 1 = más separadas (las de delante de la imagen).
    /// </summary>
    private void PlaceBottles(Transform root, float surfaceY, float clearance, float z, float halfWidth, System.Random rng, float sparsity)
    {
        if (bottlePrefabs == null || bottlePrefabs.Length == 0 || clearance < heightRange.x + 0.02f) return;
        float surfaceWorldY = transform.TransformPoint(new Vector3(0f, surfaceY, 0f)).y;
        float x = -halfWidth;
        int n = 0;
        while (x < halfWidth)
        {
            if (rng.NextDouble() < gapChance) { x += Mathf.Lerp(0.05f, 0.18f, (float)rng.NextDouble()); continue; }

            GameObject prefab = bottlePrefabs[rng.Next(bottlePrefabs.Length)];
            if (prefab == null) break;
            var bottle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            bottle.name = $"Botella_{surfaceY:0.00}_{n++}";
            bottle.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            bottle.transform.localPosition = new Vector3(0f, surfaceY, z);

            Bounds bounds = WorldBounds(bottle);
            if (bounds.size.y < 1e-4f) { DestroyImmediate(bottle); break; }

            // Alto al azar dentro del hueco (y dentro de lo realista), ancho con un poco de variación.
            float maxHeight = Mathf.Min(heightRange.y, clearance - 0.02f);
            float target = Mathf.Clamp(clearance * Mathf.Lerp(heightFill.x, heightFill.y, (float)rng.NextDouble()), heightRange.x, maxHeight);
            float k = target / bounds.size.y;
            float w = 1f + ((float)rng.NextDouble() * 2f - 1f) * widthVariation;
            bottle.transform.localScale = Vector3.Scale(bottle.transform.localScale, new Vector3(k * w, k, k * w));

            bounds = WorldBounds(bottle);
            float halfBottle = Mathf.Max(bounds.extents.x, bounds.extents.z);
            if (x + halfBottle * 2f > halfWidth) { DestroyImmediate(bottle); break; }

            float jitterZ = (float)(rng.NextDouble() - 0.5) * 0.04f;
            bottle.transform.localPosition = new Vector3(x + halfBottle, surfaceY, z + jitterZ);
            // Apoyada justo encima: su punto más bajo a la altura de la superficie (da igual dónde tenga el pivote).
            bounds = WorldBounds(bottle);
            bottle.transform.position += Vector3.up * (surfaceWorldY - bounds.min.y);

            ApplyVariant(bottle, rng);
            Tint(bottle, rng);
            x += halfBottle * 2f + Mathf.Lerp(bottleGap.x, bottleGap.y, (float)rng.NextDouble()) + (sparsity - 1f) * halfBottle * 2f;
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

    /// <summary>Un material por textura de color (copia del material original de la botella con otra textura).</summary>
    private Material[] BuildBottleVariants()
    {
        if (bottleColorTextures == null || bottleColorTextures.Length == 0 || bottlePrefabs == null || bottlePrefabs.Length == 0 || bottlePrefabs[0] == null)
            return null;
        Renderer source = bottlePrefabs[0].GetComponentInChildren<Renderer>();
        if (source == null || source.sharedMaterial == null) return null;

        var list = new List<Material>();
        foreach (Texture2D texture in bottleColorTextures)
        {
            if (texture == null) continue;
            string assetName = "M_Botella_" + texture.name.Replace("T_BotellaMesa_A_", "").Replace("T_", "");
            Material m = LoadMaterial(assetName);
            if (m == null) m = SaveMaterial(new Material(source.sharedMaterial) { name = assetName });
            m.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(m);
            list.Add(m);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    private void ApplyVariant(GameObject bottle, System.Random rng)
    {
        if (bottleVariants == null) return;
        Material variant = bottleVariants[rng.Next(bottleVariants.Length)];
        foreach (Renderer r in bottle.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = variant;
    }

    private void Tint(GameObject bottle, System.Random rng)
    {
        if (bottleTints == null || bottleTints.Length == 0 || tintStrength <= 0f) return;
        Color tint = Color.Lerp(Color.white, bottleTints[rng.Next(bottleTints.Length)], tintStrength);
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in bottle.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            r.SetPropertyBlock(block);
        }
    }

    private void AddShelfLights(Transform root, int shelf, float y, float z, float half)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(width / lightSpacing));
        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? 0f : Mathf.Lerp(-half + lightSpacing * 0.5f, half - lightSpacing * 0.5f, i / (float)(count - 1));
            Light l = NewLight(root, $"Luz_Balda_{shelf + 1}_{i + 1}", new Vector3(x, y, z), LightType.Spot, ledColor, shelfLightIntensity, 1.3f);
            l.spotAngle = 120f;
            l.innerSpotAngle = 60f;
            l.transform.localRotation = Quaternion.Euler(80f, 0f, 0f); // hacia abajo, un poco hacia la sala
        }
    }

    private static Light NewLight(Transform parent, string lightName, Vector3 localPos, LightType type, Color color, float intensity, float range)
    {
        var go = new GameObject(lightName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void Box(Transform parent, string boxName, Vector3 localPos, Vector3 size, Material material, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = boxName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
    }

    // ---------------- MATERIALES ----------------

    private Material LitMaterial(string assetName, Color color, float smoothness)
    {
        Material existing = LoadMaterial(assetName);
        if (existing != null) return existing;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = assetName };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        return SaveMaterial(m);
    }

    private Material EmissiveMaterial(string assetName, Color color, float glow, Texture2D texture, bool gradient = false)
    {
        // El panel se rehace cada vez (por si cambias la imagen o el color); el LED también (por si cambias el brillo).
        Material m = LoadMaterial(assetName);
        if (m == null) m = SaveMaterial(new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = assetName });

        Texture2D tex = texture != null ? texture : gradient ? GradientTexture() : null;
        m.SetColor("_BaseColor", texture != null ? Color.white : color * 0.4f);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", 0.3f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", color * glow);
        EditorUtility.SetDirty(m);
        return m;
    }

    private Texture2D GradientTexture()
    {
        string path = $"{materialFolder}/T_BackBar_Glow.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        // Más brillante en el centro y abajo (como si la luz viniera de detrás de las botellas), oscuro en los bordes.
        const int w = 256, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "T_BackBar_Glow", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f) * 2f - 1f, v = y / (h - 1f);
                float edge = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(u), 3f));
                float vertical = Mathf.Lerp(1f, 0.35f, v);
                float k = edge * vertical;
                px[y * w + x] = new Color(k, k, k, 1f);
            }
        }
        t.SetPixels(px);
        t.Apply();
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    private Material LoadMaterial(string assetName) => AssetDatabase.LoadAssetAtPath<Material>($"{materialFolder}/{assetName}.mat");

    private Material SaveMaterial(Material m)
    {
        AssetDatabase.CreateAsset(m, $"{materialFolder}/{m.name}.mat");
        return m;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.7f, 0.3f);
        Gizmos.DrawWireCube(new Vector3(0f, panelTop * 0.5f, cabinetDepth * 0.5f), new Vector3(width, panelTop, cabinetDepth));
        Gizmos.color = new Color(1f, 0.9f, 0.6f);
        Gizmos.DrawLine(new Vector3(-width * 0.4f, pendantHeight, pendantDistance), new Vector3(width * 0.4f, pendantHeight, pendantDistance));
        Gizmos.DrawRay(Vector3.zero, Vector3.forward * 1f); // hacia la sala
    }
#endif
}

#if UNITY_EDITOR
/// <summary>Botones grandes en el Inspector para construir y borrar (además del menú ⋮ del componente).</summary>
[CustomEditor(typeof(BackBarBuilder))]
public class BackBarBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Pon este objeto contra la pared, en el suelo y en el centro de la barra, con la flecha azul mirando a la sala. Rellena los campos y pulsa Construir. Al reconstruir se pierden los retoques hechos a mano dentro de \"Generado\".", MessageType.Info);
        GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
        if (GUILayout.Button("Construir contrabarra", GUILayout.Height(32)))
            foreach (Object t in targets) ((BackBarBuilder)t).Build();
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Borrar lo generado"))
            foreach (Object t in targets) ((BackBarBuilder)t).Clear();
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
#endif
