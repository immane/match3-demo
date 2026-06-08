# Task 22: 宠物系统服务层

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) §6 (服务层), §7 (进化系统), §8 (序列化与持久化) |

## 状态
- [ ] 待执行

## 依赖
- Task 20 (PetDefinition, PetRarity — 枚举与 Resource 定义)
- Task 21 (PetInstance, PetCollection, IPetDataSource, PetSaveData — 运行时模型)
- Task 15 (IPersistentStorage — 持久化存储接口)
- Task 16 (EventBus PetAcquired / PetLeveledUp / PetEvolved / ActivePetChanged 信号)

## 产出文件
```
scripts/pets/services/PetLevelCalculator.cs      [新增]
scripts/pets/services/IPetCollectionService.cs    [新增]
scripts/pets/services/PetCollectionService.cs     [新增]
scripts/pets/services/PetSaveService.cs           [新增]
```

## 实现要求

### PetLevelCalculator.cs — 纯静态计算类

```csharp
using System;

namespace Match3Demo;

public static class PetLevelCalculator
{
    /// XP(n) = baseXP * n^exponent
    public static int XPForLevel(int level, int baseXP = 10, double exponent = 1.5)
    {
        if (level <= 0) return 0;
        return (int)(baseXP * Math.Pow(level, exponent));
    }

    /// Total XP needed from level 1 to target level
    public static int TotalXPForLevel(int targetLevel, int baseXP = 10)
    {
        int total = 0;
        for (int i = 1; i < targetLevel; i++)
            total += XPForLevel(i, baseXP);
        return total;
    }

    /// Rarity-based stat multiplier
    public static float RarityStatMultiplier(PetRarity rarity) => rarity switch
    {
        PetRarity.Common => 1.0f,
        PetRarity.Rare => 1.3f,
        PetRarity.Epic => 1.6f,
        PetRarity.Legendary => 2.0f,
        _ => 1.0f
    };

    /// XP reward from match-3 gameplay based on combo/score
    public static int XPFromMatch(int matchScore, int comboLevel)
    {
        int baseXP = matchScore / 10;
        int comboBonus = comboLevel * 5;
        return Math.Max(1, baseXP + comboBonus);
    }

    /// Check if a pet can level up
    public static bool CanLevelUp(PetInstance pet, PetDefinition def)
    {
        if (pet.IsMaxLevel(def)) return false;
        return pet.CurrentXP >= XPForLevel(pet.Level + 1);
    }

    /// Level up a pet, returning levels gained
    public static int LevelUp(ref PetInstance pet, PetDefinition def)
    {
        int levelsGained = 0;
        while (CanLevelUp(pet, def) && levelsGained < 10) // max 10 levels per batch
        {
            pet.CurrentXP -= XPForLevel(pet.Level + 1);
            pet.Level++;
            levelsGained++;
        }
        return levelsGained;
    }
}
```

### IPetCollectionService.cs

```csharp
namespace Match3Demo;

public interface IPetCollectionService
{
    PetInstance AddPet(string petDefId);
    PetInstance GetPet(string petInstanceId);
    List<PetInstance> GetAllOwnedPets();
    bool HasPet(string petDefId);
    int GetDuplicateCount(string petDefId);
    int AddXP(string petInstanceId, int amount);
    bool TryEvolve(string petInstanceId);
    bool SetFavorite(string petInstanceId, bool isFavorite);
    bool SetNickname(string petInstanceId, string nickname);
    bool SetActivePet(string petInstanceId);
    string GetActivePetId();
    Task SaveAsync();
    Task LoadAsync();
}
```

### PetCollectionService.cs — 完整实现

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Match3Demo;

public class PetCollectionService : IPetCollectionService
{
    private readonly IPetDataSource _dataSource;
    private readonly EventBus _eventBus;
    private readonly IPersistentStorage _storage;
    private PetCollection _collection = new();

    public PetCollectionService(IPetDataSource dataSource, EventBus eventBus,
        IPersistentStorage storage = null)
    {
        _dataSource = dataSource;
        _eventBus = eventBus;
        _storage = storage;
    }

    public PetInstance AddPet(string petDefId)
    {
        var pet = _collection.AddPet(petDefId);
        _eventBus.EmitSignal(EventBus.SignalName.PetAcquired, petDefId);
        _ = SaveIfAvailable();
        return pet;
    }

    public PetInstance GetPet(string petInstanceId) => _collection.GetPet(petInstanceId);

    public List<PetInstance> GetAllOwnedPets() => _collection.OwnedPets;

    public bool HasPet(string petDefId) => _collection.HasPet(petDefId);

    public int GetDuplicateCount(string petDefId) => _collection.GetDuplicateCount(petDefId);

    public int AddXP(string petInstanceId, int amount)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return 0;
        var def = _dataSource.GetPetDefinition(pet.PetDefId);
        if (def == null) return 0;

