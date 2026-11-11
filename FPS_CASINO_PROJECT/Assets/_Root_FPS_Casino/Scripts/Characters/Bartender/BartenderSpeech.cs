using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lo que dice el barman al darte una botella: una frase con personalidad que cuenta qué hace al beberla y al lanzarla,
/// y debajo un resumen rápido (DRINK / THROW) que se genera solo con los números del BottleEffect, así siempre es
/// correcto aunque se ajusten los valores.
///
/// Sale en uno de dos sitios, según dónde mires:
/// - Si miras al barman: bocadillo de cómic saliendo de su boca (en el mundo, siempre del mismo tamaño en pantalla).
/// - Si no lo miras (o hay algo en medio): el mismo bocadillo arriba en el centro de la pantalla, con su cara al lado.
/// Si giras la cámara mientras habla, pasa de un sitio al otro sin cortar la frase.
///
/// El texto aparece letra a letra (rápido, que el juego no para) con un pequeño rebote en cada letra, se queda lo justo
/// para leerlo según lo larga que sea la frase y se va. Si gastas la botella antes, se va antes.
///
/// Todo se construye por código al empezar: solo hay que poner este componente en el barman y asignar la boca, las
/// fuentes y el retrato. Las frases de cada botella están en su TicketData (Bartender Lines); las palabras entre
/// asteriscos (*así*) salen resaltadas con el color de la botella.
/// </summary>
public class BartenderSpeech : MonoBehaviour
{
    private const float ReferenceHeight = 1080f;
    private static readonly Regex HighlightPattern = new Regex(@"\*(.+?)\*");

    [Header("Referencias")]
    [Tooltip("Punto de la boca del barman: de ahí sale la cola del bocadillo. Vacío = 1.7 m por encima de este objeto.")]
    [SerializeField] private Transform mouthAnchor;
    [Tooltip("Cámara del jugador. Vacío = Camera.main.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Cara del barman para el bocadillo de arriba (SPR_BartenderFace). Vacío = sin retrato.")]
    [SerializeField] private Sprite portrait;
    [Tooltip("Nombre que sale en la etiqueta del bocadillo de arriba.")]
    [SerializeField] private string speakerName = "BARTENDER";

    [Header("Fuentes")]
    [Tooltip("Fuente de la frase y el resumen (Font_Montserrat SemiBold, la normal, no la Lux).")]
    [SerializeField] private TMP_FontAsset textFont;
    [Tooltip("Fuente del nombre y de las etiquetas DRINK / THROW (Font_BebasNeue, la normal).")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("Colores (sacados del concept del barman)")]
    [Tooltip("Relleno del bocadillo (el blanco roto de su traje).")]
    [SerializeField] private Color paperColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    [Tooltip("Contorno grueso, como el trazo del dibujo.")]
    [SerializeField] private Color inkColor = new Color(0.11f, 0.09f, 0.15f, 1f);
    [Tooltip("Color del texto.")]
    [SerializeField] private Color textColor = new Color(0.15f, 0.12f, 0.2f, 1f);
    [Tooltip("Morado de su pajarita y guantes: sombra, etiquetas y marco del retrato.")]
    [SerializeField] private Color accentPurple = new Color(0.44f, 0.18f, 0.6f, 1f);
    [Tooltip("Sombra dura del bocadillo.")]
    [SerializeField] private Color shadowColor = new Color(0.27f, 0.11f, 0.38f, 0.95f);

    [Header("Texto")]
    [Tooltip("Letras por segundo. 55 = rápido, se lee sin esperar.")]
    [Min(1f)]
    [SerializeField] private float charsPerSecond = 55f;
    [Tooltip("Segundos mínimos que se queda la frase entera en pantalla.")]
    [Min(0f)]
    [SerializeField] private float minHoldSeconds = 2.5f;
    [Tooltip("Segundos extra por cada palabra (tiempo de lectura).")]
    [Min(0f)]
    [SerializeField] private float secondsPerWord = 0.2f;
    [Tooltip("Máximo de segundos que se queda, por larga que sea la frase.")]
    [Min(0f)]
    [SerializeField] private float maxHoldSeconds = 7f;
    [Tooltip("Si gastas la botella (bebida o lanzada) mientras habla, el bocadillo se va antes.")]
    [SerializeField] private bool hideWhenBottleUsed = true;

