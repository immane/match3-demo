# Task 27: 集成 — 数据资产与场景组装

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture_update.md](../design/architecture_update.md) §2 — 更新后的场景树 (PetPanel, GachaUI, PullResultOverlay) |
| ↖ 设计 | [design/architecture_update.md](../design/architecture_update.md) §5 — ServiceInitializer 完整版 (BuildServices 服务注册) |
| ↖ 设计 | [design/architecture_update.md](../design/architecture_update.md) §3 — 更新后的目录结构 (data/ 资源文件) |

## 状态
- [x] 已完成

## 依赖
- Task 15 (IPersistentStorage, GodotFileStorage, WeightedRandom, IDataSource)
- Task 16 (EventBus 扩展信号 + GameData 扩展属性)
- Task 17 (ServiceInitializer 骨架 + ServiceRegistry)
- Task 18 (ICurrencyService, CurrencyService)
- Task 19 (CurrencyDisplay UI)
- Task 20 (PetDefinition, RarityDef, PetType, PetRarity — Resource 类与枚举)
- Task 21 (PetInstance, PetCollection, IPetDataSource, ResourcePetDataSource)
- Task 22 (IPetCollectionService, PetCollectionService, PetLevelCalculator)
- Task 23 (PetCollectionPanel, PetDetailPopup, PetSlot — 宠物 UI)
- Task 24 (GachaPoolEntryResource, GachaBannerResource, GachaBanner, GachaPoolEntry — 卡池数据模型)
- Task 25 (GachaRollService, GachaPityTracker, GachaDrawService — 抽卡服务)
- Task 26 (GachaBannerUI, PullResultOverlay, GachaEntryCard — 抽卡 UI, 假定已完成)
- 现有主场景: `res://assets/scenes/main.tscn`

## 产出文件

### 新建目录与数据资产
```
data/rarities/                         [新建目录]
data/rarities/common.tres              [稀有度定义 Resource]
data/rarities/rare.tres                [稀有度定义 Resource]
data/rarities/epic.tres                [稀有度定义 Resource]
data/rarities/legendary.tres           [稀有度定义 Resource]
data/pets/                             [新建目录]
data/pets/cat_sleepy_01.tres           [示例宠物 Resource]
data/pets/cat_playful_02.tres          [示例宠物 Resource]
data/pets/dog_happy_01.tres            [示例宠物 Resource]
data/gacha/                            [新建目录]
data/gacha/standard_banner.tres        [常驻卡池 Resource]
data/gacha/limited_banner.tres         [限定卡池 Resource]
```

### 修改文件
```
project.godot                          [修改 — 注册 ServiceInitializer autoload]
scripts/autoload/ServiceInitializer.cs [修改 — 添加所有 using, 补全服务注册]
assets/scenes/main.tscn                [修改 — 添加 GachaUI, PetCollectionPanel, CurrencyDisplay 子节点]
```

## 实现要求

### 1. 目录创建

在项目根目录下创建 `data/` 及其子目录 `data/rarities/`、`data/pets/`、`data/gacha/`。

### 2. 稀有度定义 .tres 文件

创建 4 个 `RarityDef` Resource 文件（`[GlobalClass] partial class RarityDef : Resource`，定义于 Task 20）。每个文件使用 Godot 编辑器创建，确保属性类型与 `RarityDef.cs` 中 `[Export]` 声明一致：

- `data/rarities/common.tres`
  - `Rarity` = `Common` (int 0)
  - `StatMultiplier` = `1.0`
  - `DisplayColor` = `Color(0.6, 0.6, 0.6, 1)` (灰色)
  - `GachaWeight` = `70.0`
- `data/rarities/rare.tres`
  - `Rarity` = `Rare` (int 1)
  - `StatMultiplier` = `1.3`
  - `DisplayColor` = `Color(0.2, 0.5, 1, 1)` (蓝色)
  - `GachaWeight` = `22.0`
- `data/rarities/epic.tres`
  - `Rarity` = `Epic` (int 2)
  - `StatMultiplier` = `1.6`
  - `DisplayColor` = `Color(0.7, 0.2, 1, 1)` (紫色)
  - `GachaWeight` = `6.0`
