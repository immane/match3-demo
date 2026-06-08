# Task 24: 抽卡数据模型层

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §2 — 数据模型（RewardType, GachaPoolEntry, GachaBanner, GachaRollResult, GachaPityState, GachaPitySaveData） |
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §3 — 概率系统（稀有度权重组、CDF 加权选择） |
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §4 — 保底系统（Soft Pity / Hard Pity / 50:50 Rate-Up） |

## 状态
- [x] 已完成

## 依赖
- Task 20 (PetRarity 枚举 — `scripts/pets/models/PetRarity.cs`)
- Task 15 (IDataSource 泛型接口 — `scripts/utils/IDataSource.cs`)

## 产出文件
```
scripts/gacha/models/RewardType.cs              [新增, enum]
scripts/gacha/models/GachaPoolEntry.cs          [新增, 纯 C# POCO]
scripts/gacha/models/GachaBanner.cs             [新增, 纯 C# POCO]
scripts/gacha/models/GachaRollResult.cs         [新增, C# record]
scripts/gacha/models/GachaPityState.cs          [新增, record struct]
scripts/gacha/models/GachaPitySaveData.cs       [新增, 序列化 DTO]
scripts/gacha/data/GachaPoolEntryResource.cs    [新增, GlobalClass Resource]
scripts/gacha/data/GachaBannerResource.cs       [新增, GlobalClass Resource]
scripts/gacha/data/GachaBannerDataSource.cs     [新增, 数据源实现]
```

## 实现要求

### RewardType.cs — 奖励类型枚举

`scripts/gacha/models/RewardType.cs`，命名空间 `Match3Demo`。定义抽卡系统可获得的奖励类型。

```csharp
namespace Match3Demo;
public enum RewardType { Pet, Accessory }
```

关键点：
- 目前仅 `Pet` 和 `Accessory` 两种类型
- 纯 C# 枚举，无 Godot 依赖

### GachaPoolEntry.cs — 卡池条目 POCO

`scripts/gacha/models/GachaPoolEntry.cs`，命名空间 `Match3Demo`。定义卡池中单个奖励条目的运行时数据结构，为纯 C# 类，无 Godot 依赖。

```csharp
namespace Match3Demo;

public class GachaPoolEntry
{
    public string RewardId { get; init; }
    public RewardType Type { get; init; }
    public PetRarity Rarity { get; init; }
    public double Weight { get; init; } = 1.0;
}
```

关键点：
- 所有属性使用 `{ get; init; }` 保证不可变性
- `Weight` 默认值为 `1.0`，用于稀有度判定的加权随机
- `Type` 字段使用 `RewardType` 枚举区分宠物与饰品
- 文件顶部无 `using Godot`

### GachaBanner.cs — 卡池运行时 DTO

`scripts/gacha/models/GachaBanner.cs`，命名空间 `Match3Demo`。定义抽卡卡池的完整配置信息，为纯 C# 类，无 Godot 依赖。

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class GachaBanner
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public List<GachaPoolEntry> Pool { get; init; } = new();
    public int CostPerPull { get; init; } = 50;
    public int SoftPityStart { get; init; } = 70;
    public int HardPityGuarantee { get; init; } = 90;
    public double SoftPityRateIncrease { get; init; } = 0.06;
    public string RateUpRewardId { get; init; }
    public double RateUpChanceOnSSR { get; init; } = 0.5;

    public Dictionary<PetRarity, double> GetRarityWeights()
    {
        var weights = new Dictionary<PetRarity, double>();
        foreach (var entry in Pool)
        {
            weights.TryGetValue(entry.Rarity, out double current);
            weights[entry.Rarity] = current + entry.Weight;
        }
        return weights;
    }

    public double GetTotalWeight() => Pool.Sum(e => e.Weight);

    public List<GachaPoolEntry> GetEntriesByRarity(PetRarity rarity)
        => Pool.Where(e => e.Rarity == rarity).ToList();
}
```

关键点：
- `GetRarityWeights()` 按稀有度分组，返回各稀有度的总权重，用于阶段一「稀有度判定」
- `GetTotalWeight()` 返回池内所有条目的总权重
- `GetEntriesByRarity(PetRarity)` 返回指定稀有度的条目列表，用于阶段二「池内角色判定」
- `HardPityGuarantee` 默认为 90（第 90 抽触发硬保底）
- `SoftPityStart` 默认为 70（第 70 抽起 SSR 权重线性递增）
- `SoftPityRateIncrease` 默认为 0.06（每次增量）
- 文件顶部无 `using Godot`

### GachaRollResult.cs — 抽卡结果记录

`scripts/gacha/models/GachaRollResult.cs`，命名空间 `Match3Demo`。使用 C# `record` 定义单次抽卡返回的不可变结果。

```csharp
namespace Match3Demo;

