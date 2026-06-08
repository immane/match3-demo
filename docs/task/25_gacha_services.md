# Task 25: 抽卡服务层

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §3 — 概率算法（CDF 加权选择、两阶段抽取、Soft Pity 公式） |
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §4 — 保底系统（Soft Pity / Hard Pity / 50:50 Rate-Up 状态流转） |
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §5 — 服务层（GachaRollService / GachaDrawService / GachaPityTracker） |
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §7 — 多抽（十连抽保底规则、货币预检、批量信号） |

## 状态
- [x] 已完成

## 依赖
- Task 15 (WeightedRandom, IPersistentStorage — `scripts/utils/WeightedRandom.cs`, `scripts/utils/IPersistentStorage.cs`)
- Task 18 (ICurrencyService — `scripts/currency/ICurrencyService.cs`)
- Task 22 (IPetCollectionService — `scripts/pets/services/IPetCollectionService.cs`)
- Task 24 (GachaBanner, GachaRollResult, GachaPityState, GachaPitySaveData, IDataSource<GachaBanner> — 数据模型层)
- Task 16 (EventBus GachaPullResult / GachaMultiPullResult / GachaBeforePull / GachaPityMilestone 信号)

## 产出文件
```
scripts/gacha/services/GachaRollService.cs    [新增]
scripts/gacha/services/GachaPityTracker.cs    [新增]
scripts/gacha/services/GachaDrawService.cs    [新增]
```

## 实现要求

### GachaRollService.cs — 纯 C# 概率引擎

`scripts/gacha/services/GachaRollService.cs`，命名空间 `Match3Demo`。完整的加权随机抽卡引擎，零 Godot 依赖（文件顶部无 `using Godot`），100% 可单元测试。