    [Header("Mirar al barman")]
    [Tooltip("Grados desde el centro de la pantalla para que cuente como que lo miras.")]
    [Range(1f, 60f)]
    [SerializeField] private float lookAngle = 22f;
    [Tooltip("Grados para dejar de contar como que lo miras. Algo más que Look Angle, para que no parpadee en el borde.")]
    [Range(1f, 80f)]
    [SerializeField] private float lookAngleExit = 30f;
    [Tooltip("Más lejos de esto, el bocadillo va siempre arriba.")]
    [Min(0f)]
    [SerializeField] private float maxWorldDistance = 12f;
    [Tooltip("Capas que tapan al barman (paredes, columnas...). Si algo de estas capas está en medio, va arriba.")]
    [SerializeField] private LayerMask occlusionMask = ~0;
    [Tooltip("Segundos del fundido al pasar de un sitio a otro.")]
    [Min(0.01f)]
    [SerializeField] private float switchSeconds = 0.15f;

    [Header("Bocadillo en el mundo")]
    [Tooltip("Tamaño en pantalla respecto al de arriba. 0.9 = un pelín más pequeño.")]
    [Range(0.3f, 2f)]
    [SerializeField] private float worldSize = 0.9f;
    [Tooltip("Separación de la punta de la cola respecto a la boca, en píxeles del bocadillo (derecha, arriba).")]
    [SerializeField] private Vector2 worldTailOffset = new Vector2(30f, 40f);
    [Tooltip("Metros que se adelanta hacia la cámara, para que no se meta dentro de su cabeza.")]
    [Min(0f)]
    [SerializeField] private float worldForwardBias = 0.3f;

    [Header("Bocadillo en pantalla")]
    [Tooltip("Ancho del bocadillo en píxeles (a 1080p).")]
    [Min(200f)]
    [SerializeField] private float bubbleWidth = 600f;
    [Tooltip("Distancia al borde de arriba de la pantalla.")]
    [SerializeField] private float screenTopMargin = 40f;
    [Tooltip("Orden del Canvas de pantalla: más alto = por encima de más HUD.")]
    [SerializeField] private int screenSortingOrder = 40;

    [Header("Voz (opcional)")]
    [Tooltip("AudioSource para la voz. Vacío = el del propio barman, si tiene.")]
    [SerializeField] private AudioSource voiceSource;
    [Tooltip("Pitidos de voz de robot. Vacío = habla en silencio.")]
    [SerializeField] private AudioClip[] voiceBlips;
    [Tooltip("Un pitido cada tantas letras.")]
    [Min(1)]
    [SerializeField] private int blipEveryChars = 3;
    [Tooltip("Variación de tono de los pitidos (mín, máx).")]
    [SerializeField] private Vector2 blipPitch = new Vector2(0.9f, 1.15f);

    [Header("Depuración")]
    [Tooltip("Muestra sobre el barman si estás mirándolo, dónde sale el bocadillo y cuánto le queda.")]
    [SerializeField] private bool showDebug;

    // Medidas del bocadillo (en píxeles a 1080p; el del mundo usa las mismas y se escala).
    private const float PadX = 24f, PadY = 20f, AccentWidth = 12f;
    private const float LineFontSize = 26f, RowFontSize = 20f, LabelFontSize = 24f, NameFontSize = 26f;
    private const float ChipWidth = 96f, ChipHeight = 32f, RowGap = 8f, SectionGap = 14f;
    private const float PortraitSize = 124f, PortraitGap = 34f, NamePlateHeight = 36f;
    private const float PopSeconds = 0.3f, PopChars = 3f;

