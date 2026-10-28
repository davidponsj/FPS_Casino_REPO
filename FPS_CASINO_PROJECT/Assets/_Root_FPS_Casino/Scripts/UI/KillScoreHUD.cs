using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Popup de la puntuación de las bajas, junto a la mira (estilo How to Fish). Escucha a KillScore:
/// - Cada truco entra arriba, grande y con un "klink" que sube de tono con cada línea del combo.
///   La línea anterior se encoge, se vuelve gris y baja. Como mucho se ven Max Visible Lines.
/// - Si se acumulan muchas líneas (escopetazo, combo rápido), se revelan más deprisa para no ir por detrás.
/// - Al cobrar el combo entra el total con "!" y suena el "chan". Si se pierde, "¡COMBO PERDIDO!" en rojo.
///   Después de Hold Seconds, todo el popup se desvanece.
///
/// Las líneas se crean al empezar copiando Line Template (no se crea nada mientras juegas).
/// </summary>
public class KillScoreHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("KillScore del jugador. Vacío = lo busca en el objeto con el tag Player.")]
    [SerializeField] private KillScore killScore;
    [Tooltip("Texto hijo DESACTIVADO que sirve de molde: cada línea del popup es una copia suya. Pivote a la izquierda.")]
    [SerializeField] private TMP_Text lineTemplate;
    [Tooltip("Texto opcional con el multiplicador (\"COMBO ×1.4\"). Vacío = no se enseña.")]
    [SerializeField] private TMP_Text comboText;
    [Tooltip("CanvasGroup del popup, para desvanecerlo entero al final.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("AudioSource que suena los sonidos del popup (Play On Awake desmarcado). Vacío = sin sonido.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Líneas")]
    [Tooltip("Líneas que se ven a la vez. Cuando entra una más, la de abajo se desvanece. 5 = no satura la pantalla.")]
    [Min(1)]
    [SerializeField] private int maxVisibleLines = 5;
    [Tooltip("Píxeles entre una línea y la de debajo. 45 = con letra de 64 y las viejas al 60%.")]
    [Min(0f)]
    [SerializeField] private float lineSpacingPixels = 45f;
    [Tooltip("Tamaño de las líneas viejas respecto a la nueva. 0.6 = un 60%.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float olderLineScale = 0.6f;
    [Tooltip("Tamaño con el que ENTRA una línea antes de asentarse (el \"golpe\"). 1.6 = entra un 60% más grande.")]
    [Min(1f)]
    [SerializeField] private float punchScale = 1.6f;
    [Tooltip("Rapidez con la que las líneas se mueven, encogen y cambian de color. Alto = seco, bajo = suave. 14 = rápido pero se ve.")]
    [Min(0.1f)]
    [SerializeField] private float animationSpeed = 14f;
    [Tooltip("Tamaño del nombre del truco respecto al número, en %. 45 = \"250 No scope\" con el nombre pequeño.")]
    [Range(10f, 100f)]
    [SerializeField] private float labelSizePercent = 45f;

    [Header("Ritmo")]
    [Tooltip("Segundos entre una línea y la siguiente. 0.25 = da tiempo a leer cada klink.")]
    [Min(0f)]
    [SerializeField] private float revealSeconds = 0.25f;
    [Tooltip("Segundos entre líneas cuando hay muchas esperando. 0.08 = ráfaga de klinks.")]
    [Min(0f)]
    [SerializeField] private float fastRevealSeconds = 0.08f;
    [Tooltip("Líneas esperando a partir de las que se revela con Fast Reveal Seconds. 3 = un escopetazo con trucos ya acelera.")]
    [Min(1)]
    [SerializeField] private int fastRevealFromQueued = 3;
    [Tooltip("Segundos que se queda el total en pantalla antes de desvanecerse. 1.5 = da tiempo a disfrutarlo.")]
    [Min(0f)]
    [SerializeField] private float holdSeconds = 1.5f;
    [Tooltip("Segundos que tarda en desvanecerse todo el popup. 0.5 = suave.")]
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 0.5f;

    [Header("Total")]
    [Tooltip("Tamaño del total respecto a una línea normal. 1.4 = destaca sobre todo lo demás.")]
    [Min(0.1f)]
    [SerializeField] private float totalScale = 1.4f;
    [Tooltip("Texto cuando pierdes el combo.")]
    [SerializeField] private string comboLostText = "¡COMBO PERDIDO!";

    [Header("Colores")]
    [Tooltip("Color de la línea nueva.")]
    [SerializeField] private Color newestColor = Color.white;
    [Tooltip("Color de las líneas viejas (gris).")]
    [SerializeField] private Color olderColor = new Color(0.6f, 0.6f, 0.6f);
    [Tooltip("Color de la línea del JACKPOT.")]
    [SerializeField] private Color jackpotColor = new Color(1f, 0.82f, 0.2f);
    [Tooltip("Color del total.")]
    [SerializeField] private Color totalColor = new Color(1f, 0.95f, 0.4f);
    [Tooltip("Color de ¡COMBO PERDIDO!.")]
    [SerializeField] private Color comboLostColor = new Color(1f, 0.25f, 0.25f);

    [Header("Sonidos (opcionales)")]
    [Tooltip("Suena con cada línea (el \"klink\"). Vacío = sin sonido.")]
    [SerializeField] private AudioClip klinkSound;
    [Tooltip("Suena con el total (el \"chan\"). Vacío = sin sonido.")]
    [SerializeField] private AudioClip chanSound;
    [Tooltip("Suena con el JACKPOT en vez del klink. Vacío = usa el klink.")]
    [SerializeField] private AudioClip jackpotSound;
    [Tooltip("Suena al perder el combo. Vacío = sin sonido.")]
    [SerializeField] private AudioClip comboLostSound;
    [Tooltip("Tono del primer klink del combo. 1 = normal.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float klinkBasePitch = 1f;
    [Tooltip("Lo que sube el tono con cada klink del combo. 0.05 = el 10º suena un 45% más agudo.")]
    [Min(0f)]
    [SerializeField] private float klinkPitchPerLine = 0.05f;
    [Tooltip("Tono máximo del klink. 2 = una octava más agudo.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float klinkMaxPitch = 2f;

    /// <summary>Una línea en pantalla: su texto y su puesto (0 = la nueva, arriba).</summary>
    private class Line
    {
        public TMP_Text Text;
        public RectTransform Rect;
        public int Slot;
        public float Scale; // tamaño al que se asienta (la nueva 1, el total Total Scale)
        public Color Color;
    }

    private enum Ending { None, Cashed, Lost }

    private readonly List<Line> lines = new List<Line>();
    private readonly Queue<KillScore.ScoreLine> queued = new Queue<KillScore.ScoreLine>();

    private float nextRevealTime;
    private int klinksThisCombo;
    private Ending pendingEnding;
    private int pendingTotal;
    private float fadeStartTime = float.PositiveInfinity;

    private void Awake()
    {
        if (killScore == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) killScore = player.GetComponent<KillScore>();
        }

        if (killScore == null || lineTemplate == null || canvasGroup == null)
        {
            Debug.LogWarning($"{name}: falta KillScore (en el Player), Line Template o Canvas Group. El popup no se verá.", this);
            enabled = false;
            return;
        }

        // Una más de las visibles: la que se está desvaneciendo por abajo mientras entra la nueva.
        for (int i = 0; i <= maxVisibleLines; i++)
        {
            TMP_Text text = Instantiate(lineTemplate, lineTemplate.transform.parent);
            text.gameObject.SetActive(false);
            lines.Add(new Line { Text = text, Rect = text.rectTransform });
        }

        canvasGroup.alpha = 0f;
        if (comboText != null) comboText.text = "";
    }

    private void OnEnable()
    {
        if (killScore == null) return;
        killScore.OnKillScored += HandleKillScored;
        killScore.OnComboCashed += HandleComboCashed;
        killScore.OnComboLost += HandleComboLost;
    }

    private void OnDisable()
    {
        if (killScore == null) return;
        killScore.OnKillScored -= HandleKillScored;
        killScore.OnComboCashed -= HandleComboCashed;
        killScore.OnComboLost -= HandleComboLost;
    }

    private void HandleKillScored()
    {
        // Baja nueva mientras se desvanece el combo anterior: se limpia y empieza de cero.
        if (fadeStartTime < float.PositiveInfinity)
            ClearAll();

        foreach (KillScore.ScoreLine line in killScore.LastKillLines)
            queued.Enqueue(line);

        canvasGroup.alpha = 1f;

        if (comboText != null)
            comboText.text = killScore.ComboKills >= 2 ? $"COMBO ×{killScore.ComboMultiplier:0.0}" : "";
    }

    private void HandleComboCashed(int total)
    {
        // Se enseña cuando hayan salido todas las líneas que quedan en cola.
        pendingEnding = Ending.Cashed;
        pendingTotal = total;
    }

    private void HandleComboLost()
    {
        // Perder corta en seco: lo que quedaba en cola ya no sale.
        queued.Clear();
        pendingEnding = Ending.Lost;
    }

    private void Update()
    {
        // Sin escala de tiempo: el popup termina igual aunque el juego vaya a cámara lenta o en pausa.
        float now = Time.unscaledTime;

        if (now >= nextRevealTime)
        {
            if (queued.Count > 0)
            {
                KillScore.ScoreLine data = queued.Dequeue();
                string text = $"{data.Points:N0} <size={labelSizePercent}%>{data.Label}</size>";
                ShowLine(text, data.IsJackpot ? jackpotColor : newestColor, 1f);
                PlayKlink(data.IsJackpot);
                nextRevealTime = now + (queued.Count >= fastRevealFromQueued ? fastRevealSeconds : revealSeconds);
            }
            else if (pendingEnding != Ending.None)
            {
                ShowEnding();
            }
        }

        AnimateLines();

        if (now >= fadeStartTime)
        {
            canvasGroup.alpha = 1f - (now - fadeStartTime) / fadeSeconds;
            if (canvasGroup.alpha <= 0f) ClearAll();
        }
    }

    private void ShowEnding()
    {
        if (pendingEnding == Ending.Cashed)
        {
            ShowLine($"{pendingTotal:N0}!", totalColor, totalScale);
            Play(chanSound, 1f);
        }
        else
        {
            ShowLine(comboLostText, comboLostColor, totalScale);
            Play(comboLostSound, 1f);
        }

        pendingEnding = Ending.None;
        klinksThisCombo = 0;
        if (comboText != null) comboText.text = "";
        fadeStartTime = Time.unscaledTime + holdSeconds;
    }

    /// <summary>Baja un puesto todas las líneas y pone una nueva arriba, entrando con el "golpe" de tamaño.</summary>
    private void ShowLine(string text, Color color, float scale)
    {
        Line newest = null;

        foreach (Line line in lines)
        {
            if (!line.Text.gameObject.activeSelf)
            {
                if (newest == null) newest = line;
                continue;
            }

            line.Slot++;
            // Si no queda ninguna libre, se reutiliza la que ya se estaba yendo por abajo.
            if (newest == null && line.Slot > maxVisibleLines) newest = line;
        }

        if (newest == null) return;

        newest.Slot = 0;
        newest.Scale = scale;
        newest.Color = color;
        newest.Text.text = text;
        newest.Text.color = color;
        newest.Rect.anchoredPosition = Vector2.zero;
        newest.Rect.localScale = Vector3.one * scale * punchScale;
        newest.Text.gameObject.SetActive(true);
    }

    /// <summary>Lleva cada línea suavemente a su puesto, tamaño y color; las que pasan del tope se desvanecen.</summary>
    private void AnimateLines()
    {
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);

        foreach (Line line in lines)
        {
            if (!line.Text.gameObject.activeSelf) continue;

            bool isNewest = line.Slot == 0;
            float targetScale = isNewest ? line.Scale : olderLineScale;
            Color targetColor = isNewest ? line.Color : olderColor;
            targetColor.a = line.Slot < maxVisibleLines ? 1f : 0f;

            Vector2 targetPosition = new Vector2(0f, -line.Slot * lineSpacingPixels);
            line.Rect.anchoredPosition = Vector2.Lerp(line.Rect.anchoredPosition, targetPosition, t);
            line.Rect.localScale = Vector3.one * Mathf.Lerp(line.Rect.localScale.x, targetScale, t);
            line.Text.color = Color.Lerp(line.Text.color, targetColor, t);

            if (line.Slot >= maxVisibleLines && line.Text.color.a < 0.02f)
                line.Text.gameObject.SetActive(false);
        }
    }

    private void PlayKlink(bool isJackpot)
    {
        float pitch = Mathf.Min(klinkBasePitch + klinkPitchPerLine * klinksThisCombo, klinkMaxPitch);
        klinksThisCombo++;

        if (isJackpot && jackpotSound != null) Play(jackpotSound, 1f);
        else Play(klinkSound, pitch);
    }

    private void Play(AudioClip clip, float pitch)
    {
        if (audioSource == null || clip == null) return;

        // PlayOneShot usa el pitch del AudioSource: así cada klink suena un poco más agudo que el anterior.
        // shortcut: cambiar el pitch también afina la cola del klink anterior si aún suena; si se nota, usar 2 AudioSources alternos.
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip);
    }

    private void ClearAll()
    {
        foreach (Line line in lines)
            line.Text.gameObject.SetActive(false);

        queued.Clear();
        pendingEnding = Ending.None;
        klinksThisCombo = 0;
        fadeStartTime = float.PositiveInfinity;
        canvasGroup.alpha = 0f;
        if (comboText != null) comboText.text = "";
    }
}