构造函数接受可选 `seed` 参数以支持可复现的随机序列：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class GachaRollService
{
    private readonly System.Random _rng;

    public GachaRollService(int? seed = null)
    {
        _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
    }

    /// <summary>
    /// Execute a single roll on a banner with current pity state.
    /// Pure computation, no I/O, no Godot dependency.
    /// </summary>
    public GachaRollResult Roll(GachaBanner banner, GachaPityState pityState)
    {
        // 1. Hard pity check — guaranteed Legendary
        if (pityState.PullsSinceLastSSR >= banner.HardPityGuarantee - 1)
            return ForceRarity(banner, pityState, PetRarity.Legendary);

        // 2. Roll rarity with soft pity bonus
        PetRarity rolledRarity = RollRarity(banner, pityState);

        // 3. Select specific item from the rarity pool
        string rewardId = SelectRewardFromRarity(banner, rolledRarity, pityState);

        // 4. Determine reward type from pool entry
        var entry = banner.Pool.First(e => e.RewardId == rewardId);

        // 5. Build new pity state
        var newPity = new GachaPityState(
            pityState.TotalPulls + 1,
            rolledRarity >= PetRarity.Epic
                ? (rolledRarity == PetRarity.Legendary ? 0 : pityState.PullsSinceLastSSR)
                : pityState.PullsSinceLastSSR + 1,
            rolledRarity >= PetRarity.Rare
                ? (rolledRarity >= PetRarity.Epic ? 0 : pityState.PullsSinceLastEpic)
                : pityState.PullsSinceLastEpic + 1,
            // Update 50/50 guarantee
            rolledRarity == PetRarity.Legendary
                ? (banner.RateUpRewardId != null && rewardId != banner.RateUpRewardId)
                : pityState.GuaranteedRateUpNext
        );

        return new GachaRollResult(rewardId, entry.Type, rolledRarity, newPity);
    }

    /// <summary>
    /// Rarity roll using CDF with soft pity bonus applied to SSR weight.
    /// Soft pity formula: bonus = (pullsBeyondStart) × rateIncrease
    /// </summary>
    private PetRarity RollRarity(GachaBanner banner, GachaPityState pity)
    {
        var rarityWeights = banner.GetRarityWeights();
        double baseSSRWeight = rarityWeights.GetValueOrDefault(PetRarity.Legendary, 0);

        // Apply soft pity bonus to SSR weight
        double softPityBonus = 0;
        if (pity.PullsSinceLastSSR >= banner.SoftPityStart)
        {
            int overSoftPity = pity.PullsSinceLastSSR - banner.SoftPityStart + 1;
            softPityBonus = overSoftPity * banner.SoftPityRateIncrease;
        }

        // Build cumulative distribution
        double totalWeight = 0;
        var rarityEntries = new List<(PetRarity Rarity, double CumulativeWeight)>();

        foreach (var kv in rarityWeights.OrderBy(x => x.Key))
        {
            double weight = kv.Key == PetRarity.Legendary
                ? kv.Value + softPityBonus
                : kv.Value;
            totalWeight += weight;
            rarityEntries.Add((kv.Key, totalWeight));
        }

        double roll = _rng.NextDouble() * totalWeight;
        foreach (var (rarity, cumulative) in rarityEntries)
        {
            if (roll <= cumulative)
                return rarity;
        }

        return PetRarity.Common; // fallback for floating-point precision edge case
    }

    /// <summary>
    /// Select a specific reward from the pool for the given rarity.
    /// Handles 50/50 Rate-Up logic for Legendary items.
    /// </summary>
    private string SelectRewardFromRarity(GachaBanner banner, PetRarity rarity, GachaPityState pity)
    {
        var candidates = banner.GetEntriesByRarity(rarity);

        // 50/50 check for Legendary items
        if (rarity == PetRarity.Legendary && banner.RateUpRewardId != null)
        {
            if (pity.GuaranteedRateUpNext)
            {
                // Guaranteed rate-up from previous 50/50 loss
                return banner.RateUpRewardId;
            }
            if (_rng.NextDouble() < banner.RateUpChanceOnSSR)
            {
                return banner.RateUpRewardId;
            }
        }

        // Weighted random from candidates
        return WeightedRandom.Pick(candidates, e => e.Weight, _rng).RewardId;
    }

    /// <summary>
    /// Force a guaranteed rarity pull (used for Hard Pity and 10-pull guarantee).
    /// </summary>
    private GachaRollResult ForceRarity(GachaBanner banner, GachaPityState pity, PetRarity rarity)
    {
        string rewardId = SelectRewardFromRarity(banner, rarity, pity);
        var entry = banner.Pool.First(e => e.RewardId == rewardId);

        var newPity = new GachaPityState(
            pity.TotalPulls + 1,
            0, // reset SSR pity
            pity.PullsSinceLastEpic,
            banner.RateUpRewardId != null && rewardId != banner.RateUpRewardId
        );

        return new GachaRollResult(rewardId, entry.Type, rarity, newPity);
    }

    /// <summary>
    /// Perform multiple pulls (10-pull).
    /// 10-pull guarantee: at least one Rare+ in the batch.
    /// </summary>
    public List<GachaRollResult> RollMultiple(GachaBanner banner, GachaPityState pityState, int count)
    {
        var results = new List<GachaRollResult>();
        var currentPity = pityState;

        for (int i = 0; i < count; i++)
        {
            var result = Roll(banner, currentPity);
            results.Add(result);
            currentPity = result.NewPityState;
        }

        // 10-pull guarantee: at least one Rare+
        if (count >= 10 && !results.Any(r => r.Rarity >= PetRarity.Rare))
        {
            var lastResult = results[^1];
            var rareUpResult = ForceRarity(banner, lastResult.NewPityState, PetRarity.Rare);
            results[^1] = rareUpResult;
        }

        return results;
    }
}
```

关键点：
- 文件顶部无 `using Godot`，纯 C# 实现
- 构造函数接受 `int? seed`，传 null 使用系统时间种子
- `Roll()` 为两阶段抽取：阶段一 `RollRarity()`（稀有度判定），阶段二 `SelectRewardFromRarity()`（池内选择）
- `RollRarity()` 使用 CDF 累积分布函数，按稀有度枚举值排序后构建累积权重列表
- Soft Pity 公式：`bonus = (PullsSinceLastSSR - SoftPityStart + 1) × SoftPityRateIncrease`，线性递增
- Hard Pity：当 `PullsSinceLastSSR >= HardPityGuarantee - 1` 时，强制 Legendary
- 50/50 系统：抽到 Legendary 且非 UP 时设置 `GuaranteedRateUpNext = true`，下次 SSR 必定为 UP
- `RollMultiple()` 的 10 连保底通过替换最后一条结果实现
- 使用 `WeightedRandom.Pick()` 进行加权随机选择（复用 Task 15 的工具方法）

### GachaPityTracker.cs — 保底状态跟踪器

`scripts/gacha/services/GachaPityTracker.cs`，命名空间 `Match3Demo`。跟踪并持久化每个卡池的保底状态，通过 `IPersistentStorage` 实现存档持久化。

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Match3Demo;

public class GachaPityTracker
{
    private readonly IPersistentStorage _storage;
    private const string SaveKey = "gacha_pity";
    private readonly Dictionary<string, GachaPityState> _bannerStates = new();

    public GachaPityTracker(IPersistentStorage storage)
    {
        _storage = storage;
        _ = LoadAsync(); // fire and forget — load saved pity states on construction
    }

    /// <summary>
    /// Get current pity state for a banner.
    /// Returns default state (all zeros) if no state has been recorded yet.
    /// </summary>
    public GachaPityState GetPityState(string bannerId)
        => _bannerStates.GetValueOrDefault(bannerId, new GachaPityState(0, 0, 0, false));

    /// <summary>
    /// Update pity state for a banner and persist asynchronously.
    /// </summary>
    public void UpdatePityState(string bannerId, GachaPityState newState)
    {
        _bannerStates[bannerId] = newState;
        _ = SaveAsync();
    }

    /// <summary>
    /// Reset pity state for a banner to default (all zeros, no guarantee).
    /// </summary>
    public void ResetPityState(string bannerId)
    {
        _bannerStates[bannerId] = new GachaPityState(0, 0, 0, false);
        _ = SaveAsync();
    }

    /// <summary>
    /// Load saved pity states from persistent storage.
    /// </summary>
    public async Task LoadAsync()
    {
        var data = await _storage.LoadAsync<GachaPitySaveData>(SaveKey);
        if (data?.BannerPityStates == null) return;

        foreach (var kv in data.BannerPityStates)
        {
            _bannerStates[kv.Key] = new GachaPityState(
                kv.Value.TotalPulls,
                kv.Value.PullsSinceLastSSR,
                kv.Value.PullsSinceLastEpic,
                kv.Value.GuaranteedRateUpNext
            );
        }
    }

    /// <summary>
    /// Persist all banner pity states to storage.
    /// </summary>
    public async Task SaveAsync()
    {
        var data = new GachaPitySaveData
        {
            BannerPityStates = _bannerStates.ToDictionary(
                kv => kv.Key,
                kv => new GachaPityStateDto
                {
                    TotalPulls = kv.Value.TotalPulls,
                    PullsSinceLastSSR = kv.Value.PullsSinceLastSSR,
                    PullsSinceLastEpic = kv.Value.PullsSinceLastEpic,
                    GuaranteedRateUpNext = kv.Value.GuaranteedRateUpNext
                })
        };
        await _storage.SaveAsync(SaveKey, data);
    }
}
```

