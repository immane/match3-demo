# 抽卡（Gacha/Draw）系统设计

> 基于纯概率数学的抽卡系统，通过接口与宠物系统、货币系统解耦，包含完整的保底（Pity）机制和十连抽设计。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 参考 | `pet_system.md` — 宠物定义、PetInstance、IPetCollectionService |
| ← 参考 | `currency_system.md` — ICurrencyService、soft_currency |
| ↔ 同级 | [architecture.md](architecture.md) — EventBus 信号流、ServiceInitializer DI 引导 |
| → 实现 | `scripts/gacha/` — 完整 Gacha 模块文件结构 |

---

---

## 目录

1. [概述与设计目标](#1-概述与设计目标)
2. [数据模型](#2-数据模型)
3. [概率系统详解](#3-概率系统详解)
4. [保底系统（Pity System）](#4-保底系统pity-system)
5. [服务层](#5-服务层)
6. [抽卡流程时序图](#6-抽卡流程时序图)
7. [多抽（十连抽）设计](#7-多抽十连抽设计)
8. [EventBus 扩展信号](#8-eventbus-扩展信号)
9. [文件结构](#9-文件结构)
10. [附录](#10-附录)

---

## 1. 概述与设计目标

### 1.1 系统定位

Gacha 系统是游戏的核心商业化与留存机制，玩家消耗游戏内货币（soft_currency）从限定卡池（Banner）中抽取宠物和饰品。

### 1.2 设计目标

| 目标 | 说明 |
|------|------|
| **纯概率数学** | `GachaRollService` 零 Godot 依赖，100% xUnit 可测试 |
| **系统解耦** | 通过接口注入 ICurrencyService、IPetCollectionService，不直接依赖具体实现 |
| **公平透明** | Soft Pity / Hard Pity / 50:50 Rate-Up 三重保底，概率线性提升 |
| **编辑器友好** | `GachaBannerResource` / `GachaPoolEntryResource` 为 `[GlobalClass] Resource`，可在 Godot 编辑器中直接配置卡池 |
| **持久化** | 保底状态通过 IPersistentStorage 按卡池 ID 分别存储，重启不丢失 |

### 1.3 核心流程

```
玩家点击抽卡按钮
    │
    ▼
检查货币 (ICurrencyService)
    │
    ▼
概率抽取 (GachaRollService)
    │
    ├─ 稀有度判定 (Rarity Roll)
    ├─ 池内角色判定 (Pool Select)
    └─ 保底修正 (Pity Adjustment)
    │
    ▼
发放奖励 (IPetCollectionService)
    │
    ▼
更新保底状态 (GachaPityTracker)
    │
    ▼
发送信号 (EventBus) → UI 播放动画
```

---

## 2. 数据模型

### 2.1 RewardType 枚举

```csharp
// scripts/gacha/models/RewardType.cs
namespace Match3Demo;

public enum RewardType
{
    Pet,
    Accessory
}
```

### 2.2 PetRarity 枚举

```csharp
// scripts/gacha/models/PetRarity.cs
namespace Match3Demo;

public enum PetRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3
}
```

### 2.3 GachaPoolEntry（纯数据类）

```csharp
// scripts/gacha/data/GachaBannerData.cs (部分)
namespace Match3Demo;

public class GachaPoolEntry
{
    public string RewardId { get; init; }
    public RewardType RewardType { get; init; }
    public PetRarity Rarity { get; init; }
    public double Weight { get; init; }
}
```

### 2.4 GachaBanner（运行时 DTO）

```csharp
// scripts/gacha/data/GachaBannerData.cs
namespace Match3Demo;

public class GachaBanner
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public List<GachaPoolEntry> Pool { get; init; } = new();
    public int CostPerPull { get; init; } = 150;
    public int SoftPityStart { get; init; } = 75;
    public int HardPityGuarantee { get; init; } = 90;
    public double SoftPityRateIncrease { get; init; } = 0.05;
    public string RateUpRewardId { get; init; }
    public double RateUpChanceOnSSR { get; init; } = 0.5;

    /// <summary>
    /// 按稀有度分组，返回各稀有度的总权重。
    /// 用于稀有度判定阶段：先摇稀有度，再在对应稀有度池内摇具体奖励。
    /// </summary>
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

    /// <summary>
    /// 获取指定稀有度池内的所有条目列表。
    /// </summary>
    public List<GachaPoolEntry> GetPoolByRarity(PetRarity rarity)
    {
        return Pool.Where(e => e.Rarity == rarity).ToList();
    }
}
```

### 2.5 GachaPoolEntryResource（编辑器资源）

```csharp
// scripts/gacha/data/GachaPoolEntryResource.cs
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class GachaPoolEntryResource : Resource
{
    [Export] public string RewardId { get; set; }
    [Export] public RewardType RewardType { get; set; }
    [Export] public PetRarity Rarity { get; set; }
    [Export(PropertyHint.Range, "0,100,or_greater")] public double Weight { get; set; } = 1.0;

    public GachaPoolEntry ToRuntime()
    {
        return new GachaPoolEntry
        {
            RewardId = RewardId,
            RewardType = RewardType,
            Rarity = Rarity,
            Weight = Weight
        };
    }
}
```

### 2.6 GachaBannerResource（编辑器资源）

```csharp
// scripts/gacha/data/GachaBannerResource.cs
using Godot;
using Godot.Collections;

namespace Match3Demo;

[GlobalClass]
public partial class GachaBannerResource : Resource
{
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public Array<GachaPoolEntryResource> PoolEntries { get; set; } = new();
    [Export] public int CostPerPull { get; set; } = 150;
    [Export(PropertyHint.Range, "1,90")] public int SoftPityStart { get; set; } = 75;
    [Export(PropertyHint.Range, "1,90")] public int HardPityGuarantee { get; set; } = 90;
    [Export(PropertyHint.Range, "0,1,0.01")] public double SoftPityRateIncrease { get; set; } = 0.05;
    [Export] public string RateUpRewardId { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public double RateUpChanceOnSSR { get; set; } = 0.5;

    public GachaBanner ToRuntime()
    {
        var banner = new GachaBanner
        {
            Id = Id,
            DisplayName = DisplayName,
            CostPerPull = CostPerPull,
            SoftPityStart = SoftPityStart,
            HardPityGuarantee = HardPityGuarantee,
            SoftPityRateIncrease = SoftPityRateIncrease,
            RateUpRewardId = RateUpRewardId,
            RateUpChanceOnSSR = RateUpChanceOnSSR
        };

        foreach (var entry in PoolEntries)
            banner.Pool.Add(entry.ToRuntime());

        return banner;
    }
}
```

### 2.7 GachaPityState（不可变记录结构体）

```csharp
// scripts/gacha/models/GachaPityState.cs
namespace Match3Demo;

public readonly record struct GachaPityState
{
    /// <summary>该卡池累计抽数</summary>
    public int TotalPulls { get; init; }

    /// <summary>距离上一次 SSR（Legendary）的抽数，>= 0</summary>
    public int PullsSinceLastSSR { get; init; }

    /// <summary>距离上一次 Epic 的抽数，>= 0</summary>
    public int PullsSinceLastEpic { get; init; }

    /// <summary>下一次 SSR 是否100%为当期 UP 角色（50:50 落败后担保）</summary>
    public bool GuaranteedRateUpNext { get; init; }

    public static GachaPityState Default => new()
    {
        TotalPulls = 0,
        PullsSinceLastSSR = 0,
        PullsSinceLastEpic = 0,
        GuaranteedRateUpNext = false
    };
}
```

### 2.8 GachaRollResult（不可变记录）

```csharp
// scripts/gacha/models/GachaRollResult.cs
namespace Match3Demo;

public record GachaRollResult
{
    public string RewardId { get; init; }
    public RewardType RewardType { get; init; }
    public PetRarity Rarity { get; init; }
    public GachaPityState NewPityState { get; init; }
    public bool IsRateUp { get; init; }
    public bool IsHardPity { get; init; }
}
```

### 2.9 GachaPitySaveData（持久化 DTO）

```csharp
// scripts/gacha/services/GachaPityTracker.cs (部分)
namespace Match3Demo;

public class GachaPitySaveData
{
    public string BannerId { get; set; }
    public int TotalPulls { get; set; }
    public int PullsSinceLastSSR { get; set; }
    public int PullsSinceLastEpic { get; set; }
    public bool GuaranteedRateUpNext { get; set; }

    public GachaPityState ToState()
    {
        return new GachaPityState
        {
            TotalPulls = TotalPulls,
            PullsSinceLastSSR = PullsSinceLastSSR,
            PullsSinceLastEpic = PullsSinceLastEpic,
            GuaranteedRateUpNext = GuaranteedRateUpNext
        };
    }

    public static GachaPitySaveData FromState(string bannerId, GachaPityState state)
    {
        return new GachaPitySaveData
        {
            BannerId = bannerId,
            TotalPulls = state.TotalPulls,
            PullsSinceLastSSR = state.PullsSinceLastSSR,
            PullsSinceLastEpic = state.PullsSinceLastEpic,
            GuaranteedRateUpNext = state.GuaranteedRateUpNext
        };
    }
}
```

---

## 3. 概率系统详解

### 3.1 加权随机选择算法（CDF 累积分布函数）

```csharp
// scripts/gacha/services/GachaRollService.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class GachaRollService
{
    private readonly Random _rng;

    public GachaRollService(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// 从加权条目列表中按 CDF（累积分布函数）随机选择一项。
    /// 时间复杂度 O(n)，空间复杂度 O(1)。
    /// </summary>
    public T SelectWeighted<T>(List<T> entries, Func<T, double> weightSelector)
    {
        if (entries == null || entries.Count == 0)
            throw new ArgumentException("Entries must not be null or empty.");

        var totalWeight = entries.Sum(weightSelector);
        if (totalWeight <= 0)
            throw new InvalidOperationException("Total weight must be > 0.");

        double roll = _rng.NextDouble() * totalWeight;
        double cumulative = 0;

        foreach (var entry in entries)
        {
            cumulative += weightSelector(entry);
            if (roll <= cumulative)
                return entry;
        }

        // 浮点精度回退：返回最后一个
        return entries[^1];
    }
}
```

### 3.2 默认稀有度概率表

| 稀有度 | 权重 | 基础概率 | 保底规则 |
|--------|------|---------|---------|
| Common | 700 | 70.00% | 无 |
| Rare | 220 | 22.00% | 10 连保底至少 1 个 Rare+ |
| Epic | 60 | 6.00% | 无独立保底，由 Soft Pity 覆盖 |
| Legendary (SSR) | 20 | 2.00% | Soft Pity（75 抽起）+ Hard Pity（90 抽必出） |

### 3.3 两阶段抽取流程

```
┌─────────────────────────────────────────┐
│  阶段一：稀有度判定 (Rarity Roll)          │
│                                         │
│  GetRarityWeights() → CDF 随机摇取稀有度   │
│  - 应用 Soft Pity 修正 SSR 权重           │
│  - 若 Hard Pity 触发，直接返回 Legendary   │
│                                         │
│  输入: GachaBanner, GachaPityState      │
│  输出: PetRarity                         │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  阶段二：稀有度池内选择 (Pool Select)       │
│                                         │
│  GetPoolByRarity(rarity) → CDF 随机选择  │
│  - 若为 Legendary：处理 50:50 Rate-Up    │
│  - 若 GuaranteedRateUpNext=true：       │
│    强制返回 RateUpRewardId              │
│  输入: GachaBanner, PetRarity           │
│  输出: GachaPoolEntry                   │
└─────────────────────────────────────────┘
```

### 3.4 完整 Roll 实现

```csharp
// scripts/gacha/services/GachaRollService.cs (续)

/// <summary>
/// 主抽卡方法：一次完整抽取，返回 GachaRollResult。
/// 纯数学计算，无 I/O 无 Godot 依赖，100% 可单元测试。
/// </summary>
public GachaRollResult Roll(GachaBanner banner, GachaPityState pityState)
{
    // 阶段一：稀有度判定
    var rarity = RollRarity(banner, pityState);

    bool isHardPity = false;
    bool isRateUp = false;

    // Hard Pity 判定：90 抽内无 SSR → 强制 Legendary
    if (pityState.PullsSinceLastSSR >= banner.HardPityGuarantee - 1)
    {
        rarity = PetRarity.Legendary;
        isHardPity = true;
    }

    // 阶段二：池内选择
    var entry = SelectFromPool(banner, rarity, pityState, out isRateUp);

    // 构建新的保底状态
    var newPityState = BuildNewPityState(pityState, rarity);

    return new GachaRollResult
    {
        RewardId = entry.RewardId,
        RewardType = entry.RewardType,
        Rarity = rarity,
        NewPityState = newPityState,
        IsRateUp = isRateUp,
        IsHardPity = isHardPity
    };
}

/// <summary>
/// 稀有度判定：使用保底修正后的权重进行 CDF 抽取。
/// </summary>
public PetRarity RollRarity(GachaBanner banner, GachaPityState pityState)
{
    var baseWeights = banner.GetRarityWeights();
    var adjustedWeights = new Dictionary<PetRarity, double>(baseWeights);

    // Soft Pity：连续抽数超过 SoftPityStart 后，SSR 权重线性递增
    if (pityState.PullsSinceLastSSR >= banner.SoftPityStart)
    {
        var pullsOver = pityState.PullsSinceLastSSR - banner.SoftPityStart + 1;
        var ssrIncrease = pullsOver * banner.SoftPityRateIncrease;

        double currentSSRWeight = baseWeights.GetValueOrDefault(PetRarity.Legendary, 0);
        adjustedWeights[PetRarity.Legendary] = currentSSRWeight + ssrIncrease;
    }

    // 使用调整后的权重做 CDF 选择
    var rarityEntries = adjustedWeights
        .Select(kvp => new { Rarity = kvp.Key, Weight = kvp.Value })
        .ToList();

    var selected = SelectWeighted(rarityEntries, e => e.Weight);
    return selected.Rarity;
}

/// <summary>
/// 从指定稀有度池中选择具体奖励，处理 50:50 Rate-Up 逻辑。
/// </summary>
public GachaPoolEntry SelectFromPool(
    GachaBanner banner,
    PetRarity rarity,
    GachaPityState pityState,
    out bool isRateUp)
{
    isRateUp = false;

    // Legendary 稀有度：处理 50:50 Rate-Up
    if (rarity == PetRarity.Legendary && !string.IsNullOrEmpty(banner.RateUpRewardId))
    {
        if (pityState.GuaranteedRateUpNext)
        {
            // 100% 保底：强制返回当期 UP
            isRateUp = true;
            var upEntry = banner.Pool.FirstOrDefault(e =>
                e.RewardId == banner.RateUpRewardId && e.Rarity == PetRarity.Legendary);
            if (upEntry != null)
                return upEntry;
        }
        else
        {
            // 50:50 判定
            var dice = _rng.NextDouble();
            if (dice < banner.RateUpChanceOnSSR)
            {
                isRateUp = true;
                var upEntry = banner.Pool.FirstOrDefault(e =>
                    e.RewardId == banner.RateUpRewardId && e.Rarity == PetRarity.Legendary);
                if (upEntry != null)
                    return upEntry;
            }
        }
    }

    // 普通稀有度池内加权选择
    var pool = banner.Pool.Where(e => e.Rarity == rarity).ToList();
    if (pool.Count == 0)
    {
        // 回退：选最大稀有度池
        var maxRarity = banner.Pool.Max(e => e.Rarity);
        pool = banner.Pool.Where(e => e.Rarity == maxRarity).ToList();
    }

    return SelectWeighted(pool, e => e.Weight);
}

/// <summary>
/// 根据抽取结果构建新的保底状态。
/// </summary>
private static GachaPityState BuildNewPityState(
    GachaPityState prev, PetRarity rolledRarity)
{
    var next = new GachaPityState
    {
        TotalPulls = prev.TotalPulls + 1,
        PullsSinceLastEpic = prev.PullsSinceLastEpic + 1,
        PullsSinceLastSSR = prev.PullsSinceLastSSR + 1,
        GuaranteedRateUpNext = prev.GuaranteedRateUpNext
    };

    if (rolledRarity == PetRarity.Legendary)
    {
        next.PullsSinceLastSSR = 0;
        // 若是非 UP 的 SSR → 下次必 UP
        next.GuaranteedRateUpNext = !prev.GuaranteedRateUpNext;
    }
    else
    {
        // 掉率 UP 时，GuaranteedRateUpNext 不会因为普通 SSR 改变
        // 仅在 Legendary 且非 UP 时设置
    }

    if (rolledRarity == PetRarity.Epic || rolledRarity == PetRarity.Legendary)
    {
        next.PullsSinceLastEpic = 0;
    }

    return next;
}

/// <summary>
/// 强制指定稀有度抽取（用于调试 / Hard Pity 触发）。
/// </summary>
public GachaPoolEntry ForceRarity(GachaBanner banner, PetRarity rarity)
{
    var pool = banner.Pool.Where(e => e.Rarity == rarity).ToList();
    if (pool.Count == 0)
        throw new ArgumentException($"No entries in pool for rarity: {rarity}");
    return SelectWeighted(pool, e => e.Weight);
}
```

### 3.5 Soft Pity 概率公式

```
调整后 SSR 权重 = 基础 SSR 权重 + (当前距上次 SSR 抽数 - SoftPityStart + 1) × 每次增加量

示例（基础 SSR 权重 = 20，SoftPityStart = 75，RateIncrease = 0.05）：

第 75 抽（距 SSR 75 抽）：20 + (75 - 75 + 1) × 0.05 = 20.05
第 80 抽（距 SSR 80 抽）：20 + (80 - 75 + 1) × 0.05 = 20.30
第 89 抽（距 SSR 89 抽）：20 + (89 - 75 + 1) × 0.05 = 20.75
第 90 抽 → Hard Pity 触发，强制 Legendary
```

---

## 4. 保底系统（Pity System）

### 4.1 三种保底机制对照表

| 保底类型 | 触发条件 | 效果 | 重置条件 |
|---------|---------|------|---------|
| **Soft Pity** | PullsSinceLastSSR >= SoftPityStart（默认 75） | SSR 权重线性递增 | 抽出 SSR |
| **Hard Pity** | PullsSinceLastSSR >= HardPityGuarantee（默认 90） | 强制出 SSR | 抽出 SSR |
| **50:50 Rate-Up** | 上次 SSR 非 UP（落败） | 下次 SSR 100% 为 UP 角色 | 抽出 UP SSR |

### 4.2 保底状态流转图

```
                    ┌─────────────────────┐
                    │    开始抽卡          │
                    │  TotalPulls = 0     │
                    └──────────┬──────────┘
                               │
                 ┌─────────────▼──────────────┐
                 │   PullsSinceLastSSR 计数    │
                 │   < SoftPityStart (75)     │
                 │   默认概率 2.0%             │
                 └─────────────┬──────────────┘
                               │
                 ┌─────────────▼──────────────┐
                 │   PullsSinceLastSSR >= 75  │
                 │   Soft Pity 激活            │
                 │   SSR 权重线性递增          │
                 └─────────────┬──────────────┘
                               │
                   ┌───────────┴───────────┐
                   │                       │
              SSR 抽出              PullsSinceLastSSR = 89,
                   │               下一抽必 SSR
                   │                       │
         ┌─────────▼──────────┐   ┌────────▼────────┐
         │  50:50 Rate-Up     │   │  Hard Pity 触发　│
         │  判断是否 UP 角色    │   │  强制出 SSR      │
         └─────────┬──────────┘   └────────┬────────┘
                   │                       │
          ┌────────┴────────┐     ┌────────▼────────┐
          ▼                 ▼     │  同左：进入      │
    UP 角色抽出      非UP角色抽出  │  50:50 判定      │
    Guaranteed=false   Guaranteed  │                 │
    重置SSR计数        =true      │                 │
                             │    └─────────────────┘
                             │
                    下次SSR必UP
```

### 4.3 十连保底（10-Pull Guarantee）

十连抽中至少保证一个 **Rare+**（Rare / Epic / Legendary）奖励。若前 9 抽均为 Common，第 10 抽强制跳过 Common 池，仅在 Rare / Epic / Legendary 池中加权抽取。

实现见 §7 多抽设计。

---

## 5. 服务层

### 5.1 GachaRollService（纯 C#，零 Godot 依赖）

完整实现见 §3.4。服务对外 API：

```csharp
// 主抽取
GachaRollResult Roll(GachaBanner banner, GachaPityState pityState);

// 稀有度判定（公开，便于单元测试各保底分支）
PetRarity RollRarity(GachaBanner banner, GachaPityState pityState);

// 指定稀有度强制抽取
GachaPoolEntry ForceRarity(GachaBanner banner, PetRarity rarity);

// 加权选择（泛型方法，可复用）
T SelectWeighted<T>(List<T> entries, Func<T, double> weightSelector);
```

**可测试性**：所有方法均为纯函数。通过传入固定 seed 的 Random 可完全复现概率结果。xUnit 测试示例见 §10.3。

### 5.2 GachaDrawService（Orchestrator）

```csharp
// scripts/gacha/services/GachaDrawService.cs
using System;
using System.Collections.Generic;

namespace Match3Demo;

public class GachaDrawService
{
    private readonly GachaRollService _rollService;
    private readonly GachaPityTracker _pityTracker;
    private readonly IPetCollectionService _petCollection;
    private readonly ICurrencyService _currencyService;
    private readonly Func<string, GachaBanner> _bannerLoader;

    public GachaDrawService(
        GachaRollService rollService,
        GachaPityTracker pityTracker,
        IPetCollectionService petCollection,
        ICurrencyService currencyService,
        Func<string, GachaBanner> bannerLoader)
    {
        _rollService = rollService ?? throw new ArgumentNullException(nameof(rollService));
        _pityTracker = pityTracker ?? throw new ArgumentNullException(nameof(pityTracker));
        _petCollection = petCollection ?? throw new ArgumentNullException(nameof(petCollection));
        _currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
        _bannerLoader = bannerLoader ?? throw new ArgumentNullException(nameof(bannerLoader));
    }

    /// <summary>
    /// 执行单次抽取。
    /// </summary>
    public GachaRollResult PerformPull(string bannerId)
    {
        var banner = _bannerLoader(bannerId);
        if (banner == null)
            throw new InvalidOperationException($"Banner not found: {bannerId}");

        var pityState = _pityTracker.GetPityState(bannerId);

        // 1. 检查货币
        if (!_currencyService.HasEnough("soft_currency", banner.CostPerPull))
            throw new InvalidOperationException("Insufficient currency for gacha pull.");

        // 2. 扣除货币
        _currencyService.Spend("soft_currency", banner.CostPerPull);

        // 3. 执行抽取
        var result = _rollService.Roll(banner, pityState);

        // 4. 发放奖励
        GrantReward(result);

        // 5. 更新保底状态
        _pityTracker.UpdatePityState(bannerId, result.NewPityState);

        // 6. 发送信号
        EmitPullResult(result);

        // 7. 发送保底里程碑信号
        EmitPityMilestone(result, banner);

        return result;
    }

    /// <summary>
    /// 执行 N 连抽（通常为 10 连）。
    /// </summary>
    public List<GachaRollResult> PerformMultiPull(string bannerId, int count = 10)
    {
        var banner = _bannerLoader(bannerId);
        if (banner == null)
            throw new InvalidOperationException($"Banner not found: {bannerId}");

        // 预先检查总货币
        int totalCost = banner.CostPerPull * count;
        if (!_currencyService.HasEnough("soft_currency", totalCost))
            throw new InvalidOperationException(
                $"Insufficient currency for {count}x pull. Need {totalCost}.");

        var results = new List<GachaRollResult>(count);

        for (int i = 0; i < count; i++)
        {
            var pityState = _pityTracker.GetPityState(bannerId);
            GachaRollResult result;

            // 十连保底：前 count-1 抽无 Rare+ → 最后一抽保底
            if (i == count - 1 && !HasRarityAbove(results, PetRarity.Common))
            {
                result = GuaranteedRarePlusPull(banner, pityState);
            }
            else
            {
                result = _rollService.Roll(banner, pityState);
            }

            // 扣除货币（仅第一次扣全部，后续不重复扣）
            if (i == 0)
                _currencyService.Spend("soft_currency", totalCost);

            GrantReward(result);
            _pityTracker.UpdatePityState(bannerId, result.NewPityState);
            EmitPullResult(result);
            results.Add(result);
        }

        // 发送多抽结果汇总信号
        EmitMultiPullResult(results);

        return results;
    }

    /// <summary>十连保底：在 Rare+ 稀有度池中强制抽一抽。</summary>
    private GachaRollResult GuaranteedRarePlusPull(
        GachaBanner banner, GachaPityState pityState)
    {
        var rarePlusPool = banner.Pool
            .Where(e => e.Rarity != PetRarity.Common)
            .ToList();

        var entry = _rollService.SelectWeighted(rarePlusPool, e => e.Weight);
        var rarity = entry.Rarity;

        // Hard Pity 判定
        bool isHardPity = false;
        if (pityState.PullsSinceLastSSR >= banner.HardPityGuarantee - 1)
        {
            rarity = PetRarity.Legendary;
            isHardPity = true;
            entry = _rollService.ForceRarity(banner, PetRarity.Legendary);
        }

        var newPityState = new GachaPityState
        {
            TotalPulls = pityState.TotalPulls + 1,
            PullsSinceLastSSR = rarity == PetRarity.Legendary ? 0 : pityState.PullsSinceLastSSR + 1,
            PullsSinceLastEpic = (rarity == PetRarity.Epic || rarity == PetRarity.Legendary)
                ? 0 : pityState.PullsSinceLastEpic + 1,
            GuaranteedRateUpNext = pityState.GuaranteedRateUpNext
        };

        return new GachaRollResult
        {
            RewardId = entry.RewardId,
            RewardType = entry.RewardType,
            Rarity = rarity,
            NewPityState = newPityState,
            IsRateUp = false,
            IsHardPity = isHardPity
        };
    }

    private static bool HasRarityAbove(List<GachaRollResult> results, PetRarity threshold)
    {
        foreach (var r in results)
        {
            if ((int)r.Rarity > (int)threshold)
                return true;
        }
        return false;
    }

    private void GrantReward(GachaRollResult result)
    {
        switch (result.RewardType)
        {
            case RewardType.Pet:
                _petCollection.AddPet(result.RewardId);
                break;
            case RewardType.Accessory:
                // 饰品发放通过 IPetCollectionService 或独立 IAccessoryService
                // 此处预留扩展
                break;
        }
    }

    #region EventBus 信号发送

    private static void EmitPullResult(GachaRollResult result)
    {
        EventBus.Instance.EmitSignal(
            EventBus.SignalName.GachaPullResult,
            result.RewardId,
            (int)result.Rarity,
            new Godot.Collections.Dictionary
            {
                ["totalPulls"] = result.NewPityState.TotalPulls,
                ["pullsSinceLastSSR"] = result.NewPityState.PullsSinceLastSSR,
                ["pullsSinceLastEpic"] = result.NewPityState.PullsSinceLastEpic,
                ["guaranteedRateUpNext"] = result.NewPityState.GuaranteedRateUpNext
            });
    }

    private static void EmitPityMilestone(GachaRollResult result, GachaBanner banner)
    {
        int pullsToward = banner.HardPityGuarantee - result.NewPityState.PullsSinceLastSSR;
        EventBus.Instance.EmitSignal(
            EventBus.SignalName.GachaPityMilestone,
            pullsToward);
    }

    private static void EmitMultiPullResult(List<GachaRollResult> results)
    {
        var array = new Godot.Collections.Array();
        foreach (var r in results)
        {
            array.Add(new Godot.Collections.Dictionary
            {
                ["rewardId"] = r.RewardId,
                ["rarity"] = (int)r.Rarity,
                ["rewardType"] = (int)r.RewardType,
                ["isRateUp"] = r.IsRateUp,
                ["isHardPity"] = r.IsHardPity
            });
        }
        EventBus.Instance.EmitSignal(EventBus.SignalName.GachaMultiPullResult, array);
    }

    #endregion
}
```

### 5.3 服务依赖接口定义（预留）

```csharp
// 货币服务接口
public interface ICurrencyService
{
    bool HasEnough(string currencyId, int amount);
    void Spend(string currencyId, int amount);
    int GetBalance(string currencyId);
}

// 宠物收集服务接口
public interface IPetCollectionService
{
    void AddPet(string petDefId);
    bool HasPet(string petDefId);
}
```

### 5.4 GachaPityTracker

```csharp
// scripts/gacha/services/GachaPityTracker.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Match3Demo;

public class GachaPityTracker
{
    private readonly IPersistentStorage _storage;
    private readonly Dictionary<string, GachaPityState> _cache;
    private const string SaveKeyPrefix = "gacha_pity_";

    public GachaPityTracker(IPersistentStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _cache = new Dictionary<string, GachaPityState>();
    }

    /// <summary>获取指定卡池的当前保底状态。</summary>
    public GachaPityState GetPityState(string bannerId)
    {
        if (_cache.TryGetValue(bannerId, out var cached))
            return cached;

        // 尝试从持久化存储加载
        var loaded = _storage.Get<GachaPitySaveData>(SaveKeyPrefix + bannerId);
        if (loaded != null)
        {
            var state = loaded.ToState();
            _cache[bannerId] = state;
            return state;
        }

        return GachaPityState.Default;
    }

    /// <summary>更新指定卡池的保底状态（内存，需调用 SaveAsync 持久化）。</summary>
    public void UpdatePityState(string bannerId, GachaPityState newState)
    {
        _cache[bannerId] = newState;
    }

    /// <summary>重置指定卡池的保底状态。</summary>
    public void ResetPityState(string bannerId)
    {
        _cache[bannerId] = GachaPityState.Default;
    }

    /// <summary>将所有卡池保底状态持久化到磁盘。</summary>
    public async Task SaveAsync()
    {
        foreach (var (bannerId, state) in _cache)
        {
            var saveData = GachaPitySaveData.FromState(bannerId, state);
            await _storage.SetAsync(SaveKeyPrefix + bannerId, saveData);
        }
    }

    /// <summary>从持久化存储加载所有卡池保底状态到内存。</summary>
    public async Task LoadAsync()
    {
        var keys = await _storage.GetKeysAsync(SaveKeyPrefix);
        foreach (var key in keys)
        {
            var saveData = _storage.Get<GachaPitySaveData>(key);
            if (saveData != null)
            {
                _cache[saveData.BannerId] = saveData.ToState();
            }
        }
    }
}
```

### 5.5 IPersistentStorage 接口定义（预留）

```csharp
public interface IPersistentStorage
{
    T Get<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value) where T : class;
    Task<List<string>> GetKeysAsync(string prefix);
}
```

### 5.6 ServiceInitializer 注册示例

```csharp
// 在 ServiceInitializer 中注册 Gacha 相关服务
public static class GachaServiceRegistration
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<GachaRollService>();
        services.AddSingleton<GachaPityTracker>();
        services.AddSingleton<GachaDrawService>();
        // bannerLoader: 从 GameData 或 IDataSource<GachaBanner> 获取
        services.AddSingleton<Func<string, GachaBanner>>(sp =>
        {
            var dataSource = sp.GetRequiredService<IDataSource<GachaBanner>>();
            return bannerId => dataSource.GetById(bannerId);
        });
    }
}
```

---

## 6. 抽卡流程时序图

```
  UI                GachaDrawService     ICurrencyService    GachaRollService   IPetCollection    GachaPityTracker    EventBus
  │                      │                     │                   │                  │                  │               │
  │   Tap "抽卡" 按钮    │                     │                   │                  │                  │               │
  │─────────────────────►│                     │                   │                  │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │  PerformPull("banner_001")             │                  │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │  HasEnough("soft_currency", 150)       │                  │                  │               │
  │                      │────────────────────►│                   │                  │                  │               │
  │                      │        true         │                   │                  │                  │               │
  │                      │◄────────────────────│                   │                  │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │  Spend("soft_currency", 150)           │                  │                  │               │
  │                      │────────────────────►│                   │                  │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │                     │  Roll(banner, pityState)            │                  │               │
  │                      │─────────────────────────────────────────►│                  │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │                     │          ┌────────┴────────┐         │                  │               │
  │                      │                     │          │ RollRarity()    │         │                  │               │
  │                      │                     │          │ Soft Pity 修正   │         │                  │               │
  │                      │                     │          │ CDF 加权选择     │         │                  │               │
  │                      │                     │          └────────┬────────┘         │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │                     │          ┌────────┴────────┐         │                  │               │
  │                      │                     │          │ SelectFromPool()│         │                  │               │
  │                      │                     │          │ 50:50 Rate-Up   │         │                  │               │
  │                      │                     │          └────────┬────────┘         │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │        GachaRollResult (RewardId, Rarity, NewPityState)    │                  │               │
  │                      │◄─────────────────────────────────────────│                  │                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │  AddPet(rewardId)   │                   │                  │                  │               │
  │                      │──────────────────────────────────────────────────────────►│                  │               │
  │                      │                     │                   │                  │                  │               │
  │                      │  UpdatePityState("banner_001", newState)│                  │                  │               │
  │                      │─────────────────────────────────────────────────────────────────────────────►│               │
  │                      │                     │                   │                  │                  │               │
  │                      │  GachaPullResult(rewardId, rarity, pityDict)               │                  │               │
  │                      │─────────────────────────────────────────────────────────────────────────────────────────────►│
  │                      │                     │                   │                  │                  │               │
  │  ◄── 动画播放 ──────► │                     │                   │                  │                  │               │
  │  (抽卡动画展示稀有度)  │                     │                   │                  │                  │               │
```

---

## 7. 多抽（十连抽）设计

### 7.1 十连抽规则

| 规则 | 说明 |
|------|------|
| **货币检查** | 一次性检查 10 次抽卡所需总货币，而非逐次检查 |
| **保底继承** | 每次单抽都继承上一次的保底状态（PullsSinceLastSSR 持续累加） |
| **十连保底** | 若前 9 抽全部为 Common（Rare+ 未出现），第 10 抽保底 Rare+ |
| **十连保底与 Hard Pity 共存** | 第 10 抽保底时仍检查 Hard Pity，若触发则出 SSR |
| **批量 UI 展示** | 10 个结果显示完成后发送 `GachaMultiPullResult` 汇总信号，UI 一次性展示 |

### 7.2 PerformMultiPull 流程

```
  PerformMultiPull("banner_001", 10)
      │
      ├─ 加载 GachaBanner
      ├─ 检查 soft_currency >= CostPerPull × 10
      ├─ 扣除总货币
      │
      ├─ for i in 0..9:
      │     │
      │     ├─ 获取当前保底状态
      │     ├─ if i == 9 && 前 9 抽无 Rare+:
      │     │     调用 GuaranteedRarePlusPull()（Rare+ 保底）
      │     │     if 同时触发 Hard Pity → 强制 Legendary
      │     ├─ else:
      │     │     调用 Roll(banner, pityState)
      │     ├─ 发放奖励 GrantReward()
      │     ├─ 更新保底状态
      │     └─ 收集结果到 List<GachaRollResult>
      │
      └─ EmitMultiPullResult(results) → UI 批量展示
```

### 7.3 多抽结果汇总信号

`GachaMultiPullResult` 信号的 `Array` 中包含 10 个 `Dictionary`，每个结构为：

| 字段 | 类型 | 说明 |
|------|------|------|
| `rewardId` | `string` | 奖励 ID |
| `rarity` | `int` | 稀有度枚举值 |
| `rewardType` | `int` | 奖励类型枚举值 |
| `isRateUp` | `bool` | 是否当期 UP |
| `isHardPity` | `bool` | 是否硬保底触发 |

---

## 8. EventBus 扩展信号

在 `EventBus.cs` 中新增以下信号：

```csharp
// scripts/autoload/EventBus.cs (追加)

/// <summary>单次抽卡结果</summary>
[Signal]
public delegate void GachaPullResultEventHandler(
    string rewardId,
    int rarity,
    Godot.Collections.Dictionary pityState);

/// <summary>保底里程碑更新（距离约定的抽数）</summary>
[Signal]
public delegate void GachaPityMilestoneEventHandler(
    int pullsTowardGuarantee);

/// <summary>多抽（十连）汇总结果</summary>
[Signal]
public delegate void GachaMultiPullResultEventHandler(
    Godot.Collections.Array results);
```

### 信号订阅示例

```csharp
// 在 UI 脚本中订阅
public override void _Ready()
{
    EventBus.Instance.GachaPullResult += OnGachaPullResult;
    EventBus.Instance.GachaPityMilestone += OnGachaPityMilestone;
    EventBus.Instance.GachaMultiPullResult += OnGachaMultiPullResult;
}

private void OnGachaPullResult(
    string rewardId, int rarity, Godot.Collections.Dictionary pityState)
{
    // 播放稀有度展示动画
    PlayRarityReveal((PetRarity)rarity);
}

private void OnGachaPityMilestone(int pullsTowardGuarantee)
{
    // 更新 UI 显示 "保底还差 X 抽"
    UpdatePityCounter(pullsTowardGuarantee);
}

private void OnGachaMultiPullResult(Godot.Collections.Array results)
{
    // 展示十连结果列表
    ShowMultiPullResults(results);
}

public override void _ExitTree()
{
    EventBus.Instance.GachaPullResult -= OnGachaPullResult;
    EventBus.Instance.GachaPityMilestone -= OnGachaPityMilestone;
    EventBus.Instance.GachaMultiPullResult -= OnGachaMultiPullResult;
}
```

---

## 9. 文件结构

```
scripts/gacha/
├── data/
│   ├── GachaBannerResource.cs          # [GlobalClass] Resource — 编辑器配置卡池
│   ├── GachaPoolEntryResource.cs       # [GlobalClass] Resource — 编辑器配置池条目
│   └── GachaBannerData.cs              # 运行时 DTO：GachaPoolEntry, GachaBanner
├── models/
│   ├── GachaRollResult.cs              # 抽卡结果 record
│   ├── GachaPityState.cs               # 保底状态 readonly record struct
│   ├── PetRarity.cs                    # 稀有度枚举
│   └── RewardType.cs                   # 奖励类型枚举
├── services/
│   ├── GachaRollService.cs             # 纯 C# 概率引擎（零 Godot 依赖）
│   ├── GachaDrawService.cs             # 抽卡编排器（货币/宠物/信号协调）
│   └── GachaPityTracker.cs             # 保底状态追踪（每卡池独立、持久化）
└── ui/
    ├── GachaBannerUI.cs                # 卡池 UI（展示 UP 角色、概率、保底进度）
    ├── PullAnimation.cs                # 抽卡动画控制器
    └── RarityRevealEffect.cs           # 稀有度展示特效（闪光、震动等）
```

---

## 10. 附录

### 10.1 标准稀有度概率表

| 稀有度 | 权重 | 概率 | 保底保护 |
|--------|------|------|---------|
| Common | 700 | 70.00% | 无 |
| Rare | 220 | 22.00% | 十连保底 ≥ 1 |
| Epic | 60 | 6.00% | 无独立保底 |
| Legendary (SSR) | 20 | 2.00% | Soft Pity (75+) + Hard Pity (90) |
| **合计** | **1000** | **100.00%** | |

### 10.2 不同卡池类型保底配置参考

| 参数 | 标准卡池 | 新手卡池 | 限定卡池 | 友情卡池 |
|------|---------|---------|---------|---------|
| CostPerPull | 150 | 0（仅一次） | 300 | 50 |
| SoftPityStart | 75 | 无 | 75 | 无 |
| HardPityGuarantee | 90 | 50 | 90 | 无 |
| SoftPityRateIncrease | 0.05 | 0 | 0.05 | 0 |
| RateUpChanceOnSSR | 0.5 | 0 | 0.5 | 0 |
| 十连保底 | Rare+ | Rare+ | Rare+ | 无 |
| 硬保底可跨卡池 | 否 | 否 | 否 | 否 |

### 10.3 测试策略

#### 10.3.1 GachaRollService 单元测试（xUnit）

```csharp
// tests/gacha/GachaRollServiceTests.cs
using Xunit;

namespace Match3Demo.Tests;

public class GachaRollServiceTests
{
    [Fact]
    public void Roll_BasicBanner_ReturnsValidResult()
    {
        var service = new GachaRollService(seed: 42);
        var banner = CreateTestBanner();
        var state = GachaPityState.Default;

        var result = service.Roll(banner, state);

        Assert.NotNull(result.RewardId);
        Assert.Equal(state.TotalPulls + 1, result.NewPityState.TotalPulls);
    }

    [Fact]
    public void Roll_HardPity_ForcesSSR()
    {
        var service = new GachaRollService(seed: 42);
        var banner = new GachaBanner
        {
            Id = "test",
            SoftPityStart = 999,   // 禁用 Soft Pity
            HardPityGuarantee = 90,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "common_cat", Rarity = PetRarity.Common, Weight = 1000 },
                new() { RewardId = "ssr_cat", Rarity = PetRarity.Legendary, Weight = 1 }
            }
        };

        var nearPity = new GachaPityState
        {
            TotalPulls = 89,
            PullsSinceLastSSR = 89, // 下一抽必保底
        };

        var result = service.Roll(banner, nearPity);

        Assert.Equal(PetRarity.Legendary, result.Rarity);
        Assert.True(result.IsHardPity);
    }

    [Fact]
    public void Roll_SoftPity_IncreasesSSRProbability()
    {
        var service = new GachaRollService(seed: 42);
        var banner = new GachaBanner
        {
            Id = "test",
            SoftPityStart = 75,
            HardPityGuarantee = 90,
            SoftPityRateIncrease = 0.05,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "common_cat", Rarity = PetRarity.Common, Weight = 700 },
                new() { RewardId = "rare_cat", Rarity = PetRarity.Rare, Weight = 220 },
                new() { RewardId = "ssr_cat", Rarity = PetRarity.Legendary, Weight = 20 }
            }
        };

        // 远离保底：SSR 概率很低
        var farFromPity = new GachaPityState { TotalPulls = 10, PullsSinceLastSSR = 10 };
        var farRarity = service.RollRarity(banner, farFromPity);

        // 接近保底：SSR 概率应有所提升
        var nearPity = new GachaPityState { TotalPulls = 80, PullsSinceLastSSR = 80 };
        var nearRarity = service.RollRarity(banner, nearPity);

        // 由于随机性，这里不直接断言 rarity，而是验证调整后的权重增长
        var baseWeights = banner.GetRarityWeights();
        var ssrBase = baseWeights.GetValueOrDefault(PetRarity.Legendary, 0);
        Assert.Equal(20.0, ssrBase);
    }

    [Fact]
    public void Roll_GuaranteedRateUp_EnsuresRateUpOnNextSSR()
    {
        var service = new GachaRollService(seed: 42);
        var rateUpId = "legendary_pet_up";
        var banner = new GachaBanner
        {
            Id = "test",
            RateUpRewardId = rateUpId,
            RateUpChanceOnSSR = 0.5,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = rateUpId, Rarity = PetRarity.Legendary, Weight = 1 },
                new() { RewardId = "other_ssr", Rarity = PetRarity.Legendary, Weight = 1 }
            }
        };

        // 模拟 50:50 落败后的状态：GuaranteedRateUpNext = true
        var guaranteedState = new GachaPityState
        {
            TotalPulls = 10,
            PullsSinceLastSSR = 10,
            GuaranteedRateUpNext = true
        };

        var result = service.Roll(banner, guaranteedState);

        Assert.Equal(rateUpId, result.RewardId);
        Assert.True(result.IsRateUp);
        Assert.False(result.NewPityState.GuaranteedRateUpNext); // 已消耗
    }

    [Fact]
    public void SelectWeighted_ReturnsCorrectDistribution()
    {
        var service = new GachaRollService(seed: 12345);
        var entries = new List<int> { 1, 2, 3 };
        var counts = new Dictionary<int, int>();

        for (int i = 0; i < 10000; i++)
        {
            var selected = service.SelectWeighted(entries, e => (double)e);
            if (!counts.ContainsKey(selected))
                counts[selected] = 0;
            counts[selected]++;
        }

        // 权重 1:2:3 → 期望频率 1/6 : 2/6 : 3/6
        // 10000 次抽样，容许 ±3% 误差
        Assert.InRange(counts[1] / 10000.0, 0.136, 0.197); // 期望 0.166
        Assert.InRange(counts[2] / 10000.0, 0.300, 0.367); // 期望 0.333
        Assert.InRange(counts[3] / 10000.0, 0.470, 0.530); // 期望 0.500
    }

    private static GachaBanner CreateTestBanner()
    {
        return new GachaBanner
        {
            Id = "test_banner",
            DisplayName = "测试卡池",
            CostPerPull = 150,
            SoftPityStart = 75,
            HardPityGuarantee = 90,
            SoftPityRateIncrease = 0.05,
            RateUpRewardId = "ssr_001",
            RateUpChanceOnSSR = 0.5,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "common_001", RewardType = RewardType.Pet, Rarity = PetRarity.Common, Weight = 700 },
                new() { RewardId = "rare_001", RewardType = RewardType.Pet, Rarity = PetRarity.Rare, Weight = 220 },
                new() { RewardId = "epic_001", RewardType = RewardType.Pet, Rarity = PetRarity.Epic, Weight = 60 },
                new() { RewardId = "ssr_001", RewardType = RewardType.Pet, Rarity = PetRarity.Legendary, Weight = 20 }
            }
        };
    }
}
```

#### 10.3.2 GachaDrawService 集成测试（使用 Mock）

```csharp
// tests/gacha/GachaDrawServiceTests.cs
using Moq;
using Xunit;

namespace Match3Demo.Tests;

public class GachaDrawServiceTests
{
    [Fact]
    public void PerformPull_WithSufficientCurrency_ReturnsResult()
    {
        var rollService = new GachaRollService(seed: 42);
        var banner = CreateTestBanner();

        var mockCurrency = new Mock<ICurrencyService>();
        mockCurrency.Setup(c => c.HasEnough("soft_currency", 150)).Returns(true);

        var mockPet = new Mock<IPetCollectionService>();
        var mockStorage = new Mock<IPersistentStorage>();
        var pityTracker = new GachaPityTracker(mockStorage.Object);

        var service = new GachaDrawService(
            rollService,
            pityTracker,
            mockPet.Object,
            mockCurrency.Object,
            _ => banner);

        var result = service.PerformPull("test_banner");

        Assert.NotNull(result);
        mockCurrency.Verify(c => c.Spend("soft_currency", 150), Times.Once);
    }

    [Fact]
    public void PerformPull_InsufficientCurrency_Throws()
    {
        var rollService = new GachaRollService();
        var banner = CreateTestBanner();
        var mockCurrency = new Mock<ICurrencyService>();
        mockCurrency.Setup(c => c.HasEnough("soft_currency", 150)).Returns(false);

        var mockPet = new Mock<IPetCollectionService>();
        var mockStorage = new Mock<IPersistentStorage>();
        var pityTracker = new GachaPityTracker(mockStorage.Object);

        var service = new GachaDrawService(
            rollService,
            pityTracker,
            mockPet.Object,
            mockCurrency.Object,
            _ => banner);

        Assert.Throws<InvalidOperationException>(() =>
            service.PerformPull("test_banner"));
    }

    [Fact]
    public void PerformMultiPull_10Rolls_HasAtLeastOneRarePlus()
    {
        var rollService = new GachaRollService(seed: 9999);
        var banner = CreateTestBanner();

        var mockCurrency = new Mock<ICurrencyService>();
        mockCurrency.Setup(c => c.HasEnough("soft_currency", 1500)).Returns(true);

        var mockPet = new Mock<IPetCollectionService>();
        var mockStorage = new Mock<IPersistentStorage>();
        var pityTracker = new GachaPityTracker(mockStorage.Object);

        var service = new GachaDrawService(
            rollService,
            pityTracker,
            mockPet.Object,
            mockCurrency.Object,
            _ => banner);

        var results = service.PerformMultiPull("test_banner", 10);

        Assert.Equal(10, results.Count);
        // 十连保底：至少一个非 Common
        Assert.Contains(results, r => r.Rarity != PetRarity.Common);
    }

    private static GachaBanner CreateTestBanner()
    {
        return new GachaBanner
        {
            Id = "test_banner",
            DisplayName = "测试卡池",
            CostPerPull = 150,
            SoftPityStart = 999,
            HardPityGuarantee = 999,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "common_001", RewardType = RewardType.Pet, Rarity = PetRarity.Common, Weight = 700 },
                new() { RewardId = "rare_001", RewardType = RewardType.Pet, Rarity = PetRarity.Rare, Weight = 220 },
                new() { RewardId = "epic_001", RewardType = RewardType.Pet, Rarity = PetRarity.Epic, Weight = 60 },
                new() { RewardId = "ssr_001", RewardType = RewardType.Pet, Rarity = PetRarity.Legendary, Weight = 20 }
            }
        };
    }
}
```

#### 10.3.3 测试覆盖矩阵

| 测试场景 | 覆盖类 | 关键断言 |
|---------|--------|---------|
| 基础单抽返回正确结构 | `GachaRollService` | result 非 null，PityState 递增 |
| Hard Pity 90 抽触发 | `GachaRollService` | Rarity == Legendary, IsHardPity |
| Soft Pity 权重线性增长 | `GachaRollService` | 权重公式计算正确 |
| 50:50 落败后下次必 UP | `GachaRollService` | 连续两次 SSR，第二次 IsRateUp=true |
| 加权选择分布统计 | `GachaRollService` | 10000 次抽样分布在期望 ±3% |
| 货币不足抛异常 | `GachaDrawService` | InvalidOperationException |
| 抽后货币扣除 | `GachaDrawService` | mock.Verify Spend 调用 |
| 十连保底 Rare+ | `GachaDrawService` | 10 结果中有非 Common |
| 保底状态持久化 | `GachaPityTracker` | 存盘 → 读盘 → 状态一致 |
| 跨抽次保底累计 | `GachaPityTracker` | 两次单抽后 TotalPulls = 2 |

---

### 10.4 系统间依赖关系图

```
                    ┌──────────────────────┐
                    │   GachaBannerUI.cs    │  ← 卡池界面，用户交互
                    │   PullAnimation.cs    │  ← 抽卡动画播放
                    │   RarityRevealEffect  │  ← 稀有度特效
                    └──────────┬───────────┘
                               │ 订阅 EventBus 信号
                               │
              ┌────────────────┼────────────────┐
              │                │                │
    GachaPullResult   GachaPityMilestone   GachaMultiPullResult
              │                │                │
              └────────────────┼────────────────┘
                               │
                         EventBus.cs
                               │
              GachaDrawService (Orchestrator)
              │        │        │        │
              │        │        │        │
    ┌─────────▼──┐ ┌──▼────┐ ┌─▼──────┐ ┌▼─────────┐
    │GachaRoll-  │ │ICurr- │ │IPetCol-│ │GachaPity-│
    │Service     │ │encySvc│ │lection │ │Tracker   │
    │(纯C#概率)  │ │       │ │Service │ │          │
    └────────────┘ └───────┘ └────────┘ └────┬─────┘
                                             │
                                      IPersistentStorage
                                      (保底状态持久化)
```

---

### 10.5 关键术语对照表

| 中文 | 英文 | 说明 |
|------|------|------|
| 卡池 | Banner | 限定时间内的抽取池 |
| 保底 | Pity | 连续未出高稀有度后的概率补偿 |
| 软保底 | Soft Pity | 达到阈值后概率线性递增 |
| 硬保底 | Hard Pity | 达到上限后强制出货 |
| 50/50 | 50:50 Rate-Up | 当期 UP 角色与非 UP 角色各 50% 概率 |
| 大保底 | Guaranteed Rate-Up | 上次 SSR 非 UP → 下次 SSR 必 UP |
| 十连抽 | 10-Pull / Multi Pull | 一次性抽取 10 次 |
| 十连保底 | 10-Pull Guarantee | 十连中至少一个 Rare+ |
| 歪 | Lost 50:50 | 抽到 SSR 但不是当期 UP 角色 |
| 常驻池 | Standard Banner | 永久存在的卡池 |
