using System;
using UnityEngine;

/// <summary>
/// Barra de blackout. Cada botella que te BEBES sube 1/bottlesToBlackout de la barra (un tercio con 3);
/// las que lanzas no cuentan. La barra baja sola: llena del todo, tarda secondsToEmpty segundos en
/// vaciarse. Si llega al 100%, el jugador sufre un blackout y muere.
///
/// Se engancha al evento OnEffectStarted del BottleHolder (que solo salta al beber), así que no
/// hay que tocar el BottleHolder: basta con poner este componente en el Player.
///
/// Internamente la barra se cuenta en "botellas" (0 a bottlesToBlackout) en vez de en porcentaje:
/// así tres botellas son exactamente 3, sin errores de redondeo de 1/3 + 1/3 + 1/3.
/// </summary>
public class PlayerBlackout : MonoBehaviour
{
    [SerializeField] private BottleHolder bottleHolder;
    [SerializeField] private PlayerHealth playerHealth;

    [Tooltip("Botellas seguidas que provocan el blackout. Con 3, cada botella sube un tercio de la barra.")]
    [Min(1)]
    [SerializeField] private int bottlesToBlackout = 3;
    [Tooltip("Segundos que tarda la barra en vaciarse ENTERA (de 100% a 0%). Con 60 y 3 botellas, cada botella baja en 20 s.")]
    [Min(0.1f)]
    [SerializeField] private float secondsToEmpty = 60f;

    /// <summary>(nivel 0-1). Se dispara cada vez que cambia la barra.</summary>
    public event Action<float> OnBlackoutChanged;
    /// <summary>Al llegar al 100%, justo antes de matar al jugador.</summary>
    public event Action OnBlackout;

    private float bottlesInBody; // de 0 a bottlesToBlackout
    private bool blackedOut;

    /// <summary>Nivel de la barra de 0 (sobrio) a 1 (blackout).</summary>
    public float Blackout01 => bottlesInBody / bottlesToBlackout;

    private void Awake()
    {
        if (bottleHolder == null) bottleHolder = GetComponent<BottleHolder>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (bottleHolder != null) bottleHolder.OnEffectStarted += OnBottleDrunk;
    }

    private void OnDisable()
    {
        if (bottleHolder != null) bottleHolder.OnEffectStarted -= OnBottleDrunk;
    }

    private void Start()
    {
        OnBlackoutChanged?.Invoke(Blackout01);
    }

    private void OnBottleDrunk(TicketData ticket)
    {
        if (blackedOut) return;

        bottlesInBody = Mathf.Min(bottlesInBody + 1f, bottlesToBlackout);
        OnBlackoutChanged?.Invoke(Blackout01);

        // Pequeño margen por si acaso: 2.9999 cuenta como 3.
        if (bottlesInBody >= bottlesToBlackout - 0.001f)
            TriggerBlackout();
    }

    private void Update()
    {
        if (blackedOut || bottlesInBody <= 0f) return;

        // La barra entera (bottlesToBlackout botellas) se vacía en secondsToEmpty segundos.
        float bottlesPerSecond = bottlesToBlackout / secondsToEmpty;
        bottlesInBody = Mathf.Max(bottlesInBody - bottlesPerSecond * Time.deltaTime, 0f);
        OnBlackoutChanged?.Invoke(Blackout01);
    }

    private void TriggerBlackout()
    {
        blackedOut = true;
        Debug.Log("[Blackout] Demasiadas botellas seguidas: el jugador muere");
        OnBlackout?.Invoke();

        if (playerHealth != null)
            playerHealth.Kill();
    }

    [ContextMenu("TEST: Beber una botella (sin efecto)")]
    private void TestDrink() => OnBottleDrunk(null);
}