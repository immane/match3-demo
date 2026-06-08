using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Match3Demo;

public class CurrencyService : ICurrencyService
{
    private readonly IPersistentStorage _storage;
    private const string SaveKey = "currency";

    public event Action<string, int>? BalanceChanged;

    public CurrencyService(IPersistentStorage storage, EventBus? eventBus = null)
    {
        _storage = storage;
        _ = LoadAsync();
    }

    public bool CanAfford(string currencyId, int amount)
    {
        return GameData.Instance.GetCurrencyBalance(currencyId) >= amount;
    }

    public bool Spend(string currencyId, int amount, string reason)
    {
        if (!GameData.Instance.SpendCurrency(currencyId, amount))
            return false;

        BalanceChanged?.Invoke(currencyId, GameData.Instance.GetCurrencyBalance(currencyId));
        GD.Print($"[Currency] Spent {amount} {currencyId} for {reason}");
        _ = SaveAsync();
        return true;
    }

    public void Grant(string currencyId, int amount, string reason)
    {
        GameData.Instance.AddCurrency(currencyId, amount);
        BalanceChanged?.Invoke(currencyId, GameData.Instance.GetCurrencyBalance(currencyId));
        GD.Print($"[Currency] Granted {amount} {currencyId} for {reason}");
        _ = SaveAsync();
    }

    public int GetBalance(string currencyId)
    {
        return GameData.Instance.GetCurrencyBalance(currencyId);
    }

    private void SaveToGameData(Dictionary<string, int> balances)
    {
        foreach (var kv in balances)
            GameData.Instance.AddCurrency(kv.Key, kv.Value);
    }

    public async Task LoadAsync()
    {
        var data = await _storage.LoadAsync<CurrencySaveData>(SaveKey);
        if (data?.Balances != null)
        {
            foreach (var kv in data.Balances)
                GameData.Instance.AddCurrency(kv.Key, kv.Value);
        }
    }

    public async Task SaveAsync()
    {
        var balances = new Dictionary<string, int>();
        balances["soft_currency"] = GameData.Instance.GetCurrencyBalance("soft_currency");
        balances["hard_currency"] = GameData.Instance.GetCurrencyBalance("hard_currency");
        balances["gacha_ticket"] = GameData.Instance.GetCurrencyBalance("gacha_ticket");

        var data = new CurrencySaveData
        {
            Balances = balances,
            LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _storage.SaveAsync(SaveKey, data);
    }
}
