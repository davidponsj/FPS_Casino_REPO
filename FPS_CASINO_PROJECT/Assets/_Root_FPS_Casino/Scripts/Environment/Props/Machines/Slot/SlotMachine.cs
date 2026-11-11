using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Tragaperras. Eliges apuesta con un SlotBetButton y tiras con E: se cobra la apuesta y giran los
/// cilindros (símbolo de cada uno elegido por pesos con WeightedRandom). Si salen todos iguales,
/// ganas apuesta × multiplicador del símbolo. La suerte de la botella morada (PlayerBuffs.SlotLuck)
/// es la probabilidad de premio seguro. SlotLeverAnimator baja la palanca mientras gira.
/// Cada símbolo puede tener una marca en el primer cilindro para que pare justo en su centro.
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
        [Tooltip("Objeto vacío sobre el centro de esta fruta, dentro del primer cilindro. La marca del símbolo 0 indica la posición \"de frente\". Vacío = reparto automático.")]
        public Transform marker;
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

    [Header("Parada")]
    [Tooltip("Desmarcado = frenada suave. Marcado = gira a velocidad constante, frena en seco y rebota un poco al parar.")]
    [SerializeField] private bool snappyStop = false;
    [Tooltip("Parte final del giro en la que frena (solo con parada rápida). 0.02 = frenazo seco, 0.15 = frenada corta.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float snappyBrakePortion = 0.02f;
    [Tooltip("Grados que se pasa de la fruta antes de volver a su sitio (solo con parada rápida). 6 = rebote suave.")]
    [Min(0f)]
    [SerializeField] private float bounceDegrees = 6f;
    [Tooltip("Segundos que dura el rebote (solo con parada rápida). 0.35 = rebote corto.")]
    [Min(0.01f)]
    [SerializeField] private float bounceSeconds = 0.35f;

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

    [Header("Eventos (opcionales, para sonidos/VFX)")]
    [Tooltip("Al elegir apuesta con un botón.")]
    public UnityEvent onBetSelected;
    [Tooltip("Al empezar a girar.")]
    public UnityEvent onSpinStart;
    [Tooltip("Cada vez que se para un cilindro. Recibe su número (0 = el de la izquierda).")]
    public UnityEvent<int> onReelStopped;
    [Tooltip("Al ganar. Recibe los puntos ganados.")]
    public UnityEvent<int> onWin;
    [Tooltip("Al perder.")]
    public UnityEvent onLose;

    private PlayerPoints shownPoints;   // los que pinta pointsText
    private SlotBetButton selectedBet;
    private bool spinning;
    private float[] reelAngles;
    private float[] symbolAngles;       // giro con el que cada símbolo queda de frente
    private Quaternion[] reelStartRotations;
    private Coroutine hideRoutine;
    private string lastResult = "-";

    public bool IsSpinning => spinning;
    public int ReelCount => reels != null ? reels.Length : 0;

    public string InteractionPrompt
    {
        get
        {
            if (spinning) return "Spinning...";
            if (selectedBet == null) return "Choose a bet first";
            return $"Spin ({selectedBet.BetAmount} pts) [E]";
        }
    }

    private void Awake()
    {
        reelAngles = new float[reels.Length];
        reelStartRotations = new Quaternion[reels.Length];
        for (int i = 0; i < reels.Length; i++)
            reelStartRotations[i] = reels[i].localRotation;

        // Se mide con los cilindros todavía en reposo (la fruta 0 de frente)
        symbolAngles = new float[symbols.Length];
        for (int i = 0; i < symbols.Length; i++)
            symbolAngles[i] = MeasureSymbolAngle(i);

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

    private void RefreshPointsText() => pointsText.text = $"Points: {shownPoints.CurrentPoints}";

    /// <summary>
    /// Grados que hay que girar el cilindro para que el símbolo quede donde está la marca del símbolo 0.
    /// Sin marcas, reparte los símbolos a partes iguales.
    /// </summary>
    private float MeasureSymbolAngle(int index)
    {
        float evenAngle = index * (360f / symbols.Length);

        Transform front = symbols[0].marker;
        Transform mark = symbols[index].marker;
        if (front == null || mark == null || reels.Length == 0) return evenAngle;

        Transform reel = reels[0];
        if (!mark.IsChildOf(reel) || !front.IsChildOf(reel))
        {
            Debug.LogWarning($"[Tragaperras] La marca de {symbols[index].symbolName} tiene que estar dentro del primer cilindro ({reel.name}).", this);
            return evenAngle;
        }

        // Posiciones de las marcas vistas desde el centro del cilindro, sin la componente del eje
        Quaternion toLocal = Quaternion.Inverse(reel.rotation);
        Vector3 frontDir = Vector3.ProjectOnPlane(toLocal * (front.position - reel.position), reelAxis);
        Vector3 markDir = Vector3.ProjectOnPlane(toLocal * (mark.position - reel.position), reelAxis);

        return Mathf.Repeat(Vector3.SignedAngle(markDir, frontDir, reelAxis), 360f);
    }

    // ---------------- Apuesta ----------------

    public void SelectBet(SlotBetButton button)
    {
        if (spinning) return;

        selectedBet = button;
        foreach (var b in betButtons)
            if (b != null) b.SetSelected(b == button);

        onBetSelected?.Invoke();
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
            ShowResult("Not enough points");
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
        onSpinStart?.Invoke();

        int count = reels.Length;

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

            float target = symbolAngles[results[i]];
            float delta = Mathf.Repeat(target - start[i], 360f);
            total[i] = delta + 360f * UnityEngine.Random.Range(minSpins, maxSpins + 1);
            duration[i] = firstReelSeconds + i * extraSecondsPerReel;
        }

        // Con parada rápida, cada cilindro se pasa un poco y luego rebota hasta su sitio
        float overshoot = snappyStop ? bounceDegrees : 0f;
        float settle = snappyStop ? bounceSeconds : 0f;

        float time = 0f;
        float endTime = duration[count - 1] + settle;
        bool[] stopped = new bool[count];

        while (time < endTime)
        {
            time += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                SetReelAngle(i, start[i] + ReelProgress(time, duration[i], total[i], overshoot, settle));

                // Aviso en el instante en que el cilindro llega a su fruta (antes del rebote)
                if (!stopped[i] && time >= duration[i])
                {
                    stopped[i] = true;
                    onReelStopped?.Invoke(i);
                }
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

    // Grados que ha girado un cilindro en el instante "time" de la tirada
    private float ReelProgress(float time, float duration, float total, float overshoot, float settle)
    {
        if (time < duration)
        {
            float k = Mathf.Clamp01(time / duration);
            if (!snappyStop) return total * (1f - Mathf.Pow(1f - k, 3f));
            return (total + overshoot) * SnappyEase(k);
        }

        if (!snappyStop || settle <= 0f) return total;

        // Rebote: se ha pasado "overshoot" grados y vuelve a su sitio con un pequeño vaivén
        float x = Mathf.Clamp01((time - duration) / settle);
        return total + overshoot * Mathf.Exp(-4f * x) * Mathf.Cos(x * Mathf.PI * 3f) * (1f - x);
    }

    // Velocidad constante y frenada corta al final. Devuelve el progreso (0..1) para un tiempo k (0..1).
    private float SnappyEase(float k)
    {
        float brake = snappyBrakePortion;
        float speed = 1f / (1f - brake * 0.5f);   // para que al final el progreso sea justo 1

        if (k < 1f - brake)
            return speed * k;

        float u = (k - (1f - brake)) / brake;
        return speed * (1f - brake) + speed * brake * (u - 0.5f * u * u);
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
            ShowResult("You lost");
            onLose?.Invoke();
            return;
        }

        float multiplier = symbols[results[0]].multiplier;
        int won = Mathf.RoundToInt(bet * multiplier);

        if (points != null) points.AddPoints(won);

        lastResult = $"{names} · ganado x{multiplier} = {won} pts";
        ShowResult($"You won! x{multiplier}\n{won} points");
        onWin?.Invoke(won);
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
    // ---------------- Prueba de paradas en el editor (SlotMachineEditor) ----------------

    private static readonly HashSet<SlotMachine> previewing = new HashSet<SlotMachine>();
    private Quaternion[] previewRestRotations;   // reposo guardado mientras se prueba una parada

    public bool IsPreviewing => previewRestRotations != null;

    public string[] SymbolNames
    {
        get
        {
            var names = new string[symbols.Length];
            for (int i = 0; i < symbols.Length; i++) names[i] = symbols[i].symbolName;
            return names;
        }
    }

    /// <summary>Solo editor: coloca los cilindros como pararían con este símbolo, sin darle a Play.</summary>
    public void PreviewSymbol(int index)
    {
        if (reels == null || reels.Length == 0 || index < 0 || index >= symbols.Length) return;

        ResetPreview();   // las marcas se miden con los cilindros en reposo

        previewRestRotations = new Quaternion[reels.Length];
        for (int i = 0; i < reels.Length; i++)
            previewRestRotations[i] = reels[i].localRotation;

        float angle = MeasureSymbolAngle(index);
        for (int i = 0; i < reels.Length; i++)
            reels[i].localRotation = previewRestRotations[i] * Quaternion.AngleAxis(angle, reelAxis);

        previewing.Add(this);
    }

    /// <summary>Solo editor: devuelve los cilindros a su reposo.</summary>
    public void ResetPreview()
    {
        if (previewRestRotations == null) return;

        for (int i = 0; i < reels.Length && i < previewRestRotations.Length; i++)
            if (reels[i] != null) reels[i].localRotation = previewRestRotations[i];

        previewRestRotations = null;
        previewing.Remove(this);
    }

    /// <summary>Solo editor: deja en reposo todas las máquinas que estén en prueba (antes de Play, guardar o recompilar).</summary>
    public static void ResetAllPreviews()
    {
        foreach (SlotMachine machine in new List<SlotMachine>(previewing))
            if (machine != null) machine.ResetPreview();
        previewing.Clear();
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string bet = spinning ? "girando" : selectedBet != null ? $"apuesta {selectedBet.BetAmount} pts" : "sin apuesta";
        DebugLabels.At(transform.position + Vector3.up * 2.5f, 0, $"Tragaperras · {bet}\nÚltimo: {lastResult}", DebugLabels.Casino);
    }

    private void OnDrawGizmosSelected()
    {
        // Marcas de las frutas: verde = la de frente (símbolo 0), amarillo = las demás
        if (symbols == null) return;
        for (int i = 0; i < symbols.Length; i++)
        {
            if (symbols[i].marker == null) continue;
            Gizmos.color = i == 0 ? Color.green : Color.yellow;
            Gizmos.DrawSphere(symbols[i].marker.position, 0.01f);
        }
    }
#endif
}