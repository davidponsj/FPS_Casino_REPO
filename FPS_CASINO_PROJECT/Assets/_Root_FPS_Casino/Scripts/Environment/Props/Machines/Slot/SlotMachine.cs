using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class SlotMachine : MonoBehaviour, IInteractable
{
    [Serializable]
    public class Symbol
    {
        public string symbolName = "Cereza";
        [Min(0f)] public float weight = 1f;       // probabilidad relativa
        [Min(0f)] public float multiplier = 5f;   // si salen 3 iguales: apuesta x multiplicador
    }

    [Header("Referencias")]
    [SerializeField] private Transform[] reels;
    [SerializeField] private SlotBetButton[] betButtons;
    [SerializeField] private TMP_Text resultText;
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
    [SerializeField] private int minSpins = 3;
    [SerializeField] private int maxSpins = 5;
    [SerializeField] private float baseDuration = 2.5f;
    [SerializeField] private float extraDurationPerReel = 0.7f;

    [Header("Resultado")]
    [SerializeField] private float resultDisplayTime = 3f;
    [Tooltip("Si está desmarcado, hay que elegir apuesta de nuevo en cada tirada")]
    [SerializeField] private bool keepBetAfterSpin = false;

    private PlayerPoints shownPoints;   // los que pinta pointsText
    private SlotBetButton selectedBet;
    private bool spinning;
    private float[] reelAngles;
    private Quaternion[] reelStartRotations;
    private Coroutine hideRoutine;

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

        if (selectedBet == null)
        {
            Debug.Log("[Tragaperras] Elige una apuesta primero.");
            return;
        }

        int bet = selectedBet.BetAmount;

        // Paga (y cobra el premio) el jugador que interactúa.
        PlayerPoints points = interactor != null ? interactor.GetComponentInParent<PlayerPoints>() : null;
        if (points == null)
            Debug.LogWarning("[Tragaperras] El jugador no tiene PlayerPoints: la tirada será gratis y sin premio.", this);
        else if (bet > 0 && !points.TrySpend(bet))
        {
            Debug.Log("[Tragaperras] Puntos insuficientes.");
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
            duration[i] = baseDuration + i * extraDurationPerReel;
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

        Debug.Log($"[Tragaperras] {names}");

        if (!allEqual)
        {
            ShowResult("Has perdido");
            return;
        }

        float multiplier = symbols[results[0]].multiplier;
        int won = Mathf.RoundToInt(bet * multiplier);

        if (points != null) points.AddPoints(won);

        Debug.Log($"[Tragaperras] ¡Has ganado! x{multiplier} = {won} puntos");
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
        yield return new WaitForSeconds(resultDisplayTime);
        resultText.gameObject.SetActive(false);
    }
}