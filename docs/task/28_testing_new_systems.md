# Task 28: 新系统 xUnit 测试

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §10.3 — 测试策略（xUnit 测试、假实现、覆盖矩阵） |
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) — 宠物系统完整设计 |
| ↖ 设计 | [design/currency_system.md](../design/currency_system.md) — 货币系统完整设计 |
| ↖ 实现 | [task 14_testing.md](14_testing.md) — 已有 xUnit 测试基础设施（41 个 match-3 核心测试） |

## 状态
- [ ] 待执行

## 依赖
- Task 18 (`CurrencyService`, `ICurrencyService`)
- Task 22 (`PetLevelCalculator`, `PetCollectionService`, `IPetCollectionService`)
- Task 25 (`GachaRollService`, `GachaPityTracker`, `GachaDrawService`)
- Task 24 (`GachaBanner`, `GachaPityState`, `GachaRollResult`)
- 已有 xUnit 测试基础设施（`Tests/` 目录，xUnit 2.9.2，`Microsoft.NET.Test.Sdk` 17.11.1）

## 产出文件

```
Tests/PetSystemTests/PetLevelCalculatorTests.cs       [新增]
Tests/PetSystemTests/PetCollectionServiceTests.cs     [新增]
Tests/GachaSystemTests/GachaRollServiceTests.cs       [新增]
Tests/GachaSystemTests/GachaPityTrackerTests.cs       [新增]
Tests/GachaSystemTests/GachaDrawServiceTests.cs       [新增]
Tests/CurrencySystemTests/CurrencyServiceTests.cs     [新增]
Tests/Fakes/FakeEventBus.cs                           [新增]
Tests/Fakes/InMemoryStorage.cs                        [新增]
Tests/Fakes/FakeCurrencyService.cs                    [新增]
Tests/Fakes/FakePetDataSource.cs                      [新增]
Tests/Fakes/FakePetCollectionService.cs               [新增]
Tests/Fakes/FakeBannerDataSource.cs                   [新增]
Tests/Fakes/TestData.cs                               [新增]
```

## 实现要求

### 通用测试设置

所有测试类放在 `namespace Match3Demo.Tests` 下，使用 xUnit `[Fact]` 特性。需要在测试中构造 **Fake 实现** 而非使用 Moq 等外部 Mock 库，确保测试不依赖 Godot API 且无需额外 NuGet 包。

### Fake 实现（放在 `Tests/Fakes/` 下）

#### FakeEventBus.cs

```csharp
namespace Match3Demo.Tests;

public class FakeEventBus
{
    public List<string> EmittedSignals { get; } = new();
    public object? LastSignalArg { get; private set; }

    public void EmitSignal(string signalName, params object[] args)
    {
        EmittedSignals.Add(signalName);
        if (args.Length > 0) LastSignalArg = args[0];
    }
}
```

#### InMemoryStorage.cs

实现 `IPersistentStorage` 接口（`scripts/utils/IPersistentStorage.cs`）：

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Match3Demo.Tests;

public class InMemoryStorage : IPersistentStorage
{
    private readonly Dictionary<string, object> _store = new();

    public bool Exists(string key) => _store.ContainsKey(key);

    public Task<T?> LoadAsync<T>(string key) where T : class
    {
        _store.TryGetValue(key, out var val);
        return Task.FromResult(val as T);
    }

    public Task SaveAsync<T>(string key, T data) where T : class
    {
        _store[key] = data;
        return Task.CompletedTask;
    }
}
```

#### FakeCurrencyService.cs

实现 `ICurrencyService` 接口（`scripts/currency/ICurrencyService.cs`）：

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Match3Demo.Tests;

public class FakeCurrencyService : ICurrencyService
{
    private readonly Dictionary<string, int> _balances = new();

    public event Action<string, int>? BalanceChanged;

    public FakeCurrencyService(int initialBalance = 0)
    {
        _balances["soft_currency"] = initialBalance;
    }

    public bool CanAfford(string id, int amount) =>
        _balances.GetValueOrDefault(id, 0) >= amount;

    public bool Spend(string id, int amount, string reason)
    {
        if (!CanAfford(id, amount)) return false;
        _balances[id] -= amount;
        BalanceChanged?.Invoke(id, _balances[id]);
        return true;
    }

    public void Grant(string id, int amount, string reason)
    {
        if (!_balances.ContainsKey(id)) _balances[id] = 0;
        _balances[id] += amount;
        BalanceChanged?.Invoke(id, _balances[id]);
    }

    public int GetBalance(string id) =>
        _balances.GetValueOrDefault(id, 0);

    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
}
```

