using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Interacción con la tecla E. Va en el Player. Cada frame lanza un rayo desde la cámara; si toca
/// algo que implementa IInteractable (máquinas, armas de pared, barman...) enseña su texto en
/// promptText, y al pulsar Interact (Player Input → Unity Events → OnInteract) llama a su Interact.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Cámara desde la que sale el rayo. Si está vacío, usa la cámara hija del Player.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Texto del HUD donde sale el prompt (\"Coger Lanzacartas [E]\"). Se oculta si no miras nada.")]
    [SerializeField] private TMP_Text promptText;

    [Header("Ajustes")]
    [Tooltip("Metros hasta los que llega el rayo de interacción. 3 = alcance de la mano.")]
    [Min(0.1f)]
    [SerializeField] private float interactionRange = 3f;
    [Tooltip("Capas que puede tocar el rayo. Con Everything, una pared delante de la máquina la tapa.")]
    [SerializeField] private LayerMask interactableMask = ~0;

    [Header("Depuración")]
    [Tooltip("Dibuja el rayo en la vista Scene (verde si toca algo interactuable) y el nombre de lo que miras.")]
    [SerializeField] private bool showDebug;

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

        currentInteractable = found;

        if (currentInteractable == null)
        {
            SetPromptVisible(false);
            return;
        }

        if (promptText != null)
        {
            promptText.text = currentInteractable.InteractionPrompt;
            SetPromptVisible(true);
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || playerCamera == null) return;

        Transform cam = playerCamera.transform;
        Vector3 end = cam.position + cam.forward * interactionRange;
        Gizmos.color = currentInteractable != null ? Color.green : Color.gray;
        Gizmos.DrawLine(cam.position, end);

        if (currentInteractable is Component target)
            DebugLabels.At(end, 0, $"Mirando: {target.name}\n{currentInteractable.InteractionPrompt}", DebugLabels.Interaction);
    }
#endif
}
