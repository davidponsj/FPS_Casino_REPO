using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// "+350" flotando encima de cada robot que matas: sale de golpe sobre su cabeza, sube, se ladea un poco y se desvanece.
/// Cuantos más puntos da la baja, más grande y de otro color (dorado → magenta), y el JACKPOT sale en arcoíris.
///
/// Escucha a KillScore (OnKillScored) y lee de él a quién has matado (LastKilledEnemy) y cuántos puntos ha dado esa baja
/// (LastKillPoints, sin el multiplicador del combo). Así sale para cualquier baja que puntúe, también con granadas o charcos.
///
/// Va en un objeto vacío del Canvas, anclado al centro de la pantalla. Necesita un texto hijo DESACTIVADO de molde
/// (Text Template) con la fuente y el material de neón. Los textos se crean al empezar copiando el molde.
/// Usa el tiempo real, así no le afecta la micro‑pausa al matar.
/// </summary>
public class KillPointsHUD : MonoBehaviour
{
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

    [Header("Referencias")]
    [Tooltip("KillScore del jugador. Vacío = lo busca en el objeto con el tag Player.")]
    [SerializeField] private KillScore killScore;
    [Tooltip("Texto hijo DESACTIVADO que sirve de molde (fuente Orbitron, material Neon, centrado). Cada \"+puntos\" es una copia suya.")]
    [SerializeField] private TMP_Text textTemplate;
    [Tooltip("Cámara del jugador, para pasar la posición del robot a la pantalla. Vacío = Camera.main.")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Textos que puede haber a la vez. 10 = sobra para un escopetazo a una horda. Si se llenan, se reutiliza el más antiguo.")]
    [Min(1)]
    [SerializeField] private int maxPopups = 10;

    [Header("Posición")]
    [Tooltip("Metros por encima de la cabeza del robot donde aparece. 0.3 = justo encima.")]
    [SerializeField] private float aboveHeadMeters = 0.3f;
    [Tooltip("Metros que sube mientras dura. 0.8 = sube flotando.")]
    [SerializeField] private float riseMeters = 0.8f;
    [Tooltip("Píxeles que se desvía a un lado al azar mientras sube. 50 = cada uno sale hacia un sitio y no se tapan.")]
    [Min(0f)]
    [SerializeField] private float sideDriftPixels = 50f;
    [Tooltip("Grados de giro al azar de cada texto. 12 = ladeados, más dinámicos.")]
    [Range(0f, 45f)]
    [SerializeField] private float randomTiltDegrees = 12f;

    [Header("Tiempos")]
    [Tooltip("Segundos que dura cada texto. 1.1 = da tiempo a leerlo sin estorbar.")]
    [Min(0.1f)]
    [SerializeField] private float lifetimeSeconds = 1.1f;
    [Tooltip("Segundos del final en los que se desvanece. 0.35 = se apaga rápido.")]
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 0.35f;
    [Tooltip("Tamaño con el que aparece antes de encogerse. 1.8 = sale como un golpe.")]
    [Min(1f)]
    [SerializeField] private float punchScale = 1.8f;
    [Tooltip("Segundos que tarda en encogerse a su tamaño. 0.12 = golpe rápido.")]
    [Min(0.01f)]
    [SerializeField] private float popSeconds = 0.12f;

    [Header("Tamaños según los puntos")]
    [Tooltip("Puntos a partir de los que la baja cuenta como \"gorda\" (más grande y en magenta). 300 = baja con varios trucos.")]
    [Min(0)]
    [SerializeField] private int bigKillPoints = 300;
    [Tooltip("Tamaño de una baja normal.")]
    [Min(0.1f)]
    [SerializeField] private float normalScale = 1f;
    [Tooltip("Tamaño de una baja gorda. 1.35 = un 35% más grande.")]
    [Min(0.1f)]
    [SerializeField] private float bigScale = 1.35f;
    [Tooltip("Tamaño del JACKPOT. 1.7 = enorme.")]
    [Min(0.1f)]
    [SerializeField] private float jackpotScale = 1.7f;

    [Header("Colores (degradado de arriba a abajo de las letras)")]
    [Tooltip("Arriba de las letras de una baja normal.")]
    [SerializeField] private Color normalTopColor = new Color(1f, 0.97f, 0.75f);
    [Tooltip("Abajo de las letras de una baja normal. Dorado.")]
    [SerializeField] private Color normalBottomColor = new Color(1f, 0.62f, 0.1f);
    [Tooltip("Brillo de una baja normal. A = fuerza.")]
    [SerializeField] private Color normalGlowColor = new Color(1f, 0.55f, 0.05f, 0.75f);
    [Tooltip("Arriba de las letras de una baja gorda.")]
    [SerializeField] private Color bigTopColor = Color.white;
    [Tooltip("Abajo de las letras de una baja gorda. Magenta neón.")]
    [SerializeField] private Color bigBottomColor = new Color(1f, 0.24f, 0.72f);
    [Tooltip("Brillo de una baja gorda.")]
    [SerializeField] private Color bigGlowColor = new Color(1f, 0.16f, 0.6f, 0.85f);
    [Tooltip("Vueltas por segundo del arcoíris del JACKPOT. 1 = rápido.")]
    [Min(0f)]
    [SerializeField] private float jackpotRainbowSpeed = 1f;

    private class Popup
    {
        public TMP_Text Text;
        public RectTransform Rect;
        public Material Material;
        public Transform Target;     // el robot (lo sigue mientras cae)
        public Vector3 LocalPoint;   // punto sobre su cabeza, relativo a él
        public Vector3 WorldPoint;   // último punto conocido (si el robot desaparece, se queda aquí)
        public float ShowTime = -1f; // -1 = libre
        public float Scale;
        public float Drift;          // píxeles a un lado al final de su vida (con signo)
        public bool IsJackpot;
        public bool IsActive => ShowTime >= 0f;
    }

    private readonly List<Popup> popups = new List<Popup>();
    private RectTransform rect;
    private Canvas canvas;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();

        if (killScore == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) killScore = player.GetComponent<KillScore>();
        }

