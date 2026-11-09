using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Objeto "Jugar" del menú: la puerta del casino. Va en un objeto 3D con HoverOutline, y MenuHoverManager llama a Play
/// al hacer clic. Entonces:
/// 1) Lanza On Play Start (sonido de puerta, música...) y abre las hojas de la puerta (si las hay).
/// 2) La cámara entra por la puerta hasta Enter Point, cada vez más rápido, como si caminaras hacia dentro.
/// 3) La pantalla se funde a negro y carga la escena de juego.
/// Mientras tanto el menú no responde a nada (IsStarting). Al morir, el botón Menú de la pantalla de muerte vuelve
/// aquí: así se cierra el bucle menú → partida → menú.
/// </summary>
public class PlayGameTarget : MonoBehaviour
{
    [Serializable]
    public class DoorLeaf
    {
        [Tooltip("Hoja de la puerta (para que gire, su pivote tiene que estar en la bisagra).")]
        public Transform leaf;
        [Tooltip("Giro al abrirse, en grados (en sus ejes locales). (0, 0, 0) = no gira (puerta corredera).")]
        public Vector3 openEuler = Vector3.zero;
        [Tooltip("Desplazamiento al abrirse, en los ejes de su padre (puerta corredera: se aparta hacia un lado). (0, 0, 0) = no se desplaza.")]
        public Vector3 openOffset = Vector3.zero;

        [NonSerialized] public Quaternion closedRotation;
        [NonSerialized] public Vector3 closedPosition;
    }

    [Header("Escena")]
    [Tooltip("Escena de juego que se carga (tiene que estar en File > Build Profiles / Build Settings).")]
    [SerializeField] private string gameplaySceneName = "SCN_Gameplay";

    [Header("Puerta (opcional)")]
    [Tooltip("Hojas de la puerta que se abren al hacer clic. Vacío = la puerta no se mueve (solo entra la cámara).")]
    [SerializeField] private DoorLeaf[] doors = new DoorLeaf[0];
    [Tooltip("Segundos que tardan en abrirse las puertas.")]
    [Min(0.01f)]
    [SerializeField] private float doorOpenSeconds = 0.8f;

    [Header("Cámara")]
    [Tooltip("Cámara del menú. Vacío = Camera.main.")]
    [SerializeField] private Camera menuCamera;
    [Tooltip("Punto (vacío) hasta el que avanza la cámara: delante de la puerta, a media altura. Vacío = la cámara no se mueve.")]
    [SerializeField] private Transform enterPoint;
    [Tooltip("Desmarcado (recomendado) = la cámara va derecha a la puerta mirando hacia donde avanza, sin importar cómo esté girado Enter Point. Marcado = acaba con el giro de Enter Point.")]
    [SerializeField] private bool useEnterPointRotation = false;
    [Tooltip("Segundos desde el clic hasta que la cámara empieza a avanzar. 0.3 = primero se ve abrirse la puerta.")]
    [Min(0f)]
    [SerializeField] private float enterDelaySeconds = 0.3f;
    [Tooltip("Segundos que tarda la cámara en llegar a Enter Point.")]
    [Min(0.01f)]
    [SerializeField] private float enterSeconds = 1.6f;

    [Header("Fundido")]
    [Tooltip("Segundos del fundido final antes de cargar la partida. Empieza mientras la cámara aún está entrando.")]
    [Min(0f)]
    [SerializeField] private float fadeSeconds = 0.6f;
    [Tooltip("Color del fundido.")]
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Eventos")]
    [Tooltip("Se lanza al hacer clic, antes de todo (sonido de la puerta, bajar la música...).")]
    [SerializeField] private UnityEvent onPlayStart;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cada paso de la entrada al casino.")]
    [SerializeField] private bool showDebug;

    /// <summary>True desde el clic hasta que carga la partida: el menú deja de responder.</summary>
    public static bool IsStarting { get; private set; }

    /// <summary>
    /// True si la partida se acaba de cargar entrando por la puerta (y no dándole a Play directamente en SCN_Gameplay).
    /// La animación de despertar lo usa para empezar del mismo color en que acabó el fundido, sin corte.
    /// </summary>
    public static bool CameFromMenu { get; private set; }

    /// <summary>Color en que acabó el fundido al entrar por la puerta.</summary>
    public static Color EnterFadeColor { get; private set; } = Color.black;

