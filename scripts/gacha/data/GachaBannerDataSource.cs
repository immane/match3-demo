using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public class GachaBannerDataSource : IDataSource<GachaBanner>
{
    private readonly Dictionary<string, GachaBanner> _cache = new();
    private bool _allLoaded;

    public GachaBanner? Get(string id)
    {
        if (_cache.TryGetValue(id, out var banner))
            return banner;

        var res = GD.Load<GachaBannerResource>($"res://data/gacha/{id}.tres");
        if (res != null)
        {
            banner = res.ToBanner();
            _cache[id] = banner;
        }
        return banner;
    }

    public IEnumerable<GachaBanner> GetAll()
    {
        if (!_allLoaded)
            LoadAll();
        return _cache.Values;
    }

    public bool Has(string id)
    {
        return Get(id) != null;
    }

    public void LoadAll()
    {
        using var dir = DirAccess.Open("res://data/gacha/");
        if (dir == null)
        {
            _allLoaded = true;
            return;
        }

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
                    if (res != null)
                        _cache[id] = res.ToBanner();
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        _allLoaded = true;
    }

    public GachaBanner? GetOrCreateDefault(string id)
    {
        var banner = Get(id);
        if (banner != null) return banner;

        if (id == "standard_banner")
        {
            banner = new GachaBanner
            {
                Id = "standard_banner",
                DisplayName = "Standard Banner",
                CostPerPull = 50,
                SoftPityStart = 70,
                HardPityGuarantee = 90,
                SoftPityRateIncrease = 0.06,
                Pool = new System.Collections.Generic.List<GachaPoolEntry>
                {
                    new() { RewardId = "cat_sleepy_01", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 40 },
                    new() { RewardId = "dog_common_01", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 40 },
                    new() { RewardId = "cat_playful_02", Type = RewardType.Pet, Rarity = PetRarity.Rare, Weight = 20 },
                    new() { RewardId = "bunny_rare_01", Type = RewardType.Pet, Rarity = PetRarity.Rare, Weight = 10 },
                    new() { RewardId = "dog_happy_01", Type = RewardType.Pet, Rarity = PetRarity.Epic, Weight = 5 },
                    new() { RewardId = "fox_legendary_01", Type = RewardType.Pet, Rarity = PetRarity.Legendary, Weight = 1 },
                }
            };
            _cache[id] = banner;
            return banner;
        }
        return null;
    }
}
