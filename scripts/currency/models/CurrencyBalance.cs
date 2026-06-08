using System.Collections.Generic;

namespace Match3Demo;

public class CurrencyBalance
{
    public Dictionary<CurrencyType, int> Balances { get; set; } = new();

    public int GetBalance(CurrencyType type)
    {
        return Balances.TryGetValue(type, out int value) ? value : 0;
    }

    public void SetBalance(CurrencyType type, int amount)
    {
        Balances[type] = amount;
    }

    public static string GetCurrencyId(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.SoftCurrency => "soft_currency",
            CurrencyType.HardCurrency => "hard_currency",
            CurrencyType.GachaTicket => "gacha_ticket",
            _ => "unknown"
        };
    }
}
