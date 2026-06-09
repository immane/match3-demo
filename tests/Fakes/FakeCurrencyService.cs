using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Match3Demo;

namespace Match3Demo.Tests;

public class FakeCurrencyService : ICurrencyService
{
    private readonly Dictionary<string, int> _balances = new();

    public event Action<string, int>? BalanceChanged;

    public FakeCurrencyService(int initialBalance = 0)
    {
        _balances["soft_currency"] = initialBalance;
    }

    public bool CanAfford(string id, int amount)
        => _balances.TryGetValue(id, out int value) && value >= amount;

    public bool Spend(string id, int amount, string reason)
    {
        if (!CanAfford(id, amount)) return false;
        _balances[id] -= amount;
        BalanceChanged?.Invoke(id, _balances[id]);
        return true;
    }

    public void Grant(string id, int amount, string reason)
    {
        if (!_balances.ContainsKey(id)) _balances[id] = 0;
        _balances[id] += amount;
        BalanceChanged?.Invoke(id, _balances[id]);
    }

    public int GetBalance(string id)
        => _balances.TryGetValue(id, out int value) ? value : 0;

    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
}
