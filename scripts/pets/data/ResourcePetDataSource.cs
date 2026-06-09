using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public class ResourcePetDataSource : IPetDataSource
{
    private readonly Dictionary<string, PetDefinition> _cache = new();
    private readonly string _resourcePath;
    private bool _allLoaded;

    public ResourcePetDataSource(string resourcePath = "res://data/pets/")
    {
        _resourcePath = resourcePath;
    }

    public PetDefinition? GetPetDefinition(string id)
    {
        if (_cache.TryGetValue(id, out var def))
            return def;

        def = GD.Load<PetDefinition>($"{_resourcePath}{id}.tres");
        if (def != null)
            _cache[id] = def;
        return def;
    }

    public IEnumerable<PetDefinition> GetAllPets()
    {
        if (!_allLoaded)
            LoadAll();
        return _cache.Values;
    }

    public bool HasPet(string id)
    {
        return GetPetDefinition(id) != null;
    }

    public IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity)
    {
        if (!_allLoaded)
            LoadAll();
        foreach (var kv in _cache)
            if (kv.Value.Rarity == rarity)
                yield return kv.Value;
    }

    public IEnumerable<PetDefinition> GetPetsByType(PetType type)
    {
        if (!_allLoaded)
            LoadAll();
        foreach (var kv in _cache)
            if (kv.Value.Type == type)
                yield return kv.Value;
    }

    private void LoadAll()
    {
        using var dir = DirAccess.Open(_resourcePath);
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
                    var def = GD.Load<PetDefinition>($"{_resourcePath}{fileName}");
                    if (def != null)
                        _cache[id] = def;
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        _allLoaded = true;
    }
}
