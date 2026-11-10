using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón del menú de pausa con efecto de "despegarse": con el ratón encima sube un poco y crece, pero su sombra se
/// queda en el sitio (se alarga), como si flotara sobre la ventana. Al pulsarlo baja hasta pegarse a la sombra.
/// Va en el objeto del botón, que tiene que tener un componente Shadow. Funciona en tiempo real (con el juego parado).
/// </summary>
public class PauseButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Tamaño con el ratón encima. 1.06 = un 6 % más grande.")]
    [Min(1f)]
    [SerializeField] private float hoverScale = 1.06f;
    [Tooltip("Píxeles que sube con el ratón encima. La sombra se alarga lo mismo y se queda en su sitio.")]
    [Min(0f)]
    [SerializeField] private float liftPixels = 10f;
    [Tooltip("Tamaño mientras se pulsa. 0.97 = se aplasta un poco.")]
    [Range(0.8f, 1f)]
    [SerializeField] private float pressedScale = 0.97f;
    [Tooltip("Lo rápido que se mueve. 15 = rápido y suave.")]
    [Min(1f)]
    [SerializeField] private float speed = 15f;

    private RectTransform rect;
    private Shadow shadow;
    private Vector3 baseScale;
    private Vector2 baseShadow;
    private float lift;     // píxeles que está subido ahora mismo
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseScale = rect.localScale;

        // El Outline también es un Shadow por dentro: se busca el que NO es Outline
        foreach (Shadow s in GetComponents<Shadow>())
            if (!(s is Outline)) shadow = s;
        if (shadow != null) baseShadow = shadow.effectDistance;
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        SetLift(0f);
        rect.localScale = baseScale;
    }

    private void Update()
    {
        float targetScale = pressed ? pressedScale : hovered ? hoverScale : 1f;
        float targetLift = pressed ? 0f : hovered ? liftPixels : 0f;

        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        rect.localScale = Vector3.Lerp(rect.localScale, baseScale * targetScale, k);
        SetLift(Mathf.Lerp(lift, targetLift, k));
    }

    private void SetLift(float value)
    {
        // Sube el botón y alarga la sombra lo mismo: la sombra se queda quieta en su sitio
        rect.anchoredPosition += Vector2.up * (value - lift);
        lift = value;
        if (shadow != null) shadow.effectDistance = baseShadow + Vector2.down * lift;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
}