using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Tragaperras. Eliges apuesta con un SlotBetButton y tiras con E: se cobra la apuesta y giran los
/// cilindros (símbolo de cada uno elegido por pesos con WeightedRandom). Si salen todos iguales,
/// ganas apuesta × multiplicador del símbolo. La suerte de la botella morada (PlayerBuffs.SlotLuck)
/// es la probabilidad de premio seguro. SlotLeverAnimator baja la palanca mientras gira.
/// </summary>
public class SlotMachine : MonoBehaviour, IInteractable
{
    [Serializable]
    public class Symbol
    {
        [Tooltip("Nombre del símbolo (sale en la depuración).")]
        public string symbolName = "Cereza";
        [Tooltip("Probabilidad relativa en cada cilindro. Un símbolo con 4 sale 4 veces más que uno con 1.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Si salen todos iguales, ganas apuesta × este número. 5 = una apuesta de 10 da 50 puntos.")]
        [Min(0f)] public float multiplier = 5f;
    }

    [Header("Referencias")]
    [Tooltip("Los cilindros, de izquierda a derecha. El último es el que se para más tarde.")]
    [SerializeField] private Transform[] reels;
    [Tooltip("Botones de apuesta de esta máquina (se iluminan al elegir uno).")]
    [SerializeField] private SlotBetButton[] betButtons;
    [Tooltip("Texto donde sale \"Has ganado\" o \"Has perdido\". Opcional.")]
    [SerializeField] private TMP_Text resultText;
    [Tooltip("Texto con los puntos del jugador en la máquina. Opcional.")]
    [SerializeField] private TMP_Text pointsText;

    [Header("Símbolos (el orden es el de los cilindros)")]
    [SerializeField]
    private Symbol[] symbols =
    {
        new Symbol { symbolName = "Cereza",  weight = 4f, multiplier = 5f },
        new Symbol { symbolName = "Platano", weight = 3f, multiplier = 10f },
        new Symbol { symbolName = "Siete",   weight = 1f, multiplier = 50f }
    };

    [Header("Animación")]
    [Tooltip("Eje local del cilindro sobre el que gira. Si gira al revés, pon -1")]
    [SerializeField] private Vector3 reelAxis = Vector3.up;
    [Tooltip("Vueltas completas mínimas de cada cilindro.")]
    [Min(0)]
    [SerializeField] private int minSpins = 3;
    [Tooltip("Vueltas completas máximas. Cada cilindro elige entre el mínimo y este valor.")]
    [Min(0)]
    [SerializeField] private int maxSpins = 5;
    [Tooltip("Segundos que gira el primer cilindro. 2.5 = tirada ágil.")]
    [FormerlySerializedAs("baseDuration")]
    [Min(0.1f)]
    [SerializeField] private float firstReelSeconds = 2.5f;
    [Tooltip("Segundos de más que gira cada cilindro respecto al anterior, para que se paren de uno en uno. 0.7 = se nota la pausa.")]
    [FormerlySerializedAs("extraDurationPerReel")]
    [Min(0f)]
    [SerializeField] private float extraSecondsPerReel = 0.7f;

    [Header("Resultado")]
    [Tooltip("Segundos que se ve el texto del resultado. 3 = da tiempo a leerlo.")]
    [FormerlySerializedAs("resultDisplayTime")]
    [Min(0f)]
    [SerializeField] private float resultSeconds = 3f;
    [Tooltip("Si está desmarcado, hay que elegir apuesta de nuevo en cada tirada")]
    [SerializeField] private bool keepBetAfterSpin = false;

