# 宠物系统设计

> 为 Match-3 消消乐游戏提供收集、养成、进化元游戏系统，通过数据驱动架构实现完全解耦、可测试的宠物生态。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/11_pet_and_gacha_system.md](../research/11_pet_and_gacha_system.md) — 宠物与抽卡系统架构研究 |
| ← 研究 | [research/01_game_rules.md](../research/01_game_rules.md) — 游戏基础规则（匹配得分驱动宠物经验获得） |
| ↔ 同级 | [architecture.md](architecture.md) — 整体架构、EventBus 模式、文件结构规范 |
| ↔ 同级 | [data_models.md](data_models.md) — 核心数据结构设计模式（POCO 分离原则） |
| → 任务 | 宠物系统实现任务（待分配） |

---

---

## 目录

1. [概述与设计目标](#1-概述与设计目标)
2. [枚举定义](#2-枚举定义)
3. [数据定义层 (Resource)](#3-数据定义层-resource)
4. [运行时模型层 (POCO)](#4-运行时模型层-poco)
5. [数据源接口与实现](#5-数据源接口与实现)
6. [服务层](#6-服务层)
7. [进化系统](#7-进化系统)
8. [序列化与持久化](#8-序列化与持久化)
9. [EventBus 扩展信号](#9-eventbus-扩展信号)
10. [文件结构](#10-文件结构)
11. [附录：设计决策表](#11-附录设计决策表)

---

## 1. 概述与设计目标

### 1.1 为什么需要宠物系统

经典的 Match-3 游戏（如 Candy Crush）存在**单局游戏深度不足**的问题——玩家完成一关后缺少长期留存动力。宠物系统引入**元游戏进度层**（meta-game progression），通过以下机制提升玩家粘性：

- **收集驱动**：多种类、多稀有度的宠物激励玩家反复游玩
- **养成反馈**：等级提升、进化等成长体系提供持续的正向反馈
- **策略深度**：不同宠物提供不同被动/主动技能，影响消除策略
- **情感连接**：可命名、可装饰的宠物建立玩家情感投入

### 1.2 设计目标

| 目标 | 描述 | 实现策略 |
|------|------|---------|
| **解耦** | 宠物系统不依赖棋盘、UI、场景树 | 纯 C# POCO + 接口注入，仅通过 EventBus 通信 |
| **数据驱动** | 宠物属性不硬编码 | `[GlobalClass] Resource` 定义数据，.tres 文件编辑 |
| **可测试** | 所有逻辑层可单元测试 | 服务层依赖接口，不依赖 Godot Node |
| **可扩展** | 新增宠物种类、进化链、能力无需改代码 | 基于 Resource 的 ID 引用 + 枚举扩展 |
| **持久化** | 玩家宠物数据可存档 | 序列化 DTO + `IPersistentStorage` 接口 |

### 1.3 架构分层

```
┌──────────────────────────────────────────────────┐
│                   表现层 (scripts/pets/ui/)        │
│  PetCollectionPanel, PetDetailPopup,              │
│  PetLevelUpAnimation (Godot Node, 消费 EventBus)   │
├──────────────────────────────────────────────────┤
│                   服务层 (scripts/pets/services/)  │
│  IPetCollectionService, PetCollectionService,     │
│  PetLevelCalculator, PetSaveService               │
│  (纯 C# + 接口注入, 依赖 IPetDataSource)           │
├──────────────────────────────────────────────────┤
│                   数据定义层 (scripts/pets/data/)  │
│  PetDefinition, PetAbilityDef, EvolutionStep,     │
│  RarityDef  (Godot [GlobalClass] Resource)        │
├──────────────────────────────────────────────────┤
│                   运行时模型层 (scripts/pets/models/)│
│  PetInstance, PetCollection, PetSaveData          │
│  (纯 C# POCO, 零 Godot 依赖)                       │
└──────────────────────────────────────────────────┘
```

---

## 2. 枚举定义

```csharp
// scripts/pets/models/PetType.cs
namespace Match3Demo;

public enum PetType
{
    Cat   = 0,
    Dog   = 1,
    Bunny = 2,
    Bird  = 3,
    Fox   = 4,
    Bear  = 5
}

// scripts/pets/models/PetRarity.cs
namespace Match3Demo;

public enum PetRarity
{
    Common    = 0,
    Rare      = 1,
    Epic      = 2,
    Legendary = 3
}

// scripts/pets/data/PetAbilityDef.cs (部分)
namespace Match3Demo;

public enum AbilityType
{
    Passive = 0,  // 常驻生效（如分数加成）
    Active  = 1   // 触发型（如消除额外一行）
}

public enum TriggerCondition
{
    OnMatch      = 0,  // 每次匹配时触发
    OnCombo      = 1,  // 达到指定连击数时触发
    OnLevelStart = 2   // 关卡开始时触发
}
```

---

## 3. 数据定义层 (Resource)

所有宠物数据定义使用 Godot `[GlobalClass] Resource`，存储在 `.tres` 文件中，支持在 Godot 编辑器 Inspector 中直接编辑。

### 3.1 PetDefinition

```csharp
// scripts/pets/data/PetDefinition.cs
using Godot;

namespace Match3Demo;

/// <summary>
/// 宠物定义 — 不可变模板数据。
/// 一个 Resource 对应一种宠物，存储于 data/pets/ 目录。
/// </summary>
[GlobalClass]
public partial class PetDefinition : Resource
{
    /// <summary>唯一标识符，如 "cat_sleepy_01"</summary>
    [Export] public string Id { get; set; }

    /// <summary>显示名称，如 "瞌睡猫"</summary>
    [Export] public string DisplayName { get; set; }

    /// <summary>宠物种类</summary>
    [Export] public PetType Type { get; set; }

    /// <summary>稀有度</summary>
    [Export] public PetRarity Rarity { get; set; }

    /// <summary>初始等级</summary>
    [Export] public int BaseLevel { get; set; } = 1;

    /// <summary>最高等级（满级后不可继续升级，但可进化）</summary>
    [Export] public int MaxLevel { get; set; } = 50;

    /// <summary>列表缩略图</summary>
    [Export] public Texture2D Icon { get; set; }

    /// <summary>精灵表（含多帧动画）</summary>
    [Export] public Texture2D SpriteSheet { get; set; }

    /// <summary>精灵表帧数</summary>
    [Export] public int FrameCount { get; set; } = 4;

    /// <summary>风味文本描述</summary>
    [Export] public string Description { get; set; }

    /// <summary>进化链 — 该宠物可以进化到的目标（可能多个分支）</summary>
    [Export] public Godot.Collections.Array<EvolutionStep> EvolutionChain { get; set; } = new();
}
```

### 3.2 PetAbilityDef

```csharp
// scripts/pets/data/PetAbilityDef.cs
using Godot;

namespace Match3Demo;

/// <summary>
/// 宠物能力定义 — 描述一个被动或主动技能的效果和触发条件。
/// </summary>
[GlobalClass]
public partial class PetAbilityDef : Resource
{
    /// <summary>能力唯一标识</summary>
    [Export] public string AbilityId { get; set; }

    /// <summary>显示名称，如 "分数加成"</summary>
    [Export] public string DisplayName { get; set; }

    /// <summary>能力描述</summary>
    [Export] public string Description { get; set; }

    /// <summary>能力类型：被动 或 主动</summary>
    [Export] public AbilityType Type { get; set; }

    /// <summary>触发条件</summary>
    [Export] public TriggerCondition Trigger { get; set; }

    /// <summary>
    /// 效果数值。
    /// Passive: 倍率加成（如 0.15 = +15% 分数）
    /// Active:  具体效果量（如消除 1 行）
    /// </summary>
    [Export] public float EffectValue { get; set; }
}
```

### 3.3 EvolutionStep

```csharp
// scripts/pets/data/EvolutionStep.cs
using Godot;

namespace Match3Demo;

/// <summary>
/// 进化步骤 — 定义从当前宠物进化到目标宠物所需的条件。
/// 存储在 PetDefinition.EvolutionChain 数组中。
/// </summary>
[GlobalClass]
public partial class EvolutionStep : Resource
{
    /// <summary>进化目标宠物的 PetDefinition.Id</summary>
    [Export] public string EvolvesToDefId { get; set; }

    /// <summary>当前宠物需达到的最低等级</summary>
    [Export] public int RequiredLevel { get; set; }

    /// <summary>需消耗的重复宠物数量（同种类）</summary>
    [Export] public int RequiredDuplicates { get; set; }

    /// <summary>需要的特殊道具 ID（为空则不需要道具）</summary>
    [Export] public string RequiredItemId { get; set; }
}
```

### 3.4 RarityDef

```csharp
// scripts/pets/data/RarityDef.cs
using Godot;

namespace Match3Demo;

/// <summary>
/// 稀有度定义 — 定义每种稀有度的数值倍率和视觉参数。
/// 每种稀有度一个 .tres 文件（如 common.tres, rare.tres, epic.tres, legendary.tres）。
/// </summary>
[GlobalClass]
public partial class RarityDef : Resource
{
    /// <summary>对应的稀有度等级</summary>
    [Export] public PetRarity Rarity { get; set; }

    /// <summary>属性倍率（1.0 = 基准，2.0 = 翻倍）</summary>
    [Export] public float StatMultiplier { get; set; } = 1.0f;

    /// <summary>UI 显示颜色（边框、名字颜色等）</summary>
    [Export] public Color DisplayColor { get; set; }

    /// <summary>抽卡/召唤权重（越高越容易获得）</summary>
    [Export] public double GachaWeight { get; set; } = 1.0;
}
```

### 3.5 资源文件示例 (common.tres)

```tres
[gd_resource type="Resource" script_class="RarityDef" load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/pets/data/RarityDef.cs" id="1"]

[resource]
script = ExtResource("1")
Rarity = 0
StatMultiplier = 1.0
DisplayColor = Color(0.6, 0.6, 0.6, 1.0)
GachaWeight = 0.70
```

---

## 4. 运行时模型层 (POCO)

运行时模型是**纯 C# POCO**，不继承任何 Godot 类型，可以脱离 Godot 引擎进行单元测试。

### 4.1 PetInstance

```csharp
// scripts/pets/models/PetInstance.cs
using System;

namespace Match3Demo;

/// <summary>
/// 宠物实例 — 玩家拥有的一个宠物的运行时状态。
/// 纯 C# POCO，不依赖 Godot。
/// </summary>
public class PetInstance
{
    /// <summary>全局唯一实例 ID（GUID 字符串）</summary>
    public string Id { get; set; }

    /// <summary>引用的 PetDefinition.Id</summary>
    public string PetDefId { get; set; }

    /// <summary>当前等级</summary>
    public int Level { get; set; } = 1;

    /// <summary>当前累积经验值</summary>
    public int CurrentXP { get; set; } = 0;

    /// <summary>距离下一级还需的经验值</summary>
    public int XPToNextLevel => PetLevelCalculator.XPForLevel(Level + 1) - CurrentXP;

    /// <summary>是否标记为最爱的宠物</summary>
    public bool IsFavorite { get; set; }

    /// <summary>玩家自定义昵称（null 则使用默认名称）</summary>
    public string Nickname { get; set; }

    /// <summary>已装备的配饰 ID（null 则无配饰）</summary>
    public string EquippedAccessoryId { get; set; }

    /// <summary>获得时间（UTC）</summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// 添加经验值，返回是否因此升级。
    /// </summary>
    public bool AddXP(int amount)
    {
        if (Level >= 50)
            return false;

        CurrentXP += amount;
        bool leveledUp = false;

        while (CurrentXP >= PetLevelCalculator.XPForLevel(Level + 1) && Level < 50)
        {
            CurrentXP -= PetLevelCalculator.XPForLevel(Level + 1);
            Level++;
            leveledUp = true;
        }

        return leveledUp;
    }
}
```

### 4.2 PetCollection

```csharp
// scripts/pets/models/PetCollection.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

/// <summary>
/// 宠物合集 — 管理玩家拥有的所有宠物。
/// 纯 C# POCO，不依赖 Godot。
/// </summary>
public class PetCollection
{
    /// <summary>拥有的所有宠物实例</summary>
    public List<PetInstance> OwnedPets { get; set; } = new();

    /// <summary>当前主界面展示的宠物 ID</summary>
    public string ActivePetId { get; set; }

    /// <summary>最大宠物槽位数（可扩展）</summary>
    public int MaxSlots { get; set; } = 50;

    /// <summary>
    /// 添加一个新宠物实例。
    /// </summary>
    /// <returns>新创建的 PetInstance</returns>
    public PetInstance AddPet(string petDefId)
    {
        var pet = new PetInstance
        {
            Id = Guid.NewGuid().ToString(),
            PetDefId = petDefId,
            Level = 1,
            CurrentXP = 0,
            AcquiredAt = DateTime.UtcNow
        };
        OwnedPets.Add(pet);
        return pet;
    }

    /// <summary>
    /// 移除指定宠物实例。
    /// </summary>
    public bool RemovePet(string petInstanceId)
    {
        var pet = OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
        if (pet == null)
            return false;

        if (ActivePetId == petInstanceId)
            ActivePetId = null;

        return OwnedPets.Remove(pet);
    }

    /// <summary>
    /// 是否拥有指定定义的宠物（至少一只）。
    /// </summary>
    public bool HasPet(string petDefId)
    {
        return OwnedPets.Any(p => p.PetDefId == petDefId);
    }

    /// <summary>
    /// 获取指定定义 ID 的重复宠物数量。
    /// </summary>
    public int GetDuplicateCount(string petDefId)
    {
        return OwnedPets.Count(p => p.PetDefId == petDefId);
    }

    /// <summary>
    /// 获取指定定义 ID 的所有宠物实例。
    /// </summary>
    public List<PetInstance> GetPetsByDefId(string petDefId)
    {
        return OwnedPets.Where(p => p.PetDefId == petDefId).ToList();
    }

    /// <summary>
    /// 获取当前活跃宠物实例（用于技能计算）。
    /// </summary>
    public PetInstance GetActivePet()
    {
        if (ActivePetId == null)
            return null;
        return OwnedPets.FirstOrDefault(p => p.Id == ActivePetId);
    }
}
```

---

## 5. 数据源接口与实现

### 5.1 IPetDataSource 接口

```csharp
// scripts/pets/data/IPetDataSource.cs
using System.Collections.Generic;

namespace Match3Demo;

/// <summary>
/// 宠物数据源抽象 — 隔离数据加载方式。
/// 实现可以是 Resource 文件、JSON 文件、或远程服务器。
/// </summary>
public interface IPetDataSource
{
    /// <summary>根据 ID 获取单个宠物定义</summary>
    PetDefinition GetPetDefinition(string id);

    /// <summary>获取所有宠物定义</summary>
    IEnumerable<PetDefinition> GetAllPets();

    /// <summary>根据稀有度过滤</summary>
    IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity);

    /// <summary>根据种类过滤</summary>
    IEnumerable<PetDefinition> GetPetsByType(PetType type);
}
```

### 5.2 ResourcePetDataSource 实现

```csharp
// scripts/pets/data/ResourcePetDataSource.cs
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Match3Demo;

/// <summary>
/// 从 Godot .tres Resource 文件加载宠物定义。
/// 使用 Dictionary 缓存已加载的定义，避免重复 I/O。
/// </summary>
public class ResourcePetDataSource : IPetDataSource
{
    private const string DataPath = "res://data/pets/";

    private readonly Dictionary<string, PetDefinition> _cache = new();
    private List<PetDefinition> _allPetsCache;

    public PetDefinition GetPetDefinition(string id)
    {
        if (_cache.TryGetValue(id, out var def))
            return def;

        var path = $"{DataPath}{id}.tres";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[ResourcePetDataSource] 未找到宠物定义: {path}");
            return null;
        }

        def = ResourceLoader.Load<PetDefinition>(path);
        _cache[id] = def;
        return def;
    }

    public IEnumerable<PetDefinition> GetAllPets()
    {
        if (_allPetsCache != null)
            return _allPetsCache;

        _allPetsCache = new List<PetDefinition>();
        using var dir = DirAccess.Open(DataPath);
        if (dir == null)
        {
            GD.PushWarning($"[ResourcePetDataSource] 无法打开数据目录: {DataPath}");
            return _allPetsCache;
        }

        dir.ListDirBegin();
        var fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                var id = fileName.Replace(".tres", "");
                var def = GetPetDefinition(id);
                if (def != null)
                    _allPetsCache.Add(def);
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        return _allPetsCache;
    }

    public IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity)
    {
        return GetAllPets().Where(p => p.Rarity == rarity);
    }

    public IEnumerable<PetDefinition> GetPetsByType(PetType type)
    {
        return GetAllPets().Where(p => p.Type == type);
    }
}
```

---

## 6. 服务层

### 6.1 IPetCollectionService 接口

```csharp
// scripts/pets/services/IPetCollectionService.cs
using System.Collections.Generic;

namespace Match3Demo;

/// <summary>
/// 宠物合集服务接口 — 封装所有宠物增删改查操作。
/// </summary>
public interface IPetCollectionService
{
    /// <summary>添加宠物，返回新创建的实例</summary>
    PetInstance AddPet(string petDefId);

    /// <summary>移除指定实例（进化消耗等场景）</summary>
    bool RemovePet(string petInstanceId);

    /// <summary>获取所有拥有的宠物</summary>
    List<PetInstance> GetOwnedPets();

    /// <summary>给指定宠物增添经验值</summary>
    bool AddPetXP(string petInstanceId, int xpAmount);

    /// <summary>设置宠物昵称</summary>
    void SetNickname(string petInstanceId, string nickname);

    /// <summary>切换最爱标记</summary>
    void ToggleFavorite(string petInstanceId);

    /// <summary>设置活跃宠物</summary>
    void SetActivePet(string petInstanceId);

    /// <summary>获取活跃宠物</summary>
    PetInstance GetActivePet();

    /// <summary>检查是否可以进化</summary>
    bool CanEvolve(string petInstanceId, string evolutionTargetDefId);

    /// <summary>获取指定宠物的获取重复数</summary>
    int GetDuplicateCount(string petDefId);
}
```

### 6.2 PetLevelCalculator 静态类

```csharp
// scripts/pets/services/PetLevelCalculator.cs
using System;

namespace Match3Demo;

/// <summary>
/// 宠物等级与经验值计算器 — 纯静态数学类，零依赖，完全可测试。
/// </summary>
public static class PetLevelCalculator
{
    /// <summary>
    /// 计算到达指定等级所需的经验值。
    /// 公式: XP(n) = baseXP × n ^ exponent
    ///
    /// 示例（baseXP=10, exponent=1.5）:
    ///   Level 1 → 2: 10 × 1.0¹·⁵  = 10 XP
    ///   Level 49 → 50: 10 × 49¹·⁵ ≈ 3,430 XP
    /// </summary>
    public static int XPForLevel(int level, int baseXP = 10, double exponent = 1.5)
    {
        if (level <= 1)
            return 0;
        return (int)(baseXP * Math.Pow(level - 1, exponent));
    }

    /// <summary>
    /// 计算从 1 级升到指定等级所需的累计经验值。
    /// </summary>
    public static int TotalXPForLevel(int targetLevel, int baseXP = 10, double exponent = 1.5)
    {
        int total = 0;
        for (int i = 2; i <= targetLevel; i++)
            total += XPForLevel(i, baseXP, exponent);
        return total;
    }

    /// <summary>
    /// 根据稀有度返回属性倍率。
    /// </summary>
    public static float RarityStatMultiplier(PetRarity rarity)
    {
        return rarity switch
        {
            PetRarity.Common    => 1.0f,
            PetRarity.Rare      => 1.3f,
            PetRarity.Epic      => 1.6f,
            PetRarity.Legendary => 2.0f,
            _                   => 1.0f
        };
    }

    /// <summary>
    /// 获取指定稀有度的显示颜色。
    /// </summary>
    public static Godot.Color RarityColor(PetRarity rarity)
    {
        return rarity switch
        {
            PetRarity.Common    => new Godot.Color(0.60f, 0.60f, 0.60f),
            PetRarity.Rare      => new Godot.Color(0.27f, 0.50f, 1.00f),
            PetRarity.Epic      => new Godot.Color(0.67f, 0.27f, 1.00f),
            PetRarity.Legendary => new Godot.Color(1.00f, 0.75f, 0.00f),
            _                   => new Godot.Color(1.00f, 1.00f, 1.00f)
        };
    }

    /// <summary>
    /// 计算宠物能力的实际效果值（已应用稀有度倍率）。
    /// </summary>
    public static float CalculateEffectiveValue(
        float baseEffectValue,
        PetRarity rarity,
        int level)
    {
        float rarityMult = RarityStatMultiplier(rarity);
        float levelMult = 1.0f + (level - 1) * 0.02f;  // 每级 +2%
        return baseEffectValue * rarityMult * levelMult;
    }
}
```

### 6.3 PetCollectionService 实现

```csharp
// scripts/pets/services/PetCollectionService.cs
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Match3Demo;

/// <summary>
/// 宠物合集服务 — 实现 IPetCollectionService。
/// 通过构造函数注入 IPetDataSource 和 EventBus，实现依赖倒置。
/// </summary>
public class PetCollectionService : IPetCollectionService
{
    private readonly IPetDataSource _dataSource;
    private readonly PetCollection _collection;

    public PetCollectionService(IPetDataSource dataSource)
    {
        _dataSource = dataSource;
        _collection = new PetCollection();
    }

    /// <summary>从存档数据恢复集合状态（内部使用）</summary>
    internal void RestoreFromSaveData(PetSaveData saveData)
    {
        _collection.OwnedPets = saveData.OwnedPets ?? new List<PetInstance>();
        _collection.ActivePetId = saveData.ActivePetId;
        _collection.MaxSlots = saveData.MaxSlots > 0 ? saveData.MaxSlots : 50;
    }

    /// <summary>导出当前集合为存档数据（内部使用）</summary>
    internal PetSaveData ToSaveData()
    {
        return new PetSaveData
        {
            OwnedPets = _collection.OwnedPets,
            ActivePetId = _collection.ActivePetId,
            MaxSlots = _collection.MaxSlots
        };
    }

    public PetInstance AddPet(string petDefId)
    {
        var def = _dataSource.GetPetDefinition(petDefId);
        if (def == null)
        {
            GD.PushWarning($"[PetCollectionService] 无效的宠物定义 ID: {petDefId}");
            return null;
        }

        var pet = _collection.AddPet(petDefId);
        pet.Level = def.BaseLevel;

        EventBus.Instance.EmitSignal(
            EventBus.SignalName.PetAcquired, petDefId);

        // 如果是第一只宠物，自动设为活跃
        if (_collection.ActivePetId == null)
        {
            _collection.ActivePetId = pet.Id;
            EventBus.Instance.EmitSignal(
                EventBus.SignalName.ActivePetChanged, pet.Id);
        }

        return pet;
    }

    public bool RemovePet(string petInstanceId)
    {
        bool removed = _collection.RemovePet(petInstanceId);
        if (removed)
        {
            GD.Print($"[PetCollectionService] 移除宠物实例: {petInstanceId}");
        }
        return removed;
    }

    public List<PetInstance> GetOwnedPets()
    {
        return _collection.OwnedPets;
    }

    public bool AddPetXP(string petInstanceId, int xpAmount)
    {
        var pet = _collection.OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
        if (pet == null)
            return false;

        int oldLevel = pet.Level;
        bool leveledUp = pet.AddXP(xpAmount);

        if (leveledUp)
        {
            EventBus.Instance.EmitSignal(
                EventBus.SignalName.PetLeveledUp, petInstanceId, pet.Level);

            GD.Print($"[PetCollectionService] 宠物升级: {pet.PetDefId} " +
                     $"从 Lv.{oldLevel} 升至 Lv.{pet.Level}");
        }

        return leveledUp;
    }

    public void SetNickname(string petInstanceId, string nickname)
    {
        var pet = _collection.OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
        if (pet != null)
            pet.Nickname = nickname;
    }

    public void ToggleFavorite(string petInstanceId)
    {
        var pet = _collection.OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
        if (pet != null)
            pet.IsFavorite = !pet.IsFavorite;
    }

    public void SetActivePet(string petInstanceId)
    {
        var pet = _collection.OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
        if (pet == null)
            return;

        _collection.ActivePetId = petInstanceId;
        EventBus.Instance.EmitSignal(
            EventBus.SignalName.ActivePetChanged, petInstanceId);
    }

    public PetInstance GetActivePet()
    {
        return _collection.GetActivePet();
    }

    public bool CanEvolve(string petInstanceId, string evolutionTargetDefId)
    {
        var pet = _collection.OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
        if (pet == null)
            return false;

        var def = _dataSource.GetPetDefinition(pet.PetDefId);
        if (def == null || def.EvolutionChain == null)
            return false;

        foreach (var step in def.EvolutionChain)
        {
            if (step.EvolvesToDefId != evolutionTargetDefId)
                continue;

            // 检查等级
            if (pet.Level < step.RequiredLevel)
                return false;

            // 检查重复数量
            if (step.RequiredDuplicates > 0)
            {
                int duplicates = _collection.GetDuplicateCount(pet.PetDefId) - 1; // 减去自身
                if (duplicates < step.RequiredDuplicates)
                    return false;
            }

            // 检查道具（当前版本道具系统未实现，忽略道具检查）
            return true;
        }

        return false;
    }

    public int GetDuplicateCount(string petDefId)
    {
        return _collection.GetDuplicateCount(petDefId);
    }
}
```

---

## 7. 进化系统

### 7.1 进化流程

```
客户端请求进化(petInstanceId, targetDefId)
          │
          ▼
  ┌──────────────────────────┐
  │ 1. 检查进化条件           │
  │    ├─ 宠物是否存在         │
  │    ├─ 等级是否达标         │
  │    ├─ 重复数是否足够       │
  │    └─ 道具是否持有         │
  └──────────┬───────────────┘
             │ 条件满足
             ▼
  ┌──────────────────────────┐
  │ 2. 消耗进化材料           │
  │    ├─ 移除指定数量的重复   │
  │    └─ 消耗道具（若有）     │
  └──────────┬───────────────┘
             │
             ▼
  ┌──────────────────────────┐
  │ 3. 创建进化后实例         │
  │    ├─ 保留原始 ID（可选）  │
  │    ├─ 重置为 BaseLevel    │
  │    └─ 保留 Nickname       │
  └──────────┬───────────────┘
             │
             ▼
  ┌──────────────────────────┐
  │ 4. 触发信号               │
  │    ├─ PetEvolved 发射     │
  │    └─ 继承活跃状态         │
  └──────────────────────────┘
```

### 7.2 进化系统实现

```csharp
// scripts/pets/services/PetCollectionService.cs (追加方法)

/// <summary>
/// 执行宠物进化。
/// </summary>
/// <param name="petInstanceId">要进化的宠物实例 ID</param>
/// <param name="evolutionTargetDefId">进化目标 PetDefinition.Id</param>
/// <returns>进化后新宠物的实例，失败则返回 null</returns>
public PetInstance EvolvePet(string petInstanceId, string evolutionTargetDefId)
{
    var oldPet = _collection.OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
    if (oldPet == null)
    {
        GD.PushWarning($"[PetCollectionService] 未找到宠物实例: {petInstanceId}");
        return null;
    }

    var oldDef = _dataSource.GetPetDefinition(oldPet.PetDefId);
    if (oldDef == null || oldDef.EvolutionChain == null)
        return null;

    // 查找匹配的进化步骤
    EvolutionStep matchingStep = null;
    foreach (var step in oldDef.EvolutionChain)
    {
        if (step.EvolvesToDefId == evolutionTargetDefId)
        {
            matchingStep = step;
            break;
        }
    }

    if (matchingStep == null)
    {
        GD.PushWarning($"[PetCollectionService] 未找到进化步骤: " +
                       $"{oldPet.PetDefId} → {evolutionTargetDefId}");
        return null;
    }

    if (!CanEvolve(petInstanceId, evolutionTargetDefId))
    {
        GD.PushWarning($"[PetCollectionService] 进化条件不满足");
        return null;
    }

    // 消耗重复宠物
    if (matchingStep.RequiredDuplicates > 0)
    {
        var duplicates = _collection.GetPetsByDefId(oldPet.PetDefId);
        int consumed = 0;
        foreach (var dup in duplicates)
        {
            if (dup.Id == petInstanceId)
                continue;  // 跳过自身
            if (consumed >= matchingStep.RequiredDuplicates)
                break;
            _collection.RemovePet(dup.Id);
            consumed++;
        }
    }

    // 移除原宠物
    _collection.RemovePet(petInstanceId);
    string oldPetInstanceId = petInstanceId;

    // 创建进化后宠物
    var newDef = _dataSource.GetPetDefinition(evolutionTargetDefId);
    var newPet = new PetInstance
    {
        Id = Guid.NewGuid().ToString(),
        PetDefId = evolutionTargetDefId,
        Level = newDef.BaseLevel,
        CurrentXP = 0,
        Nickname = oldPet.Nickname,  // 保留昵称
        IsFavorite = oldPet.IsFavorite,  // 保留最爱标记
        AcquiredAt = DateTime.UtcNow
    };
    _collection.OwnedPets.Add(newPet);

    // 如果原宠物是活跃宠物，进化后的宠物自动成为活跃
    if (_collection.ActivePetId == oldPetInstanceId)
    {
        _collection.ActivePetId = newPet.Id;
        EventBus.Instance.EmitSignal(
            EventBus.SignalName.ActivePetChanged, newPet.Id);
    }

    // 触发进化信号
    EventBus.Instance.EmitSignal(
        EventBus.SignalName.PetEvolved, oldPetInstanceId, evolutionTargetDefId);

    GD.Print($"[PetCollectionService] 进化成功: " +
             $"{oldDef.DisplayName} → {newDef.DisplayName}");

    return newPet;
}
```

### 7.3 进化规则参考表

| 稀有度 | 典型进化次数 | 等级要求（第一/第二/最终） | 重复需求 | 道具需求 |
|--------|-------------|--------------------------|---------|---------|
| Common | 1 → 2 次 | Lv.10 / Lv.30 | 3 / 5 只 | 无 / 进化石 |
| Rare | 1 → 2 次 | Lv.15 / Lv.35 | 2 / 3 只 | 无 / 进化石·蓝 |
| Epic | 1 次 | Lv.25 | 1 只 | 进化石·紫 |
| Legendary | 0 → 1 次 | Lv.50 | 1 只 | 传说进化石 |

### 7.4 进化链示例 (cat_sleepy_01.tres)

```tres
[gd_resource type="Resource" script_class="PetDefinition" load_steps=3 format=3]

[ext_resource type="Script" path="res://scripts/pets/data/PetDefinition.cs" id="1"]
[ext_resource type="Script" path="res://scripts/pets/data/EvolutionStep.cs" id="2"]

[resource]
script = ExtResource("1")
Id = "cat_sleepy_01"
DisplayName = "瞌睡猫"
Type = 0
Rarity = 0
BaseLevel = 1
MaxLevel = 50
Description = "总是睡眼惺忪的猫咪，但消除时意外地专注。"
EvolutionChain = Array[ExtResource("2")]([
    {
        "EvolvesToDefId": "cat_graceful_01",
        "RequiredLevel": 10,
        "RequiredDuplicates": 3,
        "RequiredItemId": ""
    },
    {
        "EvolvesToDefId": "cat_mystic_01",
        "RequiredLevel": 30,
        "RequiredDuplicates": 5,
        "RequiredItemId": "evo_stone_rare"
    }
])
```

---

## 8. 序列化与持久化

### 8.1 PetSaveData DTO

```csharp
// scripts/pets/models/PetSaveData.cs
using System.Collections.Generic;

namespace Match3Demo;

/// <summary>
/// 宠物存档数据 DTO — 序列化专用。
/// 仅包含需要持久化的字段，不包含运行时计算属性。
/// </summary>
public class PetSaveData
{
    /// <summary>拥有的所有宠物实例</summary>
    public List<PetInstance> OwnedPets { get; set; } = new();

    /// <summary>当前活跃宠物 ID</summary>
    public string ActivePetId { get; set; }

    /// <summary>最大槽位数</summary>
    public int MaxSlots { get; set; } = 50;
}
```

### 8.2 IPersistentStorage 接口

```csharp
// scripts/core/IPersistentStorage.cs
using System.Threading.Tasks;

namespace Match3Demo;

/// <summary>
/// 持久化存储抽象 — 与实际存储方式解耦。
/// 实现可以是本地文件、云端、或内存（测试用）。
/// </summary>
public interface IPersistentStorage
{
    Task<T> LoadAsync<T>(string key) where T : class;
    Task SaveAsync<T>(string key, T data) where T : class;
    bool Exists(string key);
}
```

### 8.3 GodotFileStorage 实现

```csharp
// scripts/core/GodotFileStorage.cs
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace Match3Demo;

/// <summary>
/// 基于 Godot FileAccess 的文件持久化实现。
/// 存储于 user://saves/ 目录。
/// </summary>
public class GodotFileStorage : IPersistentStorage
{
    private const string SaveDir = "user://saves/";

    public async Task<T> LoadAsync<T>(string key) where T : class
    {
        var path = $"{SaveDir}{key}.json";
        await Task.Yield();  // 让出主线程

        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;

        var json = file.GetAsText();
        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SaveAsync<T>(string key, T data) where T : class
    {
        var path = $"{SaveDir}{key}.json";
        await Task.Yield();

        // 确保目录存在
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(data, options);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }

    public bool Exists(string key)
    {
        return FileAccess.FileExists($"{SaveDir}{key}.json");
    }
}
```

### 8.4 PetSaveService

```csharp
// scripts/pets/services/PetSaveService.cs
using System;
using System.Threading.Tasks;
using Godot;

namespace Match3Demo;

/// <summary>
/// 宠物存档服务 — 负责 PetCollection 的持久化读写。
/// </summary>
public class PetSaveService
{
    private readonly IPersistentStorage _storage;
    private readonly PetCollectionService _collectionService;
    private const string SaveKey = "pet_collection";

    public PetSaveService(
        IPersistentStorage storage,
        PetCollectionService collectionService)
    {
        _storage = storage;
        _collectionService = collectionService;
    }

    /// <summary>
    /// 从持久化存储加载宠物合集数据。
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            var saveData = await _storage.LoadAsync<PetSaveData>(SaveKey);
            if (saveData != null)
            {
                _collectionService.RestoreFromSaveData(saveData);
                GD.Print($"[PetSaveService] 加载成功: " +
                         $"{saveData.OwnedPets?.Count ?? 0} 只宠物");
            }
            else
            {
                GD.Print("[PetSaveService] 无存档数据，使用空合集");
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"[PetSaveService] 加载失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存宠物合集数据到持久化存储。
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var saveData = _collectionService.ToSaveData();
            await _storage.SaveAsync(SaveKey, saveData);
            GD.Print($"[PetSaveService] 保存成功: " +
                     $"{saveData.OwnedPets?.Count ?? 0} 只宠物");
        }
        catch (Exception ex)
        {
            GD.PushError($"[PetSaveService] 保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 自动保存 — 在宠物合集变更时调用。
    /// 可通过 EventBus 信号触发。
    /// </summary>
    public void SubscribeToAutoSave()
    {
        EventBus.Instance.PetAcquired += (string defId) => _ = SaveAsync();
        EventBus.Instance.PetLeveledUp += (string id, int level) => _ = SaveAsync();
        EventBus.Instance.PetEvolved += (string oldId, string newDefId) => _ = SaveAsync();
        EventBus.Instance.ActivePetChanged += (string id) => _ = SaveAsync();
    }
}
```

---

## 9. EventBus 扩展信号

以下信号需要添加到 `scripts/autoload/EventBus.cs` 中。

```csharp
// 添加到 EventBus.cs 的信号声明区域

/// <summary>宠物获得（新增一只宠物）</summary>
[Signal] public delegate void PetAcquiredEventHandler(string petDefId);

/// <summary>宠物升级（返回实例 ID 和新等级）</summary>
[Signal] public delegate void PetLeveledUpEventHandler(string petInstanceId, int newLevel);

/// <summary>宠物进化完成（返回旧实例 ID 和新宠物定义 ID）</summary>
[Signal] public delegate void PetEvolvedEventHandler(string oldPetInstanceId, string newPetDefId);

/// <summary>活跃宠物切换</summary>
[Signal] public delegate void ActivePetChangedEventHandler(string petInstanceId);
```

### 9.1 信号消费示例

```csharp
// scripts/pets/ui/PetLevelUpAnimation.cs
using Godot;

namespace Match3Demo;

/// <summary>
/// 宠物升级动画 — 监听 PetLeveledUp 信号并播放升级特效。
/// </summary>
public partial class PetLevelUpAnimation : Control
{
    [Export] private AnimationPlayer _animPlayer;
    [Export] private Label _levelLabel;

    public override void _Ready()
    {
        EventBus.Instance.PetLeveledUp += OnPetLeveledUp;
    }

    private void OnPetLeveledUp(string petInstanceId, int newLevel)
    {
        _levelLabel.Text = $"Lv.{newLevel}!";
        _animPlayer?.Play("level_up_flash");
    }
}

// scripts/pets/ui/PetCollectionPanel.cs
using Godot;

namespace Match3Demo;

/// <summary>
/// 宠物合集面板 — 展示所有拥有的宠物列表。
/// </summary>
public partial class PetCollectionPanel : Control
{
    private IPetCollectionService _petService;
    private IPetDataSource _dataSource;

    public void Initialize(IPetCollectionService petService, IPetDataSource dataSource)
    {
        _petService = petService;
        _dataSource = dataSource;
        EventBus.Instance.PetAcquired += OnPetAcquired;
        EventBus.Instance.PetEvolved += OnPetEvolved;
        RefreshList();
    }

    private void OnPetAcquired(string petDefId)
    {
        RefreshList();
    }

    private void OnPetEvolved(string oldPetInstanceId, string newPetDefId)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        // 清空并重建宠物列表 UI
        foreach (var pet in _petService.GetOwnedPets())
        {
            var def = _dataSource.GetPetDefinition(pet.PetDefId);
            // 创建列表项 UI 元素...
        }
    }
}
```

### 9.2 信号流向图

```
                    ┌──────────────────┐
                    │  PetCollection   │
                    │  Service         │
                    └───┬──────┬───┬───┘
                        │      │   │
          PetAcquired ──┘      │   └── ActivePetChanged
                               │
                    PetLeveledUp
                               │
                        PetEvolved
                        │      │       │
            ┌───────────┼──────┼───────┼───────────┐
            ▼           ▼      ▼       ▼           ▼
    ┌──────────┐ ┌────────┐ ┌──────┐ ┌──────────┐
    │ 宠物合集  │ │升级动画 │ │进化   │ │ 成就系统  │
    │ UI Panel │ │        │ │动画   │ │          │
    └──────────┘ └────────┘ └──────┘ └──────────┘
```

---

## 10. 文件结构

```
scripts/pets/
├── data/
│   ├── PetDefinition.cs          # [GlobalClass] Resource — 宠物定义
│   ├── PetAbilityDef.cs          # [GlobalClass] Resource — 能力定义
│   ├── EvolutionStep.cs          # [GlobalClass] Resource — 进化步骤
│   ├── RarityDef.cs              # [GlobalClass] Resource — 稀有度定义
│   ├── IPetDataSource.cs         # Interface — 数据源抽象
│   └── ResourcePetDataSource.cs  # Class — Resource 文件加载 + 缓存
│
├── models/
│   ├── PetInstance.cs            # Pure C# POCO — 宠物运行时实例
│   ├── PetCollection.cs          # Pure C# POCO — 宠物合集容器
│   ├── PetSaveData.cs            # Pure C# DTO — 序列化数据结构
│   ├── PetType.cs                # Enum — 宠物种类
│   └── PetRarity.cs              # Enum — 稀有度
│
├── services/
│   ├── IPetCollectionService.cs  # Interface — 合集操作抽象
│   ├── PetCollectionService.cs   # Class — 合集操作实现
│   ├── PetLevelCalculator.cs     # Static Class — 等级/经验计算
│   └── PetSaveService.cs         # Class — 持久化读写
│
└── ui/
    ├── PetCollectionPanel.cs     # Godot Control — 宠物列表面板
    ├── PetDetailPopup.cs         # Godot Control — 宠物详情弹窗
    └── PetLevelUpAnimation.cs    # Godot Control — 升级动画

data/pets/                         # Godot Resource 数据文件
├── cat_sleepy_01.tres
├── cat_playful_01.tres
├── dog_loyal_01.tres
├── bunny_swift_01.tres
├── bird_song_01.tres
├── fox_cunning_01.tres
├── bear_mighty_01.tres
└── ...

data/rarities/                     # 稀有度定义
├── common.tres
├── rare.tres
├── epic.tres
└── legendary.tres
```

---

## 11. 附录：设计决策表

### 11.1 架构决策

| # | 决策项 | 选项 A | 选项 B | 选择 | 理由 |
|---|--------|--------|--------|------|------|
| 1 | 数据定义格式 | Godot `[GlobalClass] Resource` (.tres) | JSON + `System.Text.Json` | **A** | 编辑器可视化；类型安全；内置序列化；团队其他系统沿用此模式 |
| 2 | 运行时模型 | POCO（纯 C# 类） | Godot Node / Resource | **A** | 零引擎依赖，可单元测试；可在非 Godot 环境运行逻辑 |
| 3 | 依赖注入 | 构造函数注入（手动 DI） | 全局 Autoload 单例 | **A**（服务层）、**B**（EventBus/GameData） | 混合模式：新服务用构造函数注入提高可测试性；全局基础设施保留 Autoload 模式 |
| 4 | 服务定位 | 通过 `ServiceRegistry` 手动 DI | `Microsoft.Extensions.DI` NuGet | **A** | 避免额外 NuGet 依赖；项目规模不需要全功能 DI 容器 |
| 5 | 信号通信 | Godot Signal via EventBus | C# `event` / `Action` | **A** | 与项目现有模式一致；Godot Signal 原生支持场景树生命周期管理 |
| 6 | 经验值曲线 | 幂函数 `base × n^exponent` | 查表法（预定义数组） | **A** | 参数化程度高；易于调整；内存占用小 |
| 7 | 持久化格式 | JSON (`System.Text.Json`) | Godot `ResourceSaver` (.tres) | **A** | JSON 跨平台通用；易于调试和云同步；.NET 8 内置 AOT 兼容 |
| 8 | 进化消耗 | 消耗重复宠物实例 | 仅消耗等级 + 道具 | **A** | 增加重复宠物价值，鼓励继续抽卡/收集；行业常见做法 |

### 11.2 进化系统决策

| # | 决策项 | 选项 A | 选项 B | 选择 | 理由 |
|---|--------|--------|--------|------|------|
| 9 | 进化后保留数据 | 保留昵称、最爱标记 | 完全重置 | **A** | 保留玩家情感投入；减少挫败感 |
| 10 | 进化分支 | 支持多条进化链（分支） | 单线进化 | **A** | 增加策略深度和收集重玩价值 |
| 11 | 进化 ID 策略 | 创建全新的 PetInstance（新 GUID） | 原地修改 PetDefId | **A** | 保留进化历史记录可能；清晰的生命周期语义 |

### 11.3 数据流向

| # | 决策项 | 选项 A | 选项 B | 选择 | 理由 |
|---|--------|--------|--------|------|------|
| 12 | XP 来源 | 匹配得分按比例转化 | 独立任务系统发放 | **A**（初期阶段） | 与核心玩法直接关联，减少额外系统复杂度 |
| 13 | 活跃宠物技能 | 在 MatchDetector 阶段查询 | 在分数计算阶段查询 | **B** | 技能效果最终体现在分数加成，不改变匹配算法本身 |

### 11.4 稀有度视觉效果参考

| 稀有度 | 颜色 | RGB | 基础权重 | 典型 XP 倍率 |
|--------|------|-----|---------|-------------|
| Common | 灰色 | `(0.60, 0.60, 0.60)` | 0.70 | 1.0× |
| Rare | 蓝色 | `(0.27, 0.50, 1.00)` | 0.20 | 1.3× |
| Epic | 紫色 | `(0.67, 0.27, 1.00)` | 0.08 | 1.6× |
| Legendary | 金色 | `(1.00, 0.75, 0.00)` | 0.02 | 2.0× |
