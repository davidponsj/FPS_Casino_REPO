using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animación de la cámara al morir, en primera persona:
/// - Te matan (robots, granadas...): miras hacia arriba, la cabeza se ladea, caes al suelo con un pequeño rebote y se te
///   cierran los ojos, así el cartel sale sobre negro.
/// - Blackout (beber demasiado): se te cierran los ojos de borracho (párpados que se cierran, se abren un poco y se
///   cierran del todo), la cabeza se tambalea y te vas cayendo de lado poco a poco.
/// Va en tiempo real, así que se ve igual aunque la pantalla de muerte frene el juego. DeathScreen espera a que acabe
/// (Duration) antes de enseñar el cartel.
///
/// Va en la Main Camera del jugador. Mueve SOLO la cámara (no el CameraHolder que controla PlayerController), así que
/// no se pelea con el control del jugador. Los párpados se crean solos al empezar: un Canvas en la cámara de juego, para
/// que el cartel de muerte (que va en su propia cámara) siga viéndose por encima.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PlayerDeathAnimation : MonoBehaviour
{
    [Header("Referencias (vacías = las busca en el jugador)")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerBlackout blackout;
    [Tooltip("Cosas que se ocultan al morir: el WeaponHolder (si no, el arma gira con la cámara al caer)... Vacío = nada.")]
    [SerializeField] private GameObject[] hideOnDeath;

    [Header("Caída (te matan)")]
    [Tooltip("Segundos que dura la caída, con los ojos cerrándose al final. 1.6 = se ve caer con calma.")]
    [Min(0.1f)]
    [SerializeField] private float fallSeconds = 1.6f;
    [Tooltip("Metros que baja la cámara (de la altura de los ojos al suelo). 1.4 = tumbado.")]
    [Min(0f)]
    [SerializeField] private float fallDropMeters = 1.4f;
    [Tooltip("Hacia dónde acabas mirando: grados por encima del horizonte. 70 = casi al techo.")]
    [Range(0f, 90f)]
    [SerializeField] private float fallLookUpDegrees = 70f;
    [Tooltip("Grados que se ladea la cabeza al caer. 25 = cae de lado.")]
    [Range(-90f, 90f)]
    [SerializeField] private float fallRollDegrees = 25f;
    [Tooltip("Rebote al tocar el suelo, en metros. 0.08 = un golpe seco. 0 = sin rebote.")]
    [Min(0f)]
    [SerializeField] private float fallBounceMeters = 0.08f;
    [Tooltip("Momento de la caída en que tocas el suelo (0-1). 0.6 = toca suelo al 60% y el resto es cerrar los ojos.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float fallGroundAt = 0.6f;
    [Tooltip("Cierre de los párpados durante la caída (0 = abiertos, 1 = cerrados). Por defecto: abiertos al caer y se cierran al quedar en el suelo, con un último parpadeo.")]
    [SerializeField]
    private AnimationCurve fallEyelidCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.6f, 0f), new Keyframe(0.75f, 0.7f),
        new Keyframe(0.83f, 0.45f), new Keyframe(1f, 1f));

    [Header("Ojos cerrándose (blackout)")]
    [Tooltip("Segundos que tarda en cerrar los ojos del todo.")]
    [Min(0.1f)]
    [SerializeField] private float drunkSeconds = 2.2f;
    [Tooltip("Cierre de los párpados a lo largo del tiempo (0 = abiertos, 1 = cerrados). Por defecto: se cierran, se abren un poco, otra vez, y se cierran del todo.")]
    [SerializeField]
    private AnimationCurve eyelidCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.2f, 0.65f), new Keyframe(0.35f, 0.25f),
        new Keyframe(0.6f, 0.85f), new Keyframe(0.72f, 0.5f), new Keyframe(1f, 1f));
    [Tooltip("Color de los párpados.")]
    [SerializeField] private Color eyelidColor = Color.black;
    [Tooltip("Metros que baja la cámara al irse cayendo. 0.8 = te vas al suelo poco a poco.")]
    [Min(0f)]
    [SerializeField] private float drunkDropMeters = 0.8f;
    [Tooltip("Grados que se ladea la cabeza al final. 35 = te caes de lado.")]
    [Range(-90f, 90f)]
    [SerializeField] private float drunkRollDegrees = 35f;
    [Tooltip("Grados del tambaleo (la cabeza va de un lado a otro). 6 = mareo.")]
    [Min(0f)]
    [SerializeField] private float drunkSwayDegrees = 6f;
    [Tooltip("Velocidad del tambaleo. 1.5 = balanceo lento de borracho.")]
    [Min(0f)]
    [SerializeField] private float drunkSwaySpeed = 1.5f;

    [Header("Depuración")]
    [Tooltip("Muestra junto a la cámara qué animación de muerte se está viendo.")]
    [SerializeField] private bool showDebug;

    private enum DeathStyle { None, Fall, Drunk }

    private Camera cam;
    private DeathStyle style = DeathStyle.None;
    private bool blackoutHappened;
    private float startTime;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private Vector3 localDown;        // "abajo" del mundo en los ejes del CameraHolder (que puede estar inclinado)
    private float extraPitchToLookUp;  // giro extra para acabar mirando hacia arriba, sea cual sea el pitch del jugador
    private RectTransform topLid, bottomLid;

    /// <summary>Segundos que dura la animación de esta muerte. 0 si aún no has muerto.</summary>
    public float Duration => style == DeathStyle.Drunk ? drunkSeconds : style == DeathStyle.Fall ? fallSeconds : 0f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (blackout == null) blackout = GetComponentInParent<PlayerBlackout>();
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDied += HandleDied;
        if (blackout != null) blackout.OnBlackout += HandleBlackout;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandleDied;
        if (blackout != null) blackout.OnBlackout -= HandleBlackout;
    }

    // PlayerBlackout avisa ANTES de matarte: así sabemos que esta muerte es de borracho.
    private void HandleBlackout() => blackoutHappened = true;

    private void HandleDied()
    {
        if (style != DeathStyle.None) return;
        style = blackoutHappened ? DeathStyle.Drunk : DeathStyle.Fall;
        startTime = Time.unscaledTime;

        if (hideOnDeath != null)
            foreach (GameObject hidden in hideOnDeath)
                if (hidden != null) hidden.SetActive(false);

        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;

        Transform holder = transform.parent;
        localDown = holder != null ? holder.InverseTransformDirection(Vector3.down) : Vector3.down;
        float holderPitch = holder != null ? Mathf.DeltaAngle(0f, holder.localEulerAngles.x) : 0f;
        extraPitchToLookUp = -fallLookUpDegrees - holderPitch; // pitch negativo = mirar hacia arriba

        CreateEyelids(); // en las dos muertes acabas con los ojos cerrados
    }

    // LateUpdate: después de que PlayerController coloque el CameraHolder en este frame.
    private void LateUpdate()
    {
        if (style == DeathStyle.None) return;

        float t01 = Mathf.Clamp01((Time.unscaledTime - startTime) / Duration);
        if (style == DeathStyle.Fall) AnimateFall(t01);
        else AnimateDrunk(t01);
    }

    /// <summary>
    /// Cae acelerando (gravedad) hasta tocar el suelo en Fall Ground At, rebota un poco y la cabeza acaba mirando arriba
    /// y ladeada. Los párpados siguen su curva: se cierran ya en el suelo.
    /// </summary>
    private void AnimateFall(float t01)
    {
        float ground01 = Mathf.Clamp01(t01 / fallGroundAt);   // 0-1 hasta tocar el suelo
        float drop = fallDropMeters * ground01 * ground01;     // acelera como al caer
        float afterGround = fallGroundAt < 1f ? Mathf.Clamp01((t01 - fallGroundAt) / ((1f - fallGroundAt) * 0.5f)) : 0f;
        float bounce = afterGround > 0f && afterGround < 1f ? Mathf.Sin(afterGround * Mathf.PI) * fallBounceMeters : 0f;

        float turn = 1f - (1f - ground01) * (1f - ground01);   // la cabeza gira rápido al principio
        transform.localPosition = startLocalPosition + localDown * (drop - bounce);
        transform.localRotation = startLocalRotation * Quaternion.Euler(extraPitchToLookUp * turn, 0f, fallRollDegrees * turn);

        SetEyelids(fallEyelidCurve.Evaluate(t01));
    }

    /// <summary>Párpados según la curva, la cabeza se tambalea y se va cayendo de lado poco a poco.</summary>
    private void AnimateDrunk(float t01)
    {
        SetEyelids(eyelidCurve.Evaluate(t01));

        float sink = Mathf.SmoothStep(0f, 1f, t01);
        float sway = Mathf.Sin((Time.unscaledTime - startTime) * drunkSwaySpeed * Mathf.PI * 2f) * drunkSwayDegrees * (1f - t01 * 0.5f);
        transform.localPosition = startLocalPosition + localDown * (drunkDropMeters * sink);
        transform.localRotation = startLocalRotation * Quaternion.Euler(sway * 0.4f, sway, drunkRollDegrees * sink + sway * 0.5f);
    }

    // ---------------- PÁRPADOS ----------------

    /// <summary>
    /// Crea un Canvas pegado a esta cámara con dos franjas negras (arriba y abajo). Va en "Screen Space - Camera" y no en
    /// Overlay: así lo pinta la cámara de juego y el cartel de muerte (en su cámara Overlay) sigue saliendo encima.
    /// </summary>
    private void CreateEyelids()
    {
        var canvasObject = new GameObject("DrunkEyelids", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.02f;
        canvas.sortingOrder = 100;

        topLid = CreateLid(canvasObject.transform, "TopLid");
        bottomLid = CreateLid(canvasObject.transform, "BottomLid");
        SetEyelids(0f);
    }

    private RectTransform CreateLid(Transform parent, string lidName)
    {
        var lid = new GameObject(lidName, typeof(RectTransform), typeof(Image));
        lid.layer = gameObject.layer;
        var rect = (RectTransform)lid.transform;
        rect.SetParent(parent, false);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Image image = lid.GetComponent<Image>();
        image.color = eyelidColor;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>closed01: 0 = ojos abiertos, 1 = cerrados (las dos franjas se juntan en el centro y se solapan un poco).</summary>
    private void SetEyelids(float closed01)
    {
        if (topLid == null || bottomLid == null) return;
        float half = Mathf.Clamp01(closed01) * 0.52f;
        topLid.anchorMin = new Vector2(0f, 1f - half);
        topLid.anchorMax = Vector2.one;
        bottomLid.anchorMin = Vector2.zero;
        bottomLid.anchorMax = new Vector2(1f, half);
        topLid.offsetMin = topLid.offsetMax = bottomLid.offsetMin = bottomLid.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || style == DeathStyle.None) return;
        DebugLabels.At(transform.position + transform.forward, 0, $"Muerte · {style}", DebugLabels.Player);
    }
#endif
}
