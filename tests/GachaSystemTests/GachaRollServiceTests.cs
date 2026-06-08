using Xunit;
using System.Collections.Generic;
using System.Linq;
using Match3Demo;

namespace Match3Demo.Tests;

public class GachaRollServiceTests
{
    [Fact]
    public void Roll_ReturnsValidResult()
    {
        var service = new GachaRollService(42);
        var result = service.Roll(TestData.CreateBanner(), new GachaPityState(0, 0, 0, false));
        Assert.NotNull(result.RewardId);
        Assert.True(result.RewardId.Length > 0);
    }

    [Fact]
    public void Roll_Deterministic_SameSeed_SameResult()
    {
        var s1 = new GachaRollService(123);
        var s2 = new GachaRollService(123);
        var r1 = s1.Roll(TestData.CreateBanner(), new GachaPityState(0, 0, 0, false));
        var r2 = s2.Roll(TestData.CreateBanner(), new GachaPityState(0, 0, 0, false));
        Assert.Equal(r1.RewardId, r2.RewardId);
        Assert.Equal(r1.Rarity, r2.Rarity);
    }

    [Fact]
    public void Roll_HardPity_Guarantees_Legendary()
    {
        var service = new GachaRollService(42);
        var banner = TestData.CreateBanner(); // hardPity=10
        var pity = new GachaPityState(0, 9, 0, false);
        var result = service.Roll(banner, pity);
        Assert.Equal(PetRarity.Legendary, result.Rarity);
    }

    [Fact]
    public void Roll_HardPity_ResetsSSRCounter()
    {
        var service = new GachaRollService(42);
        var banner = TestData.CreateBanner();
        var pity = new GachaPityState(100, 9, 5, false);
        var result = service.Roll(banner, pity);
        Assert.Equal(0, result.NewPityState.PullsSinceLastSSR);
    }

    [Fact]
    public void Roll_TotalPulls_Increments()
    {
        var service = new GachaRollService(42);
        var result = service.Roll(TestData.CreateBanner(), new GachaPityState(5, 2, 1, false));
        Assert.Equal(6, result.NewPityState.TotalPulls);
    }

    [Fact]
    public void Roll_WithoutSSR_SSRCounterIncrements()
    {
        var service = new GachaRollService(42);
        var banner = TestData.CreateBanner();
        var pity = new GachaPityState(0, 0, 0, false);
        var result = service.Roll(banner, pity);
        if (result.Rarity != PetRarity.Legendary && result.Rarity != PetRarity.Epic)
            Assert.Equal(1, result.NewPityState.PullsSinceLastSSR);
    }

    [Fact]
    public void RollMultiple_ReturnsRequestedCount()
    {
        var service = new GachaRollService(42);
        var results = service.RollMultiple(TestData.CreateBanner(),
            new GachaPityState(0, 0, 0, false), 10);
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void RollMultiple_TenPull_Guarantees_RarePlus()
    {
        var service = new GachaRollService();
        for (int batch = 0; batch < 20; batch++)
        {
            var results = service.RollMultiple(TestData.CreateBanner(),
                new GachaPityState(0, 0, 0, false), 10);
            Assert.Contains(results, r => r.Rarity >= PetRarity.Rare);
        }
    }

    [Fact]
    public void Roll_FiftyFiftyGuarantee_PicksRateUp()
    {
        var banner = TestData.CreateBanner(rateUpRewardId: "legend1", rateUpChanceOnSSR: 0.5);
        var service = new GachaRollService(42);
        var pity = new GachaPityState(0, 9, 0, true); // hard pity + guaranteed
        var result = service.Roll(banner, pity);
        Assert.Equal("legend1", result.RewardId);
    }

    [Fact]
    public void Roll_FiftyFiftyNoGuarantee_CanPickNonRateUp()
    {
        var banner = TestData.CreateBanner(rateUpRewardId: "legend1", rateUpChanceOnSSR: 0.5);
        var service = new GachaRollService(42);
        var pity = new GachaPityState(0, 9, 0, false);
        var result = service.Roll(banner, pity);
        Assert.Equal(PetRarity.Legendary, result.Rarity);
    }
}
