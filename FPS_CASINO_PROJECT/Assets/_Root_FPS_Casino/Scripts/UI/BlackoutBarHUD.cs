using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de blackout en pantalla. Se rellena de 0 a 1 con el nivel de PlayerBlackout y cambia
/// de color según lo cerca que estés del blackout (verde → amarillo → rojo). En la zona de peligro
/// parpadea para avisar de que una botella más te mata.
///
/// Requiere una Image con Image Type = Filled y Fill Method = Horizontal.
/// Si el sprite de la barra no ocupa toda su imagen (barra dibujada en un lienzo más grande), Fill Range dice entre qué
/// fracciones de ancho está la barra dentro de la imagen, para que "vacía" y "llena" coincidan con el dibujo.
/// </summary>
public class BlackoutBarHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Blackout del jugador que muestra la barra.")]
    [SerializeField] private PlayerBlackout blackout;
    [Tooltip("Barra (Image Filled, Horizontal) que se rellena con el nivel de blackout.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Opcional: texto con el porcentaje.")]
    [SerializeField] private TMP_Text percentText;
    [Tooltip("Hijo que se oculta cuando la barra está a 0. NO pongas el propio objeto del script.")]
    [SerializeField] private GameObject content;

    [Header("Encaje del dibujo")]
    [Tooltip("Dónde empieza y acaba la barra dentro de su imagen, en fracción del ancho (0 = borde izquierdo, 1 = derecho). (0, 1) = la barra ocupa toda la imagen.")]
    [SerializeField] private Vector2 fillRange = new Vector2(0f, 1f);

    [Header("Colores")]
    [Tooltip("De izquierda (vacía) a derecha (llena).")]
    [SerializeField] private Gradient colorByLevel = DefaultGradient();

    [Header("Aviso de peligro")]
    [Tooltip("A partir de este nivel la barra parpadea (0.6 = con dos botellas de tres).")]
    [Range(0f, 1f)]
    [SerializeField] private float dangerThreshold = 0.6f;
    [Tooltip("Velocidad del parpadeo en la zona de peligro. 6 = rápido, llama la atención.")]
    [Min(0f)]
    [SerializeField] private float blinkSpeed = 6f;
    [Tooltip("Cuánto se apaga la barra en cada parpadeo de peligro (su transparencia). 0.4 = baja hasta el 40%. Se nota aunque el dibujo ya tenga su color.")]
    [Range(0f, 1f)]
    [SerializeField] private float blinkMinAlpha = 0.4f;

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
            fillImage.fillAmount = Mathf.Lerp(fillRange.x, fillRange.y, Mathf.Clamp01(level));

        if (percentText != null)
            percentText.text = Mathf.CeilToInt(level * 100f) + "%";
    }

    private void Update()
    {
        if (fillImage == null || level <= 0f) return;

        Color color = colorByLevel.Evaluate(level);

        // En peligro, la barra parpadea: se aclara y se apaga a ratos.
        if (level >= dangerThreshold)
        {
            float pulse = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
            color = Color.Lerp(color, Color.white, pulse * 0.5f);
            color.a *= Mathf.Lerp(1f, blinkMinAlpha, pulse);
        }

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