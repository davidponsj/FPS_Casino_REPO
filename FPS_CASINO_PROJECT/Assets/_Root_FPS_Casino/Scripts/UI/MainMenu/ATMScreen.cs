using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Pantalla del cajero del menú principal. Al encenderse (TurnOn) aparece con un efecto de monitor CRT
/// (línea horizontal que se abre y parpadeo) y muestra páginas de opciones de texto: la del ratón encima
/// se agranda y lleva una flecha (MenuArrow), y al hacer clic se lanza su evento onSelect (p. ej. ShowPage
/// para otra página o ReturnToStart para salir del zoom). Al cambiar de página la pantalla parpadea y,
/// mientras tanto, no se puede pulsar nada. Al apagarse (TurnOff) hace el efecto al revés.
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
    [Tooltip("CanvasGroup de la pantalla: su alpha y su escala hacen el efecto al encender y apagar.")]
    [SerializeField] private CanvasGroup screenGroup;
    [Tooltip("Luz de la pantalla, que se enciende con ella. Opcional.")]
    [SerializeField] private Light screenLight;
    [Tooltip("Intensidad de la luz con la pantalla encendida. 1.5 = brillo suave.")]
    [Min(0f)]
    [SerializeField] private float lightIntensity = 1.5f;

    [Header("Efecto CRT")]
    [Tooltip("Tiempo que tarda la línea horizontal en abrirse de lado a lado")]
    [Min(0f)]
    [SerializeField] private float lineDuration = 0.08f;
    [Tooltip("Tiempo que tarda la línea en abrirse a pantalla completa")]
    [Min(0f)]
    [SerializeField] private float expandDuration = 0.15f;
    [Tooltip("Duración del parpadeo al encender (al apagar dura la mitad)")]
    [Min(0f)]
    [SerializeField] private float flickerDuration = 0.3f;
    [Tooltip("Cada cuánto cambia el brillo durante el parpadeo")]
    [Min(0.01f)]
    [SerializeField] private float flickerInterval = 0.04f;
    [Tooltip("Brillo mínimo durante el parpadeo")]
    [Range(0f, 1f)]
    [SerializeField] private float flickerMinAlpha = 0.3f;
    [Tooltip("Grosor de la línea, respecto al alto de la pantalla")]
    [Min(0f)]
    [SerializeField] private float lineThickness = 0.02f;

    [Header("Cambio de panel")]
    [Tooltip("Segundos que parpadea la pantalla al cambiar de panel. Mientras dura no se puede pulsar nada. 0 = cambio instantáneo.")]
    [Min(0f)]
    [SerializeField] private float pageFlickerSeconds = 0.3f;

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
    private bool busy;
    private Coroutine screenRoutine;
    private Vector3 baseScale;

    private MenuOption[] Options => pages[currentPage].options;

    /// <summary>True mientras la pantalla se enciende, se apaga o cambia de panel: no se puede pulsar nada.</summary>
    public bool IsBusy => busy;

    private void Awake()
    {
        if (menuCamera == null) menuCamera = Camera.main;

        baseScale = screenGroup.transform.localScale;
        SetScreen(0f, 1f, 1f);
        SetBusy(false);

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

        SetPage(0);
    }

    public void TurnOn()
    {
        isOn = true;
        SetPage(0);
        StartScreenRoutine(PowerOn());
    }

    public void TurnOff()
    {
        isOn = false;
        StartScreenRoutine(PowerOff());
    }

    /// <summary>Cambia de panel. Con la pantalla encendida, parpadea y bloquea el menú mientras tanto.</summary>
    public void ShowPage(int index)
    {
        if (index < 0 || index >= pages.Length) return;

        if (!isOn || pageFlickerSeconds <= 0f)
        {
            SetPage(index);
            return;
        }

        StartScreenRoutine(SwitchPage(index));
    }

    private void SetPage(int index)
    {
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
        if (!isOn || busy || KeyBindings.BlockMenuInput || Options.Length == 0) return;

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

    // ---------- Efecto CRT ----------

    private void StartScreenRoutine(IEnumerator routine)
    {
        if (screenRoutine != null) StopCoroutine(screenRoutine);
        screenRoutine = StartCoroutine(routine);
    }

    // Mientras está ocupada, el menú y los paneles (teclas, sliders) no responden
    private void SetBusy(bool value)
    {
        busy = value;
        screenGroup.interactable = !value;
    }

    private IEnumerator PowerOn()
    {
        SetBusy(true);
        // Línea fina que se abre de lado a lado
        yield return Animate(lineDuration, k => SetScreen(1f, k, lineThickness));
        // La línea se abre hasta llenar la pantalla
        yield return Animate(expandDuration, k => SetScreen(1f, 1f, Mathf.Lerp(lineThickness, 1f, k)));
        // Parpadeo antes de estabilizarse
        yield return Flicker(flickerDuration);
        SetBusy(false);
    }

    private IEnumerator PowerOff()
    {
        SetBusy(true);
        // Parpadeo corto antes de apagarse
        yield return Flicker(flickerDuration * 0.5f);
        // La pantalla se aplasta en una línea
        yield return Animate(expandDuration, k => SetScreen(1f, 1f, Mathf.Lerp(1f, lineThickness, k)));
        // La línea se encoge hasta desaparecer
        yield return Animate(lineDuration, k => SetScreen(1f, 1f - k, lineThickness));

        SetScreen(0f, 1f, 1f);
    }

    // Parpadea el panel viejo, cambia al nuevo y parpadea otra vez
    private IEnumerator SwitchPage(int index)
    {
        SetBusy(true);
        selected = None;
        RefreshOptions();

        yield return Flicker(pageFlickerSeconds * 0.5f);
        SetPage(index);
        yield return Flicker(pageFlickerSeconds * 0.5f);

        SetBusy(false);
    }

    private IEnumerator Flicker(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            SetScreen(UnityEngine.Random.Range(flickerMinAlpha, 1f), 1f, 1f);

            float wait = Mathf.Max(flickerInterval, 0.01f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }

        SetScreen(1f, 1f, 1f);
    }

    private IEnumerator Animate(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            step(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }

        step(1f);
    }

    private void SetScreen(float alpha, float scaleX, float scaleY)
    {
        screenGroup.alpha = alpha;
        screenGroup.transform.localScale = new Vector3(baseScale.x * scaleX, baseScale.y * scaleY, baseScale.z);

        if (screenLight != null)
        {
            float intensity = alpha * scaleY * lightIntensity;
            screenLight.intensity = intensity;
            screenLight.enabled = intensity > 0f;
        }
    }
}