using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Fila del panel de controles de la pausa. Muestra el icono de la tecla actual de su acción (SO_KeyIcons) y, al
/// hacer clic, espera a que pulses otra (Esc cancela): el icono se oculta y el texto parpadea "Press a key...".
/// Usa KeyBindings, así que la tecla nueva se guarda y vale en todo el juego (también en el menú principal).
/// Es como RebindKey del ATM, pero para un Canvas normal (detecta el ratón con los eventos de UI).
/// Va en el objeto de la fila (ROW_...), que tiene que tener una Image (puede ser invisible) para recibir el ratón.
/// </summary>
public class PauseRebindKey : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Acción")]
    [Tooltip("Acción de PlayerControls que cambia esta fila (p. ej. Player/Jump).")]
    [SerializeField] private InputActionReference action;
    [Tooltip("Solo para Move: up, down, left o right. Vacío en el resto.")]
    [SerializeField] private string compositePart = "";

    [Header("Referencias")]
    [Tooltip("Iconos de las teclas (SO_KeyIcons).")]
    [SerializeField] private KeyIconLibrary icons;
    [Tooltip("Imagen de la tecla (IMG_...).")]
    [SerializeField] private Image keyImage;
    [Tooltip("Texto de la fila (TXT_...). Se aparta si la tecla es más ancha.")]
    [SerializeField] private TMP_Text label;

    [Header("Aspecto")]
    [Tooltip("Separación entre la tecla y el texto.")]
    [Min(0f)]
    [SerializeField] private float textGap = 20f;
    [Tooltip("Color del texto con el ratón encima.")]
    [SerializeField] private Color hoverColor = new Color(0.75f, 0.5f, 0.05f);
    [Tooltip("Texto mientras espera la tecla nueva.")]
    [SerializeField] private string listeningText = "Press a key...";
    [Tooltip("Parpadeos por segundo mientras espera.")]
    [Min(0.1f)]
    [SerializeField] private float blinkSpeed = 2f;

    private RectTransform keyRect;
    private RectTransform labelRect;
    private TMP_Text blankLabel;
    private string labelText;
    private Color labelColor;
    private float keyHeight;
    private int bindingIndex = -1;
    private bool hovered;
    private bool listening;

    private void Awake()
    {
        if (keyImage != null)
        {
            keyRect = keyImage.rectTransform;
            keyHeight = keyRect.sizeDelta.y;
            keyImage.preserveAspect = true;
            blankLabel = CreateBlankLabel();
        }

        if (label != null)
        {
            labelRect = label.rectTransform;
            labelText = label.text;
            labelColor = label.color;
        }

        if (action == null || action.action == null)
        {
            Debug.LogWarning("[PauseRebindKey] Falta asignar la acción.", this);
            return;
        }

        bindingIndex = KeyBindings.FindBindingIndex(action.action, compositePart);
        if (bindingIndex < 0)
            Debug.LogWarning($"[PauseRebindKey] {action.action.name} no tiene la tecla '{compositePart}'.", this);

        KeyBindings.EnsureLoaded(action.action.actionMap.asset);
    }

    private void OnEnable()
    {
        KeyBindings.OnChanged += Refresh;
        hovered = false;
        Refresh();
        ShowNormal();
    }

    private void OnDisable()
    {
        KeyBindings.OnChanged -= Refresh;
        if (listening) KeyBindings.CancelRebind();
        listening = false;
        hovered = false;
        ShowNormal();
    }

    private void Update()
    {
        if (listening) ShowListening();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (!listening) ShowNormal();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (!listening) ShowNormal();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (listening || bindingIndex < 0 || KeyBindings.BlockMenuInput) return;

        listening = true;
        if (blankLabel != null) blankLabel.gameObject.SetActive(false);
        KeyBindings.StartRebind(action.action, bindingIndex, OnRebindFinished);
    }

    private void OnRebindFinished()
    {
        listening = false;

        // Si el jugador usa su propia copia de las acciones, se le aplican también las teclas guardadas
        var playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null && playerInput.actions != null && playerInput.actions != action.action.actionMap.asset)
            KeyBindings.Load(playerInput.actions);

        ShowNormal();
    }

    // Tecla visible y texto normal (dorado con el ratón encima)
    private void ShowNormal()
    {
        if (keyImage != null) keyImage.color = Color.white;
        if (label == null) return;
        label.text = labelText;
        label.color = hovered ? hoverColor : labelColor;
    }

    // Esperando la tecla: tecla oculta y texto parpadeando
    private void ShowListening()
    {
        if (keyImage != null) keyImage.color = Color.clear;
        if (label == null) return;
        bool on = Mathf.Repeat(Time.unscaledTime * blinkSpeed, 1f) < 0.5f;
        label.text = listeningText;
        label.color = on ? hoverColor : Color.clear;
    }

    private void Refresh()
    {
        if (bindingIndex < 0 || keyImage == null) return;

        string path = action.action.bindings[bindingIndex].effectivePath;
        string control = path.Substring(path.LastIndexOf('/') + 1);

        Sprite sprite = icons != null ? icons.Find(control) : null;
        bool hasIcon = sprite != null;
        if (!hasIcon && icons != null) sprite = icons.BlankKey;

        keyImage.sprite = sprite;
        if (blankLabel != null)
        {
            blankLabel.gameObject.SetActive(!hasIcon);
            if (!hasIcon)
                blankLabel.text = InputControlPath.ToHumanReadableString(
                    path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
        }

        // Las teclas anchas (Shift, Espacio...) ocupan más y el texto se aparta
        float aspect = hasIcon && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height
                                                          : Mathf.Max(1f, (blankLabel != null ? blankLabel.text.Length : 1) * 0.5f);
        float width = keyHeight * aspect;
        keyRect.sizeDelta = new Vector2(width, keyHeight);

        if (labelRect != null)
            labelRect.offsetMin = new Vector2(keyRect.anchoredPosition.x + width + textGap, labelRect.offsetMin.y);
    }

    // Texto encima de la tecla vacía, para las teclas que no tienen icono
    private TMP_Text CreateBlankLabel()
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(keyRect, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (label != null) text.font = label.font;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.fontSizeMax = 24f;
        text.color = new Color(0.15f, 0.15f, 0.2f);
        text.raycastTarget = false;

        go.SetActive(false);
        return text;
    }
}