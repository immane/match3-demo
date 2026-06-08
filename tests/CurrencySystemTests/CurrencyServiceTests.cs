using Xunit;
using System.Threading.Tasks;
using Match3Demo;

namespace Match3Demo.Tests;

public class CurrencyServiceTests
{
    private static CurrencyService CreateService(int initialBalance = 0)
    {
        var storage = new InMemoryStorage();
        var service = new CurrencyService(storage, null);
        if (initialBalance > 0)
            service.Grant("soft_currency", initialBalance, "test_init");
        return service;
    }

    [Fact]
    public void Spend_Sufficient_ReturnsTrue()
    {
        var service = CreateService(100);
        Assert.True(service.Spend("soft_currency", 50, "test"));
        Assert.Equal(50, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void Spend_Insufficient_ReturnsFalse()
    {
        var service = CreateService(10);
        Assert.False(service.Spend("soft_currency", 50, "test"));
        Assert.Equal(10, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void Spend_Insufficient_DoesNotModifyBalance()
    {
        var service = CreateService(10);
        service.Spend("soft_currency", 50, "test");
        Assert.Equal(10, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void Grant_IncreasesBalance()
    {
        var service = CreateService(0);
        service.Grant("soft_currency", 100, "test");
        Assert.Equal(100, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void CanAfford_ChecksCorrectly()
    {
        var service = CreateService(50);
        Assert.True(service.CanAfford("soft_currency", 50));
        Assert.False(service.CanAfford("soft_currency", 51));
    }

    [Fact]
    public void CanAfford_NonexistentCurrency_ReturnsFalse()
    {
        var service = CreateService(0);
        Assert.False(service.CanAfford("nonexistent", 1));
    }

    [Fact]
    public void GetBalance_NonexistentCurrency_ReturnsZero()
    {
        var service = CreateService(0);
        Assert.Equal(0, service.GetBalance("nonexistent"));
    }

    [Fact]
    public async Task SaveAndLoad_RestoresBalance()
    {
        var storage = new InMemoryStorage();
        var service1 = new CurrencyService(storage, null);
        service1.Grant("soft_currency", 200, "test");
        await service1.SaveAsync();

        var service2 = new CurrencyService(storage, null);
        // Allow time for LoadAsync to complete
        await Task.Delay(50);
        Assert.Equal(200, service2.GetBalance("soft_currency"));
    }
}