关键点：
- 每个卡池独立跟踪保底状态，通过 `_bannerStates` Dictionary 按 `bannerId` 索引
- 构造函数中 fire-and-forget 调用 `LoadAsync()` 加载上次存档
- `GetPityState()` 对于未记录的卡池返回默认全零状态
- `UpdatePityState()` 后自动触发 `SaveAsync()` 异步持久化
- 使用 `GachaPitySaveData`（Task 24 产出）作为序列化 DTO，`GachaPityStateDto` 作为字典值类型
- `IPersistentStorage` 的 `LoadAsync<T>` / `SaveAsync<T>` 为异步方法

### GachaDrawService.cs — 抽卡编排器

`scripts/gacha/services/GachaDrawService.cs`，命名空间 `Match3Demo`。组合所有依赖（货币、概率引擎、卡池数据源、宠物收集、事件总线、保底跟踪器），编排完整的抽卡流程。

```csharp
using Godot;
using System;
using System.Collections.Generic;

namespace Match3Demo;

public class GachaDrawService
{
    private readonly ICurrencyService _currency;
    private readonly GachaRollService _roller;
    private readonly IDataSource<GachaBanner> _banners;
    private readonly IPetCollectionService _petCollection;
    private readonly EventBus _eventBus;
    private readonly GachaPityTracker _pityTracker;

    public GachaDrawService(
        ICurrencyService currency,
        GachaRollService roller,
        IDataSource<GachaBanner> banners,
        IPetCollectionService petCollection,
        EventBus eventBus,
        GachaPityTracker pityTracker)
    {
        _currency = currency;
        _roller = roller;
        _banners = banners;
        _petCollection = petCollection;
        _eventBus = eventBus;
        _pityTracker = pityTracker;
    }

    /// <summary>
    /// Perform a single gacha pull.
    /// Full orchestration: deduct currency → roll → grant reward → update pity → emit signal.
    /// </summary>
    public GachaRollResult PerformPull(string bannerId)
    {
        var banner = _banners.Get(bannerId);
        if (banner == null)
            throw new ArgumentException($"Banner '{bannerId}' not found");

        if (!_currency.Spend("soft_currency", banner.CostPerPull, $"gacha_pull_{bannerId}"))
            throw new InvalidOperationException($"Insufficient currency for {bannerId}");

        _eventBus.EmitSignal(EventBus.SignalName.GachaBeforePull, bannerId);

        var pity = _pityTracker.GetPityState(bannerId);
        var result = _roller.Roll(banner, pity);
        _pityTracker.UpdatePityState(bannerId, result.NewPityState);

        GrantReward(result);

        EmitPullResult(result);

        return result;
    }

    /// <summary>
    /// Perform a multi-pull (usually 10-pull).
    /// Checks total cost upfront, guarantees at least one Rare+.
    /// </summary>
    public List<GachaRollResult> PerformMultiPull(string bannerId, int count = 10)
    {
        var banner = _banners.Get(bannerId);
        if (banner == null)
            throw new ArgumentException($"Banner '{bannerId}' not found");

        int totalCost = banner.CostPerPull * count;
        if (!_currency.Spend("soft_currency", totalCost, $"gacha_multipull_{bannerId}_{count}"))
            throw new InvalidOperationException($"Insufficient currency for {count}x {bannerId}");

        _eventBus.EmitSignal(EventBus.SignalName.GachaBeforePull, bannerId);

        var pity = _pityTracker.GetPityState(bannerId);
        var results = _roller.RollMultiple(banner, pity, count);

        // Update pity with the LAST result's state
        var finalPity = results[^1].NewPityState;
        _pityTracker.UpdatePityState(bannerId, finalPity);

        foreach (var result in results)
            GrantReward(result);

        // Emit multi-pull result for batch UI
        var godotArray = new Godot.Collections.Array();
        foreach (var r in results)
        {
            var dict = new Godot.Collections.Dictionary
            {
                ["rewardId"] = r.RewardId,
                ["type"] = (int)r.Type,
                ["rarity"] = (int)r.Rarity
            };
            godotArray.Add(dict);
        }
        _eventBus.EmitSignal(EventBus.SignalName.GachaMultiPullResult, godotArray);

        return results;
    }

    /// <summary>
    /// Get the number of pulls remaining until the hard pity guarantee.
    /// Returns -1 if the banner is not found.
    /// </summary>
    public int GetPullsUntilGuarantee(string bannerId)
    {
        var banner = _banners.Get(bannerId);
        if (banner == null) return -1;
        var pity = _pityTracker.GetPityState(bannerId);
        return Math.Max(0, banner.HardPityGuarantee - pity.PullsSinceLastSSR);
    }

    private void GrantReward(GachaRollResult result)
    {
        if (result.Type == RewardType.Pet)
            _petCollection.AddPet(result.RewardId);
        // Accessory reward handling reserved for future extension
    }

    private void EmitPullResult(GachaRollResult result)
    {
        var pityDict = new Godot.Collections.Dictionary
        {
            ["totalPulls"] = result.NewPityState.TotalPulls,
            ["pullsSinceLastSSR"] = result.NewPityState.PullsSinceLastSSR,
            ["guaranteedRateUp"] = result.NewPityState.GuaranteedRateUpNext
        };

        _eventBus.EmitSignal(EventBus.SignalName.GachaPullResult,
            result.RewardId, (int)result.Rarity, pityDict);
    }
}
```

