using System;
using System.Threading.Tasks;

namespace Match3Demo;

public interface ICurrencyService
{
    bool CanAfford(string currencyId, int amount);
    bool Spend(string currencyId, int amount, string reason);
    void Grant(string currencyId, int amount, string reason);
    int GetBalance(string currencyId);
    event Action<string, int>? BalanceChanged;
    Task LoadAsync();
    Task SaveAsync();
}
