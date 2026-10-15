public interface IPointsService
{
    int CurrentPoints { get; }
    bool TrySpend(int amount);
}