关键点：
- `PerformPull()` 完整编排：加载卡池 → 扣除货币 → 发射 GachaBeforePull 信号 → 抽取 → 更新保底 → 发放奖励 → 发射 GachaPullResult 信号
- `PerformMultiPull()` 一次性预检总货币（而非逐次检查），`RollMultiple()` 内部保证 10 连保底
- 保底状态更新使用最后一次抽卡结果的状态（`results[^1].NewPityState`）
- `GrantReward()` 根据 `RewardType` 分发奖励，目前仅实现 `Pet` 类型的处理
- `EmitPullResult()` 将 `GachaPityState` 转换为 `Godot.Collections.Dictionary` 传递给信号
- `GetPullsUntilGuarantee()` 返回距离硬保底还需的抽数，用于 UI 显示
- 构造函数注入 6 个依赖：`ICurrencyService`, `GachaRollService`, `IDataSource<GachaBanner>`, `IPetCollectionService`, `EventBus`, `GachaPityTracker`

## 验收标准
- [ ] `GachaRollService` 为纯 C#（文件顶部无 `using Godot`），构造函数接受可选 `int? seed`
- [ ] `RollRarity()` 使用 CDF 累积分布正确计算稀有度，按稀有度枚举值排序构建累积权重列表
- [ ] Soft pity 线性递增公式：`bonus = (PullsSinceLastSSR - SoftPityStart + 1) × SoftPityRateIncrease`
- [ ] Hard pity：在第 `HardPityGuarantee` 抽（`PullsSinceLastSSR >= HardPityGuarantee - 1`）强制传说
- [ ] 50/50 系统：`GuaranteedRateUpNext` 标志在抽到 Legendary 且非 UP 时设置为 `true`，在下次 Legendary 时消耗
- [ ] `RollMultiple()` 的 10 连保底：若 10 抽内无 `Rare+`，最后一条结果强制替换为 `Rare+`
- [ ] `GachaRollService` 所有方法为纯函数，通过固定 seed 可完全复现概率结果
- [ ] `GachaDrawService.PerformPull()` 完整编排：扣货币 → 抽卡 → 发放奖励 → 更新保底 → 发射信号
- [ ] `GachaDrawService.PerformMultiPull(10)` 一次性预检 10 抽总货币，保证至少一个 Rare+
- [ ] `GachaDrawService.GetPullsUntilGuarantee()` 正确计算距离硬保底的剩余抽数
- [ ] `GachaPityTracker` 每个卡池独立跟踪保底状态（`_bannerStates` Dictionary 按 `bannerId` 索引）
- [ ] `GachaPityTracker` 构造函数中 fire-and-forget 调用 `LoadAsync()` 加载存档
- [ ] `GachaPityTracker.UpdatePityState()` 后自动触发 `SaveAsync()` 异步持久化
- [ ] `GachaPityTracker.GetPityState()` 对未记录卡池返回全零默认状态
- [ ] 所有 3 个文件可通过 `dotnet build` 编译（0 错误，0 警告）
- [ ] 命名空间统一为 `Match3Demo`

## 注意
- 目录 `scripts/gacha/services/` 为新增，需确保父目录 `scripts/gacha/` 已由 Task 24 创建
- `GachaRollService` 使用 `WeightedRandom.Pick()`（Task 15 产出），确保方法签名兼容（泛型 + `System.Random` 参数）
- `GachaPityTracker` 使用 `GachaPitySaveData` 和 `GachaPityStateDto`（Task 24 产出）作为持久化 DTO
- `GachaDrawService` 中的 `EventBus` 信号名称（`GachaBeforePull`, `GachaPullResult`, `GachaMultiPullResult`）必须与 Task 16 中 EventBus 定义的信号签名一致
- `icurrencyService.Spend()` 的签名假定为 `bool Spend(string currencyId, int amount, string reason)`，返回是否成功
