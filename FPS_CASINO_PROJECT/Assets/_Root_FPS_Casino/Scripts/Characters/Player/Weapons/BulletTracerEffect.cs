using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Línea visual instantánea desde el cañón hasta el punto de impacto (o hasta el alcance
/// máximo si no golpea nada). Se adelgaza y se autodestruye sola. El color y el grosor se
/// fijan aquí por código, así no dependen de que el material tenga el shader correcto para
/// respetar el Color del Line Renderer.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BulletTracerEffect : MonoBehaviour
{
    [Header("Trazador")]
    [Tooltip("Segundos que se ve el trazador. 0.035 = casi instantáneo, como una bala real.")]
    [FormerlySerializedAs("duration")]
    [SerializeField] private float visibleSeconds = 0.035f;

    [Tooltip("Grosor en metros en el cañón (en la punta es la mitad). 1 (el valor por defecto de Unity) es enorme; entre 0.01 y 0.03 se ve como un trazador real.")]
    [FormerlySerializedAs("width")]
    [SerializeField] private float widthMeters = 0.02f;

    [Tooltip("Color del trazador. Se desvanece a transparente hacia el final de la línea.")]
    [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.3f, 1f); // amarillo cálido

    private LineRenderer lr;
    private float timer;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;

        lr.startWidth = widthMeters;
        lr.endWidth = widthMeters * 0.5f; // un poco más fino en la punta, look de trazador

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
        timer = visibleSeconds;
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        float t = Mathf.Clamp01(timer / visibleSeconds);
        lr.widthMultiplier = t;

        if (timer <= 0f)
            Destroy(gameObject);
    }
}