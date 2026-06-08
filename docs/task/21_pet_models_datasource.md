# Task 21: 宠物运行时模型与数据源

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) §4 — 运行时模型 (PetInstance, PetCollection, PetSaveData) |
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) §5 — 数据源接口与实现 (IPetDataSource, ResourcePetDataSource) |

## 状态
- [ ] 待执行

## 依赖
- Task 15 (IDataSource 泛型接口 — `scripts/utils/IDataSource.cs`)
- Task 20 (PetDefinition, PetRarity, PetType 已定义)

## 产出文件
```
scripts/pets/models/PetInstance.cs          [新增]
scripts/pets/models/PetCollection.cs        [新增]
scripts/pets/models/PetSaveData.cs          [新增]
scripts/pets/data/IPetDataSource.cs         [新增]
scripts/pets/data/ResourcePetDataSource.cs  [新增]
```

## 实现要求

### PetInstance.cs — 纯 C# POCO，无 Godot 依赖

`scripts/pets/models/PetInstance.cs`，命名空间 `Match3Demo`。定义宠物运行时实例的可变状态，为纯 C# 类，不继承任何 Godot 类型。

```csharp
using System;

namespace Match3Demo;

public class PetInstance
{
    public string Id { get; set; }
    public string PetDefId { get; set; }
    public int Level { get; set; } = 1;
    public int CurrentXP { get; set; }
    public bool IsFavorite { get; set; }
    public string Nickname { get; set; }
    public string EquippedAccessoryId { get; set; }
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

    public int NextLevelXP => PetLevelCalculator.XPForLevel(Level + 1);
    public bool IsMaxLevel(PetDefinition def) => def != null && Level >= def.MaxLevel;
}
```

关键点：
- `NextLevelXP` 为计算属性，委托给 `PetLevelCalculator.XPForLevel(Level + 1)`
- `IsMaxLevel(PetDefinition def)` 为基于定义文件的满级判定
- `AcquiredAt` 默认值 `DateTime.UtcNow`，序列化时保留原始值
- 文件顶部无 `using Godot` 命名空间引用

### PetCollection.cs — 纯 C# POCO

`scripts/pets/models/PetCollection.cs`，命名空间 `Match3Demo`。管理玩家拥有的全部宠物实例（增删查），为纯 C# 类。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class PetCollection
{
    public List<PetInstance> OwnedPets { get; set; } = new();
    public string ActivePetId { get; set; }
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

    public bool RemovePet(string petInstanceId) =>
        OwnedPets.RemoveAll(p => p.Id == petInstanceId) > 0;

    public PetInstance GetPet(string petInstanceId) =>
        OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);

    public bool HasPet(string petDefId) =>
        OwnedPets.Any(p => p.PetDefId == petDefId);

    public int GetDuplicateCount(string petDefId) =>
        OwnedPets.Count(p => p.PetDefId == petDefId);

    public List<PetInstance> GetPetsByRarity(PetRarity rarity, IPetDataSource dataSource)
    {
        return OwnedPets.Where(p =>
        {
            var def = dataSource.GetPetDefinition(p.PetDefId);
            return def != null && def.Rarity == rarity;
        }).ToList();
    }

    public int TotalPetsOwned => OwnedPets.Count;
}
```

关键点：
- `AddPet` 自动生成 GUID 作为实例 ID，设定 `AcquiredAt = DateTime.UtcNow`
- `RemovePet` 使用 `RemoveAll` 按实例 ID 删除，返回布尔值
- `GetDuplicateCount` 返回同一 `PetDefId` 的实例数量
- `GetPetsByRarity` 需注入 `IPetDataSource` 才能按稀有度过滤
- `TotalPetsOwned` 为计算属性，直接返回列表 Count
- 文件顶部无 `using Godot` 命名空间引用

### PetSaveData.cs — 序列化 DTO

`scripts/pets/models/PetSaveData.cs`，命名空间 `Match3Demo`。定义宠物数据的序列化数据结构（Data Transfer Object），将 PetCollection 的可持久化字段与 `PetInstance` 的可序列化数据分离为独立 DTO。

```csharp
using System;
using System.Collections.Generic;

namespace Match3Demo;

public class PetSaveData
{
    public List<PetInstanceData> OwnedPets { get; set; } = new();
    public string ActivePetId { get; set; }
    public int MaxSlots { get; set; } = 50;
}

public class PetInstanceData
{
    public string Id { get; set; }
    public string PetDefId { get; set; }
    public int Level { get; set; }
    public int CurrentXP { get; set; }
    public bool IsFavorite { get; set; }
    public string Nickname { get; set; }
    public string EquippedAccessoryId { get; set; }
    public DateTime AcquiredAt { get; set; }
}
```

关键点：
- `PetSaveData.OwnedPets` 使用 `List<PetInstanceData>` 而非直接 `List<PetInstance>`，解耦序列化格式与运行时模型
- `PetInstanceData` 包含 `PetInstance` 中所有可持久化字段（不含计算属性 `NextLevelXP`、`IsMaxLevel`）
- 均为纯 C# 类，无 Godot 类型依赖

### IPetDataSource.cs — 数据源接口

`scripts/pets/data/IPetDataSource.cs`，命名空间 `Match3Demo`。抽象宠物定义数据源的获取方式，支持按 ID、全部、稀有度、类型四种维度查询。

```csharp
using System.Collections.Generic;