public record GachaRollResult(
    string RewardId,
    RewardType Type,
    PetRarity Rarity,
    GachaPityState NewPityState
);
```

关键点：
- 使用位置参数语法的 `record`，自动生成 `With` 表达式、值相等比较、`ToString()`
- `NewPityState` 包含抽卡后的最新保底状态
- 纯 C# record，无 Godot 依赖

### GachaPityState.cs — 保底状态记录结构体

`scripts/gacha/models/GachaPityState.cs`，命名空间 `Match3Demo`。使用 C# `readonly record struct` 定义不可变的保底状态快照。

```csharp
namespace Match3Demo;

public readonly record struct GachaPityState(
    int TotalPulls,
    int PullsSinceLastSSR,
    int PullsSinceLastEpic,
    bool GuaranteedRateUpNext
);
```

关键点：
- `TotalPulls`：该卡池累计总抽数
- `PullsSinceLastSSR`：距离上一次 SSR（Legendary）的抽数
- `PullsSinceLastEpic`：距离上一次 Epic 的抽数
- `GuaranteedRateUpNext`：下一次 SSR 是否 100% 为当期 UP（50:50 落败后设置）
- `readonly record struct` 保证值语义 + 不可变性，栈分配高效
- 纯 C#，无 Godot 依赖

### GachaPitySaveData.cs — 保底数据持久化 DTO

`scripts/gacha/models/GachaPitySaveData.cs`，命名空间 `Match3Demo`。定义保底状态的序列化 DTO，将多个卡池的保底状态聚合为一个存档单元。

```csharp
using System.Collections.Generic;

namespace Match3Demo;

public class GachaPitySaveData
{
    public Dictionary<string, GachaPityStateDto> BannerPityStates { get; set; } = new();
}

public class GachaPityStateDto
{
    public int TotalPulls { get; set; }
    public int PullsSinceLastSSR { get; set; }
    public int PullsSinceLastEpic { get; set; }
    public bool GuaranteedRateUpNext { get; set; }
}
```

关键点：
- `GachaPitySaveData` 使用 `Dictionary<string, GachaPityStateDto>` 按卡池 ID 存储保底状态
- `GachaPityStateDto` 为可读写序列化 DTO，字段与 `GachaPityState` 一一对应
- DTO 模式解耦持久化格式与不可变运行时结构体
- 均为纯 C# 类，无 Godot 类型依赖

### GachaPoolEntryResource.cs — 卡池条目编辑器 Resource

`scripts/gacha/data/GachaPoolEntryResource.cs`，命名空间 `Match3Demo`。为 Godot 编辑器提供可配置的卡池条目 Resource，使用 `[GlobalClass]` 使其可在 Inspector 中编辑并保存为 `.tres` 文件。

```csharp
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class GachaPoolEntryResource : Resource
{
    [Export] public string RewardId { get; set; }
    [Export] public PetRarity Rarity { get; set; }
    [Export] public double Weight { get; set; } = 1.0;
    [Export] public bool IsRateUp { get; set; }
    [Export] public RewardType Type { get; set; }

    public GachaPoolEntryResource() {}
    public GachaPoolEntryResource(string rewardId, PetRarity rarity, double weight, RewardType type)
    {
        RewardId = rewardId; Rarity = rarity; Weight = weight; Type = type;
    }

    public GachaPoolEntry ToEntry() => new GachaPoolEntry
    {
        RewardId = RewardId, Type = Type, Rarity = Rarity, Weight = Weight
    };
}
```

关键点：
- `[GlobalClass]` 使 Resource 在 Godot 编辑器中可用
- `ToEntry()` 转换为运行时 POCO `GachaPoolEntry`
- `IsRateUp` 为编辑器标注字段，方便策划在 Inspector 中可视化 UP 条目
- 必须提供无参构造函数（Godot Resource 序列化要求）
- `[Export]` 属性均可通过 Godot Inspector 直接编辑

### GachaBannerResource.cs — 卡池编辑器 Resource

`scripts/gacha/data/GachaBannerResource.cs`，命名空间 `Match3Demo`。为 Godot 编辑器提供可配置的卡池 Resource，使用 `[GlobalClass]` 使其可保存为 `.tres` 文件。

```csharp
using Godot;
using System.Linq;

