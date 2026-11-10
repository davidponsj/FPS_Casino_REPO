using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa. Con Escape la ventana sube desde abajo mientras crece desde muy pequeña, con un rebote de dibujo
/// animado; el juego se oscurece y se emborrona, se para el tiempo, se desactivan los controles y se suelta el ratón.
/// Escape o Resume la cierran (baja y encoge a la vez) y lo dejan todo como estaba. No se abre durante la intro
/// (PlayerWakeUpIntro) ni con el jugador muerto.
///
/// Va en el objeto raíz del Canvas de la pausa (SIEMPRE ACTIVO). La ventana, el oscurecido y el desenfoque empiezan
/// ocultos. Todo va en tiempo real: funciona con el juego parado (Time.timeScale = 0).
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La ventana (PNL_Pause). Sube desde abajo y crece al abrir.")]
    [SerializeField] private RectTransform window;
    [Tooltip("Imagen negra semitransparente que oscurece el juego (Dim). Su Alpha en el Inspector es el oscurecido final.")]
    [SerializeField] private Image dim;
    [Tooltip("Volume con el desenfoque (PauseVolume). Su Weight sube de 0 a 1 al pausar.")]
    [SerializeField] private Volume blurVolume;

    [Header("Botones")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [Tooltip("Escena del menú principal (tiene que estar en Build Settings).")]
    [SerializeField] private string menuSceneName = "SCN_MainMenu";

    [Header("Jugador (vacío = lo busca solo)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Animación")]
    [Tooltip("Segundos que tarda en llegar a su sitio. 0.45 = entrada ágil.")]
    [Min(0.05f)]
    [SerializeField] private float openSeconds = 0.45f;
    [Tooltip("Segundos que tarda en irse al cerrar. 0.3 = salida rápida.")]
    [Min(0.05f)]
    [SerializeField] private float closeSeconds = 0.3f;
    [Tooltip("Cuánto se pasa de su sitio y su tamaño antes de volver (rebote). 1.7 = rebote de dibujo animado.")]
    [Min(0f)]
    [SerializeField] private float overshoot = 1.7f;
    [Tooltip("Tamaño con el que sale de abajo. 0.1 = muy pequeña.")]
    [Range(0f, 1f)]
    [SerializeField] private float startScale = 0.1f;

    [Header("Audio")]
    [Tooltip("Para todos los sonidos del juego mientras está en pausa.")]
    [SerializeField] private bool pauseAudio = true;

    /// <summary>True mientras el menú de pausa está abierto.</summary>
    public static bool IsPaused { get; private set; }

    private float dimAlpha;
    private float shownY;
    private float progress;   // 0 = abajo y pequeña, 1 = en su sitio y a su tamaño
    private Coroutine routine;

    private void Awake()
    {
        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();

        if (dim != null) dimAlpha = dim.color.a;
        if (window != null) shownY = window.anchoredPosition.y;

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);

        IsPaused = false;
        Apply(0f);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (IsPaused) Resume();
        else if (CanPause()) Pause();
    }

    private void LateUpdate()
    {
        // Si algo devuelve el tiempo mientras está abierto (p. ej. la micro-pausa al matar), se vuelve a parar.
        if (IsPaused && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private bool CanPause()
    {
        if (PlayerWakeUpIntro.IsPlaying) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;
        return true;
    }

    // ---------------- Abrir / cerrar ----------------

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;
        if (playerInput != null) playerInput.DeactivateInput();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartAnimation(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (playerInput != null) playerInput.ActivateInput();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartAnimation(false);
    }

    private void GoToMainMenu()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(menuSceneName);
    }

    // ---------------- Animación ----------------

    private void StartAnimation(bool open)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(open ? 1f : 0f, open ? openSeconds : closeSeconds, open));
    }

    private IEnumerator Animate(float target, float seconds, bool open)
    {
        float start = progress;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
        {
            float eased = open ? EaseOutBack(t) : EaseInBack(t);
            Apply(Mathf.LerpUnclamped(start, target, eased));
            yield return null;
        }
        Apply(target);
        routine = null;
    }

    /// <summary>
    /// Coloca la ventana: 0 = abajo fuera de pantalla y pequeña, 1 = en su sitio y a su tamaño.
    /// Por encima de 1 (el rebote) se pasa un poco de sitio y de tamaño. También ajusta oscurecido y desenfoque.
    /// </summary>
    private void Apply(float value)
    {
        progress = value;
        float visible = Mathf.Clamp01(value);

        if (window != null)
        {
            window.gameObject.SetActive(value > 0.001f || IsPaused);

            var canvasRect = (RectTransform)window.parent;
            float hiddenY = -(canvasRect.rect.height + window.rect.height * startScale) * 0.5f - 40f;
            window.anchoredPosition = new Vector2(window.anchoredPosition.x, Mathf.LerpUnclamped(hiddenY, shownY, value));

            float scale = Mathf.Max(0f, Mathf.LerpUnclamped(startScale, 1f, value));
            window.localScale = new Vector3(scale, scale, 1f);
        }

        if (dim != null)
        {
            dim.gameObject.SetActive(visible > 0.001f);
            Color c = dim.color;
            c.a = dimAlpha * visible;
            dim.color = c;
        }

        if (blurVolume != null) blurVolume.weight = visible;
    }

    private float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + t * t * ((overshoot + 1f) * t + overshoot);
    }

    private float EaseInBack(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * ((overshoot + 1f) * t - overshoot);
    }
}