using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Popup de la puntuación de las bajas, a la derecha de la mira (estilo How to Fish, pero de casino futurista). Escucha a KillScore:
/// - Cada truco entra arriba, grande, desde la derecha y girado, con un "klink" que sube de tono con cada línea del combo.
///   Sus letras hacen una ola al entrar y llevan un degradado dorado. La línea anterior se encoge, se vuelve gris y baja.
/// - El JACKPOT va en arcoíris que se mueve y sus letras no paran de ondular.
/// - Al cobrar el combo entra el total contando hacia arriba como una tragaperras, con "!" y el "chan".
///   Si se pierde, "¡COMBO PERDIDO!" en rojo. Después de Hold Seconds, todo el popup se desvanece.
/// - El texto del combo ("COMBO ×1,4") da un golpe con cada baja y cambia de color según el multiplicador.
///
/// La inclinación del popup entero se pone girando su RectTransform en el Inspector (Rotation Z). La fuente y el brillo
/// (outline, glow, sombra) van en el material de la fuente del Line Template.
/// Las líneas se crean al empezar copiando Line Template (no se crea nada mientras juegas).
/// </summary>
public class KillScoreHUD : MonoBehaviour
{
    private enum LineStyle { Normal, Jackpot, Total, Lost }

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

    [Header("Entrada de cada línea")]
    [Tooltip("Desde dónde entra la línea nueva, en píxeles respecto a su sitio. (90, -25) = desde la derecha y un poco abajo.")]
    [SerializeField] private Vector2 entryOffset = new Vector2(90f, -25f);
    [Tooltip("Grados que entra girada antes de enderezarse. 14 = entra torcida y se coloca de golpe. 0 = recta.")]
    [Range(-45f, 45f)]
    [SerializeField] private float entryRotationDegrees = 14f;

    [Header("Ola de letras")]
    [Tooltip("Segundos que ondulan las letras de la línea nueva. 0.6 = la ola se nota y se calma. 0 = sin ola.")]
    [Min(0f)]
    [SerializeField] private float waveSeconds = 0.6f;
    [Tooltip("Cuánto suben y bajan las letras, en píxeles. 8 = se nota sin descolocar.")]
    [Min(0f)]
    [SerializeField] private float waveAmplitude = 8f;
    [Tooltip("Rapidez de la ola. 18 = vibra con energía.")]
    [Min(0f)]
    [SerializeField] private float waveSpeed = 18f;
    [Tooltip("Desfase entre una letra y la siguiente. 0.6 = la ola recorre la palabra.")]
    [Min(0f)]
    [SerializeField] private float waveSpacing = 0.6f;

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
    [Tooltip("Segundos que tarda el total en contar desde 0, como el contador de una tragaperras. 0.5 = rápido. 0 = sale directo.")]
    [Min(0f)]
    [SerializeField] private float totalCountSeconds = 0.5f;
    [Tooltip("Texto cuando pierdes el combo.")]
    [SerializeField] private string comboLostText = "¡COMBO PERDIDO!";

