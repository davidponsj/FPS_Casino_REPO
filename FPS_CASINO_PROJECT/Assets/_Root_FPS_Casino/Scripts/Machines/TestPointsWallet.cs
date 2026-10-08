using UnityEngine;

public class TestPointsWallet : MonoBehaviour, IPointsService, IPointsReceiver
{
    [SerializeField] private int startingPoints = 1000;
    private int points;

    public int CurrentPoints => points;

    private void Awake() => points = startingPoints;

    public bool TrySpend(int amount)
    {
        if (points < amount) return false;
        points -= amount;
        Debug.Log($"[Puntos] -{amount}. Restantes: {points}");
        return true;
    }

    public void AddPoints(int amount)
    {
        points += amount;
        Debug.Log($"[Puntos] +{amount}. Total: {points}");
    }

    [ContextMenu("Añadir 500 puntos")]
    private void Add500() => AddPoints(500);
}