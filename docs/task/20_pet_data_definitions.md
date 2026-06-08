# Task 20: 宠物数据定义层 (Resource + Enum)

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) §3 — PetDefinition, PetAbilityDef, EvolutionStep, RarityDef Resource 定义 |

## 状态
- [x] 已完成

## 依赖
- 无 (纯 Resource 定义，不依赖其他模块)
- Task 15 (枚举可能引用 PetRarity/PetType，但 Task 15 后期也可能引用本 Task 的枚举，实际为同级依赖)

## 产出文件
```
scripts/pets/data/PetDefinition.cs      [新增, GlobalClass Resource]
scripts/pets/data/PetAbilityDef.cs      [新增, GlobalClass Resource]
scripts/pets/data/EvolutionStep.cs      [新增, GlobalClass Resource]
scripts/pets/data/RarityDef.cs          [新增, GlobalClass Resource]
scripts/pets/models/PetType.cs          [新增, enum]
scripts/pets/models/PetRarity.cs        [新增, enum]
```

## 实现要求

### PetType.cs — 宠物种类枚举

```csharp
namespace Match3Demo;

public enum PetType
{
    Cat,
    Dog,
    Bunny,
    Bird,
    Fox,
    Bear
}
```

### PetRarity.cs — 稀有度枚举

```csharp
namespace Match3Demo;

public enum PetRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}
```

### PetAbilityDef.cs — 宠物能力定义 Resource

```csharp
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class PetAbilityDef : Resource
{
    [Export] public string AbilityId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public int AbilityType { get; set; } // 0=Passive, 1=Active
    [Export] public int TriggerCondition { get; set; } // 0=OnMatch, 1=OnCombo, 2=OnLevelStart
    [Export] public int EffectValue { get; set; } // 效果数值 (如加分百分比, base 100 = +100%)

    public PetAbilityDef() : this("", "", 0, 0, 0) {}

    public PetAbilityDef(string abilityId, string displayName, int abilityType, int triggerCondition, int effectValue)
    {
        AbilityId = abilityId;
        DisplayName = displayName;
        AbilityType = abilityType;
        TriggerCondition = triggerCondition;
        EffectValue = effectValue;
    }
}
```

### EvolutionStep.cs — 进化步骤 Resource

```csharp
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class EvolutionStep : Resource
{
    [Export] public string EvolvesToDefId { get; set; }
    [Export] public int RequiredLevel { get; set; } = 10;
    [Export] public int RequiredDuplicates { get; set; } = 3;
    [Export] public string RequiredItemId { get; set; }

    public EvolutionStep() : this("", 10, 3, "") {}

    public EvolutionStep(string evolvesToDefId, int requiredLevel, int requiredDuplicates, string requiredItemId)
    {
        EvolvesToDefId = evolvesToDefId;
        RequiredLevel = requiredLevel;
        RequiredDuplicates = requiredDuplicates;
        RequiredItemId = requiredItemId;
    }
}
```

### RarityDef.cs — 稀有度定义 Resource

```csharp
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class RarityDef : Resource
{
    [Export] public PetRarity Rarity { get; set; }
    [Export] public float StatMultiplier { get; set; } = 1.0f;
    [Export] public Color DisplayColor { get; set; } = Colors.White;
    [Export] public double GachaWeight { get; set; } = 1.0;

    public RarityDef() : this(PetRarity.Common, 1.0f, Colors.White, 1.0) {}

    public RarityDef(PetRarity rarity, float statMultiplier, Color displayColor, double gachaWeight)
    {
        Rarity = rarity;
        StatMultiplier = statMultiplier;
        DisplayColor = displayColor;
        GachaWeight = gachaWeight;
    }
}
```

### PetDefinition.cs — 宠物定义 Resource

```csharp
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class PetDefinition : Resource
{
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public PetType Type { get; set; }
    [Export] public PetRarity Rarity { get; set; }
    [Export] public int BaseLevel { get; set; } = 1;
    [Export] public int MaxLevel { get; set; } = 50;
    [Export] public Texture2D Icon { get; set; }
    [Export] public Texture2D SpriteSheet { get; set; }
    [Export] public int FrameCount { get; set; } = 4;
    [Export] public string Description { get; set; }
    [Export] public Godot.Collections.Array<PetAbilityDef> Abilities { get; set; } = new();
    [Export] public Godot.Collections.Array<EvolutionStep> EvolutionChain { get; set; } = new();

    public PetDefinition() : this("", "", PetType.Cat, PetRarity.Common) {}

    public PetDefinition(string id, string displayName, PetType type, PetRarity rarity)
    {
        Id = id;
        DisplayName = displayName;
        Type = type;
        Rarity = rarity;
    }
}
```

## 验收标准
- 所有 Resource 类使用 `[GlobalClass] partial class X : Resource`
- 所有 `[Export]` 属性正确标注
- `PetAbilityDef` 中 `AbilityType` 和 `TriggerCondition` 使用 `int` + 注释说明枚举值含义
- `PetDefinition` 支持嵌套 `Abilities` 和 `EvolutionChain` (`Godot.Collections.Array<T>`)
- `RarityDef` 使用 Godot `Color` 类型
- 枚举 `PetType` 和 `PetRarity` 定义为 `public enum`
- 每类 Resource 必须有无参构造函数和带参构造函数
- `dotnet build` 0 errors 0 warnings

## 注意
- 目录 `scripts/pets/data/` 和 `scripts/pets/models/` 为新增，需确保父目录存在
- 本 Task 创建的 `.cs` 文件是纯定义文件，不包含业务逻辑（如 `RarityStatMultiplier`、`RarityColor` 等计算函数属于后续服务层 Task）
- `PetAbilityDef` 的 `AbilityType`/`TriggerCondition` 在此 Task 中使用 `int` 裸值；后续 Task 可选择引入 `AbilityType`/`TriggerCondition` 枚举并在此处改为强类型（需考虑 Godot `[Export]` 对枚举的支持）
- `PetDefinition.Abilities` 为 `Godot.Collections.Array<PetAbilityDef>`，Godot 编辑器 Inspector 中可展开编辑嵌套 Resource
