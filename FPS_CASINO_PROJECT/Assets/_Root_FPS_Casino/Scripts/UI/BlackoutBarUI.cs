using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de blackout en pantalla. Se rellena de 0 a 1 con el nivel de PlayerBlackout y cambia
/// de color según lo cerca que estés del blackout (verde → amarillo → rojo). En la zona de peligro
/// parpadea para avisar de que una botella más te mata.
///
/// Requiere una Image con Image Type = Filled y Fill Method = Horizontal.
/// </summary>
public class BlackoutBarUI : MonoBehaviour
{
    [SerializeField] private PlayerBlackout blackout;
    [SerializeField] private Image fillImage;
    [Tooltip("Opcional: texto con el porcentaje.")]
    [SerializeField] private TMP_Text percentText;
    [Tooltip("Hijo que se oculta cuando la barra está a 0. NO pongas el propio objeto del script.")]
    [SerializeField] private GameObject content;

    [Header("Colores")]
    [Tooltip("De izquierda (vacía) a derecha (llena).")]
    [SerializeField] private Gradient colorByLevel = DefaultGradient();

    [Header("Aviso de peligro")]
    [Tooltip("A partir de este nivel la barra parpadea (0.6 = con dos botellas de tres).")]
    [Range(0f, 1f)]
    [SerializeField] private float dangerThreshold = 0.6f;
    [SerializeField] private float blinkSpeed = 6f;

    private float level;

    private void OnEnable()
    {
        if (blackout != null)
        {
            blackout.OnBlackoutChanged += SetLevel;
            SetLevel(blackout.Blackout01);
        }
    }

    private void OnDisable()
    {
        if (blackout != null)
            blackout.OnBlackoutChanged -= SetLevel;
    }

    private void SetLevel(float newLevel)
    {
        level = newLevel;

        if (content != null && content.activeSelf != level > 0f)
            content.SetActive(level > 0f);

        if (fillImage != null)
            fillImage.fillAmount = level;

        if (percentText != null)
            percentText.text = Mathf.CeilToInt(level * 100f) + "%";
    }

    private void Update()
    {
        if (fillImage == null || level <= 0f) return;

        Color color = colorByLevel.Evaluate(level);

        // En peligro, la barra parpadea entre su color y blanco.
        if (level >= dangerThreshold)
            color = Color.Lerp(color, Color.white, (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.25f);

        fillImage.color = color;
    }

    private static Gradient DefaultGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.3f, 0.85f, 0.3f), 0f),
                new GradientColorKey(new Color(0.95f, 0.8f, 0.2f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.15f, 0.15f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}