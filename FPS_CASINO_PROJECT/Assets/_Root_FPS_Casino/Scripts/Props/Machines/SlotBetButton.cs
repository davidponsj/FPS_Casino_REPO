using UnityEngine;

public class SlotBetButton : MonoBehaviour, IInteractable
{
    [SerializeField] private SlotMachine machine;
    [SerializeField] private int betAmount = 10;

    [Header("Aspecto")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color normalColor = Color.white;
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