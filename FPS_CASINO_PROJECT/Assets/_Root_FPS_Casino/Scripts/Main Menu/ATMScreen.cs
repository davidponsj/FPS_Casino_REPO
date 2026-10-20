using System.Collections;
using UnityEngine;
using UnityEngine.Events;
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
    [SerializeField] private string selectedMarker = "▶ ";
    [SerializeField] private string unselectedMarker = "▷ ";

    [Header("Acciones")]
    [SerializeField] private UnityEvent onControles;
    [SerializeField] private UnityEvent onMusicaSonido;
    [SerializeField] private UnityEvent onVolver;

    private string[] labels;
    private int selected;
    private bool isOn;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        labels = new string[options.Length];
        for (int i = 0; i < options.Length; i++)
            labels[i] = options[i].text;

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

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            Move(-1);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            Move(1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                 || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
            Confirm();
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
            options[i].text = (i == selected ? selectedMarker : unselectedMarker) + labels[i];
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