namespace Match3Demo;

[GlobalClass]
public partial class GachaBannerResource : Resource
{
    [Export] public string BannerId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public int CostPerPull { get; set; } = 50;
    [Export] public int SoftPityStart { get; set; } = 70;
    [Export] public int HardPity { get; set; } = 90;
    [Export] public double SoftPityRateIncrease { get; set; } = 0.06;
    [Export] public string RateUpRewardId { get; set; }
    [Export] public double RateUpChanceOnSSR { get; set; } = 0.5;
    [Export] public Godot.Collections.Array<GachaPoolEntryResource> Pool { get; set; } = new();

    public GachaBannerResource() {}
    public GachaBannerResource(string id, string name)
    {
        BannerId = id; DisplayName = name;
    }

    public GachaBanner ToBanner() => new GachaBanner
    {
        Id = BannerId,
        DisplayName = DisplayName,
        Pool = Pool.Select(p => p.ToEntry()).ToList(),
        CostPerPull = CostPerPull,
        SoftPityStart = SoftPityStart,
        HardPityGuarantee = HardPity,
        SoftPityRateIncrease = SoftPityRateIncrease,
        RateUpRewardId = RateUpRewardId,
        RateUpChanceOnSSR = RateUpChanceOnSSR
    };
}
```

关键点：
- `[GlobalClass]` 使 Resource 在 Godot 编辑器中可用
- `ToBanner()` 正确转换嵌套 Pool 条目，将 `HardPity` 属性映射到运行时 `HardPityGuarantee`
- `Godot.Collections.Array<GachaPoolEntryResource>` 支持 Godot Inspector 中展开编辑嵌套 Resource
- 必须提供无参构造函数（Godot Resource 序列化要求）
- `[Export]` 属性均可通过 Godot Inspector 直接编辑

### GachaBannerDataSource.cs — 卡池数据源实现

`scripts/gacha/data/GachaBannerDataSource.cs`，命名空间 `Match3Demo`。实现 `IDataSource<GachaBanner>`，从 Godot `.tres` Resource 文件加载卡池配置，使用 Dictionary 缓存已加载实例，使用 `DirAccess` 扫描目录实现全量加载。

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class GachaBannerDataSource : IDataSource<GachaBanner>
{
    private readonly Dictionary<string, GachaBanner> _cache = new();
    private bool _allLoaded;

    public GachaBanner Get(string id)
    {
        if (_cache.TryGetValue(id, out var banner)) return banner;
        var res = GD.Load<GachaBannerResource>($"res://data/gacha/{id}.tres");
        if (res != null) { banner = res.ToBanner(); _cache[id] = banner; }
        return banner;
    }

    public IEnumerable<GachaBanner> GetAll()
    {
        if (!_allLoaded) LoadAll();
        return _cache.Values;
    }

    public bool Has(string id) => Get(id) != null;

    private void LoadAll()
    {
        using var dir = DirAccess.Open("res://data/gacha/");
        if (dir == null) { _allLoaded = true; return; }
        dir.ListDirBegin();
        var fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                var id = fileName.Replace(".tres", "");
                if (!_cache.ContainsKey(id))
                {
                    var res = GD.Load<GachaBannerResource>($"res://data/gacha/{fileName}");
                    if (res != null) _cache[id] = res.ToBanner();
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        _allLoaded = true;
    }

    public static GachaBannerDataSource FromDirectory(string path = "res://data/gacha/")
    {
        var source = new GachaBannerDataSource();
        source.LoadAll();
        return source;
    }
}
```

