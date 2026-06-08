# Task 18: 货币系统核心 (模型/接口/服务)

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/currency_system.md](../design/currency_system.md) — 完整货币系统设计 (ICurrencyService, CurrencyService, CurrencySaveData, IPersistentStorage) |

## 状态
- [ ] 待执行

## 依赖
- Task 15 (IPersistentStorage — `scripts/utils/IPersistentStorage.cs`，异步泛型接口)
- Task 16 (EventBus `CurrencyChanged` 信号已定义)

## 产出文件
```
scripts/currency/models/CurrencyType.cs        [新增]
scripts/currency/models/CurrencyBalance.cs     [新增]
scripts/currency/models/CurrencySaveData.cs    [新增]
scripts/currency/services/ICurrencyService.cs  [新增]
scripts/currency/services/CurrencyService.cs   [新增]
```

## 实现要求

### CurrencyType.cs — 货币类型枚举

```csharp
namespace Match3Demo;

public enum CurrencyType
{
    SoftCurrency,
    HardCurrency,
    GachaTicket
}
```

### CurrencyBalance.cs — 纯 C# POCO

```csharp
using System.Collections.Generic;

namespace Match3Demo;

public class CurrencyBalance
{
    public Dictionary<CurrencyType, int> Balances { get; set; } = new();

    public int GetBalance(CurrencyType type) => Balances.GetValueOrDefault(type, 0);

    public void SetBalance(CurrencyType type, int amount) => Balances[type] = amount;

    public string GetCurrencyId(CurrencyType type) => type switch
    {
        CurrencyType.SoftCurrency => "soft_currency",
        CurrencyType.HardCurrency => "hard_currency",
        CurrencyType.GachaTicket => "gacha_ticket",
        _ => "unknown"
    };
}
```

### CurrencySaveData.cs — 序列化 DTO

```csharp
using System.Collections.Generic;

namespace Match3Demo;

public class CurrencySaveData
{
    public Dictionary<string, int> Balances { get; set; } = new();
    public long LastSavedTimestamp { get; set; }
}
```

### ICurrencyService.cs — 服务接口

纯 C# 接口，无 Godot 依赖，可单独进行单元测试。

```csharp
using System;
using System.Threading.Tasks;

namespace Match3Demo;

public interface ICurrencyService
{
    bool CanAfford(string currencyId, int amount);
    bool Spend(string currencyId, int amount, string reason);
    void Grant(string currencyId, int amount, string reason);
    int GetBalance(string currencyId);
    event Action<string, int> BalanceChanged;
    Task LoadAsync();
    Task SaveAsync();
}
```

### CurrencyService.cs — 完整实现

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Match3Demo;

public class CurrencyService : ICurrencyService
{
    private readonly IPersistentStorage _storage;
    private readonly EventBus _eventBus;
    private const string SaveKey = "currency";
    private readonly Dictionary<string, int> _balances = new();

    public event Action<string, int> BalanceChanged;

    public CurrencyService(IPersistentStorage storage, EventBus eventBus)
    {
        _storage = storage;
        _eventBus = eventBus;
        _ = LoadAsync(); // fire and forget load
    }

    public bool CanAfford(string currencyId, int amount)
        => _balances.GetValueOrDefault(currencyId, 0) >= amount;

    public bool Spend(string currencyId, int amount, string reason)
    {
        if (!CanAfford(currencyId, amount)) return false;
        _balances[currencyId] -= amount;
        SaveAndNotify(currencyId, -amount);
        GD.Print($"[Currency] Spent {amount} {currencyId} for {reason}");
        return true;
    }

    public void Grant(string currencyId, int amount, string reason)
    {
        _balances[currencyId] = _balances.GetValueOrDefault(currencyId, 0) + amount;
        SaveAndNotify(currencyId, amount);
        GD.Print($"[Currency] Granted {amount} {currencyId} for {reason}");
    }

    public int GetBalance(string currencyId)
        => _balances.GetValueOrDefault(currencyId, 0);

    private void SaveAndNotify(string currencyId, int delta)
    {
        var newBalance = _balances[currencyId];
        BalanceChanged?.Invoke(currencyId, newBalance);
        _eventBus.EmitSignal(EventBus.SignalName.CurrencyChanged, currencyId, newBalance, delta);
        _ = SaveAsync();
    }

    public async Task LoadAsync()
    {
        var data = await _storage.LoadAsync<CurrencySaveData>(SaveKey);
        if (data?.Balances != null)
        {
            foreach (var kv in data.Balances)
                _balances[kv.Key] = kv.Value;
        }
    }

    public async Task SaveAsync()
    {
        var data = new CurrencySaveData
        {
            Balances = new Dictionary<string, int>(_balances),
            LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _storage.SaveAsync(SaveKey, data);
    }
}
```

## 验收标准
- `CurrencyService` 构造函数正确注入 `IPersistentStorage` 和 `EventBus`
- `Spend()` 余额不足时返回 false 且不修改余额
- `Grant()` 和 `Spend()` 正确触发 `BalanceChanged` 事件 + `EventBus.CurrencyChanged` 信号
- `LoadAsync()` 从 `IPersistentStorage` 正确恢复余额
- `SaveAsync()` 正确序列化 `CurrencySaveData`
- 纯 C# 接口 `ICurrencyService` 无 Godot 依赖（可单元测试）
- `dotnet build` 0 errors 0 warnings

## 注意
- 目录 `scripts/currency/models/` 和 `scripts/currency/services/` 为新增，需确保父目录存在
- `CurrencyService` 构造函数中 `_ = LoadAsync()` 为 fire-and-forget，不阻塞构造
- `SaveAndNotify` 中 `_ = SaveAsync()` 为 fire-and-forget，避免阻塞主线程
- `CurrencyBalance` 使用 `CurrencyType` 枚举键，`CurrencyService` 内部使用 `string` 键（currencyId），两者通过 `GetCurrencyId()` 桥接
- `CurrencySaveData` 使用 `Dictionary<string, int>` 序列化，与 `IPersistentStorage` 的泛型序列化兼容
