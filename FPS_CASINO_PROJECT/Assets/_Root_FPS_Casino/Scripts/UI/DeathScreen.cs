using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Pantalla de muerte: el panel 3D "YOU LOST" aparece delante de la cámara con una tabla de dos páginas.
/// - Al morir: el juego se frena a cámara lenta hasta pararse, se ocultan el HUD y los controles, se suelta el ratón
///   y el panel entra desde abajo.
/// - Página "Tu partida": puntos totales (TotalEarned), rondas, bajas, tiempo y los puntos de cada truco, de más a menos.
///   Las filas salen una a una y los números cuentan hacia arriba.
/// - Si la partida entra en el top 10, aparece el campo para escribir el nombre. Al guardarlo (Enter o el botón), la
///   tabla pasa a la página "Ranking" con tu fila resaltada. El botón de página cambia entre las dos cuando quieras.
/// - Retry recarga la escena y Menú carga el menú (las dos tienen que estar en Build Settings).
///
/// Va en un objeto SIEMPRE ACTIVO (por ejemplo, el padre del panel). Lo que se enseña al morir es Screen Root, que
/// empieza desactivado. Si en la escena no hay EventSystem (lo que hace funcionar botones y campos de texto), lo crea.
/// Todo va en tiempo real: funciona aunque el juego esté parado (Time.timeScale = 0).
/// </summary>
public class DeathScreen : MonoBehaviour
{
    private struct RowData
    {
        public int Number;     // número que cuenta hacia arriba (si Text está vacío)
        public string Text;    // texto fijo en vez de número (el tiempo, "—"...)
        public string Label;
        public bool Highlight;
    }

    private class Row
    {
        public GameObject Root;
        public RectTransform Rect;
        public TMP_Text Value;
        public TMP_Text Label;
    }