    [Header("Depuración")]
    [Tooltip("Muestra la apuesta elegida, el último resultado y el premio sobre la máquina en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    private PlayerPoints shownPoints;   // los que pinta pointsText
    private SlotBetButton selectedBet;
    private bool spinning;
    private float[] reelAngles;
    private Quaternion[] reelStartRotations;
    private Coroutine hideRoutine;
    private string lastResult = "-";

    public bool IsSpinning => spinning;

    public string InteractionPrompt
    {
        get
        {
            if (spinning) return "Girando...";
            if (selectedBet == null) return "Elige una apuesta primero";
            return $"Tirar ({selectedBet.BetAmount} pts) [E]";
        }
    }

    private void Awake()
    {
        reelAngles = new float[reels.Length];
        reelStartRotations = new Quaternion[reels.Length];
        for (int i = 0; i < reels.Length; i++)
            reelStartRotations[i] = reels[i].localRotation;

        if (resultText != null)
            resultText.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (pointsText == null) return;

        // Solo hay un jugador: su PlayerPoints es el que se pinta en la máquina.
        shownPoints = FindAnyObjectByType<PlayerPoints>();
        if (shownPoints == null)
        {
            Debug.LogWarning("[Tragaperras] No hay ningún PlayerPoints en la escena: el texto de puntos no se actualizará.", this);
            return;
        }

        shownPoints.OnPointsChanged += RefreshPointsText;
        RefreshPointsText();
    }

    private void OnDestroy()
    {
        if (shownPoints != null) shownPoints.OnPointsChanged -= RefreshPointsText;
    }

    private void RefreshPointsText() => pointsText.text = $"Puntos: {shownPoints.CurrentPoints}";

    // ---------------- Apuesta ----------------

    public void SelectBet(SlotBetButton button)
    {
        if (spinning) return;

        selectedBet = button;
        foreach (var b in betButtons)
            if (b != null) b.SetSelected(b == button);
    }

    private void ClearBet()
    {
        selectedBet = null;
        foreach (var b in betButtons)
            if (b != null) b.SetSelected(false);
    }

    // ---------------- Interacción ----------------

    public void Interact(GameObject interactor)
    {
        if (spinning) return;
        if (symbols == null || symbols.Length == 0 || reels == null || reels.Length == 0) return;

        if (selectedBet == null) return;   // el prompt ya dice "Elige una apuesta primero"

        int bet = selectedBet.BetAmount;

        // Paga (y cobra el premio) el jugador que interactúa.
        PlayerPoints points = interactor != null ? interactor.GetComponentInParent<PlayerPoints>() : null;
        if (points == null)
            Debug.LogWarning("[Tragaperras] El jugador no tiene PlayerPoints: la tirada será gratis y sin premio.", this);
        else if (bet > 0 && !points.TrySpend(bet))
        {
            ShowResult("Puntos insuficientes");
            return;
        }

        StartCoroutine(SpinRoutine(bet, GetLuck(interactor), points));
    }

    /// <summary>Suerte extra del jugador (botella morada): probabilidad de que la tirada sea premio seguro.</summary>
    private float GetLuck(GameObject interactor)
    {
        PlayerBuffs buffs = interactor != null ? interactor.GetComponentInParent<PlayerBuffs>() : null;
        return buffs != null ? buffs.SlotLuck : 0f;
    }

    // ---------------- Giro ----------------

    private IEnumerator SpinRoutine(int bet, float luck, PlayerPoints points)
    {
        spinning = true;
        HideResult();

        int count = reels.Length;
        float step = 360f / symbols.Length;

        int[] results = new int[count];
        float[] start = new float[count];
        float[] total = new float[count];
        float[] duration = new float[count];

        // Con suerte, todos los cilindros copian al primero: tres iguales = premio.
        bool luckyWin = UnityEngine.Random.value < luck;

        for (int i = 0; i < count; i++)
        {
            results[i] = (luckyWin && i > 0) ? results[0] : WeightedRandom.PickIndex(symbols, s => s.weight);
            start[i] = reelAngles[i];

            float target = results[i] * step;
            float delta = Mathf.Repeat(target - start[i], 360f);
            total[i] = delta + 360f * UnityEngine.Random.Range(minSpins, maxSpins + 1);
            duration[i] = firstReelSeconds + i * extraSecondsPerReel;
        }

        float time = 0f;
        float maxDuration = duration[count - 1];

        while (time < maxDuration)
        {
            time += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                float k = Mathf.Clamp01(time / duration[i]);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                SetReelAngle(i, start[i] + total[i] * eased);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            SetReelAngle(i, start[i] + total[i]);
            reelAngles[i] = Mathf.Repeat(reelAngles[i], 360f);
        }

        EvaluateResult(results, bet, points);

        if (!keepBetAfterSpin) ClearBet();
        spinning = false;
    }

    private void SetReelAngle(int index, float angle)
    {
        reelAngles[index] = angle;
        reels[index].localRotation = reelStartRotations[index] * Quaternion.AngleAxis(angle, reelAxis);
    }

    private void EvaluateResult(int[] results, int bet, PlayerPoints points)
    {
        string names = "";
        bool allEqual = true;

        for (int i = 0; i < results.Length; i++)
        {
            names += symbols[results[i]].symbolName + (i < results.Length - 1 ? " | " : "");
            if (results[i] != results[0]) allEqual = false;
        }

        if (!allEqual)
        {
            lastResult = $"{names} · perdido (apuesta {bet})";
            ShowResult("Has perdido");
            return;
        }

        float multiplier = symbols[results[0]].multiplier;
        int won = Mathf.RoundToInt(bet * multiplier);

        if (points != null) points.AddPoints(won);

        lastResult = $"{names} · ganado x{multiplier} = {won} pts";
        ShowResult($"¡Has ganado! x{multiplier}\n{won} puntos");
    }

    // ---------------- Texto de resultado ----------------

    private void ShowResult(string message)
    {
        if (resultText == null) return;

        resultText.text = message;
        resultText.gameObject.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private void HideResult()
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(resultSeconds);
        resultText.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string bet = spinning ? "girando" : selectedBet != null ? $"apuesta {selectedBet.BetAmount} pts" : "sin apuesta";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
            $"Tragaperras · {bet}\nÚltimo: {lastResult}");
    }
#endif
}
