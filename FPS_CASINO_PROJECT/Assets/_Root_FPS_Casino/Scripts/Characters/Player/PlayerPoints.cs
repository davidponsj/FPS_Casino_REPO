using System;
using UnityEngine;

/// <summary>
/// Puntos del jugador, con dos contadores:
/// - CurrentPoints: los que tienes para "jugar". Bajan al apostar en la ruleta o la tragaperras.
/// - TotalEarned: todos los puntos que has conseguido en la partida (kills, premios...). Solo suben,
///   aunque te gastes los otros. Es el que irá al ranking.
///
/// Va en el Player. La ruleta y la tragaperras lo buscan en el jugador que interactúa con ellas
/// (no hay que asignarlo a mano) y la UI se entera de los cambios con OnPointsChanged.
/// </summary>
public class PlayerPoints : MonoBehaviour
{
    [Tooltip("Puntos con los que empiezas. No cuentan para el ranking (no los has ganado).")]
    [SerializeField] private int startingPoints = 0;

    /// <summary>Se dispara cuando cambia cualquiera de los dos contadores.</summary>
    public event Action OnPointsChanged;

    private int currentPoints;
    private int totalEarned;

    public int CurrentPoints => currentPoints;
    public int TotalEarned => totalEarned;

    private void Awake()
    {
        currentPoints = startingPoints;
    }

    private void Start()
    {
        // En Start: la UI ya se ha suscrito en su OnEnable y recibe los valores iniciales.
        OnPointsChanged?.Invoke();
    }

    /// <summary>Gastar puntos (apuestas). Solo baja los actuales: el ranking no se toca.</summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0 || currentPoints < amount) return false;

        currentPoints -= amount;
        OnPointsChanged?.Invoke();
        return true;
    }

    /// <summary>Ganar puntos (kills, premios de la tragaperras...). Suben los dos contadores.</summary>
    public void AddPoints(int amount)
    {
        if (amount <= 0) return;

        currentPoints += amount;
        totalEarned += amount;
        OnPointsChanged?.Invoke();
    }

    [ContextMenu("TEST: Ganar 500 puntos")]
    private void TestAdd() => AddPoints(500);

    [ContextMenu("TEST: Gastar 100 puntos")]
    private void TestSpend() => TrySpend(100);
}
