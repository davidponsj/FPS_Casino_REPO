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
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fillImage;
    [Tooltip("Opcional: barra blanca/roja detrás de la principal que baja con retraso.")]
    [SerializeField] private Image delayedFillImage;
    [Tooltip("Opcional: texto '75 / 100'.")]
    [SerializeField] private TMP_Text healthText;

    [Header("Colores")]
    [SerializeField] private Color healthyColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f);
    [Tooltip("Por debajo de este porcentaje la barra se pone del color de vida baja.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Header("Barra con retraso")]
    [SerializeField] private float delayedFillSpeed = 0.5f; // fracción de barra por segundo

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