    private class View
    {
        public RectTransform root;
        public CanvasGroup group;
        public SpeechBubbleGraphic bubble;
        public TextMeshProUGUI line;
        public Image divider;
        public readonly RectTransform[] rows = new RectTransform[2];
        public readonly TextMeshProUGUI[] rowTexts = new TextMeshProUGUI[2];
        public readonly TextMeshProUGUI[] chipTexts = new TextMeshProUGUI[2];
        public RectTransform portraitRoot;
        public RectTransform namePlate;
        public float alpha;
        public float popStart = -10f;
        public float bodyHeight;
        public bool isWorld;
        public bool settled;   // la frase ya está entera y sin animar (no hace falta tocar la malla)
    }

    private View worldView;
    private View screenView;
    private Canvas worldCanvas;
    private Canvas screenCanvas;

    private TicketData currentTicket;
    private BottleHolder currentHolder;
    private PlayerHealth playerHealth;
    private Transform playerRoot;
    private bool speaking;
    private float visibleChars;
    private int totalChars;
    private float typedTime = -1f;
    private float endTime;
    private int lastBlipChar;
    private bool looking;
    private readonly Dictionary<TicketData, int> lastLine = new Dictionary<TicketData, int>();
    private readonly RaycastHit[] hits = new RaycastHit[8];

    private Vector3 MouthPosition => mouthAnchor != null ? mouthAnchor.position : transform.position + Vector3.up * 1.7f;

    private Camera Cam
    {
        get
        {
            if (playerCamera == null) playerCamera = Camera.main;
            return playerCamera;
        }
    }

    private void Awake()
    {
        if (voiceSource == null) voiceSource = GetComponent<AudioSource>();
        BuildCanvases();
    }

    private void OnDestroy()
    {
        // Los dos Canvas son objetos sueltos (no hijos del barman): hay que borrarlos a mano.
        if (worldCanvas != null) Destroy(worldCanvas.gameObject);
        if (screenCanvas != null) Destroy(screenCanvas.gameObject);
    }

    /// <summary>El barman te da la botella de este ticket: dice su frase. holder = el BottleHolder del jugador
    /// (para irse antes si gastas la botella).</summary>
    public void Say(TicketData ticket, BottleHolder holder)
    {
        if (ticket == null) return;

        currentTicket = ticket;
        currentHolder = holder;
        if (holder != null && playerRoot == null)
        {
            playerHealth = holder.GetComponentInParent<PlayerHealth>();
            CharacterController controller = holder.GetComponentInParent<CharacterController>();
            playerRoot = controller != null ? controller.transform : null;
        }

        Color bottleColor = ticket.ticketColor;
        bottleColor.a = 1f;
        string line = Highlight(PickLine(ticket), ReadableOnPaper(bottleColor));
        BottleEffect effect = ticket.bottleEffect;
        string drink = effect != null ? effect.DrinkSummary() : "";
        string thrown = effect != null ? effect.ThrowSummary() : "";

        Fill(worldView, line, drink, thrown, bottleColor);
        Fill(screenView, line, drink, thrown, bottleColor);

        // Número de letras reales (sin las etiquetas de color) para la escritura letra a letra.
        screenView.line.ForceMeshUpdate(true, true);
        totalChars = screenView.line.textInfo.characterCount;
        visibleChars = 0f;
        lastBlipChar = 0;
        typedTime = -1f;
        speaking = true;

        // Vuelve a hacer el rebote de entrada en el que esté visible.
        worldView.popStart = screenView.popStart = Time.time;

        if (showDebug) Debug.Log($"[Barman] Dice: \"{line}\" · beber: {drink} · lanzar: {thrown}", this);
    }

