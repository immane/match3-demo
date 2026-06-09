using Godot;
using System;
using System.Collections.Generic;

namespace Match3Demo;

public class GachaDrawService
{
    private readonly ICurrencyService _currency;
    private readonly GachaRollService _roller;
    private readonly IDataSource<GachaBanner> _banners;
    private readonly IPetCollectionService _petCollection;
    private readonly EventBus _eventBus;
    private readonly GachaPityTracker _pityTracker;

    public GachaDrawService(
        ICurrencyService currency,
        GachaRollService roller,
        IDataSource<GachaBanner> banners,
        IPetCollectionService petCollection,
        EventBus eventBus,
        GachaPityTracker pityTracker)
    {
        _currency = currency;
        _roller = roller;
        _banners = banners;
        _petCollection = petCollection;
        _eventBus = eventBus;
        _pityTracker = pityTracker;
    }

    public GachaRollResult PerformPull(string bannerId)
    {
        var banner = _banners.Get(bannerId);
        if (banner == null)
            throw new ArgumentException($"Banner '{bannerId}' not found");

        if (!_currency.Spend("soft_currency", banner.CostPerPull, $"gacha_pull_{bannerId}"))
            throw new InvalidOperationException($"Insufficient currency for {bannerId}");

        _eventBus.EmitSignal(EventBus.SignalName.GachaBeforePull, bannerId);

        var pity = _pityTracker.GetPityState(bannerId);
        var result = _roller.Roll(banner, pity);
        _pityTracker.UpdatePityState(bannerId, result.NewPityState);

        GrantReward(result);
        EmitPullResult(result);

        return result;
    }

    public List<GachaRollResult> PerformMultiPull(string bannerId, int count = 10)
    {
        var banner = _banners.Get(bannerId);
        if (banner == null)
            throw new ArgumentException($"Banner '{bannerId}' not found");

        int totalCost = banner.CostPerPull * count;
        if (!_currency.Spend("soft_currency", totalCost, $"gacha_multipull_{bannerId}_{count}"))
            throw new InvalidOperationException($"Insufficient currency for {count}x {bannerId}");

        _eventBus.EmitSignal(EventBus.SignalName.GachaBeforePull, bannerId);

        var pity = _pityTracker.GetPityState(bannerId);
        var results = _roller.RollMultiple(banner, pity, count);

        var finalPity = results[^1].NewPityState;
        _pityTracker.UpdatePityState(bannerId, finalPity);

        foreach (var result in results)
            GrantReward(result);

        var arr = new Godot.Collections.Array();
        foreach (var r in results)
        {
            arr.Add(new Godot.Collections.Dictionary
            {
                ["rewardId"] = r.RewardId,
                ["type"] = (int)r.Type,
                ["rarity"] = (int)r.Rarity
            });
        }
        _eventBus.EmitSignal(EventBus.SignalName.GachaMultiPullResult, arr);
        return results;
    }

    public int GetPullsUntilGuarantee(string bannerId)
    {
        var banner = _banners.Get(bannerId);
        if (banner == null) return -1;
        var pity = _pityTracker.GetPityState(bannerId);
        return Math.Max(0, banner.HardPityGuarantee - pity.PullsSinceLastSSR);
    }

    private void GrantReward(GachaRollResult result)
    {
        if (result.Type == RewardType.Pet)
            _petCollection.AddPet(result.RewardId);
    }

    private void EmitPullResult(GachaRollResult result)
    {
        var pityDict = new Godot.Collections.Dictionary
        {
            ["totalPulls"] = result.NewPityState.TotalPulls,
            ["pullsSinceLastSSR"] = result.NewPityState.PullsSinceLastSSR,
            ["guaranteedRateUp"] = result.NewPityState.GuaranteedRateUpNext
        };
        _eventBus.EmitSignal(EventBus.SignalName.GachaPullResult, result.RewardId, (int)result.Rarity, pityDict);
    }
}
