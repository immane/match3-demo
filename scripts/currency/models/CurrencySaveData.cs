using System.Collections.Generic;

namespace Match3Demo;

public class CurrencySaveData
{
    public Dictionary<string, int> Balances { get; set; } = new();
    public long LastSavedTimestamp { get; set; }
}
