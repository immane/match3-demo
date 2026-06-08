using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public class PetCollection
{
    public List<PetInstance> OwnedPets { get; set; } = new();
    public string ActivePetId { get; set; } = "";
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

    public bool RemovePet(string petInstanceId)
    {
        return OwnedPets.RemoveAll(p => p.Id == petInstanceId) > 0;
    }

    public PetInstance? GetPet(string petInstanceId)
    {
        return OwnedPets.FirstOrDefault(p => p.Id == petInstanceId);
    }

    public bool HasPet(string petDefId)
    {
        return OwnedPets.Any(p => p.PetDefId == petDefId);
    }

    public int GetDuplicateCount(string petDefId)
    {
        return OwnedPets.Count(p => p.PetDefId == petDefId);
    }

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