#### FakePetDataSource.cs

实现 `IDataSource<PetDefinition>` 接口（`scripts/utils/IDataSource.cs`，Task 15）：

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo.Tests;

public class FakePetDataSource : IDataSource<PetDefinition>
{
    private readonly Dictionary<string, PetDefinition> _defs = new()
    {
        ["cat_sleepy_01"] = new PetDefinition
        {
            PetDefId = "cat_sleepy_01",
            DisplayName = "Sleepy Cat",
            Rarity = PetRarity.Common,
            MaxLevel = 50
        },
        ["cat_01"] = new PetDefinition
        {
            PetDefId = "cat_01",
            DisplayName = "Cat",
            Rarity = PetRarity.Common,
            MaxLevel = 50
        }
    };

    public PetDefinition? Get(string id) =>
        _defs.TryGetValue(id, out var def) ? def : null;

    public IEnumerable<PetDefinition> GetAll() => _defs.Values;

    public void Add(PetDefinition def) => _defs[def.PetDefId] = def;
}
```

#### FakePetCollectionService.cs

```csharp
using System.Collections.Generic;

namespace Match3Demo.Tests;

public class FakePetCollectionService : IPetCollectionService
{
    private readonly Dictionary<string, List<PetInstance>> _pets = new();

    public PetInstance AddPet(string petDefId)
    {
        var pet = new PetInstance
        {
            Id = Guid.NewGuid().ToString(),
            PetDefId = petDefId,
            Level = 1,
            CurrentXP = 0
        };
        if (!_pets.ContainsKey(petDefId)) _pets[petDefId] = new();
        _pets[petDefId].Add(pet);
        return pet;
    }

    public bool HasPet(string petDefId) =>
        _pets.ContainsKey(petDefId) && _pets[petDefId].Count > 0;

    public int GetDuplicateCount(string petDefId) =>
        _pets.TryGetValue(petDefId, out var list) ? list.Count : 0;

    public int AddXP(string petId, int amount)
    {
        // Simple: add XP to the first found pet
        foreach (var list in _pets.Values)
        {
            var pet = list.FirstOrDefault(p => p.Id == petId);
            if (pet != null)
            {
                pet.CurrentXP += amount;
                return pet.CurrentXP;
            }
        }
        return 0;
    }

    public IEnumerable<PetInstance> GetAllPets()
    {
        foreach (var list in _pets.Values)
            foreach (var pet in list)
                yield return pet;
    }
}
```

#### FakeBannerDataSource.cs

实现 `IDataSource<GachaBanner>` 接口：

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo.Tests;

public class FakeBannerDataSource : IDataSource<GachaBanner>
{
    private readonly Dictionary<string, GachaBanner> _banners = new();

    public void Add(GachaBanner banner) => _banners[banner.Id] = banner;

    public GachaBanner? Get(string id) =>
        _banners.TryGetValue(id, out var b) ? b : null;

    public IEnumerable<GachaBanner> GetAll() => _banners.Values;
}
```

#### TestData.cs

共享测试数据工厂，提供标准化的 PetDefinition、GachaBanner、PetInstance 等：

