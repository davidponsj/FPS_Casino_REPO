using UnityEngine;

/// <summary>
/// Lámpara vieja que está a punto de fundirse: tiembla un poco todo el rato y, de vez en cuando, pega una ráfaga de
/// bajones (se apaga y se enciende varias veces muy rápido) y vuelve a su brillo. Para el foco del cartel de muerte o
/// cualquier luz de un casino abandonado.
///
/// Va en el objeto de la luz (Light). Usa la intensidad que tenga la luz al empezar como su brillo normal.
/// Va en tiempo real: sigue fallando aunque el juego esté parado (Time.timeScale = 0).
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickeringLamp : MonoBehaviour
{
    [Header("Fallos")]
    [Tooltip("Segundos mínimo y máximo de calma entre ráfagas de bajones.")]
    [SerializeField] private Vector2 calmSeconds = new Vector2(2f, 6f);
    [Tooltip("Segundos mínimo y máximo que dura una ráfaga.")]
    [SerializeField] private Vector2 burstSeconds = new Vector2(0.2f, 0.6f);
    [Tooltip("Brillo en los bajones, respecto al normal. 0.2 = casi apagada.")]
    [Range(0f, 1f)]
    [SerializeField] private float dimTo = 0.2f;
    [Tooltip("Temblor continuo del brillo (corriente inestable). 0.05 = apenas se nota. 0 = fija.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float hum = 0.05f;
    [Tooltip("Velocidad del temblor continuo.")]
    [Min(0f)]
    [SerializeField] private float humSpeed = 8f;

    [Header("Depuración")]
    [Tooltip("Muestra junto a la luz su brillo actual y si está en plena ráfaga.")]
    [SerializeField] private bool showDebug;

    private Light lamp;
    private float baseIntensity;
    private float nextBurst, burstEnd, nextToggle;
    private bool dim;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
    }

    private void OnEnable()
    {
        nextBurst = Time.unscaledTime + Random.Range(0.3f, calmSeconds.y); // la primera, pronto
        dim = false;
    }

    private void OnDisable()
    {
        if (lamp != null) lamp.intensity = baseIntensity;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextBurst)
        {
            burstEnd = now + Random.Range(burstSeconds.x, burstSeconds.y);
            nextBurst = burstEnd + Random.Range(calmSeconds.x, calmSeconds.y);
            nextToggle = now;
        }

        float factor;
        if (now < burstEnd)
        {
            if (now >= nextToggle)
            {
                dim = !dim;
                nextToggle = now + Random.Range(0.03f, 0.1f);
            }
            factor = dim ? dimTo : 1f;
        }
        else
        {
            dim = false;
            factor = 1f - hum * Mathf.PerlinNoise(now * humSpeed, 0f);
        }
        lamp.intensity = baseIntensity * factor;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || lamp == null) return;
        string state = Time.unscaledTime < burstEnd ? "fallando" : "estable";
        DebugLabels.At(transform.position, 0, $"Lámpara · {lamp.intensity:0.00} · {state}", DebugLabels.Casino);
    }
#endif
}
