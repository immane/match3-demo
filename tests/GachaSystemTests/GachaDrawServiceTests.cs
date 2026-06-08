using Xunit;
using System;
using Match3Demo;

namespace Match3Demo.Tests;

public class GachaDrawServiceTests
{
    private GachaDrawService CreateService(
        out FakeCurrencyService currency,
        out FakePetCollectionService pets,
        out FakeEventBus eventBus)
    {
        currency = new FakeCurrencyService(500);
        var roller = new GachaRollService(123);
        var banners = new FakeBannerDataSource();
        banners.Add(TestData.CreateBanner());
        pets = new FakePetCollectionService();
        eventBus = new FakeEventBus();
        var pityTracker = new GachaPityTracker(new InMemoryStorage());

        return new GachaDrawService(currency, roller, banners, pets, eventBus, pityTracker);
    }

    [Fact]
    public void PerformPull_DeductsCurrency()
    {
        var service = CreateService(out var currency, out _, out _);
        service.PerformPull("test");
        Assert.Equal(450, currency.GetBalance("soft_currency"));
    }

    [Fact]
    public void PerformPull_InsufficientCurrency_Throws()
    {
        var service = CreateService(out var currency, out _, out _);
        currency.Spend("soft_currency", 500, "drain");
        Assert.Throws<InvalidOperationException>(() => service.PerformPull("test"));
    }

    [Fact]
    public void PerformPull_InvalidBanner_Throws()
    {
        var service = CreateService(out _, out _, out _);
        Assert.Throws<ArgumentException>(() => service.PerformPull("nonexistent"));
    }

    [Fact]
    public void PerformPull_PetReward_AddsToCollection()
    {
        var service = CreateService(out _, out var pets, out _);
        var result = service.PerformPull("test");
        if (result.Type == RewardType.Pet)
            Assert.True(pets.HasPet(result.RewardId));
    }

    [Fact]
    public void PerformPull_EmitsGachaSignals()
    {
        var service = CreateService(out _, out _, out var eventBus);
        service.PerformPull("test");
        Assert.Contains("GachaBeforePull", eventBus.EmittedSignals);
        Assert.Contains("GachaPullResult", eventBus.EmittedSignals);
    }

    [Fact]
    public void GetPullsUntilGuarantee_InitialState_ReturnsHardPity()
    {
        var service = CreateService(out _, out _, out _);
        Assert.Equal(10, service.GetPullsUntilGuarantee("test"));
    }

    [Fact]
    public void PerformMultiPull_ReturnsCorrectCount()
    {
        var service = CreateService(out var currency, out _, out _);
        currency.Grant("soft_currency", 500, "extra");
        var results = service.PerformMultiPull("test", 10);
        Assert.Equal(10, results.Count);
    }
}
