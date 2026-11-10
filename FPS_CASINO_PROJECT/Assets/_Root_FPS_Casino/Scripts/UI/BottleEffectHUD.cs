using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efecto de la botella que te has bebido, en el HUD: una botella del color de la que te has tomado (BottleGauge) cuyo
/// líquido va bajando con los segundos que le quedan al efecto, con burbujas. Aparece con un pequeño rebote al beber,
/// el líquido late cuando queda poco y la botella se encoge y desaparece al acabar.
/// Igual que ReloadHUD: lee el estado cada frame (BottleHolder.EffectTimeRemaining) y no guarda tiempos propios, así
/// nunca se desincroniza del efecto real.
///
/// Jerarquía:
/// BottleEffectHUD (este script, siempre activo)
///   - Content (lo que se muestra/oculta y se anima)
///       - Bottle (BottleGauge)
///       - Time (TMP_Text, opcional)
///       - Name (TMP_Text, opcional)
/// Fill Image es opcional (el círculo antiguo): si lo dejas, también se sigue rellenando.
/// </summary>
public class BottleEffectHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("BottleHolder del jugador (en PF_Player): de él sale el efecto activo y sus segundos.")]
    [SerializeField] private BottleHolder bottleHolder;
    [Tooltip("Hijo que se muestra mientras hay efecto. NO pongas aquí el propio objeto del script: se apagaría su Update.")]
    [SerializeField] private GameObject content;
    [Tooltip("La botella dibujada (BottleGauge): toma el color de la botella y baja con el tiempo.")]
    [SerializeField] private BottleGauge bottle;
    [Tooltip("Opcional: el círculo antiguo (Image Filled). Vacío = no se usa.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Texto con el nombre de la botella. Opcional.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Texto con los segundos que quedan. Opcional.")]
    [SerializeField] private TMP_Text timeText;

    [Header("Animación")]
    [Tooltip("Segundos del rebote al aparecer.")]
    [Min(0.01f)]
    [SerializeField] private float popSeconds = 0.35f;
    [Tooltip("Segundos que tarda en encogerse y desaparecer al acabar el efecto.")]
    [Min(0.01f)]
    [SerializeField] private float hideSeconds = 0.25f;
    [Tooltip("Por debajo de estos segundos el líquido late y el tiempo se pone en rojo.")]
    [Min(0f)]
    [SerializeField] private float warningSeconds = 3f;
    [Tooltip("Color del tiempo cuando queda poco.")]
    [SerializeField] private Color warningTextColor = new Color(1f, 0.35f, 0.3f);

    private RectTransform contentRect;
    private Color timeColor = Color.white;
    private float shownTime = -1f;   // momento en que apareció (-1 = no se está mostrando)
    private float hideStart = -1f;   // momento en que empezó a esconderse (-1 = no se está escondiendo)
    private TicketData shownEffect;  // botella que se está mostrando (si te bebes otra, vuelve a rebotar)

    private void Awake()
    {
        if (content != null)
        {
            contentRect = content.transform as RectTransform;
            content.SetActive(false);
        }
        if (timeText != null) timeColor = timeText.color;
    }

    private void Update()
    {
        bool active = bottleHolder != null && bottleHolder.HasActiveEffect;

        if (active) Show();
        else Hide();
    }

    private void Show()
    {
        if (content != null && !content.activeSelf) content.SetActive(true);
        TicketData effect = bottleHolder.ActiveEffect;
        if (shownTime < 0f || hideStart >= 0f || effect != shownEffect)
        {
            shownTime = Time.time;
            hideStart = -1f;
            shownEffect = effect;
        }

        float remaining = bottleHolder.EffectTimeRemaining;
        float duration = Mathf.Max(bottleHolder.EffectDuration, 0.0001f);
        float fill01 = remaining / duration;
        bool warning = remaining <= warningSeconds;
        Color color = effect != null ? effect.ticketColor : Color.white;
        color.a = 1f;

        if (bottle != null)
        {
            bottle.LiquidColor = color;
            bottle.Fill01 = fill01;
            bottle.Warning = warning;
        }
        if (fillImage != null)
        {
            fillImage.fillAmount = fill01;
            fillImage.color = color;
        }
        if (nameText != null && effect != null) nameText.text = effect.displayName;
        if (timeText != null)
        {
            timeText.text = remaining.ToString("0.0") + "s";
            timeText.color = warning ? warningTextColor : timeColor;
        }

        // Rebote al aparecer: crece un poco más de la cuenta y vuelve a su tamaño.
        float t = Mathf.Clamp01((Time.time - shownTime) / popSeconds);
        SetScale(t >= 1f ? 1f : BackOut(t));
    }

    private void Hide()
    {
        if (content == null || !content.activeSelf) return;
        if (hideStart < 0f) hideStart = Time.time;

        float t = Mathf.Clamp01((Time.time - hideStart) / hideSeconds);
        SetScale(1f - t * t);
        if (t >= 1f)
        {
            content.SetActive(false);
            shownTime = -1f;
            hideStart = -1f;
            shownEffect = null;
            SetScale(1f);
        }
    }

    private void SetScale(float scale)
    {
        if (contentRect != null) contentRect.localScale = Vector3.one * Mathf.Max(0f, scale);
    }

    /// <summary>De 0 a 1 pasándose un poco al final (rebote).</summary>
    private static float BackOut(float t)
    {
        const float overshoot = 1.7f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }
}
