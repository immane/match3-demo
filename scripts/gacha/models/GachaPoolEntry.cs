namespace Match3Demo;

public class GachaPoolEntry
{
    public string RewardId { get; init; } = "";
    public RewardType Type { get; init; } = RewardType.Pet;
    public PetRarity Rarity { get; init; } = PetRarity.Common;
    public double Weight { get; init; } = 1.0;
}
