using UnityEngine;

/// <summary>
/// Charco que deja una botella al romperse. De momento es un placeholder (un cilindro aplanado)
/// hasta que tengamos el modelo bueno: aparece creciendo, dura puddleLifetime segundos y
/// desaparece encogiéndose.
///
/// Tiene un Collider en modo trigger y guarda el Ticket de la botella: ahí se engancharán los
/// efectos sobre los enemigos (OnTriggerEnter/Stay según el tipo de botella) cuando los decidamos.
/// </summary>
public class BottlePuddle : MonoBehaviour
{
    [SerializeField] private Renderer puddleRenderer;
    [SerializeField] private float puddleLifetime = 8f;
    [Tooltip("Segundos que tarda en extenderse al aparecer.")]
    [SerializeField] private float growTime = 0.3f;
    [Tooltip("Segundos que tarda en encogerse al final de su vida.")]
    [SerializeField] private float shrinkTime = 1f;

    // URP/Lit usa "_BaseColor" como color principal.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Vector3 fullScale;
    private float age;

    /// <summary>Tipo de botella que creó el charco. Lo usarán los efectos sobre enemigos.</summary>
    public TicketData Ticket { get; private set; }

    private void Awake()
    {
        if (puddleRenderer == null)
            puddleRenderer = GetComponentInChildren<Renderer>();

        fullScale = transform.localScale;
        transform.localScale = new Vector3(0f, fullScale.y, 0f);
    }

    public void Init(Color color, TicketData ticket)
    {
        Ticket = ticket;

        if (puddleRenderer != null)
        {
            // MaterialPropertyBlock: cada charco su color sin crear una copia del material.
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            puddleRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            puddleRenderer.SetPropertyBlock(block);
        }
    }

    private void Update()
    {
        age += Time.deltaTime;

        // Crece al aparecer, se mantiene y se encoge al final. Solo en X/Z: el charco se
        // extiende y se retira por el suelo, su grosor no cambia.
        float grow = growTime > 0f ? Mathf.Clamp01(age / growTime) : 1f;
        float shrink = shrinkTime > 0f ? Mathf.Clamp01((puddleLifetime - age) / shrinkTime) : 1f;
        float size = Mathf.SmoothStep(0f, 1f, Mathf.Min(grow, shrink));

        transform.localScale = new Vector3(fullScale.x * size, fullScale.y, fullScale.z * size);

        if (age >= puddleLifetime)
            Destroy(gameObject);
    }
}
