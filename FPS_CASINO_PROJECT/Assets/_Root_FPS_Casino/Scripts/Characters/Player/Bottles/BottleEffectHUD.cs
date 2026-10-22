using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporizador en pantalla del efecto de la botella que has bebido: un círculo del color de la
/// botella que se va vaciando como un reloj, el nombre y los segundos que quedan.
/// Igual que ReloadRadialUI: lee el estado cada frame (BottleHolder.EffectTimeRemaining) y
/// no guarda tiempos propios, así nunca se desincroniza del efecto real.
///
/// Jerarquía esperada en el Canvas:
/// BottleEffectHUD (este script, siempre activo)
///   - Content (lo que se muestra/oculta)
///       - Fill (Image: Type = Filled, Fill Method = Radial 360)
///       - Name (TMP_Text, opcional)
///       - Time (TMP_Text)
/// </summary>
public class BottleEffectHUD : MonoBehaviour
{
    [SerializeField] private BottleHolder bottleHolder;
    [Tooltip("Hijo que se muestra mientras hay efecto. NO pongas aquí el propio objeto del script: se apagaría su Update.")]
    [SerializeField] private GameObject content;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text timeText;

    private void Update()
    {
        bool active = bottleHolder != null && bottleHolder.HasActiveEffect;

        if (content != null && content.activeSelf != active)
            content.SetActive(active);

        if (!active) return;

        TicketData effect = bottleHolder.ActiveEffect;
        float remaining = bottleHolder.EffectTimeRemaining;
        float duration = Mathf.Max(bottleHolder.EffectDuration, 0.0001f);

        if (fillImage != null)
        {
            fillImage.fillAmount = remaining / duration;
            Color color = effect.ticketColor;
            color.a = 1f;
            fillImage.color = color;
        }

        if (nameText != null) nameText.text = effect.displayName;
        if (timeText != null) timeText.text = remaining.ToString("0.0") + "s";
    }
}