- `data/rarities/legendary.tres`
  - `Rarity` = `Legendary` (int 3)
  - `StatMultiplier` = `2.0`
  - `DisplayColor` = `Color(1, 0.7, 0.1, 1)` (金色)
  - `GachaWeight` = `2.0`

关键点：
- 稀有度权重总和 = 70 + 22 + 6 + 2 = 100，对应百分比概率
- `Rarity` 属性在 Inspector 中为下拉枚举（PetRarity, int 类型）
- `DisplayColor` 在 Inspector 中为颜色选择器

### 3. 示例宠物 .tres 文件

创建 3 个 `PetDefinition` Resource 文件（定义于 Task 20）。每个包含完整属性：`Id`, `DisplayName`, `Type`, `Rarity`, `BaseLevel`, `MaxLevel`, `Description`，`Icon` 和 `SpriteSheet` 暂留空（未来补充美术资源），`Abilities` 和 `EvolutionChain` 留空数组：

- `data/pets/cat_sleepy_01.tres`
  - `Id` = `"cat_sleepy_01"`
  - `DisplayName` = `"瞌睡猫"`
  - `Type` = `Cat` (int 0)
  - `Rarity` = `Common` (int 0)
  - `BaseLevel` = `1`
  - `MaxLevel` = `30`
  - `Description` = `"永远睡不醒的小懒猫，但关键时刻意外可靠"`
  - `Icon` = 空
  - `SpriteSheet` = 空
  - `Abilities` = 空 Array[PetAbilityDef]
  - `EvolutionChain` = 空 Array[EvolutionStep]

- `data/pets/cat_playful_02.tres`
  - `Id` = `"cat_playful_02"`
  - `DisplayName` = `"活泼猫"`
  - `Type` = `Cat` (int 0)
  - `Rarity` = `Rare` (int 1)
  - `BaseLevel` = `1`
  - `MaxLevel` = `40`
  - `Description` = `"精力充沛的小家伙，消除方块时会帮忙加速"`
  - `Icon` = 空
  - `SpriteSheet` = 空
  - `Abilities` = 空 Array[PetAbilityDef]
  - `EvolutionChain` = 空 Array[EvolutionStep]

- `data/pets/dog_happy_01.tres`
  - `Id` = `"dog_happy_01"`
  - `DisplayName` = `"快乐小狗"`
  - `Type` = `Dog` (int 1)
  - `Rarity` = `Epic` (int 2)
  - `BaseLevel` = `1`
  - `MaxLevel` = `50`
  - `Description` = `"摇着尾巴的快乐小狗，连击越多越兴奋"`
  - `Icon` = 空
  - `SpriteSheet` = 空
  - `Abilities` = 空 Array[PetAbilityDef]
  - `EvolutionChain` = 空 Array[EvolutionStep]

关键点：
- `Type` 在 Inspector 中为 PetType 下拉枚举
- `Abilities` 和 `EvolutionChain` 类型为 `Godot.Collections.Array<T>`，Inspector 中可展开添加子资源
- 文件名与 `Id` 属性一致（`resource_pet_data_source` 按文件名匹配 ID）

### 4. 抽卡卡池 .tres 文件

创建 2 个 `GachaBannerResource` Resource 文件（定义于 Task 24）。`Pool` 属性为 `Godot.Collections.Array<GachaPoolEntryResource>`，需内嵌多个 `GachaPoolEntryResource` 子资源。条目覆盖 4 种稀有度，包含上述 3 只宠物：