    private void Update()
    {
        if (!speaking && worldView.alpha <= 0f && screenView.alpha <= 0f) return;

        if (speaking) Advance();

        bool canSeeWorld = speaking && IsLookingAtBartender();
        looking = canSeeWorld;
        float step = Time.deltaTime / switchSeconds;
        Fade(worldView, speaking && canSeeWorld, step);
        Fade(screenView, speaking && !canSeeWorld, step);
    }

    private void LateUpdate()
    {
        // Después de que se mueva la cámara: el bocadillo del mundo la sigue sin temblar.
        if (worldView.alpha > 0f) PlaceWorldBubble();
        if (screenView.alpha > 0f) AnimateScreenBubble();

        if (worldView.alpha > 0f) Reveal(worldView);
        if (screenView.alpha > 0f) Reveal(screenView);
    }

    // ---------------- TIEMPOS ----------------

    private void Advance()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            speaking = false;
            return;
        }

        // Sigue unas letras más allá del final para que las últimas terminen su rebote.
        float typingEnd = totalChars + PopChars;
        if (visibleChars < typingEnd)
        {
            visibleChars = Mathf.Min(typingEnd, visibleChars + charsPerSecond * Time.deltaTime);
            PlayBlips();
            return;
        }

        if (typedTime < 0f)
        {
            typedTime = Time.time;
            int words = screenView.line.textInfo.wordCount;
            endTime = typedTime + Mathf.Clamp(minHoldSeconds + words * secondsPerWord, minHoldSeconds, Mathf.Max(minHoldSeconds, maxHoldSeconds));
        }

        // Si ya ha gastado la botella, lo que haga ya no importa: se va pronto (dejando leer un momento).
        if (hideWhenBottleUsed && currentHolder != null && !currentHolder.HasBottle)
            endTime = Mathf.Min(endTime, typedTime + 1f);

        if (Time.time >= endTime) speaking = false;
    }

    private void PlayBlips()
    {
        if (voiceSource == null || voiceBlips == null || voiceBlips.Length == 0) return;

        int shown = Mathf.FloorToInt(visibleChars);
        if (shown - lastBlipChar < blipEveryChars) return;
        lastBlipChar = shown;

        TMP_TextInfo info = screenView.line.textInfo;
        int index = Mathf.Clamp(shown - 1, 0, info.characterCount - 1);
        if (info.characterCount == 0 || char.IsWhiteSpace(info.characterInfo[index].character)) return;

        AudioClip clip = voiceBlips[Random.Range(0, voiceBlips.Length)];
        if (clip == null) return;
        voiceSource.pitch = Random.Range(blipPitch.x, blipPitch.y);
        voiceSource.PlayOneShot(clip);
    }

    private bool IsLookingAtBartender()
    {
        Camera cam = Cam;
        if (cam == null) return false;

        Vector3 eye = cam.transform.position;
        Vector3 toMouth = MouthPosition - eye;
        float distance = toMouth.magnitude;
        if (distance > maxWorldDistance || distance < 0.01f) return false;

        float limit = looking ? lookAngleExit : lookAngle;
        if (Vector3.Angle(cam.transform.forward, toMouth) > limit) return false;

        // ¿Hay algo en medio? Se ignoran el propio barman y el jugador.
        int count = Physics.RaycastNonAlloc(eye, toMouth / distance, hits, distance - 0.05f, occlusionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = hits[i].transform;
            if (hit.IsChildOf(transform)) continue;
            if (playerRoot != null && hit.IsChildOf(playerRoot)) continue;
            return false;
        }
        return true;
    }

    // ---------------- ANIMACIÓN ----------------

    private void Fade(View view, bool visible, float step)
    {
        float before = view.alpha;
        view.alpha = Mathf.MoveTowards(view.alpha, visible ? 1f : 0f, step);
        if (before <= 0f && view.alpha > 0f) view.popStart = Time.time;

        // Siempre activos: se ocultan con la transparencia (así el texto se puede medir aunque no se vea).
        // Con el menú de pausa abierto no se ve ninguno (si no, el de pantalla saldría por encima de la pausa).
        view.group.alpha = PauseMenu.IsPaused ? 0f : view.alpha;
    }

    private float PopScale(View view)
    {
        float t = Mathf.Clamp01((Time.time - view.popStart) / PopSeconds);
        // Entra desde un 70% pasándose un poco (rebote) y, al irse, se encoge un poco con el fundido.
        float pop = Mathf.LerpUnclamped(0.7f, 1f, BackOut(t));
        return pop * Mathf.Lerp(0.92f, 1f, view.alpha);
    }

    private void PlaceWorldBubble()
    {
        Camera cam = Cam;
        if (cam == null) return;

        Transform canvasTransform = worldCanvas.transform;
        Vector3 mouth = MouthPosition;
        Vector3 toCamera = cam.transform.position - mouth;
        Vector3 position = mouth + toCamera.normalized * Mathf.Min(worldForwardBias, toCamera.magnitude * 0.5f);

        // Mira a la cámara y se escala con la distancia: siempre del mismo tamaño en pantalla.
        float depth = Mathf.Max(0.3f, Vector3.Dot(position - cam.transform.position, cam.transform.forward));
        float pixelsToMeters = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / ReferenceHeight;

        canvasTransform.SetPositionAndRotation(position, cam.transform.rotation);
        canvasTransform.localScale = Vector3.one * (pixelsToMeters * worldSize * PopScale(worldView));
    }

    private void AnimateScreenBubble()
    {
        float scale = PopScale(screenView);
        screenView.root.localScale = new Vector3(scale, scale, 1f);
        // Baja un poco al aparecer y luego flota muy suave.
        float slide = (1f - Mathf.Clamp01((Time.time - screenView.popStart) / PopSeconds)) * 24f;
        float bob = Mathf.Sin(Time.time * 2.2f) * 2f;
        float x = portrait != null ? (PortraitSize + PortraitGap) * 0.5f : 0f;
        screenView.root.anchoredPosition = new Vector2(x, -screenTopMargin + slide + bob);
    }

    /// <summary>Escritura letra a letra: cada letra nueva entra con un pequeño rebote.</summary>
    private void Reveal(View view)
    {
        TextMeshProUGUI text = view.line;
        float visible = visibleChars;
        bool animating = visible < totalChars + PopChars;
        int shown = Mathf.Min(totalChars, Mathf.CeilToInt(visible));
        if (!animating && view.settled) return;

        text.maxVisibleCharacters = shown;
        text.ForceMeshUpdate();
        if (!animating)
        {
            // Una última vez sin deformar: todas las letras en su sitio y a su tamaño.
            view.settled = true;
            return;
        }
        view.settled = false;

        TMP_TextInfo info = text.textInfo;
        int first = Mathf.Max(0, Mathf.FloorToInt(visible - PopChars));
        int last = Mathf.Min(info.characterCount, shown);
        for (int i = first; i < last; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;

            float p = Mathf.Clamp01((visible - i) / PopChars);
            float scale = Mathf.LerpUnclamped(0.3f, 1f, BackOut(p));
            Vector3 lift = Vector3.up * ((1f - p) * 6f);
            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            int v = character.vertexIndex;
            Vector3 center = (vertices[v] + vertices[v + 2]) * 0.5f;
            for (int k = 0; k < 4; k++)
                vertices[v + k] = center + (vertices[v + k] - center) * scale + lift;
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private static float BackOut(float t)
    {
        const float overshoot = 1.7f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    // ---------------- TEXTO ----------------

    private string PickLine(TicketData ticket)
    {
        string[] lines = ticket.bartenderLines;
        if (lines == null || lines.Length == 0)
            return "Here you go. Use it wisely... or don't, your call.";

        // Al azar, pero sin repetir la última frase de esa misma botella.
        int index = Random.Range(0, lines.Length);
        if (lines.Length > 1 && lastLine.TryGetValue(ticket, out int previous) && index == previous)
            index = (index + 1 + Random.Range(0, lines.Length - 1)) % lines.Length;
        lastLine[ticket] = index;
        return lines[index];
    }

    private static string Highlight(string line, Color color)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return HighlightPattern.Replace(line, $"<b><color=#{hex}>$1</color></b>");
    }

    /// <summary>El color de la botella oscurecido lo justo para leerse sobre el bocadillo claro (el amarillo puro no se lee).</summary>
    private static Color ReadableOnPaper(Color color)
    {
        float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        const float maxLuminance = 0.42f;
        if (luminance <= maxLuminance) return color;
        float k = maxLuminance / luminance;
        return new Color(color.r * k, color.g * k, color.b * k, 1f);
    }

    // ---------------- CONSTRUCCIÓN ----------------

    private void BuildCanvases()
    {
        // Bocadillo del mundo: Canvas propio, suelto en la escena (no hijo del barman, para que su escala o sus
        // animaciones no lo deformen).
        var worldObject = new GameObject("BartenderSpeech_World", typeof(RectTransform));
        worldCanvas = worldObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.sortingOrder = screenSortingOrder;
        ((RectTransform)worldObject.transform).sizeDelta = new Vector2(10f, 10f);
        worldView = BuildView(worldObject.transform, true);

        // Bocadillo de pantalla: Canvas propio por encima del HUD, escalado a 1080p como el resto.
        var screenObject = new GameObject("BartenderSpeech_Screen", typeof(RectTransform));
        screenCanvas = screenObject.AddComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenCanvas.sortingOrder = screenSortingOrder;
        var scaler = screenObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;
        screenView = BuildView(screenObject.transform, false);
    }

    private View BuildView(Transform parent, bool isWorld)
    {
        var view = new View { isWorld = isWorld };

        view.root = NewRect(isWorld ? "WorldBubble" : "ScreenBubble", parent);
        view.root.anchorMin = view.root.anchorMax = isWorld ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        view.group = view.root.gameObject.AddComponent<CanvasGroup>();
        view.group.alpha = 0f;
        view.group.blocksRaycasts = false;
        view.group.interactable = false;

        // Retrato (solo arriba): círculo morado con la cara saliéndose un poco por arriba, como en los cómics.
        bool hasPortrait = !isWorld && portrait != null;
        if (hasPortrait)
        {
            view.portraitRoot = NewRect("Portrait", view.root);
            view.portraitRoot.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            view.portraitRoot.pivot = new Vector2(0.5f, 0.5f);
            var ring = view.portraitRoot.gameObject.AddComponent<SpeechBubbleGraphic>();
            ring.color = accentPurple;
            ring.Configure(PortraitSize * 0.5f, SpeechBubbleGraphic.TailSide.None, 0f, 0f, 0f, 0f,
                inkColor, 5f, shadowColor, new Vector2(6f, -6f));

            RectTransform face = NewRect("Face", view.portraitRoot);
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(PortraitSize * 1.3f, PortraitSize * 1.3f);
            face.anchoredPosition = new Vector2(0f, PortraitSize * 0.12f);
            var image = face.gameObject.AddComponent<Image>();
            image.sprite = portrait;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // Cuerpo del bocadillo.
        RectTransform bubbleRect = NewRect("Bubble", view.root);
        Stretch(bubbleRect);
        view.bubble = bubbleRect.gameObject.AddComponent<SpeechBubbleGraphic>();
        view.bubble.color = paperColor;
        SpeechBubbleGraphic.TailSide tail = isWorld ? SpeechBubbleGraphic.TailSide.Bottom
            : hasPortrait ? SpeechBubbleGraphic.TailSide.Left : SpeechBubbleGraphic.TailSide.None;
        view.bubble.Configure(22f, tail, isWorld ? 0.12f : 0.5f, 34f, isWorld ? 34f : PortraitGap - 4f, -14f,
            inkColor, 5f, shadowColor, new Vector2(7f, -7f));

        // Frase.
        view.line = NewText("Line", view.root, textFont, LineFontSize, textColor, TextAlignmentOptions.TopLeft);
        view.line.lineSpacing = -6f;

        view.divider = NewRect("Divider", view.root).gameObject.AddComponent<Image>();
        Color dividerColor = inkColor;
        dividerColor.a = 0.15f;
        view.divider.color = dividerColor;
        view.divider.raycastTarget = false;

        // Resumen: DRINK / THROW (beber / lanzar).
        string[] labels = { "DRINK", "THROW" };
        for (int i = 0; i < 2; i++)
        {
            RectTransform row = NewRect(labels[i], view.root);
            view.rows[i] = row;

            RectTransform chip = NewRect("Chip", row);
            chip.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            var chipGraphic = chip.gameObject.AddComponent<SpeechBubbleGraphic>();
            chipGraphic.color = accentPurple;
            chipGraphic.Configure(ChipHeight * 0.5f, SpeechBubbleGraphic.TailSide.None, 0f, 0f, 0f, 0f,
                inkColor, 3f, Color.clear, Vector2.zero);
            view.chipTexts[i] = NewText("Label", chip, labelFont, LabelFontSize, paperColor, TextAlignmentOptions.Center);
            Stretch(view.chipTexts[i].rectTransform);
            view.chipTexts[i].text = labels[i];

            view.rowTexts[i] = NewText("Text", row, textFont, RowFontSize, textColor, TextAlignmentOptions.TopLeft);
        }

        // Etiqueta con el nombre (solo arriba), montada sobre el borde y un poco torcida.
        if (!isWorld)
        {
            view.namePlate = NewRect("NamePlate", view.root);
            view.namePlate.pivot = new Vector2(0f, 0.5f);
            view.namePlate.localRotation = Quaternion.Euler(0f, 0f, 3f);
            var plate = view.namePlate.gameObject.AddComponent<SpeechBubbleGraphic>();
            plate.color = accentPurple;
            plate.Configure(NamePlateHeight * 0.5f, SpeechBubbleGraphic.TailSide.None, 0f, 0f, 0f, 0f,
                inkColor, 4f, shadowColor, new Vector2(4f, -4f));
            TextMeshProUGUI nameText = NewText("Name", view.namePlate, labelFont, NameFontSize, paperColor, TextAlignmentOptions.Center);
            Stretch(nameText.rectTransform);
            nameText.text = speakerName;
            float nameWidth = nameText.GetPreferredValues(speakerName).x + 32f;
            view.namePlate.sizeDelta = new Vector2(nameWidth, NamePlateHeight);
        }

        return view;
    }

    /// <summary>Pone el texto de esta botella y recoloca todo según lo que ocupa.</summary>
    private void Fill(View view, string line, string drink, string thrown, Color bottleColor)
    {
        float textLeft = AccentWidth + PadX;
        float contentWidth = bubbleWidth - textLeft - PadX;
        float top = view.namePlate != null ? PadY + 12f : PadY;

        view.bubble.SetAccent(AccentWidth, bottleColor);

        view.line.text = line;
        view.line.maxVisibleCharacters = 0;
        view.settled = false;
        float lineHeight = MeasureHeight(view.line, line, contentWidth);
        Place(view.line.rectTransform, textLeft, top, contentWidth, lineHeight);
        float y = top + lineHeight;

        string[] summaries = { drink, thrown };
        bool anyRow = !string.IsNullOrEmpty(drink) || !string.IsNullOrEmpty(thrown);
        view.divider.gameObject.SetActive(anyRow);
        if (anyRow)
        {
            y += SectionGap * 0.5f;
            Place(view.divider.rectTransform, textLeft, y, contentWidth, 2f);
            y += SectionGap * 0.5f + 2f;
        }

        float rowTextWidth = contentWidth - ChipWidth - 12f;
        for (int i = 0; i < 2; i++)
        {
            bool used = !string.IsNullOrEmpty(summaries[i]);
            view.rows[i].gameObject.SetActive(used);
            if (!used) continue;

            view.rowTexts[i].text = summaries[i];
            float textHeight = MeasureHeight(view.rowTexts[i], summaries[i], rowTextWidth);
            float rowHeight = Mathf.Max(ChipHeight, textHeight);
            Place(view.rows[i], textLeft, y, contentWidth, rowHeight);
            Place((RectTransform)view.chipTexts[i].transform.parent, 0f, 0f, ChipWidth, ChipHeight);
            // El texto centrado con la etiqueta si cabe en una línea; si no, empieza arriba a su altura.
            float textTop = textHeight < ChipHeight ? (ChipHeight - textHeight) * 0.5f : 0f;
            Place(view.rowTexts[i].rectTransform, ChipWidth + 12f, textTop, rowTextWidth, textHeight);
            y += rowHeight + RowGap;
        }
        if (anyRow) y -= RowGap;

        view.bodyHeight = y + PadY;
        view.root.sizeDelta = new Vector2(bubbleWidth, view.bodyHeight);

        if (view.namePlate != null)
            view.namePlate.anchoredPosition = new Vector2(textLeft - 6f, 2f);

        if (view.portraitRoot != null)
            view.portraitRoot.anchoredPosition = new Vector2(-PortraitGap - PortraitSize * 0.5f, -view.bodyHeight * 0.5f);

        if (view.isWorld)
        {
            // El pivote del bocadillo del mundo es la punta de la cola: así crece desde la boca del barman.
            float tailLength = view.bubble.TailLength;
            float tipX = Mathf.Lerp(34f, bubbleWidth - 34f, view.bubble.TailPosition) - 14f;
            view.root.pivot = new Vector2(tipX / bubbleWidth, -tailLength / view.bodyHeight);
            view.root.anchoredPosition = worldTailOffset;
        }
        else
        {
            view.root.pivot = new Vector2(0.5f, 1f);
        }
    }

    /// <summary>
    /// Alto real de un texto con ese ancho. Lo maqueta de verdad (negritas, saltos de línea y espacios que no se parten
    /// incluidos) y se queda con lo mayor entre eso y GetPreferredValues: GetPreferredValues solo se quedaba corto con
    /// las palabras resaltadas en negrita, y la última línea se montaba sobre DRINK / THROW.
    /// </summary>
    private static float MeasureHeight(TextMeshProUGUI text, string content, float width)
    {
        int visible = text.maxVisibleCharacters;
        text.rectTransform.sizeDelta = new Vector2(width, 4000f);
        text.text = content;
        text.maxVisibleCharacters = 99999;
        text.ForceMeshUpdate(true, true);

        float laidOut = 0f;
        TMP_TextInfo info = text.textInfo;
        if (info.lineCount > 0)
            laidOut = info.lineInfo[0].ascender - info.lineInfo[info.lineCount - 1].descender;

        float preferred = text.GetPreferredValues(content, width, 0f).y;
        text.maxVisibleCharacters = visible;
        return Mathf.Max(laidOut, preferred) + text.fontSize * 0.15f;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>Coloca un hijo con la esquina de arriba a la izquierda en (x, y) contando hacia abajo.</summary>
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static TextMeshProUGUI NewText(string objectName, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
    {
        var text = NewRect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        Gizmos.color = DebugLabels.Casino;
        Gizmos.DrawWireSphere(MouthPosition, 0.05f);
        if (!Application.isPlaying) return;

        string where = !speaking ? "callado" : looking ? "bocadillo en el mundo (lo miras)" : "bocadillo arriba (no lo miras)";
        string left = speaking && typedTime >= 0f ? $" · se va en {Mathf.Max(0f, endTime - Time.time):0.0} s" : "";
        DebugLabels.At(MouthPosition + Vector3.up * 0.4f, 1, $"Barman · {where}{left}", DebugLabels.Casino);
    }
#endif
}
