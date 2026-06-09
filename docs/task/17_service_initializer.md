# Task 17: ServiceInitializer Autoload

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture.md](../design/architecture.md) — Autoload 设计、服务架构 |
| ↖ 依赖 | [Task 15](../task/15_persistent_storage.md) — IPersistentStorage, GodotFileStorage, IDataSource |
| ↖ 依赖 | [Task 16](../task/16_eventbus_gamedata_extension.md) — EventBus + GameData 扩展 |
| ↗ 产出 | [Task 18-25] — 由 ServiceInitializer 注册和注入的各服务类 |

## 状态
- [x] 已完成

## 依赖
- Task 15 (IPersistentStorage, GodotFileStorage, IDataSource)
- Task 16 (EventBus + GameData 扩展)

## 产出文件
```
scripts/autoload/ServiceInitializer.cs  [新增, 需在 project.godot 注册 autoload]
```

## 实现要求

### ServiceRegistry 内部类 (放在 ServiceInitializer.cs 中)

```csharp
public class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();
    public void Register<T>(T instance) where T : class => _services[typeof(T)] = instance;
    public T Get<T>() where T : class => _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
}
```

### ServiceInitializer 主类

```csharp
using Godot;

namespace Match3Demo;

public partial class ServiceInitializer : Node
{
    public static ServiceInitializer Instance { get; private set; }

    private ServiceRegistry _registry;

    // 服务属性 (typed accessors)
    public ICurrencyService CurrencyService { get; private set; }
    public GachaRollService GachaRollService { get; private set; }
    public GachaDrawService GachaDrawService { get; private set; }
    public IPetCollectionService PetCollectionService { get; private set; }
    public GodotFileStorage FileStorage { get; private set; }

    public override void _EnterTree() { Instance = this; }
    public override void _ExitTree() { Instance = null; }

    public override void _Ready()
    {
        _registry = new ServiceRegistry();

        // 1. 持久化存储
        FileStorage = new GodotFileStorage("user://saves/");

        // 2. 货币服务
        CurrencyService = new CurrencyService(FileStorage, EventBus.Instance);
        _registry.Register<ICurrencyService>(CurrencyService);

        // 3. 宠物数据源 + 宠物服务
        var petDataSource = new ResourcePetDataSource();
        _registry.Register<IPetDataSource>(petDataSource);
        PetCollectionService = new PetCollectionService(petDataSource, EventBus.Instance);
        _registry.Register<IPetCollectionService>(PetCollectionService);

        // 4. 抽卡概率服务
        GachaRollService = new GachaRollService();
        _registry.Register<GachaRollService>(GachaRollService);

        // 5. 抽卡保底跟踪器
        var pityTracker = new GachaPityTracker(FileStorage);
        _registry.Register<GachaPityTracker>(pityTracker);

        // 6. 抽卡编排服务
        var bannerDataSource = new GachaBannerDataSource();
        _registry.Register<IDataSource<GachaBanner>>(bannerDataSource);
        GachaDrawService = new GachaDrawService(
            CurrencyService, GachaRollService, bannerDataSource,
            PetCollectionService, EventBus.Instance, pityTracker);
        _registry.Register<GachaDrawService>(GachaDrawService);
    }

    public T GetService<T>() where T : class => _registry.Get<T>();
}
```

### Autoload 注册 (需手动操作)

在 `project.godot` 的 `[autoload]` 段追加：

```ini
ServiceInitializer = "*res://scripts/autoload/ServiceInitializer.cs"
```

### 重要说明

- ServiceInitializer 引用了 Task 18-25 中才会创建的类（CurrencyService, PetCollectionService, GachaRollService 等）—— 这些 using 语句需要在这些类创建后添加
- 建议在完成所有关联任务后再给 ServiceInitializer 添加 using 引用
- 初始版本可以只搭建框架 + ServiceRegistry，后续逐步添加服务注册
- 所有服务通过构造函数注入创建（手动 DI），无隐式依赖
- `ServiceRegistry` 为 `internal` 使用类型，不暴露给外部调用方

## 验收标准

- ServiceInitializer 注册为 autoload，游戏启动时自动执行 `_Ready()`
- `ServiceRegistry.Register<T>()` 和 `Get<T>()` 类型安全
- 所有服务通过构造函数注入创建（无隐式依赖）
- `ServiceInitializer.Instance.GetService<ICurrencyService>()` 返回正确的 CurrencyService 实例
- dotnet build 通过（或注释掉暂未实现的服务引用后通过）