```csharp
namespace Match3Demo.Tests;

public static class TestData
{
    public static GachaBanner CreateBanner()
    {
        return new GachaBanner
        {
            Id = "test",
            DisplayName = "Test Banner",
            CostPerPull = 50,
            SoftPityStart = 7,
            HardPityGuarantee = 10,
            SoftPityRateIncrease = 0.1,
            RateUpRewardId = null,
            RateUpChanceOnSSR = 0.5,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "pet1", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 80 },
                new() { RewardId = "pet2", Type = RewardType.Pet, Rarity = PetRarity.Rare, Weight = 15 },
                new() { RewardId = "pet3", Type = RewardType.Pet, Rarity = PetRarity.Epic, Weight = 4 },
                new() { RewardId = "pet4", Type = RewardType.Pet, Rarity = PetRarity.Legendary, Weight = 1 },
            }
        };
    }

    public static PetDefinition CreatePetDef(string id, PetRarity rarity = PetRarity.Common, int maxLevel = 50)
    {
        return new PetDefinition
        {
            PetDefId = id,
            DisplayName = id,
            Rarity = rarity,
            MaxLevel = maxLevel
        };
    }
}
```

### 文件 1: `Tests/PetSystemTests/PetLevelCalculatorTests.cs`

测试 `PetLevelCalculator` 静态方法（XP 计算、稀有度系数、升级判定）。**要求 ≥6 个 `[Fact]`**。

```csharp
namespace Match3Demo.Tests;

public class PetLevelCalculatorTests
{
    [Fact]
    public void XPForLevel_Level2_ReturnsExpected()
    {
        Assert.Equal(10, PetLevelCalculator.XPForLevel(1));
    }

    [Fact]
    public void XPForLevel_Level10_ReturnsLargerValue()
    {
        Assert.True(PetLevelCalculator.XPForLevel(9) > PetLevelCalculator.XPForLevel(1));
    }

    [Fact]
    public void TotalXPForLevel_Level10_SumOfAllPrevious()
    {
        int manualSum = 0;
        for (int i = 1; i < 10; i++)
            manualSum += PetLevelCalculator.XPForLevel(i);
        Assert.Equal(manualSum, PetLevelCalculator.TotalXPForLevel(10));
    }

    [Fact]
    public void RarityStatMultiplier_Legendary_Is2()
    {
        Assert.Equal(2.0f, PetLevelCalculator.RarityStatMultiplier(PetRarity.Legendary));
    }

    [Fact]
    public void XPFromMatch_ReturnsAtLeast1()
    {
        Assert.True(PetLevelCalculator.XPFromMatch(0, 0) >= 1);
    }

    [Fact]
    public void CanLevelUp_EnoughXP_ReturnsTrue()
    {
        var pet = new PetInstance { Level = 1, CurrentXP = 100 };
        var def = new PetDefinition { MaxLevel = 50 };
        Assert.True(PetLevelCalculator.CanLevelUp(pet, def));
    }

    [Fact]
    public void CanLevelUp_MaxLevel_ReturnsFalse()
    {
        var pet = new PetInstance { Level = 50, CurrentXP = 99999 };
        var def = new PetDefinition { MaxLevel = 50 };
        Assert.False(PetLevelCalculator.CanLevelUp(pet, def));
    }
}
```

### 文件 2: `Tests/PetSystemTests/PetCollectionServiceTests.cs`

测试 `PetCollectionService` 的核心逻辑（添加宠物、信号、重复计数、XP 升级）。**要求 ≥5 个 `[Fact]`**。

```csharp
namespace Match3Demo.Tests;

public class PetCollectionServiceTests
{
    [Fact]
    public void AddPet_ReturnsInstanceWithCorrectDefId()
    {
        var dataSource = new FakePetDataSource();
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(dataSource, eventBus);
        var pet = service.AddPet("cat_sleepy_01");
        Assert.Equal("cat_sleepy_01", pet.PetDefId);
        Assert.NotEmpty(pet.Id);
    }

    [Fact]
    public void AddPet_EmitsPetAcquiredSignal()
    {
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(new FakePetDataSource(), eventBus);
        service.AddPet("cat_01");
        Assert.Contains("PetAcquired", eventBus.EmittedSignals);
    }

    [Fact]
    public void HasPet_AfterAdding_ReturnsTrue()
    {
        var service = new PetCollectionService(new FakePetDataSource(), new FakeEventBus());
        service.AddPet("cat_01");
        Assert.True(service.HasPet("cat_01"));
    }

    [Fact]
    public void GetDuplicateCount_ThreeCopies_Returns3()
    {
        var service = new PetCollectionService(new FakePetDataSource(), new FakeEventBus());
        service.AddPet("cat_01");
        service.AddPet("cat_01");
        service.AddPet("cat_01");
        Assert.Equal(3, service.GetDuplicateCount("cat_01"));
    }

    [Fact]
    public void AddXP_LevelsUp_EmitsPetLeveledUp()
    {
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(new FakePetDataSource(), eventBus);
        var pet = service.AddPet("cat_01");
        pet.CurrentXP = 0;
        pet.Level = 1;
        int gained = service.AddXP(pet.Id, 1000);
        Assert.True(gained > 0);
        Assert.Contains("PetLeveledUp", eventBus.EmittedSignals);
    }
}
```

