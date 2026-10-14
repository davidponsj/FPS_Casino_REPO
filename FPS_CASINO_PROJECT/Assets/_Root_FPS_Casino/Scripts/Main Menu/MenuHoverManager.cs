using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class MenuHoverManager : MonoBehaviour
{
    [SerializeField] private Camera menuCamera;
    [SerializeField] private MenuCameraMover cameraMover;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private float maxDistance = 5000f;
    [SerializeField] private LayerMask hoverMask = ~0;

    private HoverOutline current;

    private void Awake()
    {
        if (menuCamera == null) menuCamera = Camera.main;
        if (cameraMover == null) cameraMover = GetComponent<MenuCameraMover>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (labelText != null) labelText.text = "";
    }

    private void Update()
    {
        if (menuCamera == null || Mouse.current == null) return;

        // Mientras la cámara se mueve o está en zoom, no hay selección
        if (cameraMover != null && (cameraMover.IsMoving || cameraMover.IsZoomed))
        {
            SetCurrent(null);

            if (cameraMover.IsZoomed && !cameraMover.IsMoving &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                cameraMover.ReturnToStart();
            }
            return;
        }

        HoverOutline found = null;

        Ray ray = menuCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hoverMask))
            found = hit.collider.GetComponentInParent<HoverOutline>();

        SetCurrent(found);

        if (current != null && Mouse.current.leftButton.wasPressedThisFrame)
            OnClicked(current);
    }

    private void SetCurrent(HoverOutline next)
    {
        if (next == current) return;

        if (current != null) current.SetHighlighted(false);
        current = next;
        if (current != null) current.SetHighlighted(true);

        if (labelText != null)
            labelText.text = current != null ? current.HoverLabel : "";
    }

    private void OnClicked(HoverOutline target)
    {
        var focus = target.GetComponent<CameraFocusTarget>();
        if (focus != null && cameraMover != null)
        {
            cameraMover.MoveTo(focus.ViewPoint);
            return;
        }

        var quit = target.GetComponent<QuitGameTarget>();
        if (quit != null)
            quit.Quit();
    }
}