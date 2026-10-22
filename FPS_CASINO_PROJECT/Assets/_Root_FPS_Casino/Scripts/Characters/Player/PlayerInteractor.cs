using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private TMP_Text promptText;

    [Header("Ajustes")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableMask = ~0;

    private IInteractable currentInteractable;
    private string lastPrompt = "";

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

        currentInteractable = found;

        if (currentInteractable == null)
        {
            SetPromptVisible(false);
            lastPrompt = "";
            return;
        }

        string newPrompt = currentInteractable.InteractionPrompt;

        if (promptText != null)
        {
            promptText.text = newPrompt;
            SetPromptVisible(true);
        }

        if (newPrompt != lastPrompt)
        {
            Debug.Log("[Prompt] " + newPrompt);
            lastPrompt = newPrompt;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptText != null && promptText.gameObject.activeSelf != visible)
            promptText.gameObject.SetActive(visible);
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        currentInteractable?.Interact(gameObject);
    }
}