        if (killScore == null || textTemplate == null)
        {
            Debug.LogWarning($"{name}: falta KillScore (en el Player) o Text Template. Los puntos flotantes no se verán.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < maxPopups; i++)
        {
            TMP_Text text = Instantiate(textTemplate, textTemplate.transform.parent);
            text.gameObject.SetActive(false);
            text.enableVertexGradient = true;
            // fontMaterial crea una copia del material para este texto: así cada uno brilla de su color.
            popups.Add(new Popup { Text = text, Rect = text.rectTransform, Material = text.fontMaterial });
        }
    }

    private void OnEnable()
    {
        if (killScore != null) killScore.OnKillScored += HandleKillScored;
    }

    private void OnDisable()
    {
        if (killScore != null) killScore.OnKillScored -= HandleKillScored;
    }

    private void OnDestroy()
    {
        foreach (Popup popup in popups)
            if (popup.Material != null) Destroy(popup.Material);
    }

    private void HandleKillScored()
    {
        EnemyHealth enemy = killScore.LastKilledEnemy;
        if (enemy == null) return;

        bool isJackpot = false;
        foreach (KillScore.ScoreLine line in killScore.LastKillLines)
            if (line.IsJackpot) isJackpot = true;

        int points = killScore.LastKillPoints;
        bool isBig = points >= bigKillPoints;

        Popup popup = GetFreePopup();
        popup.Target = enemy.transform;
        Vector3 head = GetHeadPoint(enemy);
        popup.LocalPoint = popup.Target.InverseTransformPoint(head);
        popup.WorldPoint = head;
        popup.ShowTime = Time.unscaledTime;
        popup.IsJackpot = isJackpot;
        popup.Scale = isJackpot ? jackpotScale : isBig ? bigScale : normalScale;
        popup.Drift = Random.Range(-sideDriftPixels, sideDriftPixels);

        popup.Text.text = $"+{points:N0}";
        popup.Text.color = Color.white;
        popup.Text.colorGradient = isJackpot ? new VertexGradient(Color.white)
            : isBig ? new VertexGradient(bigTopColor, bigTopColor, bigBottomColor, bigBottomColor)
            : new VertexGradient(normalTopColor, normalTopColor, normalBottomColor, normalBottomColor);
        if (popup.Material != null)
            popup.Material.SetColor(GlowColorId, isBig ? bigGlowColor : normalGlowColor);

        popup.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-randomTiltDegrees, randomTiltDegrees));
        popup.Rect.SetAsLastSibling();
        popup.Text.gameObject.SetActive(true);
        UpdatePopup(popup, Time.unscaledTime);
    }

    /// <summary>Punto sobre la cabeza: lo más alto de su renderer (los colliders ya están apagados al morir).</summary>
    private Vector3 GetHeadPoint(EnemyHealth enemy)
    {
        Renderer body = enemy.GetComponentInChildren<Renderer>();
        Vector3 top = body != null
            ? new Vector3(body.bounds.center.x, body.bounds.max.y, body.bounds.center.z)
            : enemy.transform.position + Vector3.up * 2f;
        return top + Vector3.up * aboveHeadMeters;
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        foreach (Popup popup in popups)
            if (popup.IsActive) UpdatePopup(popup, now);
    }

    private void UpdatePopup(Popup popup, float now)
    {
        float age = now - popup.ShowTime;
        if (age >= lifetimeSeconds)
        {
            popup.ShowTime = -1f;
            popup.Target = null;
            popup.Text.gameObject.SetActive(false);
            return;
        }

        float life01 = age / lifetimeSeconds;

        // Sigue al robot mientras cae y sube flotando (rápido al principio, frenando).
        if (popup.Target != null)
            popup.WorldPoint = popup.Target.TransformPoint(popup.LocalPoint);
        float rise = riseMeters * (1f - (1f - life01) * (1f - life01));

        if (!TryWorldToCanvas(popup.WorldPoint + Vector3.up * rise, out Vector2 position))
        {
            popup.Text.alpha = 0f; // detrás de la cámara
            return;
        }
        popup.Rect.anchoredPosition = position + new Vector2(popup.Drift * life01, 0f);

        // Golpe de tamaño al salir y se encoge a su tamaño.
        float pop01 = Mathf.Clamp01(age / popSeconds);
        float easeOut = 1f - (1f - pop01) * (1f - pop01) * (1f - pop01);
        float scale = popup.Scale * Mathf.Lerp(punchScale, 1f, easeOut);
        popup.Rect.localScale = new Vector3(scale, scale, 1f);

        // Se desvanece en los últimos fadeSeconds.
        float fadeStart = lifetimeSeconds - fadeSeconds;
        popup.Text.alpha = age < fadeStart ? 1f : 1f - (age - fadeStart) / fadeSeconds;

        if (popup.IsJackpot) ApplyRainbow(popup, now);
    }

    /// <summary>Arcoíris letra a letra que se mueve, y el brillo del mismo color.</summary>
    private void ApplyRainbow(Popup popup, float now)
    {
        TMP_Text text = popup.Text;
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;

            Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(now * jackpotRainbowSpeed + i * 0.1f, 1f), 0.75f, 1f);
            for (int v = 0; v < 4; v++)
            {
                Color32 color = rainbow;
                color.a = colors[character.vertexIndex + v].a;
                colors[character.vertexIndex + v] = color;
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

        if (popup.Material != null)
        {
            Color glow = Color.HSVToRGB(Mathf.Repeat(now * jackpotRainbowSpeed, 1f), 0.9f, 1f);
            glow.a = 0.85f;
            popup.Material.SetColor(GlowColorId, glow);
        }
    }

    private bool TryWorldToCanvas(Vector3 world, out Vector2 local)
    {
        local = Vector2.zero;
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return false;

        Vector3 screenPoint = cam.WorldToScreenPoint(world);
        if (screenPoint.z <= 0f) return false;

        // Con un Canvas "Screen Space - Overlay" no hay cámara de UI (null); con "Screen Space - Camera", la suya.
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, uiCamera, out local);
    }

    private Popup GetFreePopup()
    {
        Popup oldest = popups[0];
        foreach (Popup popup in popups)
        {
            if (!popup.IsActive) return popup;
            if (popup.ShowTime < oldest.ShowTime) oldest = popup;
        }
        return oldest; // todos ocupados: se reutiliza el más antiguo
    }
}
