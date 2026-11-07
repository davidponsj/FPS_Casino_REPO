using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Ranking del menú principal: al hacer clic en el rótulo LUXURY CASINO, la cámara se acerca (CameraFocusTarget) y
/// aparece este panel con las 10 mejores partidas guardadas (las mismas que el ranking de la pantalla de muerte).
/// Las filas salen una a una. Con Escape la cámara vuelve y el panel se esconde.
///
/// Va en el panel del Canvas del menú (con CanvasGroup). En el CameraFocusTarget del rótulo: On Arrived → Show y
/// On Leaving → Hide. La fila de molde es como la de la pantalla de muerte: dos textos hijos "Value" y "Label".
/// </summary>
public class MenuLeaderboard : MonoBehaviour
{
    private class Row
    {
        public GameObject Root;
        public TMP_Text Value;
        public TMP_Text Label;
    }

    [Header("Panel")]
    [Tooltip("CanvasGroup del panel: su alpha hace el fundido. Vacío = el de este objeto.")]
    [SerializeField] private CanvasGroup panel;
    [Tooltip("Fila DESACTIVADA de molde, con dos textos hijos llamados \"Value\" (puntos) y \"Label\" (puesto y nombre).")]
    [SerializeField] private RectTransform rowTemplate;
    [Tooltip("Filas que se enseñan. 10 = todo el ranking.")]
    [Min(1)]
    [SerializeField] private int maxRows = Leaderboard.MaxEntries;
    [Tooltip("Texto que sale si aún no hay ninguna partida guardada. Opcional.")]
    [SerializeField] private TMP_Text emptyText;

    [Header("Colores")]
    [Tooltip("Color de los puntos.")]
    [SerializeField] private Color valueColor = new Color(1f, 0.76f, 0.29f);
    [Tooltip("Color de los nombres.")]
    [SerializeField] private Color labelColor = new Color(0.95f, 0.9f, 0.82f);
    [Tooltip("Color de la primera posición (puntos y nombre).")]
    [SerializeField] private Color firstPlaceColor = new Color(0.25f, 0.88f, 1f);
    [Tooltip("Tamaño de la segunda línea (ronda, bajas, tiempo) respecto al nombre, en %. 55 = pequeña debajo.")]
    [Range(20f, 100f)]
    [SerializeField] private float detailSizePercent = 55f;
    [Tooltip("Color de la segunda línea.")]
    [SerializeField] private Color detailColor = new Color(0.79f, 0.72f, 0.6f, 0.7f);
    [Tooltip("Palabra para las bajas en la segunda línea.")]
    [SerializeField] private string killsWord = "bajas";

    [Header("Animación")]
    [Tooltip("Segundos del fundido al aparecer y al irse.")]
    [Min(0f)]
    [SerializeField] private float fadeSeconds = 0.25f;
    [Tooltip("Segundos entre una fila y la siguiente. 0.06 = ráfaga rápida.")]
    [Min(0f)]
    [SerializeField] private float rowRevealSeconds = 0.06f;

    [Header("Depuración")]
    [Tooltip("Escribe en la consola cuántas partidas enseña al abrirse.")]
    [SerializeField] private bool showDebug;

    private readonly List<Row> rows = new List<Row>();
    private Coroutine routine;

    private void Awake()
    {
        if (panel == null) panel = GetComponent<CanvasGroup>();
        SetVisible(0f);
        BuildRows();
    }

    /// <summary>Enseña el ranking (On Arrived del rótulo).</summary>
    public void Show()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    /// <summary>Esconde el ranking (On Leaving del rótulo).</summary>
    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(panel != null ? panel.alpha : 1f, 0f));
    }

    private IEnumerator ShowRoutine()
    {
        IReadOnlyList<Leaderboard.Entry> entries = Leaderboard.Entries;
        int count = Mathf.Min(entries.Count, rows.Count);
        if (showDebug) Debug.Log($"{name}: ranking con {entries.Count} partidas.", this);

        foreach (Row row in rows) row.Root.SetActive(false);
        if (emptyText != null) emptyText.gameObject.SetActive(count == 0);

        for (int i = 0; i < count; i++)
        {
            Leaderboard.Entry entry = entries[i];
            Row row = rows[i];
            bool first = i == 0;
            string detail = $"R{entry.round} · {entry.kills} {killsWord} · {RunStats.FormatTime(entry.seconds)}";

            row.Value.text = entry.points.ToString("N0");
            row.Label.text = $"{i + 1}. {entry.name}\n<size={detailSizePercent}%><color=#{ColorUtility.ToHtmlStringRGBA(detailColor)}>{detail}</color></size>";
            row.Value.color = first ? firstPlaceColor : valueColor;
            row.Label.color = first ? firstPlaceColor : labelColor;
        }

        yield return Fade(panel != null ? panel.alpha : 0f, 1f);

        // Las filas salen una a una.
        for (int i = 0; i < count; i++)
        {
            rows[i].Root.SetActive(true);
            if (rowRevealSeconds > 0f) yield return new WaitForSecondsRealtime(rowRevealSeconds);
        }
        routine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            SetVisible(Mathf.Lerp(from, to, t / fadeSeconds));
            yield return null;
        }
        SetVisible(to);
    }

    private void SetVisible(float alpha)
    {
        if (panel == null) return;
        panel.alpha = alpha;
        panel.blocksRaycasts = alpha > 0.5f;
        panel.interactable = alpha > 0.5f;
    }

    /// <summary>Crea las filas copiando el molde, una debajo de otra (una vez, al empezar).</summary>
    private void BuildRows()
    {
        if (rowTemplate == null) return;

        rowTemplate.gameObject.SetActive(false);
        float rowHeight = rowTemplate.rect.height;

        for (int i = 0; i < maxRows; i++)
        {
            RectTransform copy = Instantiate(rowTemplate, rowTemplate.parent);
            copy.anchoredPosition = rowTemplate.anchoredPosition + new Vector2(0f, -rowHeight * i);
            copy.gameObject.SetActive(false);

            var row = new Row
            {
                Root = copy.gameObject,
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
    }
}
