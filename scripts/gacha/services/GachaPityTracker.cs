using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Match3Demo;

public class GachaPityTracker
{
    private readonly IPersistentStorage _storage;
    private const string SaveKey = "gacha_pity";
    private readonly Dictionary<string, GachaPityState> _bannerStates = new();

    public GachaPityTracker(IPersistentStorage storage)
    {
        _storage = storage;
        _ = LoadAsync();
    }

    public GachaPityState GetPityState(string bannerId)
    {
        return _bannerStates.TryGetValue(bannerId, out var state)
            ? state
            : new GachaPityState(0, 0, 0, false);
    }

    public void UpdatePityState(string bannerId, GachaPityState newState)
    {
        _bannerStates[bannerId] = newState;
        _ = SaveAsync();
    }

    public void ResetPityState(string bannerId)
    {
        _bannerStates[bannerId] = new GachaPityState(0, 0, 0, false);
        _ = SaveAsync();
    }

    public async Task LoadAsync()
    {
        var data = await _storage.LoadAsync<GachaPitySaveData>(SaveKey);
        if (data?.BannerPityStates == null) return;

        foreach (var kv in data.BannerPityStates)
        {
            _bannerStates[kv.Key] = new GachaPityState(
                kv.Value.TotalPulls,
                kv.Value.PullsSinceLastSSR,
                kv.Value.PullsSinceLastEpic,
                kv.Value.GuaranteedRateUpNext
            );
        }
    }

    public async Task SaveAsync()
    {
        var data = new GachaPitySaveData
        {
            BannerPityStates = _bannerStates.ToDictionary(
                kv => kv.Key,
                kv => new GachaPityStateDto
                {
                    TotalPulls = kv.Value.TotalPulls,
                    PullsSinceLastSSR = kv.Value.PullsSinceLastSSR,
                    PullsSinceLastEpic = kv.Value.PullsSinceLastEpic,
                    GuaranteedRateUpNext = kv.Value.GuaranteedRateUpNext
                })
        };
        await _storage.SaveAsync(SaveKey, data);
    }
}