### 文件 3: `Tests/GachaSystemTests/GachaRollServiceTests.cs`

测试 `GachaRollService` 纯概率引擎（确定性、Hard Pity、Soft Pity、十连保底、50:50 保证）。**要求 ≥8 个 `[Fact]`**。

```csharp
namespace Match3Demo.Tests;

public class GachaRollServiceTests
{
    private static GachaBanner CreateTestBanner()
    {
        return new GachaBanner
        {
            Id = "test",
            DisplayName = "Test Banner",
            SoftPityStart = 7,
            HardPityGuarantee = 10,
            SoftPityRateIncrease = 0.1,
            CostPerPull = 50,
            RateUpRewardId = null,
            RateUpChanceOnSSR = 0.5,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "common1", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 70 },
                new() { RewardId = "common2", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 30 },
                new() { RewardId = "rare1",   Type = RewardType.Pet, Rarity = PetRarity.Rare,   Weight = 20 },
                new() { RewardId = "epic1",   Type = RewardType.Pet, Rarity = PetRarity.Epic,   Weight = 6 },
                new() { RewardId = "legend1", Type = RewardType.Pet, Rarity = PetRarity.Legendary, Weight = 2 },
            }
        };
    }

    [Fact]
    public void Roll_ReturnsValidResult()
    {
        var service = new GachaRollService(42);
        var result = service.Roll(CreateTestBanner(), new GachaPityState(0, 0, 0, false));
        Assert.NotNull(result.RewardId);
        Assert.True(result.RewardId.Length > 0);
    }

    [Fact]
    public void Roll_Deterministic_SameSeed_SameResult()
    {
        var s1 = new GachaRollService(123);
        var s2 = new GachaRollService(123);
        var r1 = s1.Roll(CreateTestBanner(), new GachaPityState(0, 0, 0, false));
        var r2 = s2.Roll(CreateTestBanner(), new GachaPityState(0, 0, 0, false));
        Assert.Equal(r1.RewardId, r2.RewardId);
        Assert.Equal(r1.Rarity, r2.Rarity);
    }

    [Fact]
    public void Roll_HardPity_Guarantees_Legendary()
    {
        var service = new GachaRollService(42);
        var pity = new GachaPityState(0, 9, 0, false);
        var result = service.Roll(CreateTestBanner(), pity);
        Assert.Equal(PetRarity.Legendary, result.Rarity);
    }

    [Fact]
    public void Roll_SoftPity_NearHardPity_LikelyRarePlus()
    {
        var service = new GachaRollService(42);
        var pity = new GachaPityState(0, 8, 0, false);
        var result = service.Roll(CreateTestBanner(), pity);
        Assert.True(result.Rarity >= PetRarity.Rare);
    }

    [Fact]
    public void Roll_NewPityState_SSR_ResetsCounter()
    {
        var service = new GachaRollService(42);
        var pity = new GachaPityState(100, 9, 5, false);
        var result = service.Roll(CreateTestBanner(), pity);
        Assert.Equal(0, result.NewPityState.PullsSinceLastSSR);
    }

    [Fact]
    public void RollMultiple_ReturnsRequestedCount()
    {
        var service = new GachaRollService(42);
        var results = service.RollMultiple(CreateTestBanner(), new GachaPityState(0, 0, 0, false), 10);
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void RollMultiple_TenPull_Guarantees_RarePlus()
    {
        var service = new GachaRollService();
        for (int batch = 0; batch < 20; batch++)
        {
            var pity = new GachaPityState(0, 0, 0, false);
            var results = service.RollMultiple(CreateTestBanner(), pity, 10);
            Assert.Contains(results, r => r.Rarity >= PetRarity.Rare);
        }
    }

    [Fact]
    public void Roll_FiftyFiftyGuarantee_PicksRateUp()
    {
        var banner = CreateTestBanner();
        banner.RateUpRewardId = "legend1";
        banner.RateUpChanceOnSSR = 0.5;
        var service = new GachaRollService(42);
        var pity = new GachaPityState(0, 9, 0, true);
        var result = service.Roll(banner, pity);
        Assert.Equal("legend1", result.RewardId);
    }
}
```

