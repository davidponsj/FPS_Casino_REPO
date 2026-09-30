using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cada frame lanza un raycast corto desde la cámara para ver si estás mirando algo
/// interactuable (IInteractable). Si es así, muestra un prompt en pantalla; al pulsar E
/// (acción "Interact"), ejecuta la interacción sobre lo que estés mirando en ese momento.
///
/// Conecta OnInteract desde el Player Input (Events > Player > Interact), igual que el
/// resto de acciones.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private TMP_Text promptText; // opcional: si no lo asignas, simplemente no muestra prompt

    [Header("Ajustes")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableMask = ~0;

    private IInteractable currentInteractable;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        SetPromptVisible(false);
    }

    private void Update()
    {
        DetectInteractable();
    }

    private void DetectInteractable()
    {
        IInteractable found = null;

        if (playerCamera != null &&
            Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                out RaycastHit hit, interactionRange, interactableMask, QueryTriggerInteraction.Collide))
        {
            found = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (found == currentInteractable) return;

        currentInteractable = found;

        if (currentInteractable != null)
        {
            if (promptText != null) promptText.text = currentInteractable.InteractionPrompt;
            SetPromptVisible(currentInteractable != null);
        }
        else
        {
            SetPromptVisible(false);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptText != null)
            promptText.gameObject.SetActive(visible);
    }

    // ---------------- INPUT (Player Input > Invoke Unity Events) ----------------

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        currentInteractable?.Interact(gameObject);
    }
}