namespace Match3Demo;

public interface IPetDataSource
{
    PetDefinition GetPetDefinition(string id);
    IEnumerable<PetDefinition> GetAllPets();
    bool HasPet(string id);
    IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity);
    IEnumerable<PetDefinition> GetPetsByType(PetType type);
}
```

关键点：
- 返回类型使用 `IEnumerable<PetDefinition>` 以支持延迟求值和多种实现
- `HasPet` 用于快速检查 ID 是否存在，不返回完整定义
- 接口不含 Godot 命名空间引用，保持纯 C# 抽象

### ResourcePetDataSource.cs — Godot Resource 加载实现

`scripts/pets/data/ResourcePetDataSource.cs`，命名空间 `Match3Demo`。实现 `IPetDataSource`，从 Godot `.tres` Resource 文件加载宠物定义，使用 Dictionary 缓存已加载实例，使用 `DirAccess` 扫描目录实现全量加载。

```csharp
using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public class ResourcePetDataSource : IPetDataSource
{
    private readonly Dictionary<string, PetDefinition> _cache = new();
    private bool _allLoaded;

    public PetDefinition GetPetDefinition(string id)
    {
        if (_cache.TryGetValue(id, out var def))
            return def;
        def = GD.Load<PetDefinition>($"res://data/pets/{id}.tres");
        if (def != null) _cache[id] = def;
        return def;
    }

    public IEnumerable<PetDefinition> GetAllPets()
    {
        if (!_allLoaded) LoadAll();
        return _cache.Values;
    }

    public bool HasPet(string id) => GetPetDefinition(id) != null;

    public IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity)
    {
        if (!_allLoaded) LoadAll();
        foreach (var kv in _cache)
            if (kv.Value.Rarity == rarity)
                yield return kv.Value;
    }

    public IEnumerable<PetDefinition> GetPetsByType(PetType type)
    {
        if (!_allLoaded) LoadAll();
        foreach (var kv in _cache)
            if (kv.Value.Type == type)
                yield return kv.Value;
    }

    private void LoadAll()
    {
        using var dir = DirAccess.Open("res://data/pets/");
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
                    var def = GD.Load<PetDefinition>($"res://data/pets/{fileName}");
                    if (def != null) _cache[id] = def;
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        _allLoaded = true;
    }
}
```

关键点：
- 使用 `GD.Load<T>()` 按需加载单个 Resource 文件
- 使用 `DirAccess.Open()` + `ListDirBegin/GetNext/ListDirEnd` 扫描目录加载所有 `.tres`
- `_allLoaded` 标志防止重复全量扫描
- `LoadAll()` 中 `dir` 为 null 时（目录不存在）安全降级，标记已完成
- `GetPetsByRarity` 和 `GetPetsByType` 使用 `yield return` 延迟求值
- 全量加载时跳过已缓存的 ID（`_cache.ContainsKey` 防御）

## 验收标准
- [ ] PetInstance 和 PetCollection 为纯 C# POCO，文件顶部无 `using Godot`
- [ ] PetSaveData 和 PetInstanceData 为可序列化 DTO，仅包含可持久化字段（不含计算属性）
- [ ] `PetInstance.NextLevelXP` 正确委托到 `PetLevelCalculator.XPForLevel(Level + 1)`
- [ ] `PetInstance.IsMaxLevel(PetDefinition def)` 在 def 为 null 时返回 false
- [ ] `PetCollection.AddPet` 自动生成 GUID 实例 ID，设定 `AcquiredAt = DateTime.UtcNow`
- [ ] `PetCollection.RemovePet` 使用 `RemoveAll` 删除匹配实例，返回布尔值
- [ ] `PetCollection.GetPetsByRarity` 正确依赖注入 `IPetDataSource` 进行稀有度过滤
- [ ] `PetSaveData.OwnedPets` 使用 `List<PetInstanceData>` 而非 `List<PetInstance>`（DTO 模式）
- [ ] `IPetDataSource` 接口抽象数据源，包含 `HasPet` 快速检查方法
- [ ] `ResourcePetDataSource` 实现完整缓存机制（`_cache` Dictionary + `_allLoaded` 标志）
- [ ] `ResourcePetDataSource.LoadAll()` 使用 `DirAccess` 正确扫描 `res://data/pets/` 目录
- [ ] `ResourcePetDataSource` 全量加载时跳过已缓存 ID，不重复 `GD.Load`
- [ ] 当 `res://data/pets/` 目录不存在时，`LoadAll()` 安全降级不抛异常
- [ ] 所有 5 个文件可通过 `dotnet build` 编译（0 错误，0 警告）
- [ ] 命名空间统一为 `Match3Demo`
