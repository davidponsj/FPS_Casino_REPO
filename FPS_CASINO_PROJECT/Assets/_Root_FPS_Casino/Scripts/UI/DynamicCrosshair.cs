using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ret?cula de ne?n estilo Counter-Strike: 4 l?neas (arriba/abajo/izq/dcha) que se separan del centro seg?n la
/// dispersi?n actual del arma equipada, m?s un rombo en el centro. Cada l?nea es un n?cleo fino y claro con un
/// halo de color detr?s (casino futurista). Adem?s:
/// - Con cada disparo las l?neas dan un golpe hacia fuera y vuelven (el "kick" que se siente en la mano).
/// - Si apuntas a un enemigo vivo, la ret?cula cambia de color (magenta): sabes que vas a dar antes de disparar.
/// Sin arma equipada, se oculta por completo.
///
/// No aplica ning?n suavizado propio a la dispersi?n: el arma (WeaponBase) ya la suaviza internamente,
/// as? que la ret?cula simplemente refleja ese valor tal cual, 1:1, sin a?adir m?s retraso.
///
/// Con matchRealSpread activado, la separaci?n de las l?neas se calcula a partir de los grados
/// reales de dispersi?n y el FOV de la c?mara: las l?neas marcan EXACTAMENTE el borde del cono
/// por el que pueden salir las balas (aunque cambie el FOV al esprintar o deslizarte).
///
/// Jerarqu?a esperada en el Canvas:
/// Crosshair (RectTransform vac?o, anclado al centro de la pantalla)
///   - Top (Image)
///   - Bottom (Image)
///   - Left (Image)
///   - Right (Image)
/// Cada l?nea debe tener su pivote apuntando hacia el centro (Top con pivot Y=0, Bottom pivot Y=1, etc.)
/// para que al mover el anchoredPosition, la l?nea "crezca hacia afuera" en vez de moverse entera.
/// El tama?o y el color de las l?neas los pone este script (los del Inspector de cada Image se ignoran):
/// la Image de cada l?nea hace de halo y el script le crea dentro un hijo "Core" con el n?cleo. El rombo central tambi?n lo crea ?l.
///
/// El arma la saca sola del WeaponManager del jugador (evento OnWeaponChanged): no hay que asignarla.
/// </summary>
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("L?nea de arriba (pivote Y = 0, para que crezca hacia fuera).")]
    [SerializeField] private RectTransform top;
    [Tooltip("L?nea de abajo (pivote Y = 1).")]
    [SerializeField] private RectTransform bottom;
    [Tooltip("L?nea de la izquierda (pivote X = 1).")]
    [SerializeField] private RectTransform left;
    [Tooltip("L?nea de la derecha (pivote X = 0).")]
    [SerializeField] private RectTransform right;

    [Header("Distancias (p?xeles desde el centro)")]
    [Tooltip("P?xeles de separaci?n con dispersi?n 0 (quieto). As? las 4 l?neas no se tocan en el centro. 6 = deja ver el rombo.")]
    [Min(0f)]
    [SerializeField] private float minGap = 6f;
    [Tooltip("P?xeles de separaci?n con la dispersi?n al m?ximo. Solo se usa si Match Real Spread est? desactivado. 32 = se abre mucho.")]
    [Min(0f)]
    [SerializeField] private float maxGap = 32f;

    [Header("Precisi?n de la ret?cula")]
    [Tooltip("Activado: las l?neas marcan el cono real de las balas en pantalla. Desactivado: se abren de minGap a maxGap sin tener en cuenta el FOV.")]
    [SerializeField] private bool matchRealSpread = true;
    [Tooltip("Canvas en el que est? la ret?cula. Si lo dejas vac?o, lo busca en los padres.")]
    [SerializeField] private Canvas canvas;

    [Header("Estilo de las l?neas (en unidades del Canvas: 1920x1080 de referencia)")]
    [Tooltip("Largo de cada l?nea. 12 = fina y precisa.")]
    [Min(1f)]
    [SerializeField] private float lineLength = 12f;
    [Tooltip("Grosor del n?cleo de cada l?nea. 2 = n?tida.")]
    [Min(1f)]
    [SerializeField] private float coreThickness = 2f;
    [Tooltip("Lo que sobresale el halo alrededor del n?cleo, por cada lado. 2 = brillo suave.")]
    [Min(0f)]
    [SerializeField] private float glowSize = 2f;
    [Tooltip("Color del n?cleo.")]
    [SerializeField] private Color coreColor = Color.white;
    [Tooltip("Color del halo. La transparencia (A) es la fuerza del brillo: 0.4 = suave.")]
    [SerializeField] private Color glowColor = new Color(0.2f, 0.9f, 1f, 0.4f);

    [Header("Rombo central")]
    [Tooltip("Muestra un rombo peque?o en el centro exacto.")]
    [SerializeField] private bool showCenterDot = true;
    [Tooltip("Lado del rombo. 3 = un punto preciso.")]
    [Min(1f)]
    [SerializeField] private float centerDotSize = 3f;

    [Header("Golpe al disparar")]
    [Tooltip("P?xeles que se abren las l?neas de golpe con cada disparo. 6 = se siente el disparo. 0 = sin golpe.")]
    [Min(0f)]
    [SerializeField] private float kickPixels = 6f;
    [Tooltip("M?ximo que se pueden abrir por golpes seguidos (el subfusil). 14 = no se va de madre.")]
    [Min(0f)]
    [SerializeField] private float maxKickPixels = 14f;
    [Tooltip("Rapidez con la que vuelven tras el golpe. 18 = vuelven casi al instante.")]
    [Min(0.1f)]
    [SerializeField] private float kickRecoverySpeed = 18f;

    [Header("Enemigo en la mira")]
    [Tooltip("Cambia de color al apuntar a un enemigo vivo.")]
    [SerializeField] private bool highlightEnemies = true;
    [Tooltip("Color del n?cleo apuntando a un enemigo.")]
    [SerializeField] private Color enemyCoreColor = new Color(1f, 0.85f, 0.92f, 1f);
    [Tooltip("Color del halo apuntando a un enemigo. Magenta ne?n.")]
    [SerializeField] private Color enemyGlowColor = new Color(1f, 0.1f, 0.45f, 0.55f);
    [Tooltip("Metros hasta los que detecta enemigos. 100 = toda la sala.")]
    [Min(1f)]
    [SerializeField] private float enemyDetectMeters = 100f;
    [Tooltip("Capas que puede tocar el rayo que busca enemigos (las paredes tambi?n, para que no se vea a trav?s).")]
    [SerializeField] private LayerMask enemyDetectMask = ~0;
    [Tooltip("Rapidez del cambio de color. 20 = casi instant?neo.")]
    [Min(0.1f)]
    [SerializeField] private float colorChangeSpeed = 20f;

    private WeaponManager weaponManager;
    private WeaponBase weapon;

    private RectTransform[] lines;
    private Image[] glows;
    private Image[] cores;
    private RectTransform dot;
    private float kick;
    private float enemy01; // 0 = color normal, 1 = color de enemigo

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        BuildVisuals();
    }

    private void Start()
    {
        weaponManager = FindAnyObjectByType<WeaponManager>();
        if (weaponManager == null)
        {
            Debug.LogWarning($"[{name}] No hay ning?n WeaponManager en la escena: la ret?cula no se mostrar?.", this);
            return;
        }

        weaponManager.OnWeaponChanged += SetWeapon;
        SetWeapon(weaponManager.CurrentWeapon);
    }

    private void OnDestroy()
    {
        if (weaponManager != null) weaponManager.OnWeaponChanged -= SetWeapon;
        if (weapon != null) weapon.OnFired -= HandleFired;
    }

    private void SetWeapon(WeaponBase newWeapon)
    {
        if (weapon != null) weapon.OnFired -= HandleFired;
        weapon = newWeapon;
        if (weapon != null) weapon.OnFired += HandleFired;
        kick = 0f;
    }

    private void HandleFired()
    {
        kick = Mathf.Min(kick + kickPixels, maxKickPixels);
    }

    private void Update()
    {
        if (weapon == null)
        {
            SetLinesActive(false); // sin arma equipada, no hay nada que apuntar
            return;
        }

        SetLinesActive(true);

        // El golpe del disparo vuelve solo, en tiempo real (igual con c?mara lenta).
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-kickRecoverySpeed * Time.unscaledDeltaTime));

        float gap = matchRealSpread ? GetRealSpreadGap() : Mathf.Lerp(minGap, maxGap, weapon.NormalizedSpread);
        gap += kick;

        // La Image de cada l?nea es el halo: el n?cleo est? dentro, a glowSize de cada borde.
        // Por eso se coloca glowSize antes, para que el n?cleo empiece justo en 'gap'.
        float halo = gap - glowSize;
        if (top != null) top.anchoredPosition = new Vector2(0f, halo);
        if (bottom != null) bottom.anchoredPosition = new Vector2(0f, -halo);
        if (left != null) left.anchoredPosition = new Vector2(-halo, 0f);
        if (right != null) right.anchoredPosition = new Vector2(halo, 0f);

        UpdateColors();
    }

    /// <summary>
    /// Pasa los grados de dispersi?n del arma a unidades del Canvas. Un punto que est? a X grados
    /// del centro de la c?mara aparece en pantalla a tan(X) / tan(FOV/2) * (media altura de pantalla)
    /// p?xeles del centro. Luego se divide entre el scaleFactor del Canvas porque, con un Canvas
    /// Scaler, 1 unidad del Canvas no es 1 p?xel real.
    /// </summary>
    private float GetRealSpreadGap()
    {
        Camera cam = GetCamera();
        if (cam == null) return Mathf.Lerp(minGap, maxGap, weapon.NormalizedSpread);

        float halfFovTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float spreadTan = Mathf.Tan(weapon.CurrentSpreadDegrees * Mathf.Deg2Rad);
        float pixels = spreadTan / halfFovTan * (Screen.height * 0.5f);

        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        return minGap + pixels / scaleFactor;
    }

    private Camera GetCamera() => weapon != null && weapon.PlayerCamera != null ? weapon.PlayerCamera : Camera.main;

    // ---------------- COLOR ----------------

    private void UpdateColors()
    {
        float target = highlightEnemies && IsAimingAtEnemy() ? 1f : 0f;
        enemy01 = Mathf.Lerp(enemy01, target, 1f - Mathf.Exp(-colorChangeSpeed * Time.unscaledDeltaTime));

        Color core = Color.Lerp(coreColor, enemyCoreColor, enemy01);
        Color glow = Color.Lerp(glowColor, enemyGlowColor, enemy01);

        for (int i = 0; i < glows.Length; i++)
        {
            if (glows[i] != null) glows[i].color = glow;
            if (cores[i] != null) cores[i].color = core;
        }
    }

    /// <summary>Rayo desde el centro de la c?mara: true si lo primero que toca es un enemigo vivo.</summary>
    private bool IsAimingAtEnemy()
    {
        Camera cam = GetCamera();
        if (cam == null) return false;

        Transform view = cam.transform;
        if (!Physics.Raycast(view.position, view.forward, out RaycastHit hit, enemyDetectMeters, enemyDetectMask, QueryTriggerInteraction.Ignore))
            return false;

        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
        return enemy != null && !enemy.IsDead;
    }

    // ---------------- CONSTRUCCI?N ----------------

    /// <summary>
    /// Da tama?o a las 4 l?neas (que hacen de halo), les crea dentro el n?cleo y crea el rombo central.
    /// As? el aspecto se cambia desde estos campos y no hay que tocar cada Image a mano.
    /// </summary>
    private void BuildVisuals()
    {
        float glowThickness = coreThickness + glowSize * 2f;
        float glowLength = lineLength + glowSize * 2f;

        lines = new[] { top, bottom, left, right };
        int count = lines.Length + 1; // + el rombo
        glows = new Image[count];
        cores = new Image[count];

        for (int i = 0; i < lines.Length; i++)
        {
            RectTransform line = lines[i];
            if (line == null) continue;

            bool vertical = line == top || line == bottom;
            line.sizeDelta = vertical ? new Vector2(glowThickness, glowLength) : new Vector2(glowLength, glowThickness);

            glows[i] = line.GetComponent<Image>();
            if (glows[i] != null) glows[i].raycastTarget = false;
            cores[i] = CreateCore(line);
        }

        // Rombo central: un cuadrado girado 45?, tambi?n con halo y n?cleo.
        var dotObject = new GameObject("CenterDot", typeof(RectTransform), typeof(Image));
        dot = (RectTransform)dotObject.transform;
        dot.SetParent(transform, false);
        dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(0.5f, 0.5f);
        dot.anchoredPosition = Vector2.zero;
        dot.sizeDelta = Vector2.one * (centerDotSize + glowSize * 2f);
        dot.localRotation = Quaternion.Euler(0f, 0f, 45f);
        glows[count - 1] = dotObject.GetComponent<Image>();
        glows[count - 1].raycastTarget = false;
        cores[count - 1] = CreateCore(dot);
        dotObject.SetActive(showCenterDot);
    }

    /// <summary>Crea el n?cleo dentro de un halo: ocupa todo el halo menos glowSize por cada lado.</summary>
    private Image CreateCore(RectTransform glow)
    {
        var coreObject = new GameObject("Core", typeof(RectTransform), typeof(Image));
        var core = (RectTransform)coreObject.transform;
        core.SetParent(glow, false);
        core.anchorMin = Vector2.zero;
        core.anchorMax = Vector2.one;
        core.offsetMin = new Vector2(glowSize, glowSize);
        core.offsetMax = new Vector2(-glowSize, -glowSize);

        Image image = coreObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // Ojo: esto activa/desactiva las l?neas hijas, NUNCA el propio GameObject del script
    // (si se desactivara a s? mismo, su Update() dejar?a de ejecutarse y se quedar?a oculto para siempre).
    private void SetLinesActive(bool active)
    {
        foreach (RectTransform line in lines)
            if (line != null && line.gameObject.activeSelf != active) line.gameObject.SetActive(active);

        if (dot != null)
        {
            bool dotActive = active && showCenterDot;
            if (dot.gameObject.activeSelf != dotActive) dot.gameObject.SetActive(dotActive);
        }
    }
}
