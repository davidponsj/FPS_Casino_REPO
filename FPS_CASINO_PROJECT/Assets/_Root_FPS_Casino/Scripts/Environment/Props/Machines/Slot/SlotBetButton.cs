using UnityEngine;

/// <summary>
/// Botón de apuesta de la tragaperras. Al pulsar E le dice a su SlotMachine que la apuesta es
/// betAmount. Mientras está elegido se queda hundido y brilla con su color.
/// </summary>
public class SlotBetButton : MonoBehaviour, IInteractable
{
    [Tooltip("Tragaperras a la que pertenece este botón.")]
    [SerializeField] private SlotMachine machine;
    [Tooltip("Puntos que se apuestan con este botón. 10 = apuesta pequeña.")]
    [Min(0)]
    [SerializeField] private int betAmount = 10;

    [Header("Brillo")]
    [Tooltip("Renderer que brilla. Vacío = el primero que encuentre en sus hijos.")]
    [SerializeField] private Renderer targetRenderer;
    [Tooltip("Color del brillo cuando está elegido. Pon el mismo color que el botón.")]
    [SerializeField] private Color glowColor = Color.white;
    [Tooltip("Fuerza del brillo. 2 = brillo claro sin quemar el color.")]
    [Min(0f)]
    [SerializeField] private float glowIntensity = 2f;

    [Header("Hundido")]
    [Tooltip("Eje local hacia el que se hunde (la línea amarilla al seleccionarlo). 0,0,0 = automático: el eje que más apunta hacia abajo.")]
    [SerializeField] private Vector3 pressAxis = Vector3.zero;
    [Tooltip("Metros que se hunde. 0.01 = 1 cm.")]
    [Min(0f)]
    [SerializeField] private float pressDepth = 0.01f;
    [Tooltip("Segundos que tarda en hundirse o subir. 0.08 = pulsación rápida.")]
    [Min(0.01f)]
    [SerializeField] private float pressSeconds = 0.08f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private bool selected;
    private float pressAmount;   // 0 = arriba, 1 = hundido
    private Vector3 restLocalPosition;
    private Material material;

    public int BetAmount => betAmount;

    public string InteractionPrompt
    {
        get
        {
            if (machine != null && machine.IsSpinning) return "Spinning...";
            if (selected) return $"{betAmount} pts bet selected";
            return $"Bet {betAmount} pts [E]";
        }
    }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            material = targetRenderer.material;   // copia propia: solo brilla este botón
            material.EnableKeyword("_EMISSION");
        }

        restLocalPosition = transform.localPosition;
        SetSelected(false);
    }

    private void Update()
    {
        float target = selected ? 1f : 0f;
        if (Mathf.Approximately(pressAmount, target)) return;

        pressAmount = Mathf.MoveTowards(pressAmount, target, Time.deltaTime / pressSeconds);
        ApplyPress();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (material != null)
            material.SetColor(EmissionColorId, value ? glowColor * glowIntensity : Color.black);
    }

    public void Interact(GameObject interactor)
    {
        if (machine == null)
        {
            Debug.LogWarning("[BetButton] Falta asignar la máquina.", this);
            return;
        }

        machine.SelectBet(this);
    }

    private void ApplyPress()
    {
        Vector3 rest = transform.parent != null
            ? transform.parent.TransformPoint(restLocalPosition)
            : restLocalPosition;
        transform.position = rest + PressDirection() * (pressDepth * pressAmount);
    }

    private Vector3 PressDirection()
    {
        if (pressAxis != Vector3.zero)
            return transform.TransformDirection(pressAxis).normalized;

        // Automático: de los ejes del botón, el que más apunta hacia abajo
        Vector3[] axes =
        {
            transform.right, -transform.right,
            transform.up, -transform.up,
            transform.forward, -transform.forward
        };

        Vector3 best = axes[0];
        foreach (Vector3 axis in axes)
            if (Vector3.Dot(axis, Vector3.down) > Vector3.Dot(best, Vector3.down))
                best = axis;
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        // Línea amarilla: hacia dónde se hunde el botón
        Vector3 dir = PressDirection();
        float length = Mathf.Max(pressDepth, 0.01f) * 5f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + dir * length);
        Gizmos.DrawSphere(transform.position + dir * length, length * 0.1f);
    }
}