### 文件 4: `Tests/GachaSystemTests/GachaDrawServiceTests.cs`

测试 `GachaDrawService` 编排层（货币扣除、余额不足异常、宠物入仓、保底查询）。**要求 ≥4 个 `[Fact]`**。

```csharp
namespace Match3Demo.Tests;

public class GachaDrawServiceTests
{
    private GachaDrawService CreateService(
        FakeCurrencyService currency,
        GachaRollService roller,
        FakeBannerDataSource banners,
        FakePetCollectionService pets,
        FakeEventBus eventBus)
    {
        var pityTracker = new GachaPityTracker(new InMemoryStorage());
        return new GachaDrawService(currency, roller, banners, pets, eventBus, pityTracker);
    }

    [Fact]
    public void PerformPull_DeductsCurrency()
    {
        var currency = new FakeCurrencyService(500);
        var roller = new GachaRollService(123);
        var banners = new FakeBannerDataSource();
        banners.Add(TestData.CreateBanner());
        var pets = new FakePetCollectionService();
        var eventBus = new FakeEventBus();

        var service = CreateService(currency, roller, banners, pets, eventBus);
        service.PerformPull("test");

        Assert.Equal(450, currency.GetBalance("soft_currency"));
    }

    [Fact]
    public void PerformPull_InsufficientCurrency_Throws()
    {
        var currency = new FakeCurrencyService(10);
        var roller = new GachaRollService(123);
        var banners = new FakeBannerDataSource();
        banners.Add(TestData.CreateBanner());
        var pets = new FakePetCollectionService();
        var eventBus = new FakeEventBus();

        var service = CreateService(currency, roller, banners, pets, eventBus);
        Assert.Throws<InvalidOperationException>(() => service.PerformPull("test"));
    }

    [Fact]
    public void PerformPull_PetReward_AddsToCollection()
    {
        var currency = new FakeCurrencyService(500);
        var roller = new GachaRollService(123);
        var banners = new FakeBannerDataSource();
        banners.Add(TestData.CreateBanner());
        var pets = new FakePetCollectionService();
        var eventBus = new FakeEventBus();

        var service = CreateService(currency, roller, banners, pets, eventBus);
        var result = service.PerformPull("test");

        if (result.Type == RewardType.Pet)
            Assert.True(pets.HasPet(result.RewardId));
    }

    [Fact]
    public void GetPullsUntilGuarantee_InitialState_ReturnsHardPity()
    {
        var currency = new FakeCurrencyService(500);
        var roller = new GachaRollService(123);
        var banners = new FakeBannerDataSource();
        banners.Add(TestData.CreateBanner());
        var eventBus = new FakeEventBus();

        var service = CreateService(currency, roller, banners, new FakePetCollectionService(), eventBus);
        Assert.Equal(10, service.GetPullsUntilGuarantee("test"));
    }
}
```

### 文件 5: `Tests/GachaSystemTests/GachaPityTrackerTests.cs`

测试 `GachaPityTracker` 持久化逻辑（默认状态、存盘读取、重置）。**要求 ≥3 个 `[Fact]`**。

