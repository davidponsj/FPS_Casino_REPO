using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida. Se actualiza solo cuando cambia la vida (evento OnHealthChanged), no cada frame.
/// La barra de delante (fill) baja al instante y la de detrás (delayedFill) la sigue con retraso:
/// así se ve claramente cuánto daño acabas de recibir, como en la mayoría de shooters.
///
/// Requiere Images con Image Type = Filled y Fill Method = Horizontal.
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

    [Header("Barra con retraso")]
    [Tooltip("Fracción de barra por segundo a la que baja la barra de detrás. 0.5 = media barra por segundo.")]
    [Min(0.01f)]
    [SerializeField] private float delayedFillSpeed = 0.5f;

    private float targetFill = 1f;

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
            fillImage.fillAmount = targetFill;
            fillImage.color = targetFill <= lowHealthThreshold ? lowHealthColor : healthyColor;
        }

        // Al curarte, la barra de detrás no tiene que "ir por detrás": salta directamente.
        if (delayedFillImage != null && delayedFillImage.fillAmount < targetFill)
            delayedFillImage.fillAmount = targetFill;

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void Update()
    {
        // La única cosa que sí se anima cada frame: la barra de detrás bajando hasta la real.
        if (delayedFillImage != null && delayedFillImage.fillAmount > targetFill)
            delayedFillImage.fillAmount = Mathf.MoveTowards(delayedFillImage.fillAmount, targetFill, delayedFillSpeed * Time.deltaTime);
    }
}
