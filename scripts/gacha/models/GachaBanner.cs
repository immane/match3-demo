using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class GachaBanner
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public List<GachaPoolEntry> Pool { get; init; } = new();
    public int CostPerPull { get; init; } = 50;
    public int SoftPityStart { get; init; } = 70;
    public int HardPityGuarantee { get; init; } = 90;
    public double SoftPityRateIncrease { get; init; } = 0.06;
    public string RateUpRewardId { get; init; } = "";
    public double RateUpChanceOnSSR { get; init; } = 0.5;

    public Dictionary<PetRarity, double> GetRarityWeights()
    {
        var weights = new Dictionary<PetRarity, double>();
        foreach (var entry in Pool)
        {
            weights.TryGetValue(entry.Rarity, out double current);
            weights[entry.Rarity] = current + entry.Weight;
        }
        return weights;
    }

    public double GetTotalWeight()
    {
        return Pool.Sum(e => e.Weight);
    }

    public List<GachaPoolEntry> GetEntriesByRarity(PetRarity rarity)
    {
        return Pool.Where(e => e.Rarity == rarity).ToList();
    }
}
