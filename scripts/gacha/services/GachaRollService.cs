using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class GachaRollService
{
    private readonly System.Random _rng;

    public GachaRollService(int? seed = null)
    {
        _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
    }

    public GachaRollResult Roll(GachaBanner banner, GachaPityState pityState)
    {
        if (pityState.PullsSinceLastSSR >= banner.HardPityGuarantee - 1)
            return ForceRarity(banner, pityState, PetRarity.Legendary);

        PetRarity rolledRarity = RollRarity(banner, pityState);
        string rewardId = SelectRewardFromRarity(banner, rolledRarity, pityState);
        var entry = banner.Pool.First(e => e.RewardId == rewardId);

        var newPity = new GachaPityState(
            pityState.TotalPulls + 1,
            rolledRarity >= PetRarity.Epic
                ? (rolledRarity == PetRarity.Legendary ? 0 : pityState.PullsSinceLastSSR)
                : pityState.PullsSinceLastSSR + 1,
            rolledRarity >= PetRarity.Rare
                ? (rolledRarity >= PetRarity.Epic ? 0 : pityState.PullsSinceLastEpic)
                : pityState.PullsSinceLastEpic + 1,
            rolledRarity == PetRarity.Legendary
                ? (banner.RateUpRewardId != null && rewardId != banner.RateUpRewardId)
                : pityState.GuaranteedRateUpNext
        );

        return new GachaRollResult(rewardId, entry.Type, rolledRarity, newPity);
    }

    private PetRarity RollRarity(GachaBanner banner, GachaPityState pity)
    {
        var rarityWeights = banner.GetRarityWeights();
        double baseSSRWeight = rarityWeights.GetValueOrDefault(PetRarity.Legendary, 0);

        double softPityBonus = 0;
        if (pity.PullsSinceLastSSR >= banner.SoftPityStart)
        {
            int overSoftPity = pity.PullsSinceLastSSR - banner.SoftPityStart + 1;
            softPityBonus = overSoftPity * banner.SoftPityRateIncrease;
        }

        double totalWeight = 0;
        var rarityEntries = new List<(PetRarity Rarity, double CumulativeWeight)>();

        foreach (var kv in rarityWeights.OrderBy(x => x.Key))
        {
            double weight = kv.Key == PetRarity.Legendary
                ? kv.Value + softPityBonus
                : kv.Value;
            totalWeight += weight;
            rarityEntries.Add((kv.Key, totalWeight));
        }

        double roll = _rng.NextDouble() * totalWeight;
        foreach (var (rarity, cumulative) in rarityEntries)
        {
            if (roll <= cumulative)
                return rarity;
        }

        return PetRarity.Common;
    }

    private string SelectRewardFromRarity(GachaBanner banner, PetRarity rarity, GachaPityState pity)
    {
        var candidates = banner.GetEntriesByRarity(rarity);

        if (rarity == PetRarity.Legendary && !string.IsNullOrEmpty(banner.RateUpRewardId))
        {
            if (pity.GuaranteedRateUpNext)
                return banner.RateUpRewardId;
            if (_rng.NextDouble() < banner.RateUpChanceOnSSR)
                return banner.RateUpRewardId;
        }

        return WeightedRandom.Pick(candidates, e => e.Weight, _rng).RewardId;
    }

    private GachaRollResult ForceRarity(GachaBanner banner, GachaPityState pity, PetRarity rarity)
    {
        string rewardId = SelectRewardFromRarity(banner, rarity, pity);
        var entry = banner.Pool.FirstOrDefault(e => e.RewardId == rewardId)
                     ?? banner.Pool.First(e => e.Rarity == rarity);

        var newPity = new GachaPityState(
            pity.TotalPulls + 1,
            0,
            pity.PullsSinceLastEpic,
            !string.IsNullOrEmpty(banner.RateUpRewardId) && rewardId != banner.RateUpRewardId
        );

        return new GachaRollResult(rewardId, entry.Type, rarity, newPity);
    }

    public List<GachaRollResult> RollMultiple(GachaBanner banner, GachaPityState pityState, int count)
    {
        var results = new List<GachaRollResult>();
        var currentPity = pityState;

        for (int i = 0; i < count; i++)
        {
            var result = Roll(banner, currentPity);
            results.Add(result);
            currentPity = result.NewPityState;
        }

        if (count >= 10 && !results.Any(r => r.Rarity >= PetRarity.Rare))
        {
            var lastResult = results[^1];
            var rareUpResult = ForceRarity(banner, lastResult.NewPityState, PetRarity.Rare);
            results[^1] = rareUpResult;
        }

        return results;
    }
}
