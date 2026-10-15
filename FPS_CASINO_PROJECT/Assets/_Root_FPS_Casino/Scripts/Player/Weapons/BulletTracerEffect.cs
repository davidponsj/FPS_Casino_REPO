using UnityEngine;

/// <summary>
/// Línea visual instantánea desde el cañón hasta el punto de impacto (o hasta el alcance
/// máximo si no golpea nada). Se adelgaza y se autodestruye sola. El color y el grosor se
/// fijan aquí por código, así no dependen de que el material tenga el shader correcto para
/// respetar el Color del Line Renderer.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BulletTracerEffect : MonoBehaviour
{
    [Tooltip("Cuánto dura visible el trazador. Las balas reales son casi instantáneas.")]
    [SerializeField] private float duration = 0.035f;

    [Tooltip("Grosor en unidades del mundo. 1 (el valor por defecto de Unity) es enorme; algo entre 0.01 y 0.03 se ve como un trazador real.")]
    [SerializeField] private float width = 0.02f;

    [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.3f, 1f); // amarillo cálido

    private LineRenderer lr;
    private float timer;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;

        lr.startWidth = width;
        lr.endWidth = width * 0.5f; // un poco más fino en la punta, look de trazador

        lr.startColor = tracerColor;
        lr.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f); // se desvanece hacia el final de la línea

        // Fuerza una instancia de material propia y le mete el color base también,
        // por si el shader no lee el Color del Line Renderer.
        lr.material.color = tracerColor;
    }

    public void Play(Vector3 start, Vector3 end)
    {
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        timer = duration;
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        float t = Mathf.Clamp01(timer / duration);
        lr.widthMultiplier = t;

        if (timer <= 0f)
            Destroy(gameObject);
    }
}