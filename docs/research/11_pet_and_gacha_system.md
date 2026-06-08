# 宠物与抽卡系统研究

> 为扩展 Match-3 Cat Puzzle 游戏（Godot 4.6 .NET，C# 12，.NET 8.0）的宠物系统和抽卡/召唤系统所做的研究。重点关注模块化、解耦架构，以及松耦合和可测试性。

---

## 目录

1. [宠物系统架构模式](#1-宠物系统架构模式)
2. [抽卡/召唤系统架构](#2-抽卡召唤系统架构)
3. [模块化架构模式](#3-模块化架构模式)
4. [具体实现建议](#4-具体实现建议)
5. [参考资料与来源](#5-参考资料与来源)

---

## 1. 宠物系统架构模式

### 数据驱动设计

最稳健的宠物/收集系统采用**数据驱动设计**，即将宠物定义与行为逻辑分离。这意味着：

- **宠物定义**存在于数据文件（Godot Resource 或 JSON）中——名称、类型、稀有度、基础属性、精灵引用、进化链
- **运行时实例**引用这些定义并跟踪可变状态（当前等级、经验值、装备的配饰）
- **行为/能力**通过组件或策略模式组合，而非硬编码在宠物类中

```
┌──────────────────────────────────────────────────┐
│                  宠物系统分层                      │
├──────────────────────────────────────────────────┤
│  数据层（定义）                                    │
│  ├─ PetDef.tres       （不可变模板）               │
│  ├─ RarityDef.tres    （属性倍率、颜色）            │
│  └─ AccessoryDef.tres （装饰品定义）               │
├──────────────────────────────────────────────────┤
│  运行时层（实例）                                  │
│  ├─ PetInstance       （ID、等级、经验值、心情）     │
│  └─ PetCollection     （拥有的宠物、活跃槽位）       │
├──────────────────────────────────────────────────┤
│  服务层（逻辑）                                    │
│  ├─ PetManager        （生成、升级、进化）          │
│  ├─ PetLevelCalculator（经验值曲线计算）            │
│  └─ PetSaveData       （序列化）                   │
└──────────────────────────────────────────────────┘
```

**核心原则：** 数据类是 POCO（纯 C# 对象）——不依赖 Godot Node。这使得它们可以轻松进行单元测试。只有管理器/服务层才接触 Godot API（用于信号、资源、文件系统）。

### Godot C# 自定义资源 vs JSON

在 Godot C# 中，宠物定义有两种**可行的方案**：

#### 方案 A：Godot 自定义资源（`[GlobalClass]` Resource）

```csharp
// PetDefinition.cs — 存储为 .tres 文件，位于 res://data/pets/
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class PetDefinition : Resource
{
    [Export] public string Id { get; set; }              // "cat_sleepy_01"
    [Export] public string DisplayName { get; set; }     // "Sleepy Whiskers"
    [Export] public PetType Type { get; set; }           // CAT, DOG, BUNNY 等
    [Export] public PetRarity Rarity { get; set; }       // COMMON, RARE, EPIC, LEGENDARY
    [Export] public int BaseLevel { get; set; } = 1;
    [Export] public int MaxLevel { get; set; } = 50;
    [Export] public Texture2D Icon { get; set; }
    [Export] public Texture2D SpriteSheet { get; set; }
    [Export] public int FrameCount { get; set; } = 4;
    [Export] public string Description { get; set; }
    [Export] public Godot.Collections.Array<PetAbilityDef> Abilities { get; set; }
}

public enum PetType { Cat, Dog, Bunny, Bird, Fox, Bear }
public enum PetRarity { Common, Rare, Epic, Legendary }
```

**优点：**
- 可在编辑器中查看，并能在 Godot Inspector 中编辑
- 类型安全——属性在编译时检查
- 支持嵌套 Resource（能力、进化链）
- `.tres` 格式对版本控制友好（基于文本）
- 内置序列化，无需编写解析代码
- Godot 的资源缓存（`GD.Load`）可防止重复加载

**缺点：**
- 需要 Godot 编辑器或代码来创建/编辑
- .tres 文件必须遵循特定格式
- 在没有编辑器工具的情况下难以批量编辑

#### 方案 B：JSON + C# 反序列化

```csharp
// PetDefinitionJson.cs — 从 res://data/pets.json 加载数据
using System.Text.Json;

namespace Match3Demo;

public class PetDefinitionJson
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Type { get; set; }
    public string Rarity { get; set; }
    public int BaseLevel { get; set; }
    public int MaxLevel { get; set; }
    public string IconPath { get; set; }     // "res://assets/pets/cat_01.png"
    public string[] AbilityIds { get; set; }
}
```

**优点：**
- 可通过脚本或电子表格轻松生成/修改
- 跨平台、跨工具兼容
- 可从服务器加载以进行实时更新
- 标准 .NET `System.Text.Json` 完美适用

**缺点：**
- 无编辑器可视化
- 基于字符串的引用容易产生拼写错误
- 需要手动编写加载/解析代码
- 必须处理资源的路径解析

#### 推荐的混合方案

使用 **Godot 自定义资源进行开发/创作**，并使用**可选的 JSON 导出**用于服务器端更新：

```csharp
// IPetDataSource.cs — 抽象层
public interface IPetDataSource
{
    PetDefinition GetPetDefinition(string id);
    IEnumerable<PetDefinition> GetAllPets();
}

// ResourcePetDataSource.cs — 从 .tres 文件加载
public class ResourcePetDataSource : IPetDataSource
{
    private Dictionary<string, PetDefinition> _cache = new();

    public PetDefinition GetPetDefinition(string id)
    {
        if (_cache.TryGetValue(id, out var def))
            return def;

        def = GD.Load<PetDefinition>($"res://data/pets/{id}.tres");
        _cache[id] = def;
        return def;
    }
}

// JsonPetDataSource.cs — 从 JSON 清单加载（用于服务器/实时运营）
public class JsonPetDataSource : IPetDataSource { /* ... */ }
```

### 宠物状态管理

**关注点分离**——不可变定义与可变状态之间的分离：

```csharp
// 不可变 — 运行时永不改变
public class PetDefinition { /* 同上 */ }

// 可变 — 每个玩家的实例状态
public class PetInstance
{
    public string Id { get; set; }              // 唯一实例 GUID
    public string PetDefId { get; set; }         // 引用 PetDefinition.Id
    public int Level { get; set; } = 1;
    public int CurrentXP { get; set; } = 0;
    public int NextLevelXP => LevelCalculator.XPForLevel(Level + 1);
    public bool IsFavorite { get; set; }
    public string Nickname { get; set; }
    public string EquippedAccessoryId { get; set; }
    public DateTime AcquiredAt { get; set; }
}
```

**PetCollection** 管理拥有关系：

```csharp
public class PetCollection
{
    public List<PetInstance> OwnedPets { get; set; } = new();
    public string ActivePetId { get; set; }         // 主屏幕展示的宠物
    public int MaxSlots { get; set; } = 50;

    public PetInstance AddPet(string petDefId)
    {
        var pet = new PetInstance
        {
            Id = Guid.NewGuid().ToString(),
            PetDefId = petDefId,
            AcquiredAt = DateTime.UtcNow
        };
        OwnedPets.Add(pet);
        return pet;
    }

    public bool HasPet(string petDefId)
        => OwnedPets.Any(p => p.PetDefId == petDefId);

    public int GetDuplicateCount(string petDefId)
        => OwnedPets.Count(p => p.PetDefId == petDefId);
}
```

### 进化/升级系统

经验值曲线公式应放在**纯静态类**中以便于测试：

```csharp
public static class PetLevelCalculator
{
    // 通用经验值曲线：XP(n) = 基础值 * n^指数
    // 示例：1→2 级需要 10 经验值，49→50 级需要 2,401 经验值
    public static int XPForLevel(int level, int baseXP = 10, double exponent = 1.5)
    {
        return (int)(baseXP * Math.Pow(level, exponent));
    }

    public static int TotalXPForLevel(int level, int baseXP = 10)
    {
        int total = 0;
        for (int i = 1; i < level; i++)
            total += XPForLevel(i, baseXP);
        return total;
    }

    // 基于稀有度的属性倍率
    public static float RarityStatMultiplier(PetRarity rarity) => rarity switch
    {
        PetRarity.Common => 1.0f,
        PetRarity.Rare => 1.3f,
        PetRarity.Epic => 1.6f,
        PetRarity.Legendary => 2.0f,
        _ => 1.0f
    };
}
```

对于**进化**，在 PetDefinition 中定义进化链：

```csharp
[GlobalClass]
public partial class EvolutionStep : Resource
{
    [Export] public string EvolvesToDefId { get; set; }   // 进化为哪个宠物
    [Export] public int RequiredLevel { get; set; }        // 等级要求
    [Export] public int RequiredDuplicates { get; set; }   // 需要多少重复宠物
    [Export] public string RequiredItemId { get; set; }    // 特殊进化道具
}
```

---

## 2. 抽卡/召唤系统架构

### 概率表与稀有度

任何抽卡系统的核心是**加权随机选择**算法。将其设计为纯的、可测试的服务：

```csharp
/// <summary>
/// 表示抽卡池中的单个条目。
/// 不可变 — 定义一次，多次使用。
/// </summary>
public class GachaPoolEntry
{
    public string RewardId { get; init; }        // PetDefId 或 AccessoryDefId
    public RewardType Type { get; init; }         // 宠物或配饰
    public PetRarity Rarity { get; init; }
    public double Weight { get; init; }           // 用于选择的基础权重
}

/// <summary>
/// 定义抽卡卡池的概率分布。
/// </summary>
public class GachaBanner
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public List<GachaPoolEntry> Pool { get; init; } = new();
    public int CostPerPull { get; init; } = 50;      // 货币消耗

    // 保底系统配置
    public int SoftPityStart { get; init; } = 70;    // 软保底起始抽数
    public int HardPityGuarantee { get; init; } = 90;// 硬保底 — 必定获得 SSR
    public double SoftPityRateIncrease { get; init; } = 0.06; // 软保底后每次抽卡概率增幅

    // 限定 UP 率
    public string RateUpRewardId { get; init; }       // 卡池的 UP 物品
    public double RateUpChanceOnSSR { get; init; } = 0.5; // SSR 命中时的 50/50 概率

    public Dictionary<PetRarity, double> GetRarityWeights()
    {
        var weights = new Dictionary<PetRarity, double>();
        foreach (var entry in Pool)
        {
            if (!weights.ContainsKey(entry.Rarity))
                weights[entry.Rarity] = 0;
            weights[entry.Rarity] += entry.Weight;
        }
        return weights;
    }
}
```

#### 加权随机选择算法

用于许多抽卡游戏（原神、明日方舟等）：

```csharp
/// <summary>
/// 纯服务 — 不依赖 Godot。完全可单元测试。
/// </summary>
public class GachaRollService
{
    private readonly System.Random _rng;

    public GachaRollService(int? seed = null)
    {
        _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
    }

    /// <summary>
    /// 在给定卡池上执行一次单抽。
    /// </summary>
    public GachaRollResult Roll(GachaBanner banner, GachaPityState pityState)
    {
        // 1. 检查硬保底
        if (pityState.PullsSinceLastSSR >= banner.HardPityGuarantee - 1)
            return ForceRarity(banner, pityState, PetRarity.Legendary);

        // 2. 使用软保底加成计算稀有度
        PetRarity rolledRarity = RollRarity(banner, pityState);

        // 3. 从稀有度池中选择具体物品
        var candidates = banner.Pool
            .Where(e => e.Rarity == rolledRarity)
            .ToList();

        string rewardId;
        if (rolledRarity == PetRarity.Legendary && banner.RateUpRewardId != null)
        {
            // 50/50 UP 检查
            rewardId = _rng.NextDouble() < banner.RateUpChanceOnSSR
                ? banner.RateUpRewardId
                : candidates[_rng.Next(candidates.Count)].RewardId;
        }
        else
        {
            rewardId = candidates[_rng.Next(candidates.Count)].RewardId;
        }

        // 4. 更新保底状态
        var newPity = pityState with
        {
            TotalPulls = pityState.TotalPulls + 1,
            PullsSinceLastSSR = rolledRarity == PetRarity.Legendary
                ? 0
                : pityState.PullsSinceLastSSR + 1
        };

        var entry = candidates.First(e => e.RewardId == rewardId);
        return new GachaRollResult(rewardId, entry.Type, rolledRarity, newPity);
    }

    private PetRarity RollRarity(GachaBanner banner, GachaPityState pity)
    {
        double baseSSRWeight = banner.Pool
            .Where(e => e.Rarity == PetRarity.Legendary)
            .Sum(e => e.Weight);

        double softPityBonus = 0;
        if (pity.PullsSinceLastSSR >= banner.SoftPityStart)
        {
            int overSoftPity = pity.PullsSinceLastSSR - banner.SoftPityStart + 1;
            softPityBonus = overSoftPity * banner.SoftPityRateIncrease;
        }

        double adjustedSSRWeight = baseSSRWeight + softPityBonus;

        // 限制权重范围然后进行随机选择
        double totalWeight = banner.Pool.Sum(e => e.Rarity == PetRarity.Legendary
            ? adjustedSSRWeight * (e.Weight / baseSSRWeight)
            : e.Weight);

        double roll = _rng.NextDouble() * totalWeight;
        // ... （遍历累计分布以确定稀有度）
        return PetRarity.Common; // 简化处理
    }
}
```

#### 常见稀有度表参考

| 稀有度     | 典型基础概率 | 池大小 | 示例颜色      |
|------------|-------------|--------|---------------|
| Common     | 70-80%      | 30-50  | 灰色/白色     |
| Rare       | 15-25%      | 20-30  | 蓝色          |
| Epic       | 4-8%        | 10-15  | 紫色          |
| Legendary  | 0.6-2%      | 5-10   | 金色/橙色     |

### 保底系统

**保底系统**确保运气不佳的玩家最终也能获得高稀有度物品。关键概念：

#### 软保底（Soft Pity）
在连续若干次抽卡未获得高稀有度物品后，每次后续抽卡的概率**逐渐递增**。以原神为例：
- 基础五星概率：0.6%
- 第 73 抽之后，每次抽卡概率增加约 6%
- 到第 89 抽时，概率接近 100%（第 90 抽必定获得）

#### 硬保底（Hard Pity）
达到固定抽数时，高稀有度物品**必定获得**。这是一个简单的计数器检查。

#### 50/50 系统（UP 保底）
当你抽到高稀有度物品时：
- 第一次：50% 概率获得卡池 UP 物品，50% 概率获得随机物品
- 如果 50/50 失败，则**下一次**高稀有度物品 100% 必定为 UP 物品

```csharp
/// <summary>
/// 不可变 record — 非常适合跟踪保底状态。
/// 使用 C# 12 record struct 实现零分配复制。
/// </summary>
public readonly record struct GachaPityState(
    int TotalPulls,
    int PullsSinceLastSSR,
    int PullsSinceLastEpic,
    bool GuaranteedRateUpNext    // 上一次 50/50 失败
);

/// <summary>
/// 单次抽卡的结果 — 不可变数据，无副作用。
/// </summary>
public record GachaRollResult(
    string RewardId,
    RewardType Type,
    PetRarity Rarity,
    GachaPityState NewPityState
);
```

### 货币集成

使用**货币服务接口**将抽卡系统与货币解耦：

```csharp
/// <summary>
/// 货币扣除被抽象化 — 抽卡系统不知道货币来自何处。
/// 这使得以下场景成为可能：赚取的货币、购买的货币、免费抽卡、抽卡券等。
/// </summary>
public interface ICurrencyService
{
    bool CanAfford(string currencyId, int amount);
    bool Spend(string currencyId, int amount, string reason);
    void Grant(string currencyId, int amount, string reason);
    int GetBalance(string currencyId);

    // 用于 UI 更新的事件
    event Action<string, int> BalanceChanged;
}

/// <summary>
/// 抽卡系统依赖于 ICurrencyService，而非具体实现。
/// </summary>
public class GachaDrawService
{
    private readonly ICurrencyService _currency;
    private readonly GachaRollService _roller;
    private readonly IDataSource<GachaBanner> _banners;
    private readonly IPetCollectionService _petCollection;
    private readonly EventBus _eventBus;

    public GachaDrawService(
        ICurrencyService currency,
        GachaRollService roller,
        IDataSource<GachaBanner> banners,
        IPetCollectionService petCollection,
        EventBus eventBus)
    {
        _currency = currency;
        _roller = roller;
        _banners = banners;
        _petCollection = petCollection;
        _eventBus = eventBus;
    }

    public GachaRollResult PerformPull(string bannerId, GachaPityState pity)
    {
        var banner = _banners.Get(bannerId);
        if (!_currency.Spend("soft_currency", banner.CostPerPull, $"gacha_pull_{bannerId}"))
            throw new InvalidOperationException("货币不足");

        var result = _roller.Roll(banner, pity);

        // 发放奖励
        if (result.Type == RewardType.Pet)
        {
            _petCollection.AddPet(result.RewardId);
        }
        // else: AddAccessory(result.RewardId)

        // 触发事件用于 UI 动画
        _eventBus.EmitSignal(EventBus.SignalName.GachaPullResult,
            result.RewardId, (int)result.Rarity, result.NewPityState);

        return result;
    }
}
```

### 解耦的抽卡设计

使用**依赖注入**的完整架构：

```
┌─────────────────────────────────────────────────────────────┐
│                    抽卡系统架构                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  UI 层（Godot Node）                                       │
│  ├─ GachaBannerUI  ──读取──>  GachaBannerDisplayData       │
│  ├─ PullAnimation  ──响应──>  GachaPullResult 信号         │
│  └─ CurrencyDisplay ──监听──> ICurrencyService 事件        │
│                         │                                   │
│  服务层（纯 C# + Godot 信号）                               │
│  ├─ GachaDrawService  ◄── ICurrencyService                 │
│  │    ├── GachaRollService（纯，可测试）                    │
│  │    └── GachaPityTracker  （纯，可测试）                  │
│  └─ PetCollectionService ◄── IPetCollectionService          │
│                         │                                   │
│  数据层（Resource / JSON）                                  │
│  ├─ GachaBanner.tres / .json                                │
│  ├─ GachaPoolEntry（嵌入 banner 中）                        │
│  └─ PetDefinition.tres / .json                              │
│                                                             │
│  横切关注点                                                  │
│  ├─ EventBus（Godot Autoload，基于信号）                    │
│  └─ SaveDataService（IPersistentStorage）                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. 模块化架构模式

### 基于信号的解耦（EventBus）

项目已经使用了 EventBus 模式（参见 `scripts/autoload/EventBus.cs`）。这是**主要的解耦机制**，应扩展以支持宠物/抽卡系统：

```csharp
// 添加到现有的 EventBus.cs 中

[Signal] public delegate void PetAcquiredEventHandler(string petDefId);
[Signal] public delegate void PetLeveledUpEventHandler(string petInstanceId, int newLevel);
[Signal] public delegate void PetEvolvedEventHandler(string oldPetInstanceId, string newPetDefId);

[Signal] public delegate void GachaPullResultEventHandler(
    string rewardId, int rarity, Godot.Collections.Dictionary pityState);
[Signal] public delegate void GachaPityMilestoneEventHandler(int pullsTowardGuarantee);

[Signal] public delegate void CurrencyChangedEventHandler(
    string currencyId, int newBalance, int delta);
[Signal] public delegate void AccessoryEquippedEventHandler(
    string petInstanceId, string accessoryId);
```

**使用模式**（任何系统都可以监听，无需知道来源）：

```csharp
// 在 PetDisplayUI.cs 中
public override void _Ready()
{
    EventBus.Instance.PetAcquired += OnPetAcquired;
    EventBus.Instance.PetLeveledUp += OnPetLeveledUp;
}

private void OnPetAcquired(string petDefId)
{
    // 显示获取动画 — 无需知道宠物是如何获取的
    ShowAcquisitionPopup(petDefId);
}

// 在 AchievementSystem.cs 中
public override void _Ready()
{
    EventBus.Instance.PetAcquired += CheckCollectionAchievements;
    EventBus.Instance.GachaPullResult += CheckGachaAchievements;
}
```

**核心原则：** 系统发出关于*发生了什么*的事件，而非*该做什么*。监听者决定如何响应。这是 GoF 四人帮中的观察者模式，通过 Godot 信号实现。

### 基于 Godot Resource 的架构

Godot 的 `Resource` 类型相当于 Unity 的 `ScriptableObject`。对于宠物/抽卡系统，Resource 提供：

1. **编辑器友好的数据创作** — 设计者可以在 Inspector 中调整数值
2. **内置序列化** — 保存/加载为 `.tres`（文本）或 `.res`（二进制）
3. **引用计数** — Godot 自动处理内存
4. **嵌套** — Resource 可以包含其他 Resource

```csharp
// 示例：完整的卡池定义为嵌套 Resource
[GlobalClass]
public partial class GachaBannerResource : Resource
{
    [Export] public string BannerId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public int CostPerPull { get; set; } = 50;
    [Export] public int SoftPityStart { get; set; } = 70;
    [Export] public int HardPity { get; set; } = 90;

    [Export]
    public Godot.Collections.Array<GachaPoolEntryResource> Pool { get; set; }
}

[GlobalClass]
public partial class GachaPoolEntryResource : Resource
{
    [Export] public string RewardId { get; set; }
    [Export] public PetRarity Rarity { get; set; }
    [Export] public double Weight { get; set; } = 1.0;
    [Export] public bool IsRateUp { get; set; }
    [Export] public RewardType Type { get; set; }
}
```

**重要的 C# 注意：** 始终为 Resource 提供**无参构造函数**：

```csharp
public GachaBannerResource() : this("", "", 50) {}
public GachaBannerResource(string id, string name, int cost)
{
    BannerId = id;
    DisplayName = name;
    CostPerPull = cost;
    Pool = new Godot.Collections.Array<GachaPoolEntryResource>();
}
```

### 插件/功能系统模式

对于模块化、可扩展的游戏，考虑使用**功能注册模式**。每个主要系统（消消乐核心、宠物系统、抽卡、商店）将自己注册为一个功能：

```csharp
/// <summary>
/// 每个游戏功能实现此接口。
/// Godot autoload 扫描并初始化所有功能。
/// </summary>
public interface IGameFeature
{
    string FeatureName { get; }
    int Priority { get; }             // 越低 = 越早初始化
    void Initialize(IServiceRegistry services);
    void Shutdown();
}

/// <summary>
/// 简单的服务容器 — 无需框架的手动 DI。
/// </summary>
public class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T instance) where T : class
        => _services[typeof(T)] = instance;

    public T Get<T>() where T : class
        => _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
}

/// <summary>
/// 示例：宠物系统作为注册的功能。
/// </summary>
public class PetFeature : IGameFeature
{
    public string FeatureName => "PetSystem";
    public int Priority => 10;  // 在消消乐核心（优先级 0）之后

    public void Initialize(IServiceRegistry services)
    {
        var dataSource = new ResourcePetDataSource();
        services.Register<IPetDataSource>(dataSource);
        services.Register<IPetCollectionService>(new PetCollectionService(dataSource));
    }

    public void Shutdown() { /* 清理 */ }
}
```

### Godot 中的依赖注入

Godot 没有内置的 DI 容器，但 C# 模式可以很好地使用：

#### 模式 1：手动构造函数注入（推荐）

最适合可测试性和显式性：

```csharp
// 服务通过构造函数注入
public class GachaDrawService
{
    private readonly ICurrencyService _currency;
    private readonly GachaRollService _roller;

    public GachaDrawService(ICurrencyService currency, GachaRollService roller)
    {
        _currency = currency;
        _roller = roller;
    }
}

// 在 GameMain.cs（或 Autoload）中引导
public partial class ServiceInitializer : Node
{
    public override void _Ready()
    {
        var registry = new ServiceRegistry();

        // 创建核心服务
        var currency = new CurrencyService(new SaveDataService());
        var roller = new GachaRollService();

        // 连接并注册
        var gacha = new GachaDrawService(currency, roller);
        registry.Register<GachaDrawService>(gacha);
        registry.Register<ICurrencyService>(currency);
    }
}
```

#### 模式 2：通过 Autoload 的服务定位器（项目已使用此模式）

项目目前通过 `EventBus.Instance` 和 `GameData.Instance` 使用此模式。这种方式更简单但更难测试：

```csharp
// 当前模式：静态 Instance 访问
EventBus.Instance.EmitSignal(...);
GameData.Instance.AddScore(100);

// 扩展：PetSystem Autoload
public partial class PetSystem : Node
{
    public static PetSystem Instance { get; private set; }

    public override void _EnterTree() { Instance = this; }
}
```

#### 模式 3：混合模式（推荐用于本项目）

对**众所周知的全局服务**（EventBus、GameData）使用**单例**，对**新的模块化系统**（宠物、抽卡、商店）使用**构造函数注入**：

```csharp
// 全局单例（已建立的模式）
public partial class EventBus : Node { public static EventBus Instance ... }

// 可注入的服务（新系统的新模式）
public class PetCollectionService : IPetCollectionService
{
    private readonly IPetDataSource _dataSource;
    private readonly EventBus _eventBus;  // 无静态依赖 — 注入

    public PetCollectionService(IPetDataSource dataSource, EventBus eventBus)
    {
        _dataSource = dataSource;
        _eventBus = eventBus;
    }

    public PetInstance AddPet(string petDefId)
    {
        // ... 逻辑 ...
        _eventBus.EmitSignal(EventBus.SignalName.PetAcquired, petDefId);
    }
}
```

---

## 4. 具体实现建议

### 本项目的推荐架构

基于现有代码库（autoload 中的 EventBus、GameData、AudioManager；core/ 中的消消乐逻辑），以下是推荐的文件结构：

```
scripts/
├── autoload/
│   ├── EventBus.cs          [现有 — 扩展宠物/抽卡信号]
│   ├── GameData.cs           [现有 — 添加货币、宠物收集状态]
│   ├── AudioManager.cs       [现有]
│   └── ServiceInitializer.cs [新增 — 引导并连接所有服务]
│
├── core/                     [现有 — 消消乐核心，不要修改]
│
├── pets/                     [新增 — 宠物系统]
│   ├── data/
│   │   ├── PetDefinition.cs         [GlobalClass Resource]
│   │   ├── PetAbilityDef.cs         [GlobalClass Resource]
│   │   ├── EvolutionStep.cs         [GlobalClass Resource]
│   │   ├── RarityDef.cs             [GlobalClass Resource]
│   │   └── IPetDataSource.cs
│   ├── models/
│   │   ├── PetInstance.cs           [纯 C# POCO — 不依赖 Godot]
│   │   ├── PetCollection.cs         [纯 C# POCO]
│   │   ├── PetSaveData.cs           [序列化 DTO]
│   │   └── PetType.cs, PetRarity.cs [枚举]
│   ├── services/
│   │   ├── IPetCollectionService.cs
│   │   ├── PetCollectionService.cs
│   │   ├── PetLevelCalculator.cs    [静态，纯数学]
│   │   └── PetSaveService.cs
│   └── ui/                          [Godot 场景 + 脚本]
│       ├── PetCollectionPanel.cs
│       ├── PetDetailPopup.cs
│       └── PetLevelUpAnimation.cs
│
├── gacha/                    [新增 — 抽卡系统]
│   ├── data/
│   │   ├── GachaBannerResource.cs   [GlobalClass Resource]
│   │   ├── GachaPoolEntryResource.cs [GlobalClass Resource]
│   │   └── GachaBannerData.cs       [纯 C# DTO，用于运行时]
│   ├── models/
│   │   ├── GachaRollResult.cs       [Record — 不可变]
│   │   ├── GachaPityState.cs        [Record struct — 不可变]
│   │   └── RewardType.cs            [枚举]
│   ├── services/
│   │   ├── GachaRollService.cs      [纯 C# — 不依赖 Godot，完全可测试]
│   │   ├── GachaDrawService.cs      [编排抽卡 + 货币 + 发放]
│   │   └── GachaPityTracker.cs      [跟踪并持久化保底状态]
│   └── ui/
│       ├── GachaBannerUI.cs
│       ├── PullAnimation.cs
│       └── RarityRevealEffect.cs
│
├── currency/                 [新增 — 货币系统]
│   ├── models/
│   │   ├── CurrencyType.cs
│   │   └── CurrencyBalance.cs
│   ├── services/
│   │   ├── ICurrencyService.cs
│   │   └── CurrencyService.cs
│   └── ui/
│       └── CurrencyDisplay.cs
│
├── accessories/              [新增 — 装饰/配饰系统]
│   ├── data/
│   │   └── AccessoryDefinition.cs
│   ├── models/
│   │   └── AccessoryInstance.cs
│   └── services/
│       └── AccessoryService.cs
│
├── game/                     [现有]
├── ui/                       [现有 — 添加宠物/抽卡 UI 面板]
├── fx/                       [现有 — 添加稀有度/获取特效]
└── utils/                    [现有 — 添加 WeightedRandom、SaveHelper]

tests/                        [新增 — 单元测试]
├── Match3CoreTests/
├── PetSystemTests/
│   ├── PetLevelCalculatorTests.cs
│   └── PetCollectionServiceTests.cs
└── GachaSystemTests/
    ├── GachaRollServiceTests.cs
    ├── GachaPityTrackerTests.cs
    └── GachaDrawServiceTests.cs

data/                         [新增 — 游戏数据 Resource]
├── pets/
│   ├── cat_sleepy_01.tres
│   ├── cat_playful_02.tres
│   └── ...
├── gacha/
│   ├── standard_banner.tres
│   ├── limited_banner.tres
│   └── ...
├── accessories/
│   ├── hat_tophat.tres
│   ├── bow_red.tres
│   └── ...
└── rarities/
    ├── common.tres
    ├── rare.tres
    ├── epic.tres
    └── legendary.tres
```

### 架构图（ASCII）

```
+-------------------------------------------------------------+
|                     GODOT 场景树                             |
|  /root/                                                     |
|  ├── EventBus（Autoload）    ← 信号分发中心                  |
|  ├── GameData（Autoload）    ← 全局状态                      |
|  ├── ServiceInitializer      ← 手动 DI 引导器                |
|  ├── AudioManager（Autoload）                                |
|  └── main_scene.tscn                                         |
|       ├── Board（Node2D）     ← 消消乐核心                   |
|       ├── HUD（CanvasLayer）  ← 分数、步数、货币              |
|       ├── PetPanel（Control） ← 宠物收集 UI                  |
|       └── GachaUI（Control）  ← 抽卡卡池/抽取 UI            |
+-------------------------------------------------------------+

+-------------------------------------------------------------+
|                 服务层（纯 C#）                             |
|                                                             |
|  ┌──────────────────┐    ┌─────────────────┐               |
|  │ GachaDrawService │───>│ GachaRollService│ （纯）         |
|  │  （编排器）       │    │  （概率计算）    │               |
|  └───────┬──────────┘    └─────────────────┘               |
|          │                                                  |
|  ┌───────▼──────────┐    ┌─────────────────┐               |
|  │ ICurrencyService │    │ PetCollection   │               |
|  │  （扣除/发放）     │    │ Service         │               |
|  └───────┬──────────┘    └────────┬────────┘               |
|          │                        │                         |
|  ┌───────▼──────────┐    ┌───────▼─────────┐               |
|  │ SaveDataService  │    │ IPetDataSource  │               |
|  │  （持久化）       │    │  （定义数据）    │               |
|  └──────────────────┘    └─────────────────┘               |
|                                                             |
+-------------------------------------------------------------+
                       │
                       │  EmitSignal / EventBus
                       ▼
+-------------------------------------------------------------+
|                 EVENT BUS（解耦层）                         |
|  PetAcquired、PetLeveledUp、GachaPullResult、                |
|  CurrencyChanged、AccessoryEquipped 等                       |
+-------------------------------------------------------------+
                       │
           ┌───────────┼───────────┐
           ▼           ▼           ▼
     [成就系统]   [数据分析]   [UI 更新]
     （监听并响应，与源系统无耦合）
```

### 代码示例：关键模式

#### 模式 1：不可变数据 + 纯逻辑 = 可测试

```csharp
// GachaRollService — 100% 纯 C#，零 Godot 依赖
// 可使用任何 .NET 测试框架（xUnit、NUnit）进行测试
public class GachaRollService
{
    private readonly System.Random _rng;

    public GachaRollService(int seed) => _rng = new System.Random(seed);

    public GachaRollResult Roll(GachaBanner banner, GachaPityState pity)
    {
        // 纯计算 — 无副作用，返回新状态
        // ...
    }
}

// 单元测试（xUnit 示例）
[Fact]
public void HardPity_Guarantees_SSR()
{
    var service = new GachaRollService(42);
    var banner = CreateTestBanner(hardPity: 90);
    var pity = new GachaPityState(TotalPulls: 89, PullsSinceLastSSR: 89, ...);

    var result = service.Roll(banner, pity);

    Assert.Equal(PetRarity.Legendary, result.Rarity);
}
```

#### 模式 2：信号驱动的 UI 更新

```csharp
// GachaUI.cs — 响应事件，从不直接调用服务获取结果
public partial class GachaUI : Control
{
    public override void _Ready()
    {
        EventBus.Instance.GachaPullResult += OnPullResult;
    }

    private async void OnPullResult(string rewardId, int rarity, Dictionary pity)
    {
        // 根据稀有度播放揭示动画
        await PlayRevealAnimation((PetRarity)rarity);
        ShowRewardCard(rewardId, (PetRarity)rarity);
    }

    private void OnPullButtonPressed()
    {
        // UI 仅请求操作，不处理结果
        _gachaDrawService.PerformPull(_activeBannerId, _currentPity);
    }
}
```

#### 模式 3：基于接口的持久化

```csharp
/// <summary>
/// 抽象化保存/加载 — 可在文件、云端或内存（测试用）之间切换
/// </summary>
public interface IPersistentStorage
{
    Task<T> LoadAsync<T>(string key) where T : class;
    Task SaveAsync<T>(string key, T data) where T : class;
    bool Exists(string key);
}

// 基于文件的实现，使用 Godot API
public class GodotFileStorage : IPersistentStorage
{
    public async Task<T> LoadAsync<T>(string key) where T : class
    {
        var path = $"user://saves/{key}.json";
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        return JsonSerializer.Deserialize<T>(json);
    }
}

// 用于测试的内存实现
public class InMemoryStorage : IPersistentStorage
{
    private readonly Dictionary<string, object> _store = new();
    // ...
}
```

### 测试策略

由于服务是纯 C# 且依赖被注入：

```csharp
// GachaDrawServiceTests.cs — 使用 xUnit + Moq 或手动伪造对象
[Fact]
public void PerformPull_DeductsCurrency_And_GrantsPet()
{
    var currency = new FakeCurrencyService(initialBalance: 500);
    var roller = new GachaRollService(seed: 123);
    var banners = new FakeBannerDataSource();
    var pets = new FakePetCollectionService();
    var eventBus = new FakeEventBus();

    var service = new GachaDrawService(currency, roller, banners, pets, eventBus);

    var result = service.PerformPull("standard_banner", new GachaPityState());

    Assert.Equal(450, currency.GetBalance("soft_currency")); // 500 - 50
    Assert.True(pets.HasPet(result.RewardId));
}
```

---

## 5. 参考资料与来源

### Godot 官方文档

1. **Resource（自定义 Resource 模式）**：
   https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html
   - 使用 `[GlobalClass]` 在 C# 中创建自定义 Resource
   - 编辑器集成、序列化、嵌套
   - 等同于 Unity ScriptableObject

2. **C# 信号**：
   https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_signals.html
   - `[Signal]` 委托特性
   - `+=` / `-=` 事件订阅模式
   - `EmitSignal()` 用于发射
   - Lambda 捕获与自动断开连接

3. **单例（Autoload）**：
   https://docs.godotengine.org/en/stable/tutorials/scripting/singletons_autoload.html
   - 全局服务的 Autoload 模式
   - C# 静态 `Instance` 属性模式
   - 通过延迟调用切换场景

4. **C# 基础**：
   https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
   - 项目设置，.NET SDK 要求
   - `GD.Print`、`GD.Load`、`Mathf` 静态 API
   - NuGet 包集成

5. **C# API 差异**：
   https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_differences.html
   - PascalCase API 约定
   - `[Export]` 特性用于属性
   - 带 `EventHandler` 后缀的信号委托

6. **Resource 类参考**：
   https://docs.godotengine.org/en/stable/classes/class_resource.html
   - C# Resource 的 `[GlobalClass]` 特性
   - `emit_changed()` 用于属性变更通知
   - `resource_local_to_scene` 用于每个实例的复制

### 《游戏编程模式》（Robert Nystrom）

7. **观察者模式**：
   https://gameprogrammingpatterns.com/observer.html
   - 事件驱动系统的核心解耦模式
   - 主题-观察者列表、通知遍历
   - 已失效监听者问题及解决方案
   - 现代方法：函数/方法引用替代观察者类

8. **组件模式**：
   https://gameprogrammingpatterns.com/component.html
   - 通过拆分单体实体来解耦领域
   - 组件间通信模式：
     - 共享容器状态（位置、速度）
     - 直接引用（物理组件与图形组件通信）
     - 消息传递（通过容器作为中介）

9. **服务定位器模式**：
   https://gameprogrammingpatterns.com/service-locator.html
   - 将服务消费者与具体实现解耦
   - 空服务模式用于优雅降级
   - 装饰器模式用于日志/调试
   - 编译时与运行时绑定的权衡

### 抽卡系统设计（行业实践）

10. **原神抽卡机制**（社区记录）：
    - 基础五星概率：0.6%
    - 软保底：第 74 抽开始，每次抽卡概率约增加 6%
    - 硬保底：第 90 抽必定获得
    - 50/50 系统：首次 SSR 为硬币正反面；失败则下一次必定为 UP

11. **加权随机选择**：
    - 累积分布函数（CDF）方法
    - 别名方法用于大量物品的 O(1) 选择
    - 对于抽卡：简单的 CDF 遍历足够（池大小 < 100）

12. **概率与保底系统**（Game Developer Magazine）：
    https://www.gamedeveloper.com/design/math-for-game-players-gacha-probability-and-gambler-s-fallacy
    - 有保底时获取特定 SSR 的期望抽数：约 62.5 抽（对比无保底的 166 抽）
    - 设计中需注意赌徒谬误
    - 法律合规的透明度考量

### 开源 Godot C# 项目

13. **CSharpGodotTools/Template**（167 星）：
    https://github.com/CSharpGodotTools/Template
    - Godot 4 C# 游戏模板，含 ENet 多人联机
    - 可视化游戏内调试
    - C# 项目结构的良好参考

14. **CSharpGodotTools/GodotUtils**（79 星）：
    https://github.com/CSharpGodotTools/GodotUtils
    - 不断扩展的 Godot 游戏 C# 工具库
    - 扩展方法、辅助工具、模式

15. **3ddelano/epic-online-services-godot**（296 星）：
    https://github.com/3ddelano/epic-online-services-godot
    - GitHub 上按主题统计最大的 Godot C# 项目
    - 面向服务的架构，含 EOS 集成

### 其他资源

16. **Godot 4 C# Discord/论坛**：
    - 活跃的 C# 专属 Godot 问题社区
    - 搜索：Godot Discord 中的 `#csharp` 频道

17. **非 ASP.NET 应用中的 .NET 依赖注入**：
    https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection
    - `Microsoft.Extensions.DependencyInjection` NuGet 包
    - 可在 Godot 项目中用于 DI 容器

18. **用于 Godot 的 System.Text.Json**：
    - .NET 8 内置 — 无需外部 NuGet
    - 源生成序列化器用于 AOT 兼容
    - `[JsonSerializable]` 特性用于裁剪安全

---

## 附录：快速参考卡片

### 何时使用每种模式

| 模式                        | 使用场景                               | 不使用场景                           |
|----------------------------|---------------------------------------|-------------------------------------|
| Godot Resource（.tres）     | 在 Godot 编辑器中创作/编辑的数据        | 从电子表格生成的数据                  |
| JSON + System.Text.Json    | 来自服务器、外部工具的数据              | 编辑器可视化的数据                    |
| EventBus 信号               | 跨系统通信                             | 单个模块内部                         |
| 构造函数 DI                 | 具有明确依赖的服务                     | 简单的一次性类                        |
| Autoload 单例               | 真正的全局服务（音频、EventBus）        | 功能特定状态                         |
| Record 类型（C# 12）        | 不可变数据（结果、状态快照）            | 可变运行时状态                       |
| 接口抽象                    | 可测试、可替换的实现                    | 内部实现细节                         |

### 本架构的测试金字塔

```
         ┌──────┐
         │ E2E  │  ← Godot 场景测试（GUT 或手动）
         ├──────┤
         │ 集成  │  ← 服务集成测试（xUnit + 伪造对象）
         ├──────┤
         │ 单元  │  ← 纯 C# 逻辑测试（xUnit，无 Godot）
         └──────┘
```

- **单元测试（70%）**：PetLevelCalculator、GachaRollService、GachaPityTracker
- **集成测试（20%）**：GachaDrawService + FakeCurrencyService
- **E2E 测试（10%）**：Godot 场景中的完整抽卡流程
