using UnityEngine;

/// <summary>
/// Porrazo en la cámara al recibir daño: los robots pegan con la porra desde la izquierda, así que la cabeza se va
/// hacia la derecha (gira, se ladea y se desplaza un poco), rebota un pelín y vuelve a su sitio. Cuanto más daño,
/// más fuerte el golpe (hasta un tope). Cada golpe varía un poco al azar para que no parezcan todos iguales.
///
/// Va en la Main Camera del jugador. Mueve SOLO la cámara (no el CameraHolder que controla PlayerController), así que
/// no se pelea con el ratón. El arma, que cuelga de la cámara, se mueve con ella. Al morir deja de moverse y la
/// animación de muerte sigue desde donde te dejó el último golpe.
/// </summary>
public class PlayerHitReaction : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Vida del jugador. Vacío = la busca en los padres.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Golpe")]
    [Tooltip("De dónde viene el golpe. Izquierda = la cabeza se va hacia la derecha.")]
    [SerializeField] private bool hitFromLeft = true;
    [Tooltip("Grados que gira la cabeza hacia el lado contrario al golpe. 7 = se nota sin marear.")]
    [Range(0f, 30f)]
    [SerializeField] private float yawDegrees = 7f;
    [Tooltip("Grados que se ladea la cabeza (como si el golpe la tumbara). 5 = ladeo claro.")]
    [Range(0f, 30f)]
    [SerializeField] private float rollDegrees = 5f;
    [Tooltip("Grados que baja la mirada con el golpe. 1.5 = un pequeño cabeceo.")]
    [Range(0f, 15f)]
    [SerializeField] private float pitchDownDegrees = 1.5f;
    [Tooltip("Metros que se desplaza la cabeza hacia el lado contrario. 0.04 = 4 cm.")]
    [Min(0f)]
    [SerializeField] private float sideMeters = 0.04f;
    [Tooltip("Variación al azar de cada golpe. 0.25 = cada golpe entre un 75% y un 125% en cada eje.")]
    [Range(0f, 1f)]
    [SerializeField] private float randomness = 0.25f;

    [Header("Fuerza según el daño")]
    [Tooltip("Daño que da un golpe \"normal\" (fuerza 1). 20 = el golpe de porra de un robot.")]
    [Min(0.01f)]
    [SerializeField] private float referenceDamage = 20f;
    [Tooltip("Fuerza mínima y máxima del golpe. (0.6, 1.5) = un rasguño se nota algo y un golpe enorme no marea.")]
    [SerializeField] private Vector2 strengthRange = new Vector2(0.6f, 1.5f);

    [Header("Ritmo")]
    [Tooltip("Segundos que dura todo el golpe, de que te dan a que la cabeza vuelve a su sitio.")]
    [Min(0.05f)]
    [SerializeField] private float durationSeconds = 0.45f;
    [Tooltip("Forma del golpe a lo largo del tiempo (1 = golpe completo, negativo = rebote hacia el otro lado). Por defecto: golpe seco, pequeño rebote y vuelta.")]
    [SerializeField]
    private AnimationCurve kickCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 14f), new Keyframe(0.12f, 1f), new Keyframe(0.45f, -0.18f), new Keyframe(1f, 0f));

    [Header("Depuración")]
    [Tooltip("Muestra junto a la cámara la fuerza del último golpe.")]
    [SerializeField] private bool showDebug;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private float hitTime = -999f;
    private float strength;
    private Vector3 kickAxes; // x = pitch, y = yaw, z = roll del golpe actual (ya con su variación)
    private float kickSide;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(float amount)
    {
        hitTime = Time.time;
        strength = Mathf.Clamp(amount / referenceDamage, strengthRange.x, strengthRange.y);

        float side = hitFromLeft ? 1f : -1f; // +1 = la cabeza se va a la derecha
        kickAxes = new Vector3(
            pitchDownDegrees * Vary(),
            yawDegrees * side * Vary(),
            -rollDegrees * side * Vary()); // ladeo negativo en Z = la cabeza cae hacia la derecha
        kickSide = sideMeters * side * Vary();
    }

    private float Vary() => 1f + Random.Range(-randomness, randomness);

    // LateUpdate: después de que PlayerController mueva el CameraHolder en este frame.
    private void LateUpdate()
    {
        // Muerto: no se toca la cámara (la mueve PlayerDeathAnimation).
        if (playerHealth != null && playerHealth.IsDead) return;

        float t01 = (Time.time - hitTime) / durationSeconds;
        float kick = t01 >= 0f && t01 < 1f ? kickCurve.Evaluate(t01) * strength : 0f;

        transform.localRotation = baseLocalRotation * Quaternion.Euler(kickAxes * kick);
        transform.localPosition = baseLocalPosition + Vector3.right * (kickSide * kick);
    }

    [ContextMenu("TEST: Porrazo")]
    private void TestHit() => HandleDamaged(referenceDamage);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        float age = Time.time - hitTime;
        string text = age < durationSeconds ? $"Porrazo · fuerza {strength:0.00}" : "Porrazo · quieto";
        DebugLabels.At(transform.position + transform.forward, 1, text, DebugLabels.Player);
    }
#endif
}
