# Task 16: EventBus / GameData 扩展 (宠物/抽卡/货币)

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture.md](../design/architecture.md) — 信号清单、数据字段 |

## 状态
- [x] 已完成

## 依赖
- Task 15 (infrastructure — IDataSource, IPersistentStorage 定义好名称)
- Task 07 (现有 EventBus / GameData Autoload 已完成)

## 产出文件
```
scripts/autoload/EventBus.cs  [修改]
scripts/autoload/GameData.cs  [修改]
```

## 实现要求

### EventBus.cs 新增信号

追加到现有 22 个信号之后，不删除、不修改现有信号。

```csharp
// 宠物系统信号
[Signal] public delegate void PetAcquiredEventHandler(string petDefId);
[Signal] public delegate void PetLeveledUpEventHandler(string petInstanceId, int newLevel);
[Signal] public delegate void PetEvolvedEventHandler(string oldPetInstanceId, string newPetDefId);
[Signal] public delegate void ActivePetChangedEventHandler(string petInstanceId);

// 抽卡系统信号
[Signal] public delegate void GachaPullResultEventHandler(string rewardId, int rarity, Godot.Collections.Dictionary pityState);
[Signal] public delegate void GachaPityMilestoneEventHandler(int pullsTowardGuarantee);
[Signal] public delegate void GachaMultiPullResultEventHandler(Godot.Collections.Array results);
[Signal] public delegate void GachaBeforePullEventHandler(string bannerId);

// 货币系统信号
[Signal] public delegate void CurrencyChangedEventHandler(string currencyId, int newBalance, int delta);
```

### GameData.cs 新增属性与方法

追加到现有文件中，不删除现有属性和方法。在 `IsWeb` 属性之后、`_EnterTree` 之前插入新属性，在 `UpdateCombo` 方法之后插入新方法。

**新增属性：**

```csharp
// 货币余额
public Godot.Collections.Dictionary<string, int> CurrencyBalances { get; set; } = new();

// 宠物收藏 (原始数据，每项为 { "defId": string, "instanceId": string, "level": int, "xp": int })
public Godot.Collections.Array<Godot.Collections.Dictionary> OwnedPetsRaw { get; set; } = new();
public string ActivePetId { get; set; }

// 抽卡保底状态 (key=poolId/category, value={ "totalPulls": int, "pullsSinceLastSR": int })
public Godot.Collections.Dictionary<string, Godot.Collections.Dictionary> GachaPityStates { get; set; } = new();
```

**新增货币操作方法：**

```csharp
public void AddCurrency(string currencyId, int amount)
{
    if (!CurrencyBalances.ContainsKey(currencyId))
        CurrencyBalances[currencyId] = 0;
    CurrencyBalances[currencyId] += amount;
    EventBus.Instance.EmitSignal(EventBus.SignalName.CurrencyChanged, currencyId, CurrencyBalances[currencyId], amount);
}

public bool SpendCurrency(string currencyId, int amount)
{
    int current = GetCurrencyBalance(currencyId);
    if (current < amount)
        return false;
    CurrencyBalances[currencyId] -= amount;
    EventBus.Instance.EmitSignal(EventBus.SignalName.CurrencyChanged, currencyId, CurrencyBalances[currencyId], -amount);
    return true;
}

public int GetCurrencyBalance(string currencyId)
{
    if (CurrencyBalances.TryGetValue(currencyId, out int balance))
        return balance;
    return 0;
}
```

## 验收标准
- EventBus.cs 新增 9 个信号委托，格式与现有信号一致 (`[Signal] public delegate void XEventHandler(...)`)
- GameData.cs 新增属性和方法可通过 `EventBus.Instance.CurrencyChanged` 等访问
- `dotnet build` 0 errors 0 warnings
- 现有 22 个信号不变，现有 GameData 属性不变 (向后兼容)
- `AddCurrency` / `SpendCurrency` 正确 emit `CurrencyChanged` 信号，delta 反映变化量
- `SpendCurrency` 余额不足时返回 false，不触发信号

## 注意
- 不要删除或重命名任何现有信号 / 属性 / 方法
- 新增信号使用 Godot 4.x C# 委托风格 (`delegate void XEventHandler(...)`)
- `pityState` / `results` 参数使用 `Godot.Collections.Dictionary` / `Godot.Collections.Array` 保持与 Godot 兼容
- 货币操作信号 emit 使用 `EventBus.SignalName.CurrencyChanged` 模式 (类型安全)
