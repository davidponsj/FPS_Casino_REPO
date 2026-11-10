using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Inicio de partida: el jugador se ha quedado dormido de borracho, sentado y con la cabeza apoyada en la mesa.
/// 1) Pantalla negra (ojos cerrados).
/// 2) Abre los ojos a duras penas (parpadeos) y ve la mesa de lado, con la cabeza aún apoyada.
/// 3) Levanta la cabeza (sigue sentado, mirando la mesa) y se pone de pie tambaleándose.
/// 4) Al acabar: saca la pistola, aparece el HUD, se activan los controles y empiezan las rondas.
/// Si vienes de la puerta del menú, la pantalla empieza del color en que acabó aquel fundido (dorado) y pasa a negro
/// poco a poco; solo entonces empieza la animación (así el cambio de escena no se nota).
/// Los enemigos no empiezan a salir hasta unos segundos después de levantarte (Round Start Delay Seconds).
/// Mientras dura no hay controles, ni HUD, ni enemigos. Se puede saltar con cualquier tecla o clic.
///
/// Va en la Main Camera del jugador, como PlayerDeathAnimation. Mueve SOLO la cámara (no el CameraHolder que controla
/// PlayerController). La mesa va delante del jugador y la silla detrás; las posturas de la cámara (Keys) son respecto
/// a su sitio normal, en metros y grados: ajustándolas se encaja la cámara a la altura de la mesa que pongáis.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PlayerWakeUpIntro : MonoBehaviour
{
    [Serializable]
    public class IntroKey
    {
        [Tooltip("Segundo de la animación en el que la cámara está en esta postura.")]
        [Min(0f)] public float time;
        [Tooltip("Desplazamiento de la cámara respecto a su sitio, en metros (X derecha, Y arriba, Z adelante).")]
        public Vector3 position;
        [Tooltip("Giro de la cámara respecto a su sitio, en grados (X mirar abajo, Y girar, Z ladear la cabeza).")]
        public Vector3 rotation;

        public IntroKey(float time, Vector3 position, Vector3 rotation)
        {
            this.time = time;
            this.position = position;
            this.rotation = rotation;
        }
    }

    [Header("Referencias (vacías = las busca solo)")]
    [Tooltip("Controles del jugador. Vacío = los busca en los padres.")]
    [SerializeField] private PlayerInput playerInput;
    [Tooltip("Gestor de armas del jugador. Vacío = lo busca en los padres.")]
    [SerializeField] private WeaponManager weaponManager;
    [Tooltip("Gestor de rondas: no empieza hasta que acaba la animación. Vacío = lo busca en la escena.")]
    [SerializeField] private RoundManager roundManager;
    [Tooltip("Porrazo de cámara (PlayerHitReaction): se apaga mientras dura la animación. Vacío = lo busca en este objeto.")]
    [SerializeField] private PlayerHitReaction hitReaction;
    [Tooltip("Donde aparece el arma (el WeaponHolder de la cámara), para que suba desde abajo al sacarla. Vacío = el hijo llamado WeaponHolder.")]
    [SerializeField] private Transform weaponHolder;

    [Header("Al terminar")]
    [Tooltip("Arma que se pone en la mano al levantarse (la pistola). Vacío = sin arma.")]
    [SerializeField] private WeaponBase startingWeapon;
    [Tooltip("Objetos del HUD que se esconden durante la animación y aparecen con un fundido al acabar (el GameplayCanvas...).")]
    [SerializeField] private GameObject[] hudRoots = new GameObject[0];
    [Tooltip("Segundos del fundido del HUD al aparecer.")]
    [Min(0f)]
    [SerializeField] private float hudFadeSeconds = 0.5f;
    [Tooltip("Segundos que tarda el arma en subir desde abajo de la pantalla.")]
    [Min(0f)]
    [SerializeField] private float weaponRaiseSeconds = 0.4f;
    [Tooltip("Desde dónde sube el arma, respecto a su sitio. (0, -0.3, 0) = desde abajo.")]
    [SerializeField] private Vector3 weaponRaiseOffset = new Vector3(0f, -0.3f, 0f);
    [Tooltip("Segundos desde que te levantas hasta que empiezan las rondas (y salen enemigos). 3 = tiempo para situarte.")]
    [Min(0f)]
    [SerializeField] private float roundStartDelaySeconds = 3f;
    [Tooltip("Se lanza al acabar la animación (un sonido, un mensaje...).")]
    [SerializeField] private UnityEvent onFinished;

    [Header("Posturas de la cámara")]
    [Tooltip("Posturas en orden de tiempo; entre una y otra se pasa suavemente. Por defecto: cabeza apoyada de lado en la mesa, la levanta mirando la mesa y se pone de pie.")]
    [SerializeField]
    private IntroKey[] keys =
    {
        new IntroKey(0f,   new Vector3(0f, -0.50f, 0.47f), new Vector3(15f, 0f, 75f)),
        new IntroKey(2.6f, new Vector3(0f, -0.48f, 0.45f), new Vector3(12f, -5f, 70f)),
        new IntroKey(3.7f, new Vector3(0f, -0.30f, 0.25f), new Vector3(40f, 0f, 8f)),
        new IntroKey(5.0f, Vector3.zero,                    Vector3.zero)
    };
    [Tooltip("Párpados a lo largo de la animación (1 = cerrados, 0 = abiertos), en SEGUNDOS. Por defecto: negro, se abren a duras penas con parpadeos y se abren del todo al ponerse de pie.")]
    [SerializeField]
    private AnimationCurve eyesClosed = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(1.2f, 1f), new Keyframe(1.6f, 0.55f), new Keyframe(1.85f, 0.85f),
        new Keyframe(2.3f, 0.35f), new Keyframe(2.45f, 0.7f), new Keyframe(3f, 0.15f), new Keyframe(3.7f, 0.05f),
        new Keyframe(5f, 0f));
    [Tooltip("Tambaleo de borracho al levantarse, en grados. 3 = se nota el mareo.")]
    [Min(0f)]
    [SerializeField] private float swayDegrees = 3f;
    [Tooltip("Velocidad del tambaleo.")]
    [Min(0f)]
    [SerializeField] private float swaySpeed = 0.8f;

    [Header("Entrada desde el menú")]
    [Tooltip("Segundos en que el color con el que entraste por la puerta (dorado) pasa a negro. La animación de despertar empieza cuando acaba. 0 = negro desde el principio.")]
    [Min(0f)]
    [SerializeField] private float entryFadeSeconds = 2f;

    [Header("Saltar")]
    [Tooltip("Si se puede saltar la animación con cualquier tecla o clic.")]
    [SerializeField] private bool allowSkip = true;
    [Tooltip("Segundos antes de poder saltarla (para no saltarla sin querer al cargar).")]
    [Min(0f)]
    [SerializeField] private float skipAfterSeconds = 0.5f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuándo empieza y acaba la animación.")]
    [SerializeField] private bool showDebug;

    /// <summary>True mientras dura la animación de inicio.</summary>
    public static bool IsPlaying { get; private set; }

    private Camera cam;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private float elapsed;
    private bool finished;
    private RectTransform topLid, bottomLid;
    private Image topLidImage, bottomLidImage;
    private Color entryColor = Color.black;
    private float entrySeconds;   // lo que dura el fundido de entrada en esta partida (0 si no vienes del menú)
    private GameObject eyelidsRoot;
    private CanvasGroup[] hudGroups = new CanvasGroup[0];

    private float Duration => keys.Length > 0 ? keys[keys.Length - 1].time : 0f;

    private void Awake()
    {
        IsPlaying = true;
        cam = GetComponent<Camera>();
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;

        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        if (weaponManager == null) weaponManager = GetComponentInParent<WeaponManager>();
        if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();
        if (hitReaction == null) hitReaction = GetComponent<PlayerHitReaction>();
        if (weaponHolder == null) weaponHolder = transform.Find("WeaponHolder");

        // Sin enemigos ni porrazos hasta que acabe (un componente desactivado no hace su Start hasta que se active).
        if (roundManager != null) roundManager.enabled = false;
        if (hitReaction != null) hitReaction.enabled = false;

        HideHud();
        CreateEyelids();
        SetEyelids(1f);

        // Vienes de la puerta del menú: empieza del color del fundido para que no haya corte.
        if (PlayGameTarget.CameFromMenu)
        {
            entryColor = PlayGameTarget.EnterFadeColor;
            entrySeconds = entryFadeSeconds;
        }
        PlayGameTarget.ClearEntry();
        SetLidColor(entryColor);
    }

    private void Start()
    {
        // En Start y no en Awake: el PlayerInput activa sus controles en OnEnable, que va antes.
        if (playerInput != null) playerInput.DeactivateInput();
        if (showDebug) Debug.Log($"{name}: empieza la animación de despertar ({Duration:0.0} s).", this);
    }

    private void OnDestroy()
    {
        if (!finished) IsPlaying = false;
    }

    // LateUpdate: después de que PlayerController coloque el CameraHolder en este frame.
    private void LateUpdate()
    {
        if (finished) return;

        elapsed += Time.deltaTime;

        // Primero el fundido de entrada (dorado → negro, suave al principio y al final); después, la animación.
        float t = Mathf.Max(0f, elapsed - entrySeconds);
        if (t >= Duration || WantsToSkip())
        {
            Finish();
            return;
        }

        float fade01 = entrySeconds > 0f ? Mathf.SmoothStep(0f, 1f, elapsed / entrySeconds) : 1f;
        SetLidColor(Color.Lerp(entryColor, Color.black, fade01));
        ApplyPose(t);
        SetEyelids(eyesClosed.Evaluate(t));
    }

    private bool WantsToSkip()
    {
        if (!allowSkip || elapsed < skipAfterSeconds) return false;
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return key || click;
    }

    /// <summary>Postura de la cámara en el segundo t: entre las dos posturas más cercanas, con paso suave.</summary>
    private void ApplyPose(float t)
    {
        if (keys.Length == 0) return;

        IntroKey from = keys[0], to = keys[0];
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i].time <= t) from = keys[i];
            if (keys[i].time >= t) { to = keys[i]; break; }
        }

        float span = to.time - from.time;
        float k = span > 0f ? Mathf.SmoothStep(0f, 1f, (t - from.time) / span) : 1f;
        Vector3 position = Vector3.Lerp(from.position, to.position, k);
        Vector3 rotation = Vector3.Lerp(from.rotation, to.rotation, k);

        // Tambaleo de borracho: crece al despertar y se calma al ponerse de pie.
        float awake01 = Mathf.Clamp01(1f - eyesClosed.Evaluate(t));
        float settle = 1f - Mathf.Clamp01(t / Duration);
        float sway = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayDegrees * awake01 * (0.4f + settle);
        rotation += new Vector3(sway * 0.3f, sway * 0.5f, sway);

        transform.localPosition = baseLocalPosition + position;
        transform.localRotation = baseLocalRotation * Quaternion.Euler(rotation);
    }

    private void Finish()
    {
        finished = true;
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
        if (eyelidsRoot != null) Destroy(eyelidsRoot);

        if (startingWeapon != null && weaponManager != null)
        {
            weaponManager.EquipWeapon(startingWeapon);
            if (weaponHolder != null && weaponRaiseSeconds > 0f) StartCoroutine(RaiseWeapon());
        }

        StartCoroutine(FadeInHud());
        if (playerInput != null) playerInput.ActivateInput();
        if (hitReaction != null) hitReaction.enabled = true;
        if (roundManager != null) StartCoroutine(StartRoundsAfterDelay());

        IsPlaying = false;
        if (showDebug) Debug.Log($"{name}: fin de la animación de despertar.", this);
        onFinished?.Invoke();
    }

    /// <summary>Unos segundos de calma tras levantarse; luego empiezan las rondas.</summary>
    private IEnumerator StartRoundsAfterDelay()
    {
        if (roundStartDelaySeconds > 0f) yield return new WaitForSeconds(roundStartDelaySeconds);
        roundManager.enabled = true;
        if (showDebug) Debug.Log($"{name}: empiezan las rondas.", this);
    }

    /// <summary>El arma sube desde abajo de la pantalla hasta su sitio, como al sacarla.</summary>
    private IEnumerator RaiseWeapon()
    {
        Vector3 rest = weaponHolder.localPosition;
        for (float t = 0f; t < weaponRaiseSeconds; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / weaponRaiseSeconds, 3f); // sale rápido y frena al llegar
            weaponHolder.localPosition = rest + weaponRaiseOffset * (1f - k);
            yield return null;
        }
        weaponHolder.localPosition = rest;
    }

    // ---------------- HUD ----------------

    private void HideHud()
    {
        hudGroups = new CanvasGroup[hudRoots.Length];
        for (int i = 0; i < hudRoots.Length; i++)
        {
            if (hudRoots[i] == null) continue;
            CanvasGroup group = hudRoots[i].GetComponent<CanvasGroup>();
            if (group == null) group = hudRoots[i].AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            hudGroups[i] = group;
        }
    }

    private IEnumerator FadeInHud()
    {
        for (float t = 0f; t < hudFadeSeconds; t += Time.deltaTime)
        {
            SetHudAlpha(t / hudFadeSeconds);
            yield return null;
        }
        SetHudAlpha(1f);
        foreach (CanvasGroup group in hudGroups)
            if (group != null) group.blocksRaycasts = true;
    }

    private void SetHudAlpha(float alpha)
    {
        foreach (CanvasGroup group in hudGroups)
            if (group != null) group.alpha = alpha;
    }

    // ---------------- PÁRPADOS ----------------

    /// <summary>Dos franjas negras pegadas a la cámara (arriba y abajo), como en PlayerDeathAnimation.</summary>
    private void CreateEyelids()
    {
        eyelidsRoot = new GameObject("WakeUpEyelids", typeof(RectTransform), typeof(Canvas));
        eyelidsRoot.layer = gameObject.layer;
        eyelidsRoot.transform.SetParent(transform, false);

        Canvas canvas = eyelidsRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.02f;
        canvas.sortingOrder = 100;

        topLid = CreateLid(eyelidsRoot.transform, "TopLid");
        bottomLid = CreateLid(eyelidsRoot.transform, "BottomLid");
        topLidImage = topLid.GetComponent<Image>();
        bottomLidImage = bottomLid.GetComponent<Image>();
    }

    private RectTransform CreateLid(Transform parent, string lidName)
    {
        var lid = new GameObject(lidName, typeof(RectTransform), typeof(Image));
        lid.layer = gameObject.layer;
        var rect = (RectTransform)lid.transform;
        rect.SetParent(parent, false);

        Image image = lid.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rect;
    }

    private void SetLidColor(Color color)
    {
        if (topLidImage != null) topLidImage.color = color;
        if (bottomLidImage != null) bottomLidImage.color = color;
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
        if (!showDebug || !Application.isPlaying || finished) return;
        DebugLabels.At(transform.position + transform.forward, 0, $"Despertar · {elapsed:0.0}/{Duration:0.0} s", DebugLabels.Player);
    }
#endif
}
