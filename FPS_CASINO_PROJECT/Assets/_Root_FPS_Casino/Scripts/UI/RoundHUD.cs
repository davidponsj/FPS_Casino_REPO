using TMPro;
using UnityEngine;

/// <summary>
/// Número de ronda en pantalla. Se actualiza solo cuando empieza una ronda (OnRoundStarted).
/// </summary>
public class RoundHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("RoundManager de la escena; de él sale el número de ronda.")]
    [SerializeField] private RoundManager roundManager;
    [Tooltip("Texto donde sale la ronda.")]
    [SerializeField] private TMP_Text roundText;

    [Header("Formato")]
    [Tooltip("Texto delante del número. \"Ronda \" = \"Ronda 3\".")]
    [SerializeField] private string prefix = "Ronda ";

    private void OnEnable()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStarted += UpdateText;
            UpdateText(roundManager.CurrentRound);
        }
    }

    private void OnDisable()
    {
        if (roundManager != null)
            roundManager.OnRoundStarted -= UpdateText;
    }

    private void UpdateText(int round)
    {
        // Antes de la primera ronda (0) no se pinta nada.
        if (roundText != null)
            roundText.text = round > 0 ? prefix + round : "";
    }
}
