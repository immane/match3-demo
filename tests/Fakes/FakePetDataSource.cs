using System.Collections.Generic;
using System.Linq;
using Match3Demo;

namespace Match3Demo.Tests;

public class FakePetDataSource : IPetDataSource
{
    private readonly Dictionary<string, PetDefinition> _pets = new();

    public void AddPet(PetDefinition def) => _pets[def.Id] = def;

    public PetDefinition? GetPetDefinition(string id)
        => _pets.TryGetValue(id, out var def) ? def : null;

    public IEnumerable<PetDefinition> GetAllPets() => _pets.Values;

    public bool HasPet(string id) => _pets.ContainsKey(id);

    public IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity)
        => _pets.Values.Where(p => p.Rarity == rarity);

    public IEnumerable<PetDefinition> GetPetsByType(PetType type)
        => _pets.Values.Where(p => p.Type == type);
}