    /// <summary>Lo llama quien recoge la entrada (PlayerWakeUpIntro) para que no se use dos veces.</summary>
    public static void ClearEntry() => CameFromMenu = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsStarting = false;
        CameFromMenu = false;
    }

    private void Awake()
    {
        // Al volver al menú desde la partida (botón Menú de la pantalla de muerte) el menú tiene que responder otra vez.
        IsStarting = false;
        if (menuCamera == null) menuCamera = Camera.main;

        foreach (DoorLeaf door in doors)
            if (door != null && door.leaf != null)
            {
                door.closedRotation = door.leaf.localRotation;
                door.closedPosition = door.leaf.localPosition;
            }

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            Debug.LogWarning($"{name}: la escena \"{gameplaySceneName}\" no está en Build Settings. Al hacer clic en la puerta no cargará.", this);
    }

    public void Play()
    {
        if (IsStarting) return;
        IsStarting = true;
        if (showDebug) Debug.Log($"{name}: entrando al casino → {gameplaySceneName}.", this);

        onPlayStart?.Invoke();
        StartCoroutine(EnterRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        StartCoroutine(OpenDoors());

        // Sin punto de entrada: solo se ve abrirse la puerta.
        float moveSeconds = enterPoint != null && menuCamera != null ? enterSeconds : doorOpenSeconds;
        float total = enterDelaySeconds + moveSeconds;
        float fadeStart = Mathf.Max(0f, total - fadeSeconds);

        Image fade = CreateFade();
        Transform cam = menuCamera != null ? menuCamera.transform : null;
        Vector3 fromPosition = cam != null ? cam.position : Vector3.zero;
        Quaternion fromRotation = cam != null ? cam.rotation : Quaternion.identity;
        Quaternion toRotation = fromRotation;
        if (enterPoint != null && cam != null)
        {
            Vector3 direction = enterPoint.position - fromPosition;
            if (useEnterPointRotation) toRotation = enterPoint.rotation;
            else if (direction.sqrMagnitude > 0.0001f) toRotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            // La cámara va en línea recta a la puerta, acelerando (como quien echa a andar). Al principio se encara
            // con la puerta (si no la tenía justo delante) y luego avanza recta.
            if (enterPoint != null && cam != null && t >= enterDelaySeconds)
            {
                float k = Mathf.Clamp01((t - enterDelaySeconds) / enterSeconds);
                float move = k * k * (3f - 2f * k) * 0.5f + k * k * 0.5f; // arranca suave y acaba con prisa
                cam.position = Vector3.Lerp(fromPosition, enterPoint.position, move);
                cam.rotation = Quaternion.Slerp(fromRotation, toRotation, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.5f)));
            }

            if (fade != null && t >= fadeStart)
                fade.color = WithAlpha(fadeColor, fadeSeconds > 0f ? Mathf.Clamp01((t - fadeStart) / fadeSeconds) : 1f);

            yield return null;
        }

        if (fade != null) fade.color = WithAlpha(fadeColor, 1f);
        if (showDebug) Debug.Log($"{name}: cargando {gameplaySceneName}.", this);

        // La partida empieza de este mismo color y lo funde a negro (PlayerWakeUpIntro): así no se nota el cambio de escena.
        CameFromMenu = true;
        EnterFadeColor = WithAlpha(fadeColor, 1f);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator OpenDoors()
    {
        if (doors.Length == 0) yield break;

        for (float t = 0f; t < doorOpenSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / doorOpenSeconds);
            foreach (DoorLeaf door in doors)
                SetDoor(door, k);
            yield return null;
        }

        foreach (DoorLeaf door in doors)
            SetDoor(door, 1f);
    }

    /// <summary>open01: 0 = cerrada, 1 = abierta del todo (gira y/o se desplaza).</summary>
    private static void SetDoor(DoorLeaf door, float open01)
    {
        if (door == null || door.leaf == null) return;
        door.leaf.localRotation = door.closedRotation * Quaternion.Euler(door.openEuler * open01);
        door.leaf.localPosition = door.closedPosition + door.openOffset * open01;
    }

    /// <summary>Pantalla negra (transparente al principio) por encima de todo, creada al hacer clic.</summary>
    private Image CreateFade()
    {
        var canvasObject = new GameObject("PlayFade", typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)imageObject.transform;
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = WithAlpha(fadeColor, 0f);
        image.raycastTarget = false;
        return image;
    }

    private static Color WithAlpha(Color c, float alpha)
    {
        c.a = alpha;
        return c;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || enterPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, enterPoint.position);
        Gizmos.DrawWireSphere(enterPoint.position, 5f);
        DebugLabels.At(enterPoint.position, 0, IsStarting ? "Entrada · entrando" : "Entrada · Enter Point", DebugLabels.Casino);
    }
#endif
}