    [Header("Colores (degradado de arriba a abajo de las letras)")]
    [Tooltip("Arriba de las letras de la línea nueva.")]
    [FormerlySerializedAs("newestColor")]
    [SerializeField] private Color newestTopColor = new Color(1f, 0.97f, 0.75f);
    [Tooltip("Abajo de las letras de la línea nueva. Dorado = casino.")]
    [SerializeField] private Color newestBottomColor = new Color(1f, 0.62f, 0.1f);
    [Tooltip("Tinte de las líneas viejas (gris = se apagan).")]
    [SerializeField] private Color olderColor = new Color(0.6f, 0.6f, 0.6f);
    [Tooltip("Arriba de las letras del total.")]
    [FormerlySerializedAs("totalColor")]
    [SerializeField] private Color totalTopColor = new Color(1f, 0.95f, 0.4f);
    [Tooltip("Abajo de las letras del total. Magenta neón.")]
    [SerializeField] private Color totalBottomColor = new Color(1f, 0.24f, 0.72f);
    [Tooltip("Color de ¡COMBO PERDIDO!.")]
    [SerializeField] private Color comboLostColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("Vueltas por segundo del arcoíris del JACKPOT. 0.8 = gira rápido sin marear.")]
    [Min(0f)]
    [SerializeField] private float jackpotRainbowSpeed = 0.8f;

    [Header("Texto del combo")]
    [Tooltip("Tamaño con el que da el golpe el texto del combo al subir. 1.5 = un 50% más grande y vuelve.")]
    [Min(1f)]
    [SerializeField] private float comboPunchScale = 1.5f;
    [Tooltip("Color del combo con multiplicador bajo.")]
    [SerializeField] private Color comboLowColor = new Color(0.3f, 0.95f, 1f);
    [Tooltip("Color del combo desde Combo Mid Multiplier.")]
    [SerializeField] private Color comboMidColor = new Color(1f, 0.3f, 0.8f);
    [Tooltip("Color del combo desde Combo High Multiplier.")]
    [SerializeField] private Color comboHighColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("Multiplicador a partir del que el combo pasa al color medio. 1.5 = ×1,5.")]
    [Min(1f)]
    [SerializeField] private float comboMidMultiplier = 1.5f;
    [Tooltip("Multiplicador a partir del que el combo pasa al color alto. 2 = ×2.")]
    [Min(1f)]
    [SerializeField] private float comboHighMultiplier = 2f;

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

    /// <summary>Una línea en pantalla: su texto, su puesto (0 = la nueva, arriba) y su estilo.</summary>
    private class Line
    {
        public TMP_Text Text;
        public RectTransform Rect;
        public int Slot;
        public float Scale;       // tamaño al que se asienta (la nueva 1, el total Total Scale)
        public LineStyle Style;
        public float ShowTime;    // cuándo entró (para la ola)
        public bool Deformed;     // sus letras están movidas: hay que dejarlas rectas al acabar la ola
        public int CountTarget;   // total al que cuenta (solo el total)
        public bool Counting;
    }

    private enum Ending { None, Cashed, Lost }

    private readonly List<Line> lines = new List<Line>();
    private readonly Queue<KillScore.ScoreLine> queued = new Queue<KillScore.ScoreLine>();

    private float nextRevealTime;
    private int klinksThisCombo;
    private Ending pendingEnding;
    private int pendingTotal;
    private float fadeStartTime = float.PositiveInfinity;
    private float lastComboMultiplier;

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
            text.enableVertexGradient = true; // el degradado se multiplica por el color (que usamos como tinte)
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
        UpdateComboText();
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
                ShowLine(FormatLine(data.Points, data.Label), data.IsJackpot ? LineStyle.Jackpot : LineStyle.Normal, 1f);
                PlayKlink(data.IsJackpot);
                nextRevealTime = now + (queued.Count >= fastRevealFromQueued ? fastRevealSeconds : revealSeconds);
            }
            else if (pendingEnding != Ending.None)
            {
                ShowEnding();
            }
        }

        AnimateLines(now);
        AnimateComboText();

        if (now >= fadeStartTime)
        {
            canvasGroup.alpha = 1f - (now - fadeStartTime) / fadeSeconds;
            if (canvasGroup.alpha <= 0f) ClearAll();
        }
    }

    private string FormatLine(int points, string label) => $"{points:N0} <size={labelSizePercent}%>{label}</size>";

    private void ShowEnding()
    {
        if (pendingEnding == Ending.Cashed)
        {
            Line total = ShowLine(totalCountSeconds > 0f ? "0!" : $"{pendingTotal:N0}!", LineStyle.Total, totalScale);
            if (total != null && totalCountSeconds > 0f)
            {
                total.CountTarget = pendingTotal;
                total.Counting = true;
            }
            Play(chanSound, 1f);
        }
        else
        {
            ShowLine(comboLostText, LineStyle.Lost, totalScale);
            Play(comboLostSound, 1f);
        }

        pendingEnding = Ending.None;
        klinksThisCombo = 0;
        lastComboMultiplier = 0f;
        if (comboText != null) comboText.text = "";
        fadeStartTime = Time.unscaledTime + holdSeconds;
    }

    /// <summary>Baja un puesto todas las líneas y pone una nueva arriba, entrando desde un lado, girada y con el "golpe" de tamaño.</summary>
    private Line ShowLine(string text, LineStyle style, float scale)
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

        if (newest == null) return null;

        newest.Slot = 0;
        newest.Scale = scale;
        newest.Style = style;
        newest.ShowTime = Time.unscaledTime;
        newest.Counting = false;
        newest.Text.text = text;
        newest.Text.color = Color.white;
        newest.Text.colorGradient = GetGradient(style);
        newest.Rect.anchoredPosition = entryOffset;
        newest.Rect.localRotation = Quaternion.Euler(0f, 0f, entryRotationDegrees);
        newest.Rect.localScale = Vector3.one * scale * punchScale;
        newest.Text.gameObject.SetActive(true);
        return newest;
    }

    private VertexGradient GetGradient(LineStyle style)
    {
        switch (style)
        {
            case LineStyle.Total: return new VertexGradient(totalTopColor, totalTopColor, totalBottomColor, totalBottomColor);
            case LineStyle.Lost: return new VertexGradient(comboLostColor);
            case LineStyle.Jackpot: return new VertexGradient(Color.white); // el arcoíris se pinta letra a letra
            default: return new VertexGradient(newestTopColor, newestTopColor, newestBottomColor, newestBottomColor);
        }
    }

    /// <summary>Lleva cada línea suavemente a su puesto, tamaño, giro y color; las que pasan del tope se desvanecen.</summary>
    private void AnimateLines(float now)
    {
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);

        foreach (Line line in lines)
        {
            if (!line.Text.gameObject.activeSelf) continue;

            if (line.Counting) UpdateCount(line, now);

            bool isNewest = line.Slot == 0;
            float targetScale = isNewest ? line.Scale : olderLineScale;
            Color targetColor = isNewest ? Color.white : olderColor; // tinte: blanco = degradado tal cual
            targetColor.a = line.Slot < maxVisibleLines ? 1f : 0f;

            Vector2 targetPosition = new Vector2(0f, -line.Slot * lineSpacingPixels);
            line.Rect.anchoredPosition = Vector2.Lerp(line.Rect.anchoredPosition, targetPosition, t);
            line.Rect.localScale = Vector3.one * Mathf.Lerp(line.Rect.localScale.x, targetScale, t);
            line.Rect.localRotation = Quaternion.Slerp(line.Rect.localRotation, Quaternion.identity, t);
            line.Text.color = Color.Lerp(line.Text.color, targetColor, t);

            ApplyLetterEffects(line, now);

            if (line.Slot >= maxVisibleLines && line.Text.color.a < 0.02f)
                line.Text.gameObject.SetActive(false);
        }
    }

    /// <summary>El total cuenta de 0 a su valor (rápido al principio y frenando al final), como el contador de una tragaperras.</summary>
    private void UpdateCount(Line line, float now)
    {
        float count01 = Mathf.Clamp01((now - line.ShowTime) / totalCountSeconds);
        float eased = 1f - (1f - count01) * (1f - count01);
        line.Text.text = $"{Mathf.RoundToInt(line.CountTarget * eased):N0}!";
        if (count01 >= 1f) line.Counting = false;
    }

    /// <summary>
    /// Mueve las letras una a una: ola al entrar (se calma en Wave Seconds) y, en el JACKPOT, ola continua con arcoíris.
    /// Se hace después de cambiar color y texto: ForceMeshUpdate rehace la malla y luego se tocan sus vértices.
    /// </summary>
    private void ApplyLetterEffects(Line line, float now)
    {
        bool isJackpot = line.Style == LineStyle.Jackpot;
        float age = now - line.ShowTime;
        float wave01 = isJackpot ? 1f : (waveSeconds > 0f ? 1f - age / waveSeconds : 0f);

        if (wave01 <= 0f && !isJackpot)
        {
            if (line.Deformed)
            {
                line.Text.ForceMeshUpdate(); // deja las letras rectas otra vez
                line.Deformed = false;
            }
            return;
        }

        TMP_Text text = line.Text;
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        float amplitude = waveAmplitude * wave01 * wave01; // se calma suave

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;

            int meshIndex = character.materialReferenceIndex;
            int vertex = character.vertexIndex;
            Vector3[] vertices = info.meshInfo[meshIndex].vertices;
            float offset = Mathf.Sin(now * waveSpeed - i * waveSpacing) * amplitude;
            for (int v = 0; v < 4; v++)
                vertices[vertex + v].y += offset;

            if (isJackpot)
            {
                Color32[] colors = info.meshInfo[meshIndex].colors32;
                Color rainbow = Color.HSVToRGB(Mathf.Repeat(now * jackpotRainbowSpeed + i * 0.08f, 1f), 0.75f, 1f);
                for (int v = 0; v < 4; v++)
                {
                    byte alpha = colors[vertex + v].a;
                    Color32 tinted = rainbow * text.color;
                    tinted.a = alpha;
                    colors[vertex + v] = tinted;
                }
            }
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        line.Deformed = true;
    }

    // ---------------- TEXTO DEL COMBO ----------------

    private void UpdateComboText()
    {
        if (comboText == null) return;

        if (killScore.ComboKills < 2)
        {
            comboText.text = "";
            return;
        }

        float multiplier = killScore.ComboMultiplier;
        comboText.text = $"COMBO ×{multiplier:0.0}";
        comboText.color = multiplier >= comboHighMultiplier ? comboHighColor
                        : multiplier >= comboMidMultiplier ? comboMidColor
                        : comboLowColor;

        // Golpe solo cuando sube (no con cada línea del mismo multiplicador).
        if (multiplier > lastComboMultiplier)
            comboText.rectTransform.localScale = Vector3.one * comboPunchScale;
        lastComboMultiplier = multiplier;
    }

    private void AnimateComboText()
    {
        if (comboText == null) return;
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        RectTransform rect = comboText.rectTransform;
        rect.localScale = Vector3.one * Mathf.Lerp(rect.localScale.x, 1f, t);
    }

    // ---------------- SONIDO ----------------

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
        {
            line.Text.gameObject.SetActive(false);
            line.Counting = false;
            line.Deformed = false;
        }

        queued.Clear();
        pendingEnding = Ending.None;
        klinksThisCombo = 0;
        lastComboMultiplier = 0f;
        fadeStartTime = float.PositiveInfinity;
        canvasGroup.alpha = 0f;
        if (comboText != null) comboText.text = "";
    }
}
