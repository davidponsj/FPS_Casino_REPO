using UnityEngine;

/// <summary>
/// Botón de apuesta de la tragaperras. Al pulsar E le dice a su SlotMachine que la apuesta es
/// betAmount, y se pinta de selectedColor mientras está elegido.
/// </summary>
public class SlotBetButton : MonoBehaviour, IInteractable
{
    [Tooltip("Tragaperras a la que pertenece este botón.")]
    [SerializeField] private SlotMachine machine;
    [Tooltip("Puntos que se apuestan con este botón. 10 = apuesta pequeña.")]
    [Min(0)]
    [SerializeField] private int betAmount = 10;

    [Header("Aspecto")]
    [Tooltip("Renderer que cambia de color. Vacío = el primero que encuentre en sus hijos.")]
    [SerializeField] private Renderer targetRenderer;
    [Tooltip("Color cuando no está elegido.")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("Color cuando es la apuesta elegida.")]
    [SerializeField] private Color selectedColor = Color.green;

    private bool selected;

    public int BetAmount => betAmount;

    public string InteractionPrompt
    {
        get
        {
            if (machine != null && machine.IsSpinning) return "Girando...";
            if (selected) return $"Apuesta de {betAmount} pts seleccionada";
            return $"Apostar {betAmount} pts [E]";
        }
    }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (targetRenderer != null)
            targetRenderer.material.color = value ? selectedColor : normalColor;
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
}
