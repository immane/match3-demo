using System.Collections.Generic;
using Match3Demo;

namespace Match3Demo.Tests;

public class FakeBannerDataSource : IDataSource<GachaBanner>
{
    private readonly Dictionary<string, GachaBanner> _banners = new();

    public void Add(GachaBanner banner) => _banners[banner.Id] = banner;

    public GachaBanner? Get(string id)
        => _banners.TryGetValue(id, out var banner) ? banner : null;

    public IEnumerable<GachaBanner> GetAll() => _banners.Values;

    public bool Has(string id) => _banners.ContainsKey(id);
}