    [Header("Referencias (vacías = las busca en el objeto con el tag Player)")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private RunStats runStats;
    [SerializeField] private PlayerInput playerInput;
    [Tooltip("Animación de la cámara al morir. El cartel espera a que acabe. Vacío = la busca en los padres (la Main Camera).")]
    [SerializeField] private PlayerDeathAnimation deathAnimation;

    [Header("Panel")]
    [Tooltip("Lo que aparece al morir: el modelo YOU LOST con su Canvas. Déjalo DESACTIVADO en la escena.")]
    [SerializeField] private GameObject screenRoot;
    [Tooltip("Lo que se anima al aparecer (normalmente el modelo). Vacío = Screen Root.")]
    [SerializeField] private Transform animatedRoot;
    [Tooltip("Objetos que se ocultan al morir: el Canvas del HUD de juego, el arma... Vacío = ninguno.")]
    [SerializeField] private GameObject[] hideOnDeath;

    [Header("Tabla")]
    [Tooltip("Fila DESACTIVADA de molde, con dos textos hijos llamados \"Value\" (columna azul) y \"Label\" (columna roja).")]
    [SerializeField] private RectTransform rowTemplate;
    [Tooltip("Filas que caben en la pantalla de la tabla. 13 = las del dibujo.")]
    [Min(1)]
    [SerializeField] private int maxRows = 13;
    [Tooltip("Texto opcional con el título de la página (\"TU PARTIDA\" / \"RANKING\"). Vacío = sin título.")]
    [SerializeField] private TMP_Text pageTitle;
    [Tooltip("Color de los números (columna de la izquierda). Dorado = casino.")]
    [FormerlySerializedAs("rowColor")]
    [SerializeField] private Color valueColor = new Color(1f, 0.82f, 0.25f);
    [Tooltip("Color de los nombres (columna de la derecha).")]
    [SerializeField] private Color labelColor = Color.white;
    [Tooltip("Color de la fila destacada: los puntos en \"Tu partida\" y tu partida en el ranking.")]
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.95f, 1f);
    [Tooltip("Tamaño de la segunda línea del ranking (ronda, bajas, tiempo) respecto al nombre, en %. 55 = pequeña debajo.")]
    [Range(20f, 100f)]
    [SerializeField] private float rankingDetailSizePercent = 55f;
    [Tooltip("Color de la segunda línea del ranking.")]
    [SerializeField] private Color rankingDetailColor = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("Centra las filas en la pantalla de la tabla: en vertical (entre el molde y el campo del nombre o el botón de página) y en horizontal (números + nombres como un bloque, midiendo lo que ocupan). Desmarcado = filas desde el molde hacia abajo.")]
    [SerializeField] private bool centerRows = true;

    [Header("Nombre (si entras en el top 10)")]
    [Tooltip("Objeto con el campo y el botón de guardar. Solo se enseña si la partida entra en el ranking.")]
    [SerializeField] private GameObject nameEntry;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;
    [Tooltip("Nombre si lo dejas en blanco.")]
    [SerializeField] private string defaultName = "JUGADOR";
    [Tooltip("Letras como mucho. 12 = cabe en la columna.")]
    [Min(1)]
    [SerializeField] private int maxNameLength = 12;

    [Header("Botones")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;
    [Tooltip("Botón que cambia entre \"Tu partida\" y \"Ranking\". Vacío = sin cambio manual.")]
    [SerializeField] private Button pageButton;
    [Tooltip("Texto del botón de página (pone a qué página te lleva). Opcional.")]
    [SerializeField] private TMP_Text pageButtonLabel;

    [Header("Escenas")]
    [Tooltip("Nombre de la escena del menú (tiene que estar en Build Settings).")]
    [SerializeField] private string menuSceneName = "SCN_MainMenu";

    [Header("Animación (segundos reales)")]
    [Tooltip("Segundos de cámara lenta hasta que el juego se para. 1 = se ve caer todo antes del panel.")]
    [Min(0f)]
    [SerializeField] private float slowMoSeconds = 1f;
    [Tooltip("Segundos que tarda el panel en entrar.")]
    [Min(0.01f)]
    [SerializeField] private float appearSeconds = 0.45f;
    [Tooltip("Desde dónde entra el panel, respecto a su sitio (en sus ejes locales). (0, -0.3, 0) = sube desde abajo.")]
    [SerializeField] private Vector3 appearOffset = new Vector3(0f, -0.3f, 0f);
    [Tooltip("Segundos entre una fila y la siguiente. 0.08 = ráfaga rápida.")]
    [Min(0f)]
    [SerializeField] private float rowRevealSeconds = 0.08f;
    [Tooltip("Segundos que tardan los números en contar hasta su valor. 0.5 = como una tragaperras.")]
    [Min(0f)]
    [SerializeField] private float countUpSeconds = 0.5f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola a qué PlayerHealth se ha enganchado al empezar y cuándo detecta la muerte. Para comprobar que funciona.")]
    [SerializeField] private bool showDebug = true;

    [Header("Textos")]
    [SerializeField] private string runPageTitle = "TU PARTIDA";
    [SerializeField] private string rankingPageTitle = "RANKING";
    [SerializeField] private string pointsLabel = "Puntos";
    [SerializeField] private string roundsLabel = "Rondas";
    [SerializeField] private string killsLabel = "Bajas";
    [SerializeField] private string timeLabel = "Tiempo";

    private readonly List<Row> rows = new List<Row>();
    private float rowHeight;
    private float columnGap;        // hueco entre el final de los números y el principio de los nombres, como en el molde
    private Vector2 valueTemplatePosition, labelTemplatePosition;
    private Coroutine revealRoutine;
    private bool isDead;
    private bool saved;
    private bool showingRanking;
    private int savedIndex = -1;
    private Vector3 restLocalPosition;

    private void Awake()
    {
        // Primero en los padres (lo normal: la pantalla va dentro del Player); si no, el objeto con el tag Player.
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (runStats == null) runStats = GetComponentInParent<RunStats>();
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        if (deathAnimation == null) deathAnimation = GetComponentInParent<PlayerDeathAnimation>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (playerHealth == null) playerHealth = player.GetComponent<PlayerHealth>();
            if (runStats == null) runStats = player.GetComponent<RunStats>();
            if (playerInput == null) playerInput = player.GetComponent<PlayerInput>();
        }

        if (playerHealth == null)
            Debug.LogWarning($"{name}: DeathScreen no encuentra el PlayerHealth del jugador. La pantalla de muerte no saldrá.", this);
        else if (showDebug)
            Debug.Log($"{name}: DeathScreen escuchando la muerte de {playerHealth.name}.", this);

        if (screenRoot == null)
            Debug.LogWarning($"{name}: DeathScreen no tiene Screen Root (el Panel). Al morir no se verá el cartel.", this);

        if (animatedRoot == null && screenRoot != null) animatedRoot = screenRoot.transform;
        if (animatedRoot != null) restLocalPosition = animatedRoot.localPosition;
        if (screenRoot != null) screenRoot.SetActive(false);

        BuildRows();

        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
        if (pageButton != null) pageButton.onClick.AddListener(TogglePage);
        if (saveButton != null) saveButton.onClick.AddListener(SaveName);
        if (nameInput != null)
        {
            nameInput.characterLimit = maxNameLength;
            nameInput.onSubmit.AddListener(_ => SaveName()); // Enter también guarda
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        if (isDead) return;
        isDead = true;
        if (showDebug) Debug.Log($"{name}: el jugador ha muerto, empieza la pantalla de muerte.", this);
        StartCoroutine(DeathSequence());
    }

    // ---------------- SECUENCIA ----------------

    private IEnumerator DeathSequence()
    {
        // Sin controles: ni moverse, ni disparar, ni mirar mientras escribes el nombre.
        if (playerInput != null) playerInput.DeactivateInput();
        if (hideOnDeath != null)
            foreach (GameObject hidden in hideOnDeath)
                if (hidden != null) hidden.SetActive(false);

        // Cámara lenta hasta parar el juego.
        for (float t = 0f; t < slowMoSeconds; t += Time.unscaledDeltaTime)
        {
            Time.timeScale = Mathf.Lerp(1f, 0.05f, t / slowMoSeconds);
            yield return null;
        }
        Time.timeScale = 0f;

        // Si la animación de muerte (caer, cerrar los ojos) dura más que la cámara lenta, se espera a que acabe.
        float animationLeft = (deathAnimation != null ? deathAnimation.Duration : 0f) - slowMoSeconds;
        if (animationLeft > 0f)
            yield return new WaitForSecondsRealtime(animationLeft);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();

        // El panel entra desde abajo (ease out).
        if (screenRoot != null) screenRoot.SetActive(true);
        for (float t = 0f; t < appearSeconds; t += Time.unscaledDeltaTime)
        {
            float eased = 1f - Mathf.Pow(1f - t / appearSeconds, 3f);
            if (animatedRoot != null) animatedRoot.localPosition = restLocalPosition + appearOffset * (1f - eased);
            yield return null;
        }
        if (animatedRoot != null) animatedRoot.localPosition = restLocalPosition;

        bool qualifies = runStats != null && Leaderboard.Qualifies(runStats.TotalPoints);
        if (nameEntry != null) nameEntry.SetActive(qualifies);
        if (qualifies && nameInput != null)
        {
            nameInput.text = "";
            nameInput.ActivateInputField();
        }

        ShowRunPage();
    }

    // ---------------- PÁGINAS ----------------

    private void ShowRunPage()
    {
        showingRanking = false;
        var data = new List<RowData>();

        if (runStats != null)
        {
            data.Add(new RowData { Number = runStats.TotalPoints, Label = pointsLabel, Highlight = true });
            data.Add(new RowData { Number = runStats.Round, Label = roundsLabel });
            data.Add(new RowData { Number = runStats.Kills, Label = killsLabel });
            data.Add(new RowData { Text = RunStats.FormatTime(runStats.SecondsSurvived), Label = timeLabel });

            foreach (RunStats.TrickTotal trick in runStats.GetTricksByPoints())
            {
                string label = trick.Count > 1 ? $"{trick.Label} ×{trick.Count}" : trick.Label;
                data.Add(new RowData { Number = trick.Points, Label = label });
            }
        }

        ShowPage(runPageTitle, data, rankingPageTitle);
    }

    private void ShowRankingPage()
    {
        showingRanking = true;
        var data = new List<RowData>();
        IReadOnlyList<Leaderboard.Entry> entries = Leaderboard.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            Leaderboard.Entry entry = entries[i];
            // Dos líneas: el puesto y el nombre, y debajo, pequeño, ronda · bajas · tiempo. Así los nombres largos caben.
            string detail = $"R{entry.round} · {entry.kills} {killsLabel.ToLowerInvariant()} · {RunStats.FormatTime(entry.seconds)}";
            data.Add(new RowData
            {
                Number = entry.points,
                Label = $"{i + 1}. {entry.name}\n<size={rankingDetailSizePercent}%><color=#{ColorUtility.ToHtmlStringRGBA(rankingDetailColor)}>{detail}</color></size>",
                Highlight = i == savedIndex
            });
        }

        ShowPage(rankingPageTitle, data, runPageTitle);
    }

    private void TogglePage()
    {
        if (showingRanking) ShowRunPage();
        else ShowRankingPage();
    }

    private void ShowPage(string title, List<RowData> data, string otherPageTitle)
    {
        if (pageTitle != null) pageTitle.text = title;
        if (pageButtonLabel != null) pageButtonLabel.text = otherPageTitle;

        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(RevealRows(data));
    }

    /// <summary>Las filas salen una a una y sus números cuentan desde 0, como el contador de una tragaperras.</summary>
    private IEnumerator RevealRows(List<RowData> data)
    {
        foreach (Row row in rows) row.Root.SetActive(false);

        int count = Mathf.Min(data.Count, rows.Count);

        // Las filas se activan ya (vacías, no se ven) para poder medir sus textos y centrarlas antes de que salgan.
        for (int i = 0; i < count; i++)
        {
            rows[i].Root.SetActive(true);
            rows[i].Value.text = "";
            rows[i].Label.text = "";
        }
        LayoutRows(data, count);

        float startTime = Time.unscaledTime;

        while (true)
        {
            float elapsed = Time.unscaledTime - startTime;
            bool done = true;

            for (int i = 0; i < count; i++)
            {
                float rowAge = elapsed - i * rowRevealSeconds;
                if (rowAge < 0f) { done = false; continue; }

                Row row = rows[i];
                RowData item = data[i];

                float count01 = countUpSeconds > 0f ? Mathf.Clamp01(rowAge / countUpSeconds) : 1f;
                if (count01 < 1f) done = false;
                float eased = 1f - (1f - count01) * (1f - count01);

                row.Value.text = string.IsNullOrEmpty(item.Text) ? Mathf.RoundToInt(item.Number * eased).ToString("N0") : item.Text;
                row.Label.text = item.Label;
                row.Value.color = item.Highlight ? highlightColor : valueColor;
                row.Label.color = item.Highlight ? highlightColor : labelColor;
            }

            if (done) break;
            yield return null;
        }

        revealRoutine = null;
    }

    /// <summary>
    /// Coloca las filas de esta página. En vertical, el bloque de filas queda centrado en el hueco libre de la pantalla
    /// (del molde hacia abajo hasta el campo del nombre o el botón de página). En horizontal, mide el número más ancho y
    /// el nombre más ancho y centra los dos juntos, así no queda todo a un lado cuando los números son cortos o los
    /// nombres largos.
    /// </summary>
    private void LayoutRows(List<RowData> data, int count)
    {
        if (count == 0 || rowTemplate == null) return;

        // Vertical.
        float firstOffset = 0f; // respecto al molde (hacia abajo es negativo)
        if (centerRows)
        {
            Transform parent = rowTemplate.parent;
            float templateCenter = rowTemplate.localPosition.y + rowTemplate.rect.center.y;
            float top = templateCenter + rowHeight * 0.5f;
            float bottom = top - rowHeight * maxRows;
            if (nameEntry != null && nameEntry.activeSelf) bottom = Mathf.Max(bottom, TopEdge(nameEntry.transform as RectTransform, parent));
            if (pageButton != null && pageButton.gameObject.activeSelf) bottom = Mathf.Max(bottom, TopEdge(pageButton.transform as RectTransform, parent));

            float blockHeight = rowHeight * count;
            if (blockHeight < top - bottom)
                firstOffset = (top + bottom) * 0.5f + blockHeight * 0.5f - rowHeight * 0.5f - templateCenter;
        }
        for (int i = 0; i < rows.Count; i++)
            rows[i].Rect.anchoredPosition = rowTemplate.anchoredPosition + new Vector2(0f, firstOffset - rowHeight * i);

        // Horizontal.
        RectTransform valueRect = rows[0].Value.rectTransform;
        RectTransform labelRect = rows[0].Label.rectTransform;
        float valueX = valueTemplatePosition.x, labelX = labelTemplatePosition.x;
        if (centerRows)
        {
            float widestValue = 0f, widestLabel = 0f;
            for (int i = 0; i < count; i++)
            {
                string finalValue = string.IsNullOrEmpty(data[i].Text) ? data[i].Number.ToString("N0") : data[i].Text;
                widestValue = Mathf.Max(widestValue, rows[i].Value.GetPreferredValues(finalValue).x);
                widestLabel = Mathf.Max(widestLabel, rows[i].Label.GetPreferredValues(data[i].Label).x);
            }
            widestValue = Mathf.Min(widestValue, valueRect.rect.width);
            widestLabel = Mathf.Min(widestLabel, labelRect.rect.width);

            float valueRight = -(widestValue + columnGap + widestLabel) * 0.5f + widestValue;
            valueX = valueRight - (1f - valueRect.pivot.x) * valueRect.rect.width;                // números pegados a la derecha
            labelX = valueRight + columnGap + labelRect.pivot.x * labelRect.rect.width;            // nombres pegados a la izquierda
        }
        foreach (Row row in rows)
        {
            row.Value.rectTransform.anchoredPosition = new Vector2(valueX, valueTemplatePosition.y);
            row.Label.rectTransform.anchoredPosition = new Vector2(labelX, labelTemplatePosition.y);
        }
    }

    /// <summary>Altura del borde de arriba de un objeto, medida en los ejes de otro (el padre de las filas).</summary>
    private static float TopEdge(RectTransform target, Transform space)
    {
        if (target == null || space == null) return float.NegativeInfinity;
        Vector3 topCenter = target.TransformPoint(new Vector3(target.rect.center.x, target.rect.yMax, 0f));
        return space.InverseTransformPoint(topCenter).y;
    }

    // ---------------- NOMBRE ----------------

    private void SaveName()
    {
        if (saved || runStats == null) return;
        saved = true;

        string playerName = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(playerName)) playerName = defaultName;
        if (playerName.Length > maxNameLength) playerName = playerName.Substring(0, maxNameLength);

        savedIndex = Leaderboard.Add(runStats.ToEntry(playerName.ToUpperInvariant()));
        if (nameEntry != null) nameEntry.SetActive(false);
        ShowRankingPage();
    }

    // ---------------- ESCENAS ----------------

    private void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    // ---------------- PRUEBAS (menú ⋮ del componente, en Play) ----------------

    [ContextMenu("TEST: Morir ahora")]
    private void TestDie()
    {
        if (playerHealth != null) playerHealth.Kill();
    }

    [ContextMenu("TEST: Borrar ranking")]
    private void TestClearLeaderboard() => Leaderboard.Clear();

    // ---------------- MONTAJE ----------------

    /// <summary>Crea las filas de la tabla copiando el molde (una vez, al empezar).</summary>
    private void BuildRows()
    {
        if (rowTemplate == null) return;

        rowTemplate.gameObject.SetActive(false);
        rowHeight = rowTemplate.rect.height;

        for (int i = 0; i < maxRows; i++)
        {
            RectTransform copy = Instantiate(rowTemplate, rowTemplate.parent);
            copy.anchoredPosition = rowTemplate.anchoredPosition + new Vector2(0f, -rowHeight * i);
            copy.gameObject.SetActive(false);

            var row = new Row
            {
                Root = copy.gameObject,
                Rect = copy,
                Value = copy.Find("Value")?.GetComponent<TMP_Text>(),
                Label = copy.Find("Label")?.GetComponent<TMP_Text>()
            };

            if (row.Value == null || row.Label == null)
            {
                Debug.LogWarning($"{name}: la fila de molde necesita dos textos hijos llamados \"Value\" y \"Label\".", this);
                return;
            }
            rows.Add(row);
        }

        // Cómo están las columnas en el molde: su sitio y el hueco entre el final de los números y el principio de los nombres.
        if (rows.Count > 0)
        {
            RectTransform valueRect = rows[0].Value.rectTransform;
            RectTransform labelRect = rows[0].Label.rectTransform;
            valueTemplatePosition = valueRect.anchoredPosition;
            labelTemplatePosition = labelRect.anchoredPosition;
            float valueRight = valueTemplatePosition.x + (1f - valueRect.pivot.x) * valueRect.rect.width;
            float labelLeft = labelTemplatePosition.x - labelRect.pivot.x * labelRect.rect.width;
            columnGap = Mathf.Max(0f, labelLeft - valueRight);
        }
    }

    /// <summary>Sin EventSystem no funcionan ni los botones ni el campo de texto: si la escena no tiene, se crea uno.</summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
