using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aspecto del charco de las botellas (va junto a BottlePuddle, que se encarga de crecer, durar y hacer daño):
/// - Forma de charco de verdad: una mancha irregular (no un círculo perfecto) con salpicaduras pequeñas alrededor,
///   más oscura en el centro (profundidad), con el borde algo más claro y que se difumina contra el suelo.
/// - El borde se mece despacio, como líquido.
/// - Brilla un poco con el color de la botella y late suave (como algo químico).
/// - Burbujas que salen y explotan sobre el charco (las crea él mismo con un Particle System).
///
/// El color lo pone BottlePuddle (Init) y este script lo lee al empezar: no hay que tocar nada más.
/// El material del charco tiene que ser URP Particles/Lit, transparente, con Emission activada: es el que usa los
/// colores de vértice (centro oscuro, borde claro, difuminado). Las burbujas necesitan su material (Bubble Material).
/// La forma mide 1 de ancho, como el cilindro de antes: el tamaño del charco sigue siendo la escala del objeto.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(Renderer))]
public class PuddleVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Forma")]
    [Tooltip("Puntos del borde. Más = borde más suave.")]
    [Range(16, 96)]
    [SerializeField] private int edgePoints = 48;
    [Tooltip("Lo irregular que es la mancha. 0 = círculo perfecto, 0.15 = charco natural.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float irregularity = 0.14f;
    [Tooltip("Gotas sueltas alrededor del charco (salpicaduras).")]
    [Range(0, 12)]
    [SerializeField] private int droplets = 6;
    [Tooltip("Tamaño de las gotas sueltas, respecto al charco (mín, máx).")]
    [SerializeField] private Vector2 dropletSize = new Vector2(0.03f, 0.08f);
    [Tooltip("Cuánto se mece el borde. 0.03 = se nota que es líquido sin exagerar.")]
    [Range(0f, 0.1f)]
    [SerializeField] private float wobble = 0.03f;
    [Tooltip("Velocidad del vaivén del borde.")]
    [Min(0f)]
    [SerializeField] private float wobbleSpeed = 1.2f;

    [Header("Color")]
    [Tooltip("Brillo del centro respecto al borde (profundidad). 1 = igual que el borde, 0.55 = bastante más oscuro.")]
    [Range(0f, 1f)]
    [SerializeField] private float centerShade = 0.55f;
    [Tooltip("Opacidad del líquido.")]
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.9f;
    [Tooltip("Ancho del difuminado del borde contra el suelo, respecto al charco.")]
    [Range(0.01f, 0.4f)]
    [SerializeField] private float edgeSoftness = 0.1f;

    [Header("Brillo")]
    [Tooltip("Brillo propio con el color de la botella. 0 = nada, 0.6 = se ve aunque esté oscuro.")]
    [Min(0f)]
    [SerializeField] private float glow = 0.6f;
    [Tooltip("Cuánto late el brillo. 0 = fijo.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowPulse = 0.35f;
    [Tooltip("Latidos por segundo del brillo.")]
    [Min(0f)]
    [SerializeField] private float glowPulseSpeed = 0.8f;

    [Header("Burbujas")]
    [Tooltip("Material de las burbujas (URP Particles/Unlit, transparente, con la textura Default-Particle). Vacío = sin burbujas.")]
    [SerializeField] private Material bubbleMaterial;
    [Tooltip("Burbujas por segundo en un charco de tamaño normal.")]
    [Min(0f)]
    [SerializeField] private float bubblesPerSecond = 16f;
    [Tooltip("Tamaño de las burbujas en metros (mín, máx).")]
    [SerializeField] private Vector2 bubbleSize = new Vector2(0.03f, 0.08f);
    [Tooltip("Segundos que dura cada burbuja hasta que explota (mín, máx).")]
    [SerializeField] private Vector2 bubbleLifetime = new Vector2(0.4f, 1.1f);
    [Tooltip("Velocidad a la que suben, en metros por segundo.")]
    [Min(0f)]
    [SerializeField] private float bubbleRise = 0.25f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola el color que ha leído y si ha creado las burbujas.")]
    [SerializeField] private bool showDebug;

    private class Blob
    {
        public Vector2 center;
        public float[] radii;    // radio de cada punto del borde (sin vaivén)
        public float[] phases;   // desfase del vaivén de cada punto
        public int firstVertex;
    }

    private Renderer puddleRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private MaterialPropertyBlock block;
    private Color liquidColor = Color.white;
    private readonly List<Blob> blobs = new List<Blob>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<int> triangles = new List<int>();
    private float seed;

    // Anillos de cada mancha, de dentro a fuera: centro, medio, borde claro y difuminado.
    private const int Rings = 4;
    // Ondas del borde de cada mancha (veces que sube y baja en una vuelta).
    private static readonly int[] Frequencies = { 2, 3, 4, 5, 7, 9 };

    private void Awake()
    {
        puddleRenderer = GetComponent<Renderer>();
        meshFilter = GetComponent<MeshFilter>();
        block = new MaterialPropertyBlock();
        seed = Random.value * 100f;
    }

    private void Start()
    {
        // BottlePuddle.Init ya ha puesto el color de la botella (se llama justo al crearlo, antes de Start).
        puddleRenderer.GetPropertyBlock(block);
        Color fromBlock = block.GetColor(BaseColorId);
        if (fromBlock.a > 0f) liquidColor = fromBlock;
        liquidColor.a = 1f;
        if (showDebug) Debug.Log($"{name}: charco de color {liquidColor}.", this);

        BuildBlobs();
        mesh = new Mesh { name = "PuddleBlob" };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;
        UpdateMesh(0f);
        CreateBubbles();
    }

    private void Update()
    {
        if (mesh == null) return;
        UpdateMesh(Time.time);

        // Siempre se pone (con Glow a 0 queda negro): así el color de Emission del material no se ve nunca.
        float pulse = 1f + glowPulse * Mathf.Sin(Time.time * glowPulseSpeed * Mathf.PI * 2f + seed);
        puddleRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, liquidColor * (glow * pulse));
        puddleRenderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    // ---------------- FORMA ----------------

    private void BuildBlobs()
    {
        blobs.Clear();
        // Mancha principal: radio que cambia con el ángulo (varias ondas al azar) → borde irregular.
        blobs.Add(MakeBlob(Vector2.zero, 0.44f, irregularity));

        for (int i = 0; i < droplets; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float size = Random.Range(dropletSize.x, dropletSize.y);
            float distance = Random.Range(0.42f, 0.5f - size * 0.3f);
            blobs.Add(MakeBlob(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, size, irregularity * 0.5f));
        }
    }

    private Blob MakeBlob(Vector2 center, float radius, float irregular)
    {
        var blob = new Blob { center = center, radii = new float[edgePoints], phases = new float[edgePoints] };
        // Suma de ondas con frecuencias y desfases al azar: queda una forma orgánica y cerrada.
        // Las bajas dan la forma general; las altas, pequeñas, dan el borde "derramado".
        float[] amps = new float[Frequencies.Length], offs = new float[Frequencies.Length];
        for (int k = 0; k < Frequencies.Length; k++)
        {
            amps[k] = Random.Range(0.3f, 1f) * irregular / (k + 1);
            offs[k] = Random.value * Mathf.PI * 2f;
        }
        for (int i = 0; i < edgePoints; i++)
        {
            float a = i / (float)edgePoints * Mathf.PI * 2f;
            float r = 1f;
            for (int k = 0; k < Frequencies.Length; k++) r += amps[k] * Mathf.Sin(Frequencies[k] * a + offs[k]);
            blob.radii[i] = radius * r;
            blob.phases[i] = Random.value * Mathf.PI * 2f;
        }
        return blob;
    }

    private void UpdateMesh(float time)
    {
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        Color center = new Color(centerShade, centerShade, centerShade, opacity);
        Color middle = new Color(Mathf.Lerp(centerShade, 1f, 0.5f), Mathf.Lerp(centerShade, 1f, 0.5f), Mathf.Lerp(centerShade, 1f, 0.5f), opacity);
        Color rim = new Color(1f, 1f, 1f, Mathf.Min(1f, opacity + 0.05f));
        Color faded = new Color(1f, 1f, 1f, 0f);
        float[] ringScale = { 0.55f, 1f - edgeSoftness, 1f };
        Color[] ringColor = { middle, rim, faded };

        foreach (Blob blob in blobs)
        {
            blob.firstVertex = vertices.Count;
            vertices.Add(new Vector3(blob.center.x, 0f, blob.center.y));
            colors.Add(center);

            for (int ring = 0; ring < Rings - 1; ring++)
            {
                for (int i = 0; i < edgePoints; i++)
                {
                    float a = i / (float)edgePoints * Mathf.PI * 2f;
                    float sway = 1f + wobble * Mathf.Sin(time * wobbleSpeed + blob.phases[i] + seed);
                    float r = blob.radii[i] * sway * ringScale[ring];
                    vertices.Add(new Vector3(blob.center.x + Mathf.Cos(a) * r, 0f, blob.center.y + Mathf.Sin(a) * r));
                    colors.Add(ringColor[ring]);
                }
            }

            // Centro → primer anillo (abanico).
            int first = blob.firstVertex;
            for (int i = 0; i < edgePoints; i++)
            {
                int next = (i + 1) % edgePoints;
                triangles.Add(first);
                triangles.Add(first + 1 + next);
                triangles.Add(first + 1 + i);
            }
            // Anillo a anillo (bandas).
            for (int ring = 0; ring < Rings - 2; ring++)
            {
                int inner = first + 1 + ring * edgePoints;
                int outer = inner + edgePoints;
                for (int i = 0; i < edgePoints; i++)
                {
                    int next = (i + 1) % edgePoints;
                    triangles.Add(inner + i);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                    triangles.Add(inner + i);
                    triangles.Add(outer + next);
                    triangles.Add(outer + i);
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // ---------------- BURBUJAS ----------------

    private void CreateBubbles()
    {
        if (bubbleMaterial == null || bubblesPerSecond <= 0f) return;

        var bubbleObject = new GameObject("Bubbles");
        bubbleObject.transform.SetParent(transform, false);
        var particles = bubbleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color bubbleColor = Color.Lerp(liquidColor, Color.white, 0.45f);
        bubbleColor.a = 0.85f;

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(bubbleLifetime.x, bubbleLifetime.y);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(bubbleSize.x, bubbleSize.y);
        main.startColor = bubbleColor;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape; // el charco crece: la zona de burbujas también, el tamaño no

        var emission = particles.emission;
        emission.rateOverTime = bubblesPerSecond;

        // Salen de toda la superficie (un círculo tumbado del tamaño de la mancha).
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        // Suben despacio con un poco de vaivén.
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.y = new ParticleSystem.MinMaxCurve(bubbleRise * 0.5f, bubbleRise);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

        // Se hinchan y al final "revientan": crecen de golpe y desaparecen.
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.8f, 1f), new Keyframe(0.92f, 1.35f), new Keyframe(1f, 0f)));

        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var particleRenderer = bubbleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = bubbleMaterial;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        particles.Play();
        if (showDebug) Debug.Log($"{name}: burbujas creadas ({bubblesPerSecond}/s).", this);
    }
}
