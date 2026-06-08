# 架构与框架更新设计

> 在现有 GDScript → C# 转换基础之上，引入宠物、抽卡、货币三大模块系统，定义框架级变更（信号扩展、DI 容器、持久化、新工具类），不重复各子系统详细设计。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 参考 | [architecture.md](architecture.md) — 现有架构基础 |
| ← 参考 | [data_models.md](data_models.md) — 现有数据模型 |
| ↔ 同级 | `pet_system.md` — 宠物系统详细设计（外部引用） |
| ↔ 同级 | `gacha_system.md` — 抽卡系统详细设计（外部引用） |
| ↔ 同级 | `currency_system.md` — 货币系统详细设计（外部引用） |

---

## 目录

1. [架构演进概述](#1-架构演进概述)
2. [更新后的场景树](#2-更新后的场景树)
3. [更新后的目录结构](#3-更新后的目录结构)
4. [EventBus 信号扩展](#4-eventbus-信号扩展)
5. [ServiceInitializer — DI 引导器](#5-serviceinitializer--di-引导器)
6. [依赖注入模式](#6-依赖注入模式)
7. [新增通用工具](#7-新增通用工具)
8. [跨系统交互流程](#8-跨系统交互流程)
9. [向后兼容性](#9-向后兼容性)
10. [实施顺序](#10-实施顺序)
11. [附录：关键设计决策](#11-附录关键设计决策)

---

## 1. 架构演进概述

### 1.1 已完成的工作

| 阶段 | 内容 |
|------|------|
| GDScript → C# 转换 | 全部脚本已迁移至 Godot 4.6 .NET (C# 12, .NET 8.0)，命名空间 `Match3Demo` |
| Autoload 单例 | EventBus（22 个信号）、GameData（分数/步数/设置）、AudioManager（SFX 对象池） |
| 核心系统 | BoardData、MatchDetector、GravitySystem、SpawnSystem、ScoreCalculator、ValidMoveChecker |
| 游戏层 | Board、Tile、TileManager、AnimationController、GameStateMachine、InputHandler |
| UI 层 | HUD、TitleScreen、PauseMenu、GameOverPanel、FloatingTextSpawner |
| 特效层 | ParticleController、ScreenShake |

### 1.2 架构演进方向

```
┌─────────────────────────────────────────────────────────┐
│  原架构：单一消消乐系统                                    │
│                                                          │
│  EventBus (信号中枢)  ──────  解耦游戏内组件               │
│  GameData (全局状态)  ──────  分数、步数、设置              │
│  scripts/core/        ──────  纯逻辑、可测试               │
│  scripts/game/        ──────  场景节点脚本                 │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│  新架构：多系统模块化游戏                                  │
│                                                          │
│  EventBus (扩展信号)   ──────  宠物/抽卡/货币信号新增       │
│  GameData (扩展属性)   ──────  货币余额、宠物收藏            │
│  ServiceInitializer    ──────  [新增] 手动 DI 引导器        │
│  scripts/pets/         ──────  [新增] 宠物系统              │
│  scripts/gacha/        ──────  [新增] 抽卡系统              │
│  scripts/currency/     ──────  [新增] 货币系统             │
│  scripts/core/         ──────  消消乐核心（不变）            │
└─────────────────────────────────────────────────────────┘
```

### 1.3 核心理念

1. **信号解耦**（沿用）：所有跨系统通信通过 EventBus 信号，发送方不知道接收方
2. **接口驱动服务**（新增）：所有新业务逻辑定义为接口，支持替换实现和单元测试
3. **手动 DI**（新增）：`ServiceInitializer` 在 `_Ready` 中构建服务图，按需注入，避免引入第三方 DI 框架
4. **纯 C# 逻辑 + Godot UI 分离**（沿用并强化）：`core/`、`pets/`、`gacha/`、`currency/` 中的服务类不引用 `Godot.Node`，由 UI 层消费数据

---

## 2. 更新后的场景树

```
/root/
├── EventBus (Autoload)              ← [修改] 扩展宠物/抽卡/货币信号
├── GameData (Autoload)              ← [修改] 新增货币余额、宠物收藏状态
├── AudioManager (Autoload)          ← [不变]
├── ServiceInitializer (Autoload)    ← [新增] DI 引导器，_Ready 中构建所有服务
└── main_scene.tscn
    ├── Board (Node2D)               ← 消消乐核心
    │   ├── BackgroundLayer
    │   ├── TileLayer
    │   ├── EffectLayer
    │   └── InputArea
    ├── HUD (CanvasLayer)            ← [修改] 新增 CurrencyDisplay 子节点
    │   ├── ScoreLabel
    │   ├── ComboLabel
    │   ├── MovesLabel
    │   ├── LevelLabel
    │   └── CurrencyDisplay (Control) ← [新增] 金币/钻石数值显示
    ├── UILayer (CanvasLayer)        ← 现有 UI 层
    │   ├── TitleScreen (Control)
    │   ├── PauseMenu (Control)
    │   └── GameOverPanel (Control)
    ├── PetPanel (Control)           ← [新增] 宠物收藏总览 UI
    ├── GachaUI (Control)            ← [新增] 抽卡界面（Banner、单抽/十连按钮）
    └── PullResultOverlay (Control)  ← [新增] 抽卡结果展示叠加层
```

**新增 Autoload 注册**（`project.godot` 中追加）：
```ini
[autoload]
GameData="*res://scripts/autoload/GameData.cs"
EventBus="*res://scripts/autoload/EventBus.cs"
AudioManager="*res://scripts/autoload/AudioManager.cs"
ServiceInitializer="*res://scripts/autoload/ServiceInitializer.cs"
```

---

## 3. 更新后的目录结构

标记说明：`[不变]` = 完全不动，`[修改]` = 在现有文件上增补，`[新增]` = 全新创建。

```
scripts/
├── autoload/
│   ├── EventBus.cs                    [修改 — 新增 9 个信号委托]
│   ├── GameData.cs                    [修改 — 新增货币、宠物状态属性]
│   ├── AudioManager.cs                [不变]
│   └── ServiceInitializer.cs          [新增 — 手动 DI 引导器]
│
├── core/                              [不变 — 消消乐核心逻辑]
│   ├── BoardData.cs
│   ├── MatchDetector.cs
│   ├── MatchResult.cs
│   ├── GravitySystem.cs
│   ├── SpawnSystem.cs
│   ├── ScoreCalculator.cs
│   └── ValidMoveChecker.cs
│
├── pets/                              [新增 — 宠物系统]
│   ├── PetDefinition.cs               [GlobalClass Resource]
│   ├── PetInstance.cs                 [玩家拥有的宠物实例]
│   ├── PetCollection.cs               [宠物收藏数据容器]
│   ├── PetLevelCalculator.cs          [等级/进化计算]
│   ├── PetRarity.cs                    [稀有度枚举]
│   ├── IPetDataSource.cs              [宠物数据源接口]
│   ├── ResourcePetDataSource.cs       [从 Resource 文件加载宠物定义]
│   ├── PetCollectionService.cs        [宠物收藏业务逻辑]
│   └── AccessoryDefinition.cs          [饰品定义]
│
├── gacha/                             [新增 — 抽卡系统]
│   ├── GachaBanner.cs                 [抽卡卡池定义 Resource]
│   ├── GachaRollService.cs            [单次随机抽取逻辑]
│   ├── GachaPityTracker.cs            [保底计数器（持久化）]
│   ├── GachaDrawService.cs            [抽卡流程编排（扣费→抽卡→入库→发信号）]
│   ├── GachaRarity.cs                  [抽卡稀有度枚举]
│   └── GachaPullAnimation.cs          [抽卡动画控制]
│
├── currency/                          [新增 — 货币系统]
│   ├── CurrencyType.cs                 [金币/钻石枚举]
│   ├── ICurrencyService.cs            [货币操作接口]
│   ├── CurrencyService.cs             [货币增减、余额查询]
│   └── CurrencyTransactionLog.cs      [可选：交易记录]
│
├── game/                              [不变 — 消消乐游戏层]
│   ├── Board.cs
│   ├── Tile.cs
│   ├── TileManager.cs
│   ├── AnimationController.cs
│   ├── GameStateMachine.cs
│   ├── InputHandler.cs
│   ├── BackgroundLayer.cs
│   └── Main.cs
│
├── ui/                                [修改 — 新增宠物/抽卡 UI 脚本]
│   ├── HUD.cs                         [修改 — 订阅 CurrencyChanged 信号]
│   ├── TitleScreen.cs                 [不变]
│   ├── PauseMenu.cs                   [不变]
│   ├── GameOverPanel.cs               [修改 — 显示奖励货币]
│   ├── FloatingTextSpawner.cs         [不变]
│   ├── CurrencyDisplay.cs             [新增 — 货币数值显示 Label]
│   ├── PetPanel.cs                    [新增 — 宠物收藏 UI]
│   ├── GachaUI.cs                     [新增 — 抽卡界面]
│   ├── PullResultOverlay.cs           [新增 — 抽卡结果展示]
│   └── RarityRevealEffect.cs          [新增 — 稀有度揭示特效]
│
├── fx/                                [修改 — 新增稀有度特效]
│   ├── ParticleController.cs          [不变]
│   ├── ScreenShake.cs                 [不变]
│   └── RarityVfxSpawner.cs            [新增 — 按稀有度生成相应粒子]
│
└── utils/                             [修改 — 新增枚举和工具类]
    ├── Constants.cs                   [修改 — 新增货币/抽卡常量]
    ├── Enums.cs                       [修改 — 新增 PetRarity/GachaRarity 枚举]
    ├── GridUtils.cs                   [不变]
    ├── WeightedRandom.cs              [新增 — 加权随机选择]
    ├── IPersistentStorage.cs          [新增 — 持久化存储接口]
    ├── GodotFileStorage.cs            [新增 — Godot 文件持久化实现]
    └── IDataSource.cs                 [新增 — 泛型数据源接口]
```

---

## 4. EventBus 信号扩展

在现有 `EventBus.cs` 末尾新增以下信号（现有 22 个信号不变）：

```csharp
// ========================================
// signals/autoload/EventBus.cs — 新增信号
// ========================================

using Godot;

namespace Match3Demo;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    // ---- 棋盘事件（现有，不变） ----
    [Signal] public delegate void BoardInitializedEventHandler();
    [Signal] public delegate void TileSelectedEventHandler(Node2D tile, Vector2I pos);
    [Signal] public delegate void TileDeselectedEventHandler();
    [Signal] public delegate void SwapRequestedEventHandler(Vector2I from, Vector2I to);
    [Signal] public delegate void SwapCompletedEventHandler(bool valid);
    [Signal] public delegate void SwapInvalidEventHandler();

    // ---- 匹配事件（现有，不变） ----
    [Signal] public delegate void MatchesFoundEventHandler(Godot.Collections.Array matches);
    [Signal] public delegate void TilesClearedEventHandler(Godot.Collections.Array positions);
    [Signal] public delegate void SpecialTileSpawnedEventHandler(Vector2I pos, int type);
    [Signal] public delegate void CascadeTriggeredEventHandler(int depth);

    // ---- 分数事件（现有，不变） ----
    [Signal] public delegate void ScoreChangedEventHandler(int newScore, int delta);
    [Signal] public delegate void ComboUpdatedEventHandler(int combo);
    [Signal] public delegate void MovesChangedEventHandler(int remaining);
    [Signal] public delegate void TimeChangedEventHandler(float remaining);

    // ---- 游戏状态事件（现有，不变） ----
    [Signal] public delegate void GameStateChangedEventHandler(int oldState, int newState);
    [Signal] public delegate void GamePausedEventHandler();
    [Signal] public delegate void GameResumedEventHandler();
    [Signal] public delegate void LevelCompleteEventHandler();
    [Signal] public delegate void GameOverEventHandler();

    // ---- 特效事件（现有，不变） ----
    [Signal] public delegate void PlayEffectEventHandler(string effectName, Vector2 pos);
    [Signal] public delegate void ScreenShakeEventHandler(float intensity, float duration);

    // ---- UI 事件（现有，不变） ----
    [Signal] public delegate void ShowFloatingTextEventHandler(string text, Vector2 pos, Color color);

    // =============================================================
    // 以下为新增信号
    // =============================================================

    // ---- 宠物系统信号 ----
    [Signal] public delegate void PetAcquiredEventHandler(string petId, string petName, int rarity);
    [Signal] public delegate void PetLeveledUpEventHandler(string petId, string petName, int newLevel);
    [Signal] public delegate void PetEvolvedEventHandler(string petId, string petName, int evolutionStage);
    [Signal] public delegate void ActivePetChangedEventHandler(string petId, string petName);

    // ---- 抽卡系统信号 ----
    [Signal] public delegate void GachaPullResultEventHandler(string bannerId, string[] petIds, int rarity, int pullIndex);
    [Signal] public delegate void GachaMultiPullResultEventHandler(string bannerId, string[] petIds, int[] rarities);
    [Signal] public delegate void GachaPityMilestoneEventHandler(string bannerId, int pityCount, int maxPity);
    [Signal] public delegate void GachaPullStartedEventHandler(string bannerId, int pullCount);

    // ---- 货币系统信号 ----
    [Signal] public delegate void CurrencyChangedEventHandler(int currencyType, int newBalance, int delta);

    // ---- 饰品/装备系统信号 ----
    [Signal] public delegate void AccessoryEquippedEventHandler(string petId, string accessoryId);
    [Signal] public delegate void AccessoryUnequippedEventHandler(string petId, string accessoryId);

    public override void _EnterTree()
    {
        Instance = this;
    }
}
```

**订阅示例**：
```csharp
// 在 _Ready 中订阅
EventBus.Instance.PetAcquired += OnPetAcquired;
EventBus.Instance.CurrencyChanged += OnCurrencyChanged;

// 在 _ExitTree 中取消订阅
EventBus.Instance.PetAcquired -= OnPetAcquired;
EventBus.Instance.CurrencyChanged -= OnCurrencyChanged;
```

**发射示例**：
```csharp
// 货币变化 — 由 CurrencyService 发射
EventBus.Instance.EmitSignal(
    EventBus.SignalName.CurrencyChanged,
    (int)CurrencyType.Gold, newBalance, delta);

// 宠物获得 — 由 PetCollectionService 发射
EventBus.Instance.EmitSignal(
    EventBus.SignalName.PetAcquired,
    petInstance.Id, petInstance.Definition.DisplayName, (int)petInstance.Definition.Rarity);

// 抽卡结果 — 由 GachaDrawService 发射
EventBus.Instance.EmitSignal(
    EventBus.SignalName.GachaPullResult,
    bannerId, new string[] { petId }, rarity, pullIndex);
```

---

## 5. ServiceInitializer — DI 引导器

`ServiceInitializer` 是一个 Autoload Node，在 `_Ready()` 中按依赖顺序创建所有服务并建立关联。UI 节点通过 `ServiceInitializer.Instance.Get<T>()` 获取所需服务。

```csharp
using Godot;

namespace Match3Demo;

public partial class ServiceInitializer : Node
{
    public static ServiceInitializer Instance { get; private set; }

    public IPersistentStorage Storage { get; private set; }
    public ICurrencyService CurrencyService { get; private set; }
    public GachaRollService GachaRollService { get; private set; }
    public GachaPityTracker GachaPityTracker { get; private set; }
    public IPetDataSource PetDataSource { get; private set; }
    public PetCollectionService PetCollectionService { get; private set; }
    public IPetLevelCalculator PetLevelCalculator { get; private set; }
    public GachaDrawService GachaDrawService { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        BuildServices();
    }

    private void BuildServices()
    {
        // 1. 持久化层 — 依赖最底层
        Storage = new GodotFileStorage("user://save_data.json");
        Storage.LoadAll();

        // 2. 货币服务 — 依赖持久化
        CurrencyService = new CurrencyService(Storage);

        // 3. 宠物数据源 — 从 Resource 文件加载宠物定义
        PetDataSource = new ResourcePetDataSource("res://assets/data/pets/");

        // 4. 宠物等级计算器
        PetLevelCalculator = new PetLevelCalculator();

        // 5. 宠物收藏服务 — 依赖持久化 + 数据源 + 计算器
        PetCollectionService = new PetCollectionService(Storage, PetDataSource, PetLevelCalculator);

        // 6. 抽卡随机服务 — 纯逻辑，无依赖
        GachaRollService = new GachaRollService();

        // 7. 抽卡保底追踪器 — 依赖持久化
        GachaPityTracker = new GachaPityTracker(Storage);

        // 8. 抽卡编排服务 — 依赖所有上层服务
        GachaDrawService = new GachaDrawService(
            CurrencyService,
            GachaRollService,
            GachaPityTracker,
            PetCollectionService);

        // 9. 恢复服务状态（从持久化加载的数据回填）
        CurrencyService.InitializeBalances();
        PetCollectionService.LoadFromPersistence();
        GachaPityTracker.LoadFromPersistence();

        GD.Print("[ServiceInitializer] All services initialized.");
    }

    public override void _ExitTree()
    {
        Storage?.SaveAll();
    }
}
```

**UI 节点获取服务示例**：
```csharp
// 在任意 Node 脚本的 _Ready 中
public partial class GachaUI : Control
{
    private GachaDrawService _gachaDraw;
    private ICurrencyService _currency;

    public override void _Ready()
    {
        var init = ServiceInitializer.Instance;
        _gachaDraw = init.GachaDrawService;
        _currency = init.CurrencyService;

        EventBus.Instance.GachaPullResult += OnPullResult;
        EventBus.Instance.CurrencyChanged += OnCurrencyChanged;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GachaPullResult -= OnPullResult;
        EventBus.Instance.CurrencyChanged -= OnCurrencyChanged;
    }
}
```

---

## 6. 依赖注入模式

### 6.1 现有模式（保留）

```csharp
// 静态单例访问 — 适合全局基础设施
EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, score, delta);
int moves = GameData.Instance.MovesRemaining;
```

### 6.2 新增模式（构造函数注入）

```csharp
// 通过构造函数注入依赖 — 适合业务服务
public class GachaDrawService
{
    private readonly ICurrencyService _currency;
    private readonly GachaRollService _rollService;
    private readonly GachaPityTracker _pityTracker;
    private readonly PetCollectionService _petCollection;

    public GachaDrawService(
        ICurrencyService currency,
        GachaRollService rollService,
        GachaPityTracker pityTracker,
        PetCollectionService petCollection)
    {
        _currency = currency;
        _rollService = rollService;
        _pityTracker = pityTracker;
        _petCollection = petCollection;
    }

    public GachaPullResult PerformPull(string bannerId, bool isMultiPull)
    {
        // ...
    }
}
```

### 6.3 混合策略

| 类别 | 访问方式 | 说明 |
|------|---------|------|
| EventBus | `EventBus.Instance` | 全局唯一，所有系统共用 |
| GameData | `GameData.Instance` | 全局状态，跨场景持久 |
| AudioManager | `AudioManager.Instance` | 全局音频 |
| ServiceInitializer | `ServiceInitializer.Instance` | DI 注册表入口 |
| 新业务服务 | 构造函数注入 | 可替换实现、可单元测试 |
| UI 节点 | `ServiceInitializer.Instance.Get<T>()` | 通过属性访问注入的服务 |

**为什么不用第三方 DI 框架**：
- 服务数量有限（< 10 个），手动连接足够清晰
- 避免引入额外 .NET 包依赖
- 服务创建顺序有严格依赖关系，代码即文档
- Godot Node 生命周期与标准 .NET DI 容器不兼容

### 6.4 服务间通信示例

```
┌──────────────────────────────────────────────────────────┐
│  ServiceInitializer._Ready()                             │
│                                                          │
│  1. new GodotFileStorage("save.json")                    │
│       ↓                                                  │
│  2. new CurrencyService(storage)          ──→ 货币逻辑   │
│       ↓                                                  │
│  3. new ResourcePetDataSource(dir)        ──→ 数据源     │
│  4. new PetLevelCalculator()              ──→ 算法       │
│       ↓                                                  │
│  5. new PetCollectionService(storage, datasource, calc)  │
│       ↓                                                  │
│  6. new GachaRollService()                ──→ 随机逻辑   │
│  7. new GachaPityTracker(storage)         ──→ 保底逻辑   │
│       ↓                                                  │
│  8. new GachaDrawService(currency, roll, pity, pet)      │
│                                                          │
│  所有 UI 节点通过 ServiceInitializer.Instance 获取服务    │
└──────────────────────────────────────────────────────────┘
```

---

## 7. 新增通用工具

### 7.1 WeightedRandom — 加权随机选择

用于抽卡系统中按稀有度权重随机抽取。纯 C#，不依赖 Godot。

```csharp
using System;
using System.Collections.Generic;
using Godot;

namespace Match3Demo;

public static class WeightedRandom
{
    /// <summary>
    /// 从带权重的条目中按概率随机选择一个。
    /// </summary>
    /// <typeparam name="T">条目类型</typeparam>
    /// <param name="weightedItems">(item, weight) 元组列表，weight > 0</param>
    /// <returns>选中的条目</returns>
    /// <exception cref="ArgumentException">列表为空时抛出</exception>
    public static T Pick<T>(IReadOnlyList<(T item, float weight)> weightedItems)
    {
        if (weightedItems == null || weightedItems.Count == 0)
            throw new ArgumentException("Weighted items list cannot be null or empty.");

        float totalWeight = 0f;
        foreach (var (_, weight) in weightedItems)
            totalWeight += weight;

        float roll = GD.Randf() * totalWeight;
        float cumulative = 0f;

        foreach (var (item, weight) in weightedItems)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return item;
        }

        // 浮点数精度保护：返回最后一个
        return weightedItems[^1].item;
    }

    /// <summary>
    /// 从带权重的条目中按概率随机选择多个（不放回）。
    /// </summary>
    public static List<T> PickMultiple<T>(IReadOnlyList<(T item, float weight)> weightedItems, int count)
    {
        if (count <= 0)
            return new List<T>();

        var remaining = new List<(T item, float weight)>(weightedItems);
        var results = new List<T>(count);

        int actualCount = Math.Min(count, remaining.Count);
        for (int i = 0; i < actualCount; i++)
        {
            float totalWeight = 0f;
            foreach (var (_, w) in remaining) totalWeight += w;

            float roll = GD.Randf() * totalWeight;
            float cumulative = 0f;
            int selectedIndex = 0;

            for (int j = 0; j < remaining.Count; j++)
            {
                cumulative += remaining[j].weight;
                if (roll <= cumulative)
                {
                    selectedIndex = j;
                    break;
                }
            }

            results.Add(remaining[selectedIndex].item);
            remaining.RemoveAt(selectedIndex);
        }

        return results;
    }
}
```

### 7.2 IPersistentStorage 接口

```csharp
using System.Collections.Generic;

namespace Match3Demo;

/// <summary>
/// 持久化存储抽象。实现类负责序列化/反序列化具体格式（JSON 等）。
/// </summary>
public interface IPersistentStorage
{
    /// <summary>获取整数值</summary>
    int GetInt(string key, int defaultValue = 0);

    /// <summary>设置整数值</summary>
    void SetInt(string key, int value);

    /// <summary>获取字符串值</summary>
    string GetString(string key, string defaultValue = "");

    /// <summary>设置字符串值</summary>
    void SetString(string key, string value);

    /// <summary>获取浮点值</summary>
    float GetFloat(string key, float defaultValue = 0f);

    /// <summary>设置浮点值</summary>
    void SetFloat(string key, float value);

    /// <summary>获取布尔值</summary>
    bool GetBool(string key, bool defaultValue = false);

    /// <summary>设置布尔值</summary>
    void SetBool(string key, bool value);

    /// <summary>删除一个键</summary>
    void Remove(string key);

    /// <summary>检查键是否存在</summary>
    bool HasKey(string key);

    /// <summary>从持久化介质加载所有数据到内存</summary>
    void LoadAll();

    /// <summary>将所有内存数据写回持久化介质</summary>
    void SaveAll();
}
```

### 7.3 GodotFileStorage 实现

```csharp
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Match3Demo;

/// <summary>
/// 基于 Godot FileAccess 的 JSON 文件持久化实现。
/// 将所有键值对序列化为单个 JSON 文件。
/// </summary>
public class GodotFileStorage : IPersistentStorage
{
    private readonly string _filePath;
    private readonly Dictionary<string, object> _data;

    public GodotFileStorage(string filePath)
    {
        _filePath = filePath;
        _data = new Dictionary<string, object>();
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (_data.TryGetValue(key, out var value) && value is JsonElement element)
        {
            if (element.TryGetInt32(out int intVal))
                return intVal;
        }
        return defaultValue;
    }

    public void SetInt(string key, int value)
    {
        _data[key] = value;
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (_data.TryGetValue(key, out var value) && value is JsonElement element)
        {
            string str = element.GetString();
            if (str != null)
                return str;
        }
        return defaultValue;
    }

    public void SetString(string key, string value)
    {
        _data[key] = value;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (_data.TryGetValue(key, out var value) && value is JsonElement element)
        {
            if (element.TryGetSingle(out float floatVal))
                return floatVal;
        }
        return defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        _data[key] = value;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (_data.TryGetValue(key, out var value) && value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True)
                return true;
            if (element.ValueKind == JsonValueKind.False)
                return false;
        }
        return defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        _data[key] = value;
    }

    public void Remove(string key)
    {
        _data.Remove(key);
    }

    public bool HasKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public void LoadAll()
    {
        if (!FileAccess.FileExists(_filePath))
            return;

        using var file = FileAccess.Open(_filePath, FileAccess.ModeFlags.Read);
        if (file == null)
            return;

        string json = file.GetAsText();
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (deserialized != null)
            {
                _data.Clear();
                foreach (var kvp in deserialized)
                    _data[kvp.Key] = kvp.Value;
            }
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[GodotFileStorage] Failed to parse {_filePath}: {ex.Message}");
        }
    }

    public void SaveAll()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(_data, options);

        using var file = FileAccess.Open(_filePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[GodotFileStorage] Failed to write {_filePath}");
            return;
        }

        file.StoreString(json);
    }
}
```

### 7.4 IDataSource<T> 泛型接口

```csharp
using System.Collections.Generic;

namespace Match3Demo;

/// <summary>
/// 泛型数据源接口。用于解耦数据来源（Resource 文件 / 远程 API / 硬编码）。
/// </summary>
/// <typeparam name="T">数据实体类型</typeparam>
public interface IDataSource<T>
{
    /// <summary>根据 ID 获取单个数据实体</summary>
    T GetById(string id);

    /// <summary>获取所有数据实体</summary>
    IReadOnlyList<T> GetAll();

    /// <summary>检查 ID 是否存在</summary>
    bool Exists(string id);
}
```

---

## 8. 跨系统交互流程

以下为一次「十连抽」的完整信号流：

```
                        玩家点击 "十连抽" 按钮
                                │
                                ▼
┌──────────────────────────────────────────────────────────┐
│  GachaUI.OnPullButtonPressed()                           │
│      │                                                    │
│      ▼                                                    │
│  _gachaDraw.PerformPull("standard_banner", isMultiPull: true)
└───────────────────────┬──────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────┐
│  GachaDrawService.PerformPull(bannerId, isMultiPull)     │
│                                                          │
│  1. 检查货币余额                                          │
│     int cost = isMultiPull ? 300 : 30;  // 金币           │
│     if (_currency.GetBalance(CurrencyType.Gold) < cost)   │
│         → 余额不足，直接 return                            │
│                                                          │
│  2. 扣费                                                  │
│     _currency.Spend(CurrencyType.Gold, cost);            │
│         │                                                 │
│         ▼  CurrencyService 内部:                          │
│         balance -= cost;                                  │
│         _storage.SetInt("gold", balance);                │
│         EventBus.EmitSignal(CurrencyChanged,              │
│             (int)CurrencyType.Gold, balance, -cost);     │
│             │                                              │
│             ▼  UI 响应:                                    │
│             CurrencyDisplay.OnCurrencyChanged()           │
│             HUD 金币数字滚动更新                            │
│                                                          │
│  3. 发射抽卡开始信号                                       │
│     EventBus.EmitSignal(GachaPullStarted,                 │
│         bannerId, pullCount: 10);                        │
│         │                                                 │
│         ▼  UI 响应:                                        │
│         GachaUI 按钮灰化，播放"抽卡中"动画                  │
│         PullResultOverlay 显示转圈特效                     │
│                                                          │
│  4. 保底检查                                              │
│     bool isPity = _pityTracker.IsPityTriggered(bannerId);│
│                                                          │
│  5. 逐次抽取（i = 0..9）                                   │
│     for (int i = 0; i < 10; i++)                         │
│     {                                                     │
│         GachaRollResult result =                         │
│             _rollService.Roll(banner.RateTable, isPity); │
│             │                                              │
│             │  GachaRollService 内部:                      │
│             │  WeightedRandom.Pick(rateTable)             │
│             │  → 返回抽中宠物 ID + 稀有度                   │
│             │                                              │
│         // 添加到收藏                                      │
│         PetInstance pet =                                 │
│             _petCollection.AddPet(result.PetId);          │
│             │                                              │
│             │  PetCollectionService 内部:                  │
│             │  ├─ 已有同款? 转为碎片/进化材料               │
│             │  ├─ 新品? 创建 PetInstance                   │
│             │  └─ _storage.SetString("pets", json)        │
│             │                                              │
│             ▼  PetCollectionService 发射:                  │
│             EventBus.EmitSignal(PetAcquired,              │
│                 pet.Id, pet.Def.DisplayName, rarity);     │
│                 │                                          │
│                 ▼  UI 响应:                                │
│                 PetPanel.OnPetAcquired()                  │
│                 宠物列表新增条目 / 重复提示                  │
│                                                          │
│         // 更新保底计数                                    │
│         _pityTracker.Increment(bannerId, rarity);         │
│             │                                              │
│             ▼  GachaPityTracker 内部:                      │
│             若达到保底线:                                   │
│             EventBus.EmitSignal(GachaPityMilestone,       │
│                 bannerId, pityCount, maxPity);            │
│                 │                                          │
│                 ▼  UI 响应:                                │
│                 GachaUI 高亮保底计量条                      │
│                                                          │
│         results[i] = (pet.Id, rarity);                    │
│     }                                                     │
│                                                          │
│  6. 抽卡完成 — 发射结果                                     │
│     EventBus.EmitSignal(GachaMultiPullResult,             │
│         bannerId, petIds, rarities);                     │
│         │                                                 │
│         ▼  UI 全部响应:                                    │
│         PullResultOverlay 展示 10 张宠物卡                 │
│         RarityVfxSpawner 按最高稀有度生成粒子               │
│         ScreenShake 轻微震动 (SR 及以上)                    │
│         PullAnimation 依次翻牌动画                         │
│         GachaUI 恢复按钮状态                               │
│                                                          │
│  7. 持久化                                                 │
│     _storage.SaveAll();                                  │
└──────────────────────────────────────────────────────────┘
```

---

## 9. 向后兼容性

### 9.1 保证

| 组件 | 变更 | 兼容性 |
|------|------|--------|
| `scripts/core/` 全部 | 无任何修改 | 100% 兼容 |
| `scripts/game/` 全部 | 无任何修改 | 100% 兼容 |
| `BoardData.cs` | 不变 | 现有场景及测试无需改动 |
| `MatchDetector.cs` | 不变 | 现有单元测试全量通过 |
| `EventBus.cs` 现有信号 | 仅追加新信号，已有签名不变 | 现有订阅全部有效 |
| `GameData.cs` 现有属性 | 仅追加新属性，已有字段不变 | 现有代码编译无影响 |
| `project.godot` | 追加一个 Autoload 条目 | 现有 Autoload 顺序不变 |
| `assets/scenes/main.tscn` | 追加新子节点 | 现有节点路径不变 |

### 9.2 需要注意

- **EventBus 信号编号变化**：Godot 信号按声明顺序索引，新增信号后旧信号的索引不变（`[Signal]` 属性追加在末尾），不影响已编译的 `.csproj` 引用
- **GameData 序列化**：如有存档文件使用旧格式，新字段使用默认值，不会丢失数据
- **main.tscn 版本**：Godot 场景文件为文本格式，新增子节点为纯追加操作，git diff 友好

---

## 10. 实施顺序

```
Phase 1: 基础设施 ───────────────────────────────── 最先
├── IPersistentStorage 接口 + GodotFileStorage 实现
├── IDataSource<T> 接口
├── WeightedRandom 工具类
├── ServiceInitializer 骨架（空 Ready）
├── EventBus 新信号追加
├── GameData 新属性追加
├── Constants / Enums 新枚举追加
└── project.godot 注册 ServiceInitializer
         │
         ▼  Phase 1 交付：ServiceInitializer 可以创建 Storage 和空服务
         │         所有新信号和枚举可被后续代码引用
         │
Phase 2: 货币系统 ─────────────────────────────────
├── CurrencyType 枚举（Gold, Diamond）
├── ICurrencyService 接口
├── CurrencyService 实现
├── CurrencyDisplay UI 节点
└── 测试：手动增减货币，观察 UI 更新
         │
         ▼  Phase 2 交付：可独立运行货币逻辑，不依赖宠物或抽卡
         │
Phase 3: 宠物系统 ─────────────────────────────────
├── PetRarity 枚举
├── PetDefinition Resource 类
├── AccessoryDefinition Resource 类
├── PetInstance 数据类
├── PetCollection 数据容器
├── IPetLevelCalculator 接口 + PetLevelCalculator 实现
├── IPetDataSource 接口 + ResourcePetDataSource 实现
├── PetCollectionService 核心服务
├── PetPanel UI 节点
└── 测试：手动 AddPet，观察收藏更新和持久化
         │
         ▼  Phase 3 交付：宠物收藏可独立操作，读写持久化
         │
Phase 4: 抽卡系统 ─────────────────────────────────
├── GachaRarity 枚举
├── GachaBanner Resource 类
├── GachaRollService 随机抽取
├── GachaPityTracker 保底计数
├── GachaDrawService 编排流程
├── GachaUI 界面（Banner 切换、单抽/十连按钮）
├── PullResultOverlay 结果展示
└── 测试：完整抽卡流程，涵盖保底触发边界
         │
         ▼  Phase 4 交付：完整抽卡闭环，扣费→抽取→入库→UI 更新
         │
Phase 5: 整合打磨 ─────────────────────────────────
├── PullAnimation 翻牌动画
├── RarityRevealEffect 稀有度揭示特效
├── RarityVfxSpawner 按稀有度生成粒子
├── GameOverPanel 奖励货币展示
├── 抽卡与消消乐联动（消除得分 → 奖励金币）
├── 全局性能优化（ServiceInitializer 初始化耗时 < 100ms）
└── 完整回归测试
```

---

## 11. 附录：关键设计决策

| # | 决策 | 选项 A | 选项 B | 选择 | 理由 |
|---|------|--------|--------|------|------|
| 1 | DI 方式 | 引入 Microsoft.Extensions.DependencyInjection | 手动 ServiceInitializer | **B** | 服务 < 10 个，手动更透明；避免包依赖和 Godot 兼容问题 |
| 2 | 持久化格式 | Godot Resource (.tres) | JSON 文件 | **B** | JSON 可读可调试，跨平台；Resource 不适合频繁读写 |
| 3 | 宠物数据源 | 代码内 Hardcode | Godot Resource 文件 | **B** | 策划可直接编辑 `.tres`，无需改代码 |
| 4 | 抽卡随机 | 纯 C# System.Random | Godot GD.Randf() | **B** | 使用 Godot 随机种子系统，可通过项目设置控制种子 |
| 5 | 货币类型 | 单个 int + 枚举区分 | 多个独立字段 | **A** | 方便扩展新货币类型，接口统一 |
| 6 | 保底计数器存储 | 独立 JSON key | 嵌入 GachaBanner Resource | **A** | 保底是玩家状态，不是卡池定义；持久化到存档 |
| 7 | 信号粒度 | 一个信号带 enum 区分 | 多个独立信号 | **B** | `PetAcquired` vs `PetLeveledUp` 监听方不同，分离更清晰 |
| 8 | 十连抽保证规则 | 至少 1 个 SR | 至少 1 个 R+ | **A** | 十连 SR 是行业惯例，保底价值感强 |
| 9 | 重复宠物处理 | 自动转为进化碎片 | 允许重复拥有 | **A** | 减少宠物列表膨胀，碎片系统增加长期目标 |
| 10 | GameOverPanel 货币奖励 | 按消除数量计算金币 | 固定基础通关奖励 | **A** | 激励玩家优化消除效率，增加 replay value |
