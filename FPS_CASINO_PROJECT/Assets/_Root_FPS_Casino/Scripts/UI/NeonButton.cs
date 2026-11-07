using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón con marco de neón (RETRY, MENÚ del cartel de muerte). Dibuja el marco igual que NeonFrame y además:
/// - Quieto: brilla a medias (y si es un neón viejo, falla de vez en cuando).
/// - Ratón encima: se enciende del todo, late suave y el texto crece un poco.
/// - Al pulsar: el texto se hunde un instante.
///
/// Va en el objeto del botón, EN LUGAR del Image: es el que recibe el ratón (todo el rectángulo del botón es clicable)
/// y el que dibuja el marco alrededor de ese rectángulo. En el Button pon Target Graphic = este componente y
/// Transition = None (el brillo ya lo hace este script). El color del neón es el Color de este componente.
/// </summary>
public class NeonButton : NeonFrame, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Ratón encima")]
    [Tooltip("Brillo con el ratón encima.")]
    [Range(0f, 1f)]
    [SerializeField] private float hoverBrightness = 1f;
    [Tooltip("Cuánto late el neón con el ratón encima. 0 = no late.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pulseAmount = 0.15f;
    [Tooltip("Latidos por segundo.")]
    [Min(0f)]
    [SerializeField] private float pulseSpeed = 2.5f;

    [Header("Texto")]
    [Tooltip("Texto del botón, para que crezca y se hunda. Vacío = lo busca en los hijos.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Tamaño del texto con el ratón encima. 1.06 = crece un poco.")]
    [Min(0.1f)]
    [SerializeField] private float hoverScale = 1.06f;
    [Tooltip("Tamaño del texto al pulsar. 0.94 = se hunde.")]
    [Min(0.1f)]
    [SerializeField] private float pressScale = 0.94f;

    private bool hovered;
    private bool pressed;
    private Selectable selectable;

    private bool Lit => hovered && (selectable == null || selectable.IsInteractable());

    protected override void Awake()
    {
        base.Awake();
        selectable = GetComponent<Selectable>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        hovered = pressed = false;
        if (label != null) label.rectTransform.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (showDebug) Debug.Log($"{name}: ratón encima.", this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        if (showDebug) Debug.Log($"{name}: pulsado.", this);
    }

    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    protected override float TargetBrightness()
    {
        if (!Lit) return IdleBrightness;
        float pulse = pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));
        return hoverBrightness - pulse;
    }

    // Con el ratón encima el neón no falla: el botón tiene que verse que responde.
    protected override bool CanFault() => !Lit;

    protected override void Update()
    {
        base.Update();
        if (label == null) return;

        float scale = pressed && Lit ? pressScale : Lit ? hoverScale : 1f;
        float current = label.rectTransform.localScale.x;
        label.rectTransform.localScale = Vector3.one * Mathf.Lerp(current, scale, Blend);
    }
}