关键点：
- 实现 `IDataSource<GachaBanner>` 接口（`Get`, `GetAll`, `Has`）
- 使用 `GD.Load<T>()` 按需加载单个 Resource 文件
- 使用 `DirAccess.Open()` + `ListDirBegin/GetNext/ListDirEnd` 扫描目录加载所有 `.tres`
- `_allLoaded` 标志防止重复全量扫描
- `LoadAll()` 中 `dir` 为 null 时（目录不存在）安全降级，标记已完成
- 全量加载时跳过已缓存的 ID（`_cache.ContainsKey` 防御）
- `FromDirectory()` 静态工厂方法用于便捷初始化
- Resource 文件路径约定：`res://data/gacha/{bannerId}.tres`

## 验收标准
- [ ] `GachaPoolEntry` 和 `GachaBanner` 为纯 C# POCO，文件顶部无 `using Godot`
- [ ] `GachaRollResult` 为 C# `record`（不可变，位置参数语法）
- [ ] `GachaPityState` 为 C# `readonly record struct`（不可变，值语义）
- [ ] `GachaPitySaveData` 使用 `Dictionary<string, GachaPityStateDto>` 按卡池 ID 存储保底状态
- [ ] `GachaPityStateDto` 为可序列化 DTO，字段与 `GachaPityState` 一一对应
- [ ] `GachaPoolEntryResource` 和 `GachaBannerResource` 使用 `[GlobalClass] partial class ... : Resource`
- [ ] `GachaPoolEntryResource.ToEntry()` 正确转换为运行时 POCO `GachaPoolEntry`
- [ ] `GachaBannerResource.ToBanner()` 正确转换嵌套 Pool 条目，`HardPity` 映射到 `HardPityGuarantee`
- [ ] `GachaBanner.GetRarityWeights()` 正确按稀有度分组计算总权重
- [ ] `GachaBanner.GetEntriesByRarity(PetRarity)` 正确过滤指定稀有度条目
- [ ] `GachaBannerDataSource` 实现 `IDataSource<GachaBanner>` 接口（`Get`, `GetAll`, `Has`）
- [ ] `GachaBannerDataSource` 实现完整缓存机制（`_cache` Dictionary + `_allLoaded` 标志）
- [ ] `GachaBannerDataSource.LoadAll()` 使用 `DirAccess` 正确扫描 `res://data/gacha/` 目录
- [ ] `GachaBannerDataSource` 全量加载时跳过已缓存 ID，不重复 `GD.Load`
- [ ] 当 `res://data/gacha/` 目录不存在时，`LoadAll()` 安全降级不抛异常
- [ ] 所有 9 个文件可通过 `dotnet build` 编译（0 错误，0 警告）
- [ ] 命名空间统一为 `Match3Demo`

## 注意
- 目录 `scripts/gacha/models/` 和 `scripts/gacha/data/` 为新增，需确保父目录存在
- 本 Task 创建的 `.cs` 文件是纯数据定义文件，不包含抽卡概率逻辑（概率引擎属于后续服务层 Task）
- `GachaPoolEntryResource.IsRateUp` 仅作为编辑器标注字段，`ToEntry()` 不将其传入运行时 POCO
- `GachaBannerResource.HardPity` 对应运行时 `GachaBanner.HardPityGuarantee`，命名差异反映编辑器中更简洁的展示
- `GachaPoolEntryResource.Weight` 在上帝ot 编辑器中可直接输入数值，无需 `[Export(PropertyHint.Range)]`（避免过度约束策划配置）