        pet.CurrentXP += amount;
        int levelsGained = PetLevelCalculator.LevelUp(ref pet, def);
        if (levelsGained > 0)
        {
            _eventBus.EmitSignal(EventBus.SignalName.PetLeveledUp, petInstanceId, pet.Level);
        }
        _ = SaveIfAvailable();
        return levelsGained;
    }

    public bool TryEvolve(string petInstanceId)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        var def = _dataSource.GetPetDefinition(pet.PetDefId);
        if (def?.EvolutionChain == null || def.EvolutionChain.Count == 0) return false;

        foreach (var step in def.EvolutionChain)
        {
            if (pet.Level >= step.RequiredLevel &&
                _collection.GetDuplicateCount(pet.PetDefId) >= step.RequiredDuplicates &&
                string.IsNullOrEmpty(step.RequiredItemId))
            {
                pet.PetDefId = step.EvolvesToDefId;
                pet.Level = 1;
                pet.CurrentXP = 0;
                _eventBus.EmitSignal(EventBus.SignalName.PetEvolved, petInstanceId, step.EvolvesToDefId);
                _ = SaveIfAvailable();
                return true;
            }
        }
        return false;
    }

    public bool SetFavorite(string petInstanceId, bool isFavorite)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        pet.IsFavorite = isFavorite;
        return true;
    }

    public bool SetNickname(string petInstanceId, string nickname)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        pet.Nickname = nickname;
        return true;
    }

    public bool SetActivePet(string petInstanceId)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        _collection.ActivePetId = petInstanceId;
        _eventBus.EmitSignal(EventBus.SignalName.ActivePetChanged, petInstanceId);
        return true;
    }

    public string GetActivePetId() => _collection.ActivePetId;

    public async Task SaveAsync()
    {
        if (_storage == null) return;
        var saveData = new PetSaveData
        {
            OwnedPets = _collection.OwnedPets.Select(p => new PetInstanceData
            {
                Id = p.Id, PetDefId = p.PetDefId, Level = p.Level,
                CurrentXP = p.CurrentXP, IsFavorite = p.IsFavorite,
                Nickname = p.Nickname, EquippedAccessoryId = p.EquippedAccessoryId,
                AcquiredAt = p.AcquiredAt
            }).ToList(),
            ActivePetId = _collection.ActivePetId,
            MaxSlots = _collection.MaxSlots
        };
        await _storage.SaveAsync("pet_collection", saveData);
    }

    public async Task LoadAsync()
    {
        if (_storage == null) return;
        var data = await _storage.LoadAsync<PetSaveData>("pet_collection");
        if (data == null) return;

        _collection.OwnedPets = data.OwnedPets.Select(d => new PetInstance
        {
            Id = d.Id, PetDefId = d.PetDefId, Level = d.Level,
            CurrentXP = d.CurrentXP, IsFavorite = d.IsFavorite,
            Nickname = d.Nickname, EquippedAccessoryId = d.EquippedAccessoryId,
            AcquiredAt = d.AcquiredAt
        }).ToList();
        _collection.ActivePetId = data.ActivePetId;
        _collection.MaxSlots = data.MaxSlots;
    }

    private async Task SaveIfAvailable()
    {
        if (_storage != null) await SaveAsync();
    }
}
```

### PetSaveService.cs — 可选封装

```csharp
namespace Match3Demo;

public class PetSaveService
{
    private readonly IPetCollectionService _petCollection;
    private readonly IPersistentStorage _storage;

    public PetSaveService(IPetCollectionService petCollection, IPersistentStorage storage)
    {
        _petCollection = petCollection;
        _storage = storage;
    }

    public Task SaveAsync() => _petCollection.SaveAsync();
    public Task LoadAsync() => _petCollection.LoadAsync();
}
```

## 验收标准
- PetLevelCalculator 为纯静态类，所有方法无副作用（可单元测试）
- PetLevelCalculator.XPForLevel 正确计算指数曲线
- PetLevelCalculator.LevelUp 通过 ref 参数修改 PetInstance 状态，返回升级数（单次上限 10 级）
- PetLevelCalculator.XPFromMatch 正确根据 matchScore 和 comboLevel 计算 XP 奖励
- PetCollectionService 构造函数注入 IPetDataSource、EventBus 和可选 IPersistentStorage
- AddPet() 正确 emit PetAcquired 信号并自动保存
- AddXP() 计算升级并 emit PetLeveledUp 信号
- TryEvolve() 检查进化条件（等级、重复数、道具 ID 为空），成功后 emit PetEvolved 信号
- SetActivePet() 正确 emit ActivePetChanged 信号
- SaveAsync() / LoadAsync() 通过 IPersistentStorage 持久化 PetSaveData，使用 PetInstanceData DTO 转换
- GetPet() 返回 null 时所有操作安全处理（不抛 NullReferenceException）
- PetSaveService 为 PetCollectionService 的薄封装，代理 SaveAsync/LoadAsync
- `dotnet build` 0 errors 0 warnings

## 注意
- 目录 `scripts/pets/services/` 为新增，需确保父目录存在
- PetLevelCalculator.CanLevelUp 和 LevelUp 依赖 Task 21 的 `PetInstance.IsMaxLevel(PetDefinition)` 扩展方法
- PetCollectionService.SaveAsync/LoadAsync 中的序列化使用 `PetInstanceData` DTO（Task 21 产出），非直接序列化 PetInstance
- SaveIfAvailable() 为 fire-and-forget (`_ = ...`)，不阻塞调用方
- PetCollectionService 的 `_storage` 为可选参数（允许无存储的测试场景），`SaveIfAvailable` 在 `_storage == null` 时静默跳过
- IPetCollectionService 接口中 SaveAsync/LoadAsync 使用 `Task` 而非 `Task<T>`，与 IPersistentStorage 的泛型 Task 模式兼容
- `RarityStatMultiplier` 硬编码倍率值（Common=1.0, Rare=1.3, Epic=1.6, Legendary=2.0），与设计决策表 §11.4 一致
