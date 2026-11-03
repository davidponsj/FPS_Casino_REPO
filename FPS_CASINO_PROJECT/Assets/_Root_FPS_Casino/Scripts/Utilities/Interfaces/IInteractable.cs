/// <summary>
/// Cualquier cosa con la que el jugador pueda interactuar pulsando E: armas de pared,
/// puertas, máquinas tragaperras/ruleta, botones de oleada, etc.
/// </summary>
public interface IInteractable
{
    /// <summary>Texto a mostrar en el prompt de interacción, ej. "Coger Pistola [E]".</summary>
    string InteractionPrompt { get; }

    void Interact(UnityEngine.GameObject interactor);
}