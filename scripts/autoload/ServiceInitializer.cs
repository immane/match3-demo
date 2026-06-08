using Godot;
using System;
using System.Collections.Generic;

namespace Match3Demo;

public partial class ServiceInitializer : Node
{
    public static ServiceInitializer Instance { get; private set; } = null!;

    private ServiceRegistry _registry = null!;

    public GodotFileStorage FileStorage { get; private set; } = null!;
    public ICurrencyService CurrencyService { get; private set; } = null!;
    public GachaRollService GachaRollService { get; private set; } = null!;
    public GachaDrawService GachaDrawService { get; private set; } = null!;
    public IPetCollectionService PetCollectionService { get; private set; } = null!;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        Instance = null!;
    }

    public override void _Ready()
    {
        _registry = new ServiceRegistry();

        FileStorage = new GodotFileStorage("user://saves/");
        _registry.Register<IPersistentStorage>(FileStorage);

        CurrencyService = new CurrencyService(FileStorage, EventBus.Instance);
        _registry.Register<ICurrencyService>(CurrencyService);

        var petDataSource = new ResourcePetDataSource("res://data/pets/");
        _registry.Register<IPetDataSource>(petDataSource);

        PetCollectionService = new PetCollectionService(petDataSource, (IPetEventBus)EventBus.Instance, FileStorage);
        _registry.Register<IPetCollectionService>(PetCollectionService);

        GachaRollService = new GachaRollService();
        _registry.Register<GachaRollService>(GachaRollService);

        var pityTracker = new GachaPityTracker(FileStorage);
        _registry.Register<GachaPityTracker>(pityTracker);

        var bannerDataSource = new GachaBannerDataSource();
        _registry.Register<IDataSource<GachaBanner>>(bannerDataSource);

        GachaDrawService = new GachaDrawService(
            CurrencyService, GachaRollService, bannerDataSource,
            PetCollectionService, EventBus.Instance, pityTracker);
        _registry.Register<GachaDrawService>(GachaDrawService);

        var petCareService = new PetCareService(PetCollectionService, CurrencyService, EventBus.Instance);
        _registry.Register<PetCareService>(petCareService);

        GD.Print("[ServiceInitializer] All services initialized");
    }

    public T? GetService<T>() where T : class
    {
        return _registry.Get<T>();
    }

    public class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        public T? Get<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }
    }
}
