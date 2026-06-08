# 货币系统设计

> 定义多货币类型的统一管理服务，支持安全交易、持久化存储与 UI 自动更新。

## 关联文档

| 方向 | 文档 |
|------|------|
| ↔ 同级 | [architecture.md](architecture.md) — EventBus 全局信号机制 |
| ↔ 同级 | [data_models.md](data_models.md) — 数据模型设计范式 |
| → 未来 | [gacha_system.md] — 抽卡系统消费货币 |
| → 未来 | [shop_system.md] — 商店系统消费货币 |
| → 未来 | [reward_system.md] — Match-3 玩法产出货币 |

---

## 目录

1. [概述与设计目标](#1-概述与设计目标)
2. [货币类型定义](#2-货币类型定义)
3. [服务接口](#3-服务接口)
4. [CurrencyService 实现](#4-currencyservice-实现)
5. [持久化存储](#5-持久化存储)
6. [获取途径](#6-获取途径)
7. [EventBus 扩展信号](#7-eventbus-扩展信号)
8. [UI 集成](#8-ui-集成)
9. [文件结构](#9-文件结构)
10. [附录：设计决策表](#10-附录设计决策表)

---

## 1. 概述与设计目标

### 1.1 系统定位

货币系统是一个**纯 C# 服务层组件**，不继承 `Node`，通过依赖注入获取持久化存储和事件总线实例。它向上层系统（Gacha、Shop、Reward）提供统一的增/减/查 API。

```
┌─────────────────────────────────────────────────────────────┐
│                    货币系统架构                               │
│                                                              │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐                │
│  │  Gacha   │   │  Shop    │   │  Reward  │   ← 消费者       │
│  │  System  │   │  System  │   │  System  │                │
│  └────┬─────┘   └────┬─────┘   └────┬─────┘                │
│       │              │              │                       │
│       └──────────────┼──────────────┘                       │
│                      ▼                                       │
│            ┌──────────────────┐                              │
│            │  ICurrencyService│   ← 统一接口                  │
│            │  (C# interface)  │                              │
│            └────────┬─────────┘                              │
│                     │                                        │
│                     ▼                                        │
│            ┌──────────────────┐                              │
│            │  CurrencyService │   ← 实现                      │
│            │  (C# class)      │                              │
│            └──┬───────────┬───┘                              │
│               │           │                                   │
│               ▼           ▼                                   │
│     ┌──────────────┐  ┌──────────┐                           │
│     │IPersistent   │  │ EventBus │   ← 基础设施               │
│     │Storage       │  │          │                           │
│     └──────────────┘  └──────────┘                           │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 设计目标

| 目标 | 说明 |
|------|------|
| **接口解耦** | 上层系统只依赖 `ICurrencyService` 接口，可替换实现（本地存储 / 服务器同步） |
| **原子交易** | Spend/Grant 操作内部完成 检查→修改→保存→通知，防止不一致 |
| **持久化** | 通过 `IPersistentStorage` 抽象存储后端，支持 JSON 文件 / Godot ConfigFile / 远程 |
| **可观察** | 每次余额变更通过 `EventBus` 发出信号，UI 自动刷新，无需轮询 |
| **可追踪** | 所有交易携带 `reason` 参数，用于分析玩家行为与调试 |
| **多币种** | 支持软货币（玩法获取）、硬货币（付费获取）、抽卡券等多种类型 |

### 1.3 货币类型概览

| 货币 ID | 类型 | 获取方式 | 用途 |
|---------|------|---------|------|
| `soft_currency` | SoftCurrency | Match-3 消除、成就 | 基础抽卡、商店购买 |
| `hard_currency` | HardCurrency | IAP 购买、成就 | 高级抽卡、特殊商店 |
| `gacha_ticket` | GachaTicket | 活动、登录奖励 | 单次抽卡 |

---

## 2. 货币类型定义

### 2.1 CurrencyType 枚举

```csharp
// scripts/currency/models/CurrencyType.cs

namespace Match3Demo;

public enum CurrencyType
{
    SoftCurrency = 0,
    HardCurrency = 1,
    GachaTicket  = 2,
}
```

### 2.2 CurrencyConfig 静态配置

```csharp
// scripts/currency/models/CurrencyConfig.cs

namespace Match3Demo;

public static class CurrencyConfig
{
    public const string SoftCurrencyId  = "soft_currency";
    public const string HardCurrencyId  = "hard_currency";
    public const string GachaTicketId   = "gacha_ticket";

    public static readonly Dictionary<string, CurrencyInfo> Currencies = new()
    {
        [SoftCurrencyId] = new CurrencyInfo
        {
            Id          = SoftCurrencyId,
            Type        = CurrencyType.SoftCurrency,
            DisplayName = "金币",
            IconPath    = "res://assets/textures/ui/icon_coin.png",
        },
        [HardCurrencyId] = new CurrencyInfo
        {
            Id          = HardCurrencyId,
            Type        = CurrencyType.HardCurrency,
            DisplayName = "钻石",
            IconPath    = "res://assets/textures/ui/icon_gem.png",
        },
        [GachaTicketId] = new CurrencyInfo
        {
            Id          = GachaTicketId,
            Type        = CurrencyType.GachaTicket,
            DisplayName = "抽卡券",
            IconPath    = "res://assets/textures/ui/icon_ticket.png",
        },
    };

    public static CurrencyInfo Get(string currencyId)
    {
        return Currencies.TryGetValue(currencyId, out var info)
            ? info
            : throw new ArgumentException($"Unknown currency: {currencyId}");
    }
}

public class CurrencyInfo
{
    public string       Id          { get; init; } = "";
    public CurrencyType Type        { get; init; }
    public string       DisplayName { get; init; } = "";
    public string       IconPath    { get; init; } = "";
}
```

### 2.3 CurrencyBalance 数据结构

```csharp
// scripts/currency/models/CurrencyBalance.cs

namespace Match3Demo;

/// <summary>
/// 纯数据对象 (POCO)，表示当前所有货币余额的快照。
/// </summary>
public class CurrencyBalance
{
    public Dictionary<string, int> Balances { get; init; } = new();

    public CurrencyBalance()
    {
        Balances[CurrencyConfig.SoftCurrencyId] = 0;
        Balances[CurrencyConfig.HardCurrencyId] = 0;
        Balances[CurrencyConfig.GachaTicketId]  = 0;
    }

    public int Get(string currencyId)
    {
        return Balances.TryGetValue(currencyId, out var value) ? value : 0;
    }

    public void Set(string currencyId, int amount)
    {
        Balances[currencyId] = Math.Max(0, amount);
    }

    public void Add(string currencyId, int delta)
    {
        Balances[currencyId] = Math.Max(0, Get(currencyId) + delta);
    }

    public bool TrySubtract(string currencyId, int amount)
    {
        var current = Get(currencyId);
        if (current < amount)
            return false;
        Balances[currencyId] = current - amount;
        return true;
    }
}
```

---

## 3. 服务接口

```csharp
// scripts/currency/services/ICurrencyService.cs

namespace Match3Demo;

public interface ICurrencyService
{
    /// <summary>检查是否有足够余额</summary>
    bool CanAfford(string currencyId, int amount);

    /// <summary>
    /// 消费货币：检查余额 → 扣除 → 保存 → 触发事件。
    /// 返回 true 表示扣款成功，false 表示余额不足。
    /// reason 示例："gacha_pull_standard", "shop_purchase"
    /// </summary>
    bool Spend(string currencyId, int amount, string reason);

    /// <summary>
    /// 授予货币：增加余额 → 保存 → 触发事件。
    /// reason 示例："match3_reward", "iap_purchase", "duplicate_pet"
    /// </summary>
    void Grant(string currencyId, int amount, string reason);

    /// <summary>获取当前余额</summary>
    int GetBalance(string currencyId);

    /// <summary>获取全部余额快照</summary>
    CurrencyBalance GetAllBalances();

    /// <summary>余额变更事件 (currencyId, newBalance)</summary>
    event Action<string, int> BalanceChanged;
}
```

### 3.1 操作语义

```
Spend("soft_currency", 100, "gacha_pull_standard")
    │
    ├── CanAfford? ──── 否 ──→ return false
    │
    ├── 是 ──→ Balances["soft_currency"] -= 100
    │
    ├── SaveAsync() → IPersistentStorage
    │
    └── BalanceChanged?.Invoke("soft_currency", newBalance)
         └── EventBus 下游消费者 (UI, 日志)

Grant("soft_currency", 50, "match3_reward")
    │
    ├── Balances["soft_currency"] += 50
    │
    ├── SaveAsync() → IPersistentStorage
    │
    └── BalanceChanged?.Invoke("soft_currency", newBalance)
```

---

## 4. CurrencyService 实现

```csharp
// scripts/currency/services/CurrencyService.cs

using Godot;

namespace Match3Demo;

public class CurrencyService : ICurrencyService
{
    private readonly IPersistentStorage _storage;
    private readonly EventBus            _eventBus;
    private readonly CurrencyBalance     _balance;

    public event Action<string, int> BalanceChanged;

    private const string SaveKey = "currency_balance";

    public CurrencyService(IPersistentStorage storage, EventBus eventBus)
    {
        _storage  = storage;
        _eventBus = eventBus;
        _balance  = Load();
    }

    public bool CanAfford(string currencyId, int amount)
    {
        if (amount <= 0)
            return false;
        return _balance.Get(currencyId) >= amount;
    }

    public bool Spend(string currencyId, int amount, string reason)
    {
        if (!CanAfford(currencyId, amount))
            return false;

        if (!_balance.TrySubtract(currencyId, amount))
            return false;

        SaveAndNotify(currencyId, _balance.Get(currencyId), -amount);
        LogTransaction("SPEND", currencyId, amount, reason);
        return true;
    }

    public void Grant(string currencyId, int amount, string reason)
    {
        if (amount <= 0)
            return;

        _balance.Add(currencyId, amount);

        SaveAndNotify(currencyId, _balance.Get(currencyId), amount);
        LogTransaction("GRANT", currencyId, amount, reason);
    }

    public int GetBalance(string currencyId)
    {
        return _balance.Get(currencyId);
    }

    public CurrencyBalance GetAllBalances()
    {
        return _balance;
    }

    private CurrencyBalance Load()
    {
        var data = _storage.Load<CurrencySaveData>(SaveKey);
        if (data == null)
            return new CurrencyBalance();

        var balance = new CurrencyBalance();
        foreach (var kv in data.Balances)
        {
            balance.Set(kv.Key, kv.Value);
        }
        return balance;
    }

    private void SaveAndNotify(string currencyId, int newBalance, int delta)
    {
        var saveData = new CurrencySaveData
        {
            Balances = new Dictionary<string, int>(_balance.Balances),
        };
        _storage.Save(SaveKey, saveData);

        BalanceChanged?.Invoke(currencyId, newBalance);
        _eventBus.EmitSignalCurrencyChanged(currencyId, newBalance, delta);
    }

    private static void LogTransaction(string action, string currencyId, int amount, string reason)
    {
        GD.Print($"[Currency] {action} {amount} {currencyId} | reason: {reason}");
    }
}
```

---

## 5. 持久化存储

### 5.1 IPersistentStorage 接口

```csharp
// scripts/services/IPersistentStorage.cs

namespace Match3Demo;

/// <summary>
/// 持久化存储抽象，CurrencyService 只依赖此接口。
/// 实现可以是 JSON 文件、Godot ConfigFile、远程服务器等。
/// </summary>
public interface IPersistentStorage
{
    void Save<T>(string key, T data);
    T    Load<T>(string key) where T : class;
    bool Exists(string key);
    void Delete(string key);
}
```

### 5.2 参考实现：JsonFileStorage

```csharp
// scripts/services/JsonFileStorage.cs

using System.Text.Json;

namespace Match3Demo;

public class JsonFileStorage : IPersistentStorage
{
    private readonly string _basePath;

    public JsonFileStorage(string basePath = "user://saves/")
    {
        _basePath = basePath;
    }

    public void Save<T>(string key, T data)
    {
        using var dir = DirAccess.Open("user://");
        if (dir != null && !dir.DirExists("saves"))
            dir.MakeDir("saves");

        var path  = _basePath + key + ".json";
        var json  = JsonSerializer.Serialize(data);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }

    public T Load<T>(string key) where T : class
    {
        var path = _basePath + key + ".json";
        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = file?.GetAsText();
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<T>(json);
    }

    public bool Exists(string key)
    {
        return FileAccess.FileExists(_basePath + key + ".json");
    }

    public void Delete(string key)
    {
        var path = _basePath + key + ".json";
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(path);
    }
}
```

### 5.3 CurrencySaveData DTO

```csharp
// scripts/currency/models/CurrencySaveData.cs

namespace Match3Demo;

/// <summary>
/// 序列化用的 DTO，包含所有货币余额快照和可选的交易日志。
/// </summary>
public class CurrencySaveData
{
    public Dictionary<string, int> Balances { get; set; } = new();
    public List<TransactionRecord> History  { get; set; } = new();
}

public class TransactionRecord
{
    public string Action     { get; set; } = "";   // "SPEND" or "GRANT"
    public string CurrencyId { get; set; } = "";
    public int    Amount     { get; set; }
    public string Reason     { get; set; } = "";
    public string Timestamp  { get; set; } = "";
}
```

---

## 6. 获取途径

### 6.1 Gameplay 奖励

Match-3 消除操作完成后，Reward 系统根据分数/连击计算货币奖励：

```csharp
// 伪代码：在 Board 完成一轮消除后调用
public void ProcessMatchReward(int score, int combo, int chainDepth)
{
    var baseCoins = score / 10;
    var comboMultiplier = 1.0f + (combo - 1) * 0.25f;
    var chainBonus = chainDepth * 2;

    var totalCoins = (int)(baseCoins * comboMultiplier) + chainBonus;
    totalCoins = Math.Max(1, totalCoins);

    _currencyService.Grant(CurrencyConfig.SoftCurrencyId, totalCoins, "match3_reward");
}
```

### 6.2 重复宠物转换

抽卡系统获得重复宠物时，自动转换为 soft_currency：

```csharp
public void HandleDuplicatePet(PetRarity rarity)
{
    var amount = rarity switch
    {
        PetRarity.Common    => 10,
        PetRarity.Rare      => 50,
        PetRarity.Epic      => 200,
        PetRarity.Legendary => 1000,
        _ => 0,
    };

    if (amount > 0)
        _currencyService.Grant(CurrencyConfig.SoftCurrencyId, amount, "duplicate_pet");
}
```

### 6.3 获取途径汇总表

| 途径 | 货币类型 | 触发条件 | reason |
|------|---------|---------|--------|
| Match-3 消除 | `soft_currency` | 每次有效消除 | `match3_reward` |
| 重复宠物 (Common) | `soft_currency` | 抽到已有宠物 | `duplicate_pet` |
| 重复宠物 (Rare) | `soft_currency` | 抽到已有宠物 | `duplicate_pet` |
| 重复宠物 (Epic) | `soft_currency` | 抽到已有宠物 | `duplicate_pet` |
| 重复宠物 (Legendary) | `soft_currency` | 抽到已有宠物 | `duplicate_pet` |
| IAP 购买 | `hard_currency` | 商店内购 | `iap_purchase` |
| 每日登录 (未来) | `gacha_ticket` | 每日第一次登录 | `daily_login` |
| 成就系统 (未来) | `soft_currency` | 解锁成就 | `achievement` |

---

## 7. EventBus 扩展信号

在现有 `EventBus.cs` 中新增 `CurrencyChanged` 信号：

```csharp
// scripts/autoload/EventBus.cs 中新增：

[Signal] public delegate void CurrencyChangedEventHandler(string currencyId, int newBalance, int delta);
```

`CurrencyService` 调用方式：

```csharp
// CurrencyService.SaveAndNotify 内部调用：
_eventBus.EmitSignalCurrencyChanged(currencyId, newBalance, delta);
```

下游监听示例（Godot-side listener）：

```csharp
// 在任意 Godot Node 中订阅
EventBus.Instance.CurrencyChanged += OnCurrencyChanged;

private void OnCurrencyChanged(string currencyId, int newBalance, int delta)
{
    GD.Print($"Currency {currencyId}: {newBalance} (delta: {delta:+0;-#})");
}
```

---

## 8. UI 集成

### 8.1 CurrencyDisplay 组件

```csharp
// scripts/currency/ui/CurrencyDisplay.cs
using Godot;

namespace Match3Demo;

public partial class CurrencyDisplay : Control
{
    [Export] public string CurrencyId { get; set; } = CurrencyConfig.SoftCurrencyId;

    private Label       _amountLabel;
    private TextureRect _iconRect;
    private int         _displayedValue;
    private Tween       _tween;

    public override void _Ready()
    {
        _iconRect     = GetNode<TextureRect>("Icon");
        _amountLabel  = GetNode<Label>("Amount");
        _displayedValue = EventBus.Instance != null
            ? GetService().GetBalance(CurrencyId)
            : 0;

        UpdateDisplay();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.CurrencyChanged += OnCurrencyChanged;
        }

        ApplyConfig();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.CurrencyChanged -= OnCurrencyChanged;
        }
    }

    private void OnCurrencyChanged(string currencyId, int newBalance, int delta)
    {
        if (currencyId != CurrencyId || delta == 0)
            return;

        AnimateValue(_displayedValue, newBalance);
        _displayedValue = newBalance;
    }

    private void UpdateDisplay()
    {
        _amountLabel.Text = FormatAmount(_displayedValue);
    }

    private void AnimateValue(int from, int to)
    {
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenMethod(
            Callable.From<int>(v =>
            {
                _amountLabel.Text = FormatAmount(v);
            }),
            from,
            to,
            0.3f
        ).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
    }

    private void ApplyConfig()
    {
        var info = CurrencyConfig.Get(CurrencyId);
        if (!string.IsNullOrEmpty(info.IconPath))
        {
            var texture = GD.Load<Texture2D>(info.IconPath);
            if (texture != null)
                _iconRect.Texture = texture;
        }
    }

    private static string FormatAmount(int amount)
    {
        if (amount >= 1_000_000)
            return $"{amount / 1_000_000f:F1}M";
        if (amount >= 1_000)
            return $"{amount / 1_000f:F1}K";
        return amount.ToString();
    }

    private static ICurrencyService GetService()
    {
        // 通过服务定位器获取 (或依赖注入)
        return ServiceLocator.Get<ICurrencyService>();
    }
}
```

### 8.2 ServiceLocator 辅助类

```csharp
// scripts/services/ServiceLocator.cs

namespace Match3Demo;

/// <summary>
/// 简易服务定位器，用于 Godot Node 在运行时获取注册的服务实例。
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T instance)
    {
        _services[typeof(T)] = instance;
    }

    public static T Get<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var instance)
            ? (T)instance
            : null;
    }
}
```

### 8.3 初始化流程

```csharp
// 在 Main 场景的启动脚本中：

public override void _Ready()
{
    // 1. 创建持久化存储
    IPersistentStorage storage = new JsonFileStorage();

    // 2. 创建货币服务
    var currencyService = new CurrencyService(storage, EventBus.Instance);

    // 3. 注册到服务定位器
    ServiceLocator.Register<ICurrencyService>(currencyService);

    // 4. 启动游戏 - UI 组件在 _Ready() 中自动订阅 CurrencyChanged
}
```

---

## 9. 文件结构

```
scripts/
├── autoload/
│   └── EventBus.cs                   # 新增 CurrencyChangedEventHandler 信号
├── currency/
│   ├── models/
│   │   ├── CurrencyType.cs           # 货币类型枚举
│   │   ├── CurrencyConfig.cs         # 静态配置 (ID / 显示名 / 图标路径)
│   │   ├── CurrencyBalance.cs        # POCO 余额数据
│   │   └── CurrencySaveData.cs       # 序列化 DTO + TransactionRecord
│   ├── services/
│   │   ├── ICurrencyService.cs       # 服务接口
│   │   └── CurrencyService.cs        # 实现 (依赖 IPersistentStorage + EventBus)
│   └── ui/
│       └── CurrencyDisplay.cs        # Godot Control，自动订阅余额变化
├── services/
│   ├── IPersistentStorage.cs         # 持久化存储抽象
│   ├── JsonFileStorage.cs            # JSON 文件存储实现
│   └── ServiceLocator.cs             # 简易服务定位器
└── ...
```

---

## 10. 附录：设计决策表

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 服务类型 | 纯 C# 类 (非 Node) | 无场景依赖，可单独实例化测试；与 Godot 生命周期解耦 |
| 事件机制 | C# `event Action<string, int>` + EventBus Godot Signal | 双通道：C# 层用 Action 事件（轻量），Godot UI 层通过 EventBus 信号绑定 |
| 货币标识 | 字符串 ID (`"soft_currency"`) | 可扩展性好，新增货币无需改枚举；字符串在配置表维护，不散落 |
| 整数存储 | `int` balanace | 货币无小数需求，整数避免浮点精度问题；int 范围（21 亿）足够 |
| 持久化时机 | 每次交易后立即保存 | 防止崩溃/强退导致丢币；配合脏标记 + 批量保存优化为未来可选项 |
| 余额非负保护 | `Math.Max(0, amount)` | 防御性编程，防止因逻辑错误出现负余额 |
| `reason` 参数 | 必填 string | 所有交易携带来源/目的标识，用于 analytics 和开发者调试 |
| 接口 vs 抽象类 | `interface` | `ICurrencyService` 仅定义合约，Mock 实现可用于单元测试 |
| 服务定位器 | `ServiceLocator` 静态类 | 简易模式，避免复杂 DI 框架；Godot Node 可通过 `ServiceLocator.Get<T>()` 获取服务 |
| 动画 | Tween `TweenMethod` 数字插值 | Godot 4 原生 Tween API，无需额外库；`CurrencyDisplay` 自带动画 |
| 交易日志 | `TransactionRecord` 可选存储 | 轻量记录，默认仅 print 到控制台；生产环境可扩展为文件/远程日志 |
