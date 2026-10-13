using TMPro;
using UnityEngine;

/// <summary>
/// Contadores de puntos en pantalla: arriba los puntos para jugar (CurrentPoints) y debajo
/// los totales para el ranking (TotalEarned). Se actualiza solo cuando cambian (OnPointsChanged).
/// </summary>
public class PointsHUD : MonoBehaviour
{
    [SerializeField] private PlayerPoints playerPoints;
    [SerializeField] private TMP_Text currentPointsText;
    [SerializeField] private TMP_Text totalPointsText;

    [Header("Formato")]
    [SerializeField] private string currentPrefix = "";
    [SerializeField] private string totalPrefix = "Total: ";

    private void OnEnable()
    {
        if (playerPoints != null)
        {
            playerPoints.OnPointsChanged += UpdateTexts;
            UpdateTexts();
        }
    }

    private void OnDisable()
    {
        if (playerPoints != null)
            playerPoints.OnPointsChanged -= UpdateTexts;
    }

    private void UpdateTexts()
    {
        // "N0" pone separador de miles: 12.500 en vez de 12500.
        if (currentPointsText != null)
            currentPointsText.text = currentPrefix + playerPoints.CurrentPoints.ToString("N0");

        if (totalPointsText != null)
            totalPointsText.text = totalPrefix + playerPoints.TotalEarned.ToString("N0");
    }
}
