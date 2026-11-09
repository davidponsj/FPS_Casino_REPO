using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida. Se actualiza solo cuando cambia la vida (evento OnHealthChanged), no cada frame.
/// La barra de delante (fill) baja al instante y la de detrás (delayedFill) la sigue con retraso:
/// así se ve claramente cuánto daño acabas de recibir, como en la mayoría de shooters.
///
/// Requiere Images con Image Type = Filled y Fill Method = Horizontal.
/// Si el sprite de la barra no ocupa toda su imagen (p. ej. una barra dibujada en un lienzo de 720x720 junto al retrato),
/// Fill Range dice entre qué fracciones de ancho está la barra dentro de la imagen: así "vacía" y "llena" coinciden con
/// los extremos del dibujo y todas las capas se pueden apilar en el mismo sitio.
/// </summary>
public class HealthBarHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Vida del jugador que muestra la barra.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Barra principal (Image Filled, Horizontal). Baja al instante al recibir daño.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Opcional: barra blanca/roja detrás de la principal que baja con retraso.")]
    [SerializeField] private Image delayedFillImage;
    [Tooltip("Opcional: texto '75 / 100'.")]
    [SerializeField] private TMP_Text healthText;

    [Header("Colores")]
    [Tooltip("Color de la barra con la vida por encima del umbral.")]
    [SerializeField] private Color healthyColor = new Color(0.3f, 0.9f, 0.4f);
    [Tooltip("Color de la barra con la vida por debajo del umbral.")]
    [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f);
    [Tooltip("Por debajo de este porcentaje la barra se pone del color de vida baja.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Header("Encaje del dibujo")]
    [Tooltip("Dónde empieza y acaba la barra dentro de su imagen, en fracción del ancho (0 = borde izquierdo, 1 = derecho). (0, 1) = la barra ocupa toda la imagen.")]
    [SerializeField] private Vector2 fillRange = new Vector2(0f, 1f);

    [Header("Barra con retraso")]
    [Tooltip("Fracción de barra por segundo a la que baja la barra de detrás. 0.5 = media barra por segundo.")]
    [Min(0.01f)]
    [SerializeField] private float delayedFillSpeed = 0.5f;

    private float targetFill = 1f;     // vida de 0 a 1
    private float delayedFill = 1f;    // lo que marca la barra de detrás, de 0 a 1

    /// <summary>Pasa de vida (0 a 1) al fillAmount de la imagen, según dónde está la barra dentro del dibujo.</summary>
    private float ToFillAmount(float value01) => Mathf.Lerp(fillRange.x, fillRange.y, Mathf.Clamp01(value01));

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateBar;
            UpdateBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateBar;
    }

    private void UpdateBar(float current, float max)
    {
        targetFill = max > 0f ? current / max : 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = ToFillAmount(targetFill);
            fillImage.color = targetFill <= lowHealthThreshold ? lowHealthColor : healthyColor;
        }

        // Al curarte, la barra de detrás no tiene que "ir por detrás": salta directamente.
        if (delayedFill < targetFill)
        {
            delayedFill = targetFill;
            if (delayedFillImage != null) delayedFillImage.fillAmount = ToFillAmount(delayedFill);
        }

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void Update()
    {
        // La única cosa que sí se anima cada frame: la barra de detrás bajando hasta la real.
        if (delayedFillImage != null && delayedFill > targetFill)
        {
            delayedFill = Mathf.MoveTowards(delayedFill, targetFill, delayedFillSpeed * Time.deltaTime);
            delayedFillImage.fillAmount = ToFillAmount(delayedFill);
        }
    }
}
