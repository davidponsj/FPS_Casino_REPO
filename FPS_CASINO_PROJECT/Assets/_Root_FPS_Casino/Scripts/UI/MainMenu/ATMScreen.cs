using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Serialization;

/// <summary>
/// Pantalla del cajero del menú principal. Al encenderse (TurnOn) aparece con un fundido y muestra
/// páginas de opciones de texto: la del ratón encima se agranda y lleva una flecha (MenuArrow), y al
/// hacer clic se lanza su evento onSelect (p. ej. QuitGameTarget.Quit o ShowPage para otra página).
/// </summary>
public class ATMScreen : MonoBehaviour
{
    [Serializable]
    public class MenuOption
    {
        public TMP_Text text;
        public UnityEvent onSelect;

        [NonSerialized] public float baseSize;
        [NonSerialized] public MenuArrow marker;
    }

    [Serializable]
    public class MenuPage
    {
        public string name;
        public GameObject root;
        public MenuOption[] options;
    }

    [Header("Pantalla")]
    [Tooltip("CanvasGroup de la pantalla: su alpha hace el fundido al encender y apagar.")]
    [SerializeField] private CanvasGroup screenGroup;
    [Tooltip("Luz de la pantalla, que se enciende con ella. Opcional.")]
    [SerializeField] private Light screenLight;
    [Tooltip("Segundos del fundido al encender o apagar. 0.4 = rápido pero se ve.")]
    [FormerlySerializedAs("fadeDuration")]
    [Min(0f)]
    [SerializeField] private float fadeSeconds = 0.4f;
    [Tooltip("Intensidad de la luz con la pantalla encendida. 1.5 = brillo suave.")]
    [Min(0f)]
    [SerializeField] private float lightIntensity = 1.5f;

    [Header("Menú")]
    [Tooltip("Página 0 = menú principal")]
    [SerializeField] private MenuPage[] pages;
    [Tooltip("Cámara del menú. Vacío = Camera.main")]
    [SerializeField] private Camera menuCamera;
    [Tooltip("Cuánto crece el texto seleccionado. 1.05 = 5% más grande")]
    [Min(1f)]
    [SerializeField] private float selectedScale = 1.05f;
    [Tooltip("Tamaño de la flecha respecto al tamaño de letra. 0.6 = algo más pequeña que la letra.")]
    [Min(0f)]
    [SerializeField] private float markerSize = 0.6f;
    [Tooltip("Píxeles de separación entre la flecha y el texto. 10 = separada sin alejarse.")]
    [Min(0f)]
    [SerializeField] private float markerGap = 10f;

    private const int None = -1;

    private int currentPage;
    private int selected = None;
    private bool isOn;
    private Coroutine fadeRoutine;

    private MenuOption[] Options => pages[currentPage].options;

    private void Awake()
    {
        if (menuCamera == null) menuCamera = Camera.main;

        screenGroup.alpha = 0f;
        if (screenLight != null)
        {
            screenLight.intensity = 0f;
            screenLight.enabled = false;
        }

        for (int p = 0; p < pages.Length; p++)
        {
            for (int o = 0; o < pages[p].options.Length; o++)
            {
                var option = pages[p].options[o];
                if (option.text == null)
                {
                    Debug.LogWarning($"[ATMScreen] Falta el texto en Pages > Element {p} ({pages[p].name}) > Options > Element {o}", this);
                    continue;
                }

                option.baseSize = option.text.fontSize;
                option.marker = CreateMarker(option.text);
            }
        }

        ShowPage(0);
    }

    public void TurnOn()
    {
        isOn = true;
        ShowPage(0);
        StartFade(1f);
    }

    public void TurnOff()
    {
        isOn = false;
        StartFade(0f);
    }

    public void ShowPage(int index)
    {
        if (index < 0 || index >= pages.Length) return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i].root != null)
                pages[i].root.SetActive(i == index);
        }

        currentPage = index;
        selected = None;
        RefreshOptions();
    }

    private void Update()
    {
        if (!isOn || Options.Length == 0) return;

        var mouse = Mouse.current;
        if (mouse != null && menuCamera != null)
        {
            bool moved = mouse.delta.ReadValue().sqrMagnitude > 0f;
            bool clicked = mouse.leftButton.wasPressedThisFrame;

            if (moved || clicked)
            {
                int hovered = GetHoveredOption(mouse.position.ReadValue());
                if (hovered != selected)
                {
                    selected = hovered;
                    RefreshOptions();
                }

                if (clicked && selected != None)
                {
                    Confirm();
                    return;
                }
            }
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            Move(-1);
        else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            Move(1);
        else if (selected != None &&
                 (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                  || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            Confirm();
    }

    private int GetHoveredOption(Vector2 screenPos)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            var text = Options[i].text;
            if (text != null && RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, screenPos, menuCamera))
                return i;
        }
        return None;
    }

    private void Move(int direction)
    {
        if (selected == None)
            selected = direction > 0 ? 0 : Options.Length - 1;
        else
            selected = (selected + direction + Options.Length) % Options.Length;

        RefreshOptions();
    }

    private void Confirm()
    {
        var option = Options[selected];
        option.onSelect.Invoke();
    }

    private void RefreshOptions()
    {
        for (int i = 0; i < Options.Length; i++)
        {
            var option = Options[i];
            if (option.text == null || option.marker == null) continue;

            bool isSelected = i == selected;

            option.text.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            option.text.fontSize = option.baseSize * (isSelected ? selectedScale : 1f);

            float size = option.text.fontSize * markerSize;
            option.marker.rectTransform.sizeDelta = new Vector2(size, size);
            option.marker.Filled = isSelected;
        }
    }

    private MenuArrow CreateMarker(TMP_Text text)
    {
        var go = new GameObject("Flecha", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = text.gameObject.layer;
        go.transform.SetParent(text.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-markerGap, 0f);

        var tri = go.AddComponent<MenuArrow>();
        tri.raycastTarget = false;
        tri.color = text.color;
        return tri;
    }

    private void StartFade(float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(target));
    }

    private IEnumerator Fade(float target)
    {
        if (screenLight != null) screenLight.enabled = true;

        float start = screenGroup.alpha;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start, target, t / fadeSeconds);
            screenGroup.alpha = a;
            if (screenLight != null) screenLight.intensity = a * lightIntensity;
            yield return null;
        }

        screenGroup.alpha = target;
        if (screenLight != null)
        {
            screenLight.intensity = target * lightIntensity;
            screenLight.enabled = target > 0f;
        }
    }
}