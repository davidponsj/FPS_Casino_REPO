using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tecla del panel de Controles. Muestra el icono de la tecla actual de su acción y, al hacer clic
/// en la tecla o en el texto de su fila, espera a que pulses otra (Esc cancela): la tecla se oculta y
/// el texto de la fila parpadea "Press a key...". La tecla nueva se guarda y vale también en el juego
/// (KeyBindings). Va en la imagen de la tecla (IMG_...).
/// </summary>
[RequireComponent(typeof(Image))]
public class RebindKey : MonoBehaviour
{
    [Header("Acción")]
    [Tooltip("Acción de PlayerControls que cambia esta tecla (p. ej. Player/Jump).")]
    [SerializeField] private InputActionReference action;
    [Tooltip("Solo para Move: up, down, left o right. Vacío en el resto.")]
    [SerializeField] private string compositePart = "";

    [Header("Aspecto")]
    [Tooltip("Iconos de las teclas (SO_KeyIcons).")]
    [SerializeField] private KeyIconLibrary icons;
    [Tooltip("Texto de la fila (TXT_...). También se puede clicar, y se aparta si la tecla nueva es más ancha o más estrecha.")]
    [SerializeField] private RectTransform rowText;
    [Tooltip("Separación entre la tecla y el texto. 4 = como en el panel.")]
    [Min(0f)]
    [SerializeField] private float textGap = 4f;
    [Tooltip("Color de la tecla y el texto con el ratón encima.")]
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.4f);

    [Header("Modo escucha")]
    [Tooltip("Texto de la fila mientras espera la tecla nueva.")]
    [SerializeField] private string listeningText = "Press a key...";
    [Tooltip("Parpadeos por segundo del texto mientras espera. 1 = lento, 3 = rápido.")]
    [Min(0.1f)]
    [SerializeField] private float blinkSpeed = 1f;

    private Image image;
    private RectTransform rect;
    private CanvasGroup screen;
    private Camera menuCamera;
    private TMP_Text rowLabel;
    private TMP_Text blankLabel;
    private string rowOriginalText;
    private Color rowOriginalColor;
    private int bindingIndex = -1;
    private float unitWidth;
    private float leftEdge;
    private bool listening;
    private bool pressedOnMe;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        screen = GetComponentInParent<CanvasGroup>();
        menuCamera = Camera.main;

        rowLabel = rowText != null ? rowText.GetComponent<TMP_Text>() : null;
        if (rowLabel != null)
        {
            rowOriginalText = rowLabel.text;
            rowOriginalColor = rowLabel.color;
        }

        // Ancho de una tecla cuadrada y borde izquierdo, sacados de cómo la colocaste en el panel
        unitWidth = rect.sizeDelta.x / SpriteAspect(image.sprite);
        leftEdge = rect.anchoredPosition.x - rect.sizeDelta.x * rect.pivot.x;

        blankLabel = CreateBlankLabel();

        if (action == null || action.action == null)
        {
            Debug.LogWarning("[RebindKey] Falta asignar la acción.", this);
            return;
        }

        bindingIndex = KeyBindings.FindBindingIndex(action.action, compositePart);
        if (bindingIndex < 0)
            Debug.LogWarning($"[RebindKey] {action.action.name} no tiene la tecla '{compositePart}'.", this);

        KeyBindings.EnsureLoaded(action.action.actionMap.asset);
    }

    private void OnEnable()
    {
        KeyBindings.OnChanged += Refresh;
        pressedOnMe = false;
        Refresh();
    }

    private void OnDisable()
    {
        KeyBindings.OnChanged -= Refresh;
        pressedOnMe = false;
        if (listening) KeyBindings.CancelRebind();
        ShowNormal(false);
    }

    private void Update()
    {
        if (bindingIndex < 0) return;

        if (listening)
        {
            ShowListening();
            return;
        }

        bool visible = screen == null || (screen.alpha > 0.99f && screen.interactable);
        var mouse = Mouse.current;
        bool hovered = visible && mouse != null && menuCamera != null && !KeyBindings.IsListening &&
                       IsMouseOver(mouse.position.ReadValue());

        ShowNormal(hovered);
        if (mouse == null) return;

        // Solo cuenta si el clic EMPIEZA y TERMINA encima de esta tecla: así el clic con el que
        // entras al panel de Controles no la activa
        if (hovered && mouse.leftButton.wasPressedThisFrame)
            pressedOnMe = true;

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            bool start = pressedOnMe && hovered && !KeyBindings.BlockMenuInput;
            pressedOnMe = false;

            if (start)
            {
                listening = true;
                blankLabel.gameObject.SetActive(false);
                KeyBindings.StartRebind(action.action, bindingIndex, () => listening = false);
            }
        }
    }

    // Tecla y texto normales (amarillos con el ratón encima)
    private void ShowNormal(bool hovered)
    {
        image.color = hovered ? hoverColor : Color.white;

        if (rowLabel == null) return;
        rowLabel.text = rowOriginalText;
        rowLabel.color = hovered ? hoverColor : rowOriginalColor;
    }

    // Escuchando: la tecla oculta y el texto parpadeando entre visible e invisible
    private void ShowListening()
    {
        image.color = Color.clear;

        if (rowLabel == null) return;
        bool on = Mathf.Repeat(Time.unscaledTime * blinkSpeed, 1f) < 0.5f;
        Color c = rowOriginalColor;
        c.a = on ? rowOriginalColor.a : 0f;
        rowLabel.text = listeningText;
        rowLabel.color = c;
    }

    // Vale tanto la tecla como el texto de su fila
    private bool IsMouseOver(Vector2 screenPos)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, menuCamera))
            return true;

        return rowText != null &&
               RectTransformUtility.RectangleContainsScreenPoint(rowText, screenPos, menuCamera);
    }

    private void Refresh()
    {
        if (bindingIndex < 0 || image == null) return;

        string path = action.action.bindings[bindingIndex].effectivePath;
        string control = path.Substring(path.LastIndexOf('/') + 1);

        Sprite sprite = icons != null ? icons.Find(control) : null;
        bool hasIcon = sprite != null;
        if (!hasIcon && icons != null) sprite = icons.BlankKey;

        image.sprite = sprite;
        blankLabel.gameObject.SetActive(!hasIcon);
        if (!hasIcon)
        {
            blankLabel.text = InputControlPath.ToHumanReadableString(
                path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
        }

        // Las teclas anchas (Shift, Espacio...) ocupan más y el texto de la fila se aparta
        float aspect = hasIcon ? SpriteAspect(sprite) : Mathf.Max(1f, blankLabel.text.Length * 0.5f);
        float width = unitWidth * aspect;
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(leftEdge + width * rect.pivot.x, rect.anchoredPosition.y);

        if (rowText != null)
            rowText.offsetMin = new Vector2(leftEdge + width + textGap, rowText.offsetMin.y);
    }

    private TMP_Text CreateBlankLabel()
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var label = go.AddComponent<TextMeshProUGUI>();
        if (rowLabel != null) label.font = rowLabel.font;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = rowLabel != null ? rowLabel.fontSize : 10f;
        label.color = new Color(0.15f, 0.15f, 0.2f);
        label.raycastTarget = false;

        go.SetActive(false);
        return label;
    }

    private static float SpriteAspect(Sprite sprite)
    {
        return sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
    }
}