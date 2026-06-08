using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class GachaPoolEntryResource : Resource
{
    [Export] public string RewardId { get; set; } = "";
    [Export] public PetRarity Rarity { get; set; } = PetRarity.Common;
    [Export] public double Weight { get; set; } = 1.0;
    [Export] public bool IsRateUp { get; set; }
    [Export] public RewardType Type { get; set; } = RewardType.Pet;

    public GachaPoolEntryResource() { }

    public GachaPoolEntry ToEntry()
    {
        return new GachaPoolEntry
        {
            RewardId = RewardId,
            Type = Type,
            Rarity = Rarity,
            Weight = Weight
        };
    }
}
