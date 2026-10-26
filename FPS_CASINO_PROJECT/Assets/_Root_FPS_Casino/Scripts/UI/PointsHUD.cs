using TMPro;
using UnityEngine;

/// <summary>
/// Contadores de puntos en pantalla: arriba los puntos para jugar (CurrentPoints) y debajo
/// los totales para el ranking (TotalEarned). Se actualiza solo cuando cambian (OnPointsChanged).
/// </summary>
public class PointsHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Puntos del jugador que se muestran.")]
    [SerializeField] private PlayerPoints playerPoints;
    [Tooltip("Texto con los puntos que puedes gastar.")]
    [SerializeField] private TMP_Text currentPointsText;
    [Tooltip("Texto con el total ganado en la partida (para el ranking). Opcional.")]
    [SerializeField] private TMP_Text totalPointsText;

    [Header("Formato")]
    [Tooltip("Texto delante de los puntos actuales. Vacío = solo el número.")]
    [SerializeField] private string currentPrefix = "";
    [Tooltip("Texto delante del total. \"Total: \" = \"Total: 1500\".")]
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