```csharp
namespace Match3Demo.Tests;

public class GachaPityTrackerTests
{
    [Fact]
    public void GetPityState_NewBanner_ReturnsDefault()
    {
        var tracker = new GachaPityTracker(new InMemoryStorage());
        var state = tracker.GetPityState("banner1");
        Assert.Equal(0, state.TotalPulls);
        Assert.Equal(0, state.PullsSinceLastSSR);
        Assert.False(state.GuaranteedRateUpNext);
    }

    [Fact]
    public void UpdatePityState_Persists()
    {
        var storage = new InMemoryStorage();
        var tracker = new GachaPityTracker(storage);
        var newState = new GachaPityState(50, 10, 3, true);
        tracker.UpdatePityState("banner1", newState);
        var loaded = tracker.GetPityState("banner1");
        Assert.Equal(50, loaded.TotalPulls);
        Assert.True(loaded.GuaranteedRateUpNext);
    }

    [Fact]
    public void ResetPityState_ClearsBanner()
    {
        var tracker = new GachaPityTracker(new InMemoryStorage());
        tracker.UpdatePityState("b1", new GachaPityState(100, 50, 20, true));
        tracker.ResetPityState("b1");
        var state = tracker.GetPityState("b1");
        Assert.Equal(0, state.TotalPulls);
        Assert.False(state.GuaranteedRateUpNext);
    }
}
```

### 文件 6: `Tests/CurrencySystemTests/CurrencyServiceTests.cs`

测试 `CurrencyService` 边界条件（消费、余额不足、发放、余额检查）。**要求 ≥4 个 `[Fact]`**。

```csharp
namespace Match3Demo.Tests;

public class CurrencyServiceTests
{
    private (CurrencyService service, FakeEventBus eventBus) CreateService(int initialBalance = 0)
    {
        var storage = new InMemoryStorage();
        var eventBus = new FakeEventBus();
        var service = new CurrencyService(storage, eventBus);
        if (initialBalance > 0)
            service.Grant("soft_currency", initialBalance, "test_init");
        return (service, eventBus);
    }

    [Fact]
    public void Spend_Sufficient_ReturnsTrue()
    {
        var (service, _) = CreateService(100);
        Assert.True(service.Spend("soft_currency", 50, "test"));
        Assert.Equal(50, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void Spend_Insufficient_ReturnsFalse()
    {
        var (service, _) = CreateService(10);
        Assert.False(service.Spend("soft_currency", 50, "test"));
        Assert.Equal(10, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void Grant_IncreasesBalance()
    {
        var (service, _) = CreateService(0);
        service.Grant("soft_currency", 100, "test");
        Assert.Equal(100, service.GetBalance("soft_currency"));
    }

    [Fact]
    public void CanAfford_ChecksCorrectly()
    {
        var (service, _) = CreateService(50);
        Assert.True(service.CanAfford("soft_currency", 50));
        Assert.False(service.CanAfford("soft_currency", 51));
    }
}
```

## 验收标准

- [ ] 所有新增测试文件放在 `Tests/` 目录下，命名空间 `Match3Demo.Tests`
- [ ] `dotnet test` 全部通过，无编译错误（预计 **≥30 个** xUnit `[Fact]` 通过）
  - PetLevelCalculatorTests: ≥6 tests
  - PetCollectionServiceTests: ≥5 tests
  - GachaRollServiceTests: ≥8 tests
  - GachaDrawServiceTests: ≥4 tests
  - GachaPityTrackerTests: ≥3 tests
  - CurrencyServiceTests: ≥4 tests
- [ ] 所有 Fake 实现（InMemoryStorage, FakeEventBus, FakeCurrencyService）不与 Godot API 耦合
- [ ] 无需额外 NuGet 包（使用 xUnit 2.9.2 + Test.Sdk 17.11.1，不引入 Moq）
- [ ] 确定性测试（same seed → same result）通过
- [ ] Hard pity 保证 Legendary 测试通过
- [ ] 10-pull 至少一个 Rare+ 保底测试通过
- [ ] Currency 边界条件（不足、充足、零余额）测试通过
- [ ] PityTracker 持久化测试通过（InMemoryStorage 存盘读写一致）
