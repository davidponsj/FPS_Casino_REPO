using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

public class ATMScreen : MonoBehaviour
{
    [Header("Pantalla")]
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private Light screenLight;          // Opcional
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float lightIntensity = 1.5f;

    [Header("Menú")]
    [SerializeField] private TMP_Text[] options;
    [Tooltip("Cámara del menú. Vacío = Camera.main")]
    [SerializeField] private Camera menuCamera;
    [Tooltip("Cuánto crece el texto seleccionado. 1.15 = 15% más grande")]
    [SerializeField] private float selectedScale = 1.15f;
    [Tooltip("Tamaño de la flecha respecto al tamaño de letra")]
    [SerializeField] private float markerSize = 0.6f;
    [Tooltip("Separación entre la flecha y el texto")]
    [SerializeField] private float markerGap = 10f;

    [Header("Acciones")]
    [SerializeField] private UnityEvent onControles;
    [SerializeField] private UnityEvent onMusicaSonido;
    [SerializeField] private UnityEvent onVolver;

    private float[] baseSizes;
    private MenuTriangle[] markers;
    private int selected;
    private bool isOn;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (menuCamera == null) menuCamera = Camera.main;

        baseSizes = new float[options.Length];
        markers = new MenuTriangle[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            baseSizes[i] = options[i].fontSize;
            markers[i] = CreateMarker(options[i]);
        }

        RefreshOptions();

        screenGroup.alpha = 0f;
        if (screenLight != null)
        {
            screenLight.intensity = 0f;
            screenLight.enabled = false;
        }
    }

    public void TurnOn()
    {
        isOn = true;
        selected = 0;
        RefreshOptions();
        StartFade(1f);
    }

    public void TurnOff()
    {
        isOn = false;
        StartFade(0f);
    }

    private void Update()
    {
        if (!isOn) return;

        var mouse = Mouse.current;
        if (mouse != null && menuCamera != null)
        {
            bool moved = mouse.delta.ReadValue().sqrMagnitude > 0f;
            bool clicked = mouse.leftButton.wasPressedThisFrame;

            if (moved || clicked)
            {
                int hovered = GetHoveredOption(mouse.position.ReadValue());
                if (hovered >= 0)
                {
                    if (hovered != selected)
                    {
                        selected = hovered;
                        RefreshOptions();
                    }

                    if (clicked)
                    {
                        Confirm();
                        return;
                    }
                }
            }
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            Move(-1);
        else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            Move(1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                 || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
            Confirm();
    }

    private int GetHoveredOption(Vector2 screenPos)
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(options[i].rectTransform, screenPos, menuCamera))
                return i;
        }
        return -1;
    }

    private void Move(int direction)
    {
        selected = (selected + direction + options.Length) % options.Length;
        RefreshOptions();
    }

    private void Confirm()
    {
        switch (selected)
        {
            case 0:
                Debug.Log("ATM: Controles");
                onControles.Invoke();
                break;
            case 1:
                Debug.Log("ATM: Música y Sonido");
                onMusicaSonido.Invoke();
                break;
            case 2:
                onVolver.Invoke();
                break;
        }
    }

    private void RefreshOptions()
    {
        for (int i = 0; i < options.Length; i++)
        {
            bool isSelected = i == selected;

            options[i].fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            options[i].fontSize = baseSizes[i] * (isSelected ? selectedScale : 1f);

            float size = options[i].fontSize * markerSize;
            markers[i].rectTransform.sizeDelta = new Vector2(size, size);
            markers[i].Filled = isSelected;
        }
    }

    private MenuTriangle CreateMarker(TMP_Text text)
    {
        var go = new GameObject("Flecha", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = text.gameObject.layer;
        go.transform.SetParent(text.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-markerGap, 0f);

        var tri = go.AddComponent<MenuTriangle>();
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
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start, target, t / fadeDuration);
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