- `data/gacha/standard_banner.tres`
  - `BannerId` = `"standard_banner"`
  - `DisplayName` = `"常驻卡池"`
  - `CostPerPull` = `50`
  - `SoftPityStart` = `70`
  - `HardPity` = `90`
  - `SoftPityRateIncrease` = `0.06`
  - `RateUpRewardId` = 空 (常驻卡池无 UP)
  - `RateUpChanceOnSSR` = `0`
  - `Pool` — 包含至少 10 个 `GachaPoolEntryResource` 子资源，推荐条目：
    - `cat_sleepy_01` (Pet, Common, Weight=12.0)
    - `dog_common_placeholder` (Pet, Common, Weight=12.0) — 占位，无对应 .tres 定义，用于填充概率表
    - `dog_common_02` (Pet, Common, Weight=11.0) — 占位
    - `bunny_common_01` (Pet, Common, Weight=11.0) — 占位
    - `bird_common_01` (Pet, Common, Weight=12.0) — 占位
    - `fox_common_01` (Pet, Common, Weight=12.0) — 占位
    - `cat_playful_02` (Pet, Rare, Weight=8.0)
    - `dog_rare_01` (Pet, Rare, Weight=8.0) — 占位
    - `bunny_rare_01` (Pet, Rare, Weight=6.0) — 占位
    - `dog_happy_01` (Pet, Epic, Weight=4.0)
    - `fox_epic_01` (Pet, Epic, Weight=2.0) — 占位
    - `bird_legendary_01` (Pet, Legendary, Weight=1.0) — 占位
    - `fox_legendary_01` (Pet, Legendary, Weight=1.0) — 占位

- `data/gacha/limited_banner.tres`
  - `BannerId` = `"limited_banner"`
  - `DisplayName` = `"限定卡池"`
  - `CostPerPull` = `50`
  - `SoftPityStart` = `70`
  - `HardPity` = `90`
  - `SoftPityRateIncrease` = `0.06`
  - `RateUpRewardId` = `"dog_happy_01"`
  - `RateUpChanceOnSSR` = `0.5`
  - `Pool` — 与常驻类似，但 `dog_happy_01` 权重提升至 `6.0`，并设置 `IsRateUp=true`，其余条目与常驻卡池相同

关键点：
- `Pool` 条目在 Godot 编辑器中可点击展开，逐条编辑内嵌 `GachaPoolEntryResource`
- 每个 `GachaPoolEntryResource` 需设置 `RewardId`, `Rarity`, `Weight`, `Type`(Pet/Accessory), `IsRateUp`
- 占位条目（如 `dog_common_placeholder`）仅存在于卡池概率表中，无对应 `.tres` 宠物定义，需要抽到此 ID 时 `PetCollectionService.AddPet` 应能优雅处理（返回 null 或跳过）
- 限定卡池中 `dog_happy_01` 的 `IsRateUp` = true，用于编辑器可视化标注
- 稀有度权重分布的绝对数字影响不大（由卡池权重归一化决定），关键是相对比例

### 5. project.godot autoload 注册

在 `project.godot` 文件的 `[autoload]` 段末尾追加：

```ini
ServiceInitializer="*res://scripts/autoload/ServiceInitializer.cs"
```

确保追加后 `[autoload]` 段为：

```ini
[autoload]

GameData="*res://scripts/autoload/GameData.cs"
EventBus="*res://scripts/autoload/EventBus.cs"
AudioManager="*res://scripts/autoload/AudioManager.cs"
ServiceInitializer="*res://scripts/autoload/ServiceInitializer.cs"
```

关键点：
- 仅追加一行，不修改已有 3 条注册
- Autoload 顺序：ServiceInitializer 注册在最后（`_Ready` 执行时 EventBus 和 GameData 已就绪）
- Godot 编辑器保存后会自动按字母/注册顺序在场景树中显示

### 6. ServiceInitializer.cs 补全

在现有 `ServiceInitializer.cs` 骨架（Task 17 产出）的基础上，确保所有 `using` 引用正确，`_Ready()` 中 `BuildServices()` 方法注册所有服务。

完整命名空间引用：

```csharp
using Godot;
using System;
using System.Collections.Generic;

namespace Match3Demo;

// ---- using 引用清单 (所有必需的 using) ----
// 基础设施
// from scripts/utils/: IPersistentStorage, GodotFileStorage, IDataSource<T>
using Match3Demo; // 同一命名空间下的类无需额外 using

// 货币系统
// from scripts/currency/models/: CurrencyType
// from scripts/currency/services/: ICurrencyService, CurrencyService (如果未放 models 子目录)

// 宠物系统
// from scripts/pets/models/: PetRarity, PetType, PetInstance, PetCollection, PetSaveData
// from scripts/pets/data/: PetDefinition, RarityDef, PetAbilityDef, EvolutionStep, IPetDataSource, ResourcePetDataSource
// from scripts/pets/services/: PetLevelCalculator, IPetCollectionService, PetCollectionService

// 抽卡系统
// from scripts/gacha/models/: RewardType, GachaPoolEntry, GachaBanner, GachaRollResult, GachaPityState, GachaPitySaveData
// from scripts/gacha/data/: GachaPoolEntryResource, GachaBannerResource, GachaBannerDataSource
// from scripts/gacha/services/: GachaRollService, GachaPityTracker, GachaDrawService
```

由于所有类均在同一命名空间 `Match3Demo` 下，`ServiceInitializer.cs` 中不需要额外 `using` 语句（除 `using Godot`），但需确保所有被引用的类文件已存在且编译通过。

`BuildServices()` 完整注册顺序（与 architecture_update.md §5 一致）：

```csharp
private void BuildServices()
{
    _registry = new ServiceRegistry();

    // 1. 持久化存储 — 最底层依赖
    FileStorage = new GodotFileStorage("user://save_data.json");
    FileStorage.LoadAll();
    _registry.Register<IPersistentStorage>(FileStorage);

    // 2. 货币服务 — 依赖持久化 + EventBus
    CurrencyService = new CurrencyService(FileStorage);
    _registry.Register<ICurrencyService>(CurrencyService);

    // 3. 宠物数据源 — 从 Resource 文件加载宠物定义
    var petDataSource = new ResourcePetDataSource("res://data/pets/");
    _registry.Register<IPetDataSource>(petDataSource);

    // 4. 宠物收藏服务 — 依赖持久化 + 数据源
    PetCollectionService = new PetCollectionService(petDataSource, EventBus.Instance);
    _registry.Register<IPetCollectionService>(PetCollectionService);

    // 5. 抽卡随机服务 — 纯逻辑，无持久化依赖
    GachaRollService = new GachaRollService();
    _registry.Register<GachaRollService>(GachaRollService);

    // 6. 抽卡保底追踪器 — 依赖持久化
    var pityTracker = new GachaPityTracker(FileStorage);
    _registry.Register<GachaPityTracker>(pityTracker);

    // 7. 卡池数据源 — 从 Resource 文件加载卡池定义
    var bannerDataSource = new GachaBannerDataSource();
    bannerDataSource.LoadAll();
    _registry.Register<IDataSource<GachaBanner>>(bannerDataSource);

    // 8. 抽卡编排服务 — 依赖所有上层服务
    GachaDrawService = new GachaDrawService(
        CurrencyService,
        GachaRollService,
        bannerDataSource,
        PetCollectionService,
        EventBus.Instance,
        pityTracker);
    _registry.Register<GachaDrawService>(GachaDrawService);

    GD.Print("[ServiceInitializer] All services initialized.");
}
```

`ServiceRegistry` 内部类定义（如 Task 17 中未包含则需补齐）：

```csharp
public class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
    }

    public T Get<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
    }
}
```

对外属性（供 UI 节点通过 `ServiceInitializer.Instance` 直接访问）：

```csharp
public static ServiceInitializer Instance { get; private set; }

private ServiceRegistry _registry;

public GodotFileStorage FileStorage { get; private set; }
public ICurrencyService CurrencyService { get; private set; }
public IPetCollectionService PetCollectionService { get; private set; }
public GachaRollService GachaRollService { get; private set; }
public GachaDrawService GachaDrawService { get; private set; }
```

`GetService<T>()` 泛型方便方法：

```csharp
public T GetService<T>() where T : class => _registry?.Get<T>();
```

生命周期：

```csharp
public override void _EnterTree()
{
    Instance = this;
}

public override void _Ready()
{
    BuildServices();
}

public override void _ExitTree()
{
    FileStorage?.SaveAll();
    Instance = null;
}
```

### 7. 主场景 UI 集成

在 `assets/scenes/main.tscn` 的 `UILayer` (CanvasLayer) 下添加 3 个新 UI 子节点。由于 Godot PackedScene 实例化引用机制，新增子节点可以为直接内联 (`type="Control"`) 声明（绑定 .cs 脚本后通过 `[tool]` 在编辑器中实例化子场景），也可以引用 `.tscn` PackedScene（如果 Task 23/Task 26 创建了对应场景文件）。

新增节点结构：

```
UILayer (CanvasLayer)
├── TitleScreen (Control)           [现有, res://assets/scenes/title_screen.tscn]
├── PauseMenu (Control)             [现有, res://assets/scenes/pause_menu.tscn]
├── GameOverPanel (Control)         [现有, res://assets/scenes/game_over_panel.tscn]
├── CurrencyDisplay (Control)       [新增 — 显示软货币余额]
├── PetCollectionPanel (Control)    [新增 — 默认 hidden=true, 从主菜单打开]
└── GachaBannerUI (Control)         [新增 — 默认 hidden=true, 从主菜单打开]
```

**CurrencyDisplay** (`scripts/currency/ui/CurrencyDisplay.cs`, Task 19 产出)：
- 需在 .tscn 中声明 `script` 扩展资源引用
- 在 Inspector 中设置 `CurrencyId` = `"soft_currency"`
- 默认 `Visible=true`，始终在 HUD 或 UILayer 顶部显示

**PetCollectionPanel** (`scripts/pets/ui/PetCollectionPanel.cs`, Task 23 产出)：
- 需在 .tscn 中声明 `script` 扩展资源引用
- 默认 `Visible=false`，通过 TitleScreen 的"宠物收藏"按钮切换显示
- 子节点结构：`ScrollContainer > GridContainer`（GridContainer 命名为 `Grid`）

**GachaBannerUI** (`scripts/gacha/ui/GachaBannerUI.cs`, Task 26 产出)：
- 需在 .tscn 中声明 `script` 扩展资源引用
- 默认 `Visible=false`，通过 TitleScreen 的"抽卡"按钮切换显示

导航逻辑（在 `TitleScreen.cs` 或 `HUD.cs` 中，Task 12 产出）：
- 添加"宠物收藏"按钮 → `GetNode<Control>("../PetCollectionPanel").Visible = true`（或通过 EventBus 发射自定义信号）
- 添加"抽卡"按钮 → `GetNode<Control>("../GachaBannerUI").Visible = true`

若 `TitleScreen` 中暂未添加导航按钮，可在此 Task 中一并修改 `TitleScreen` 添加按钮节点，或通过 `Main.cs` 暴露切换方法。

### 8. 主场景 .tscn 文件修改方式

由于 `.tscn` 为文本格式（`gd_scene format=3`），可手工编辑或通过 Godot 编辑器操作。推荐步骤：

1. 在 Godot 编辑器中打开 `main.tscn`
2. 右键 `UILayer` → `Add Child Node` → 选择 `Control`
3. 将新建的 Control 命名为 `CurrencyDisplay`，在 Inspector 中挂载 `scripts/currency/ui/CurrencyDisplay.cs`
4. 重复步骤 2-3，添加 `PetCollectionPanel` 和 `GachaBannerUI`
5. 在 Inspector 中将 `PetCollectionPanel` 和 `GachaBannerUI` 的 `Visible` 设为 `false`
6. 保存场景

等价的手工 `.tscn` 文本编辑示例（最小片段）：

```ini
[ext_resource type="Script" uid="uid://currency_display_uid" path="res://scripts/currency/ui/CurrencyDisplay.cs" id="7_currency_display"]
[ext_resource type="Script" uid="uid://pet_panel_uid" path="res://scripts/pets/ui/PetCollectionPanel.cs" id="8_pet_panel"]
[ext_resource type="Script" uid="uid://gacha_ui_uid" path="res://scripts/gacha/ui/GachaBannerUI.cs" id="9_gacha_ui"]

[node name="CurrencyDisplay" type="Control" parent="UILayer"]
script = ExtResource("7_currency_display")
CurrencyId = "soft_currency"

[node name="PetCollectionPanel" type="Control" parent="UILayer"]
visible = false
script = ExtResource("8_pet_panel")

[node name="GachaBannerUI" type="Control" parent="UILayer"]
visible = false
script = ExtResource("9_gacha_ui")
```

注意：`uid://` 为 Godot 4.4+ UID 格式，需由编辑器生成（`.godot/global_script_class_cache.cfg` 注册后自动分配）。手工编辑时可用 `uid://placeholder` 占位后由编辑器自动修正。

## 验收标准
- [ ] 所有 9 个 `.tres` 文件创建于正确的 `data/` 子目录下
- [ ] `data/rarities/*.tres` 可在 Godot 编辑器中双击打开，Inspector 中正确显示 `RarityDef` 属性 (Rarity 枚举, StatMultiplier, DisplayColor 颜色选择器, GachaWeight)
- [ ] `data/pets/*.tres` 可在 Godot 编辑器中双击打开，Inspector 中正确显示 `PetDefinition` 属性 (Id, DisplayName, Type 枚举, Rarity 枚举, MaxLevel, Description 等)
- [ ] `data/gacha/*.tres` 可在 Godot 编辑器中双击打开，Inspector 中正确显示 `GachaBannerResource` 属性，`Pool` 条目可展开编辑内嵌 `GachaPoolEntryResource`
- [ ] `standard_banner.tres` 包含至少 10 个 Pool 条目，覆盖 Common/Rare/Epic/Legendary 四种稀有度
- [ ] `limited_banner.tres` 中 `dog_happy_01` 权重提升至 `6.0`，`IsRateUp=true`，`RateUpRewardId="dog_happy_01"`
- [ ] `project.godot` 的 `[autoload]` 段成功追加 `ServiceInitializer` 注册行，已有 3 个 autoload 不受影响
- [ ] `ServiceInitializer.cs` 通过 `dotnet build` 编译（0 错误，0 警告）
- [ ] `ServiceInitializer.BuildServices()` 按正确依赖顺序注册所有服务（FileStorage → Currency → PetDataSource → PetCollection → GachaRoll → PityTracker → BannerDataSource → GachaDraw）
- [ ] `ServiceInitializer.Instance.GetService<ICurrencyService>()` 返回非 null 实例
- [ ] `ServiceInitializer.Instance.GetService<GachaDrawService>()` 返回非 null 实例
- [ ] `ServiceRegistry` 内部类正确实现 `Register<T>` 和 `Get<T>` 类型安全泛型方法
- [ ] 主场景 `main.tscn` 中 `UILayer` 下新增 `CurrencyDisplay`, `PetCollectionPanel`, `GachaBannerUI` 三个子节点
- [ ] Godot 编辑器中打开 `main.tscn` 无报错，节点树完整可见
- [ ] 游戏启动不崩溃（`_Ready()` 初始化所有服务并打印 `[ServiceInitializer] All services initialized.`）
- [ ] EventBus 所有新信号（`PetAcquired`, `GachaPullResult`, `CurrencyChanged` 等）可正常 emit 和 connect
- [ ] 货币余额可通过 `CurrencyService.GetBalance("soft_currency")` 查询，初始值默认 0
- [ ] 宠物收藏可通过 `PetCollectionService.GetAllOwnedPets()` 查询，初始为空
- [ ] `dotnet build` 0 errors 0 warnings（全局编译）

## 注意
- 本 Task 为集成收尾任务，必须在 Task 15-26 全部完成后执行
- `.tres` 文件中 `PetRarity` 和 `PetType` 枚举存储为 int 值，`DisplayColor` 存储为 `Color(r,g,b,a)` 格式
- 占位卡池条目（如 `dog_common_placeholder`）在抽到时 `PetCollectionService.AddPet` 应优雅处理不存在的宠物 ID（记录 warning 日志，跳过添加）
- 本 Task 创建的数据资产为最低可运行样例，完整数据表在后续内容更新中补充
- `HardPity` 属性名称为 Godot Resource 导出名（`GachaBannerResource.HardPity`），对应运行时 `GachaBanner.HardPityGuarantee`
- 由于所有 `.cs` 文件处于同一 `namespace Match3Demo`，`ServiceInitializer.cs` 中引用其他类无需额外 `using`（除 `using Godot` 外）
- `ServiceInitializer._ExitTree()` 中的 `FileStorage.SaveAll()` 确保退出时持久化所有数据
