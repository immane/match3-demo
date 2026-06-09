using System.Collections.Generic;

namespace Match3Demo;

public interface IPetDataSource
{
    PetDefinition? GetPetDefinition(string id);
    IEnumerable<PetDefinition> GetAllPets();
    bool HasPet(string id);
    IEnumerable<PetDefinition> GetPetsByRarity(PetRarity rarity);
    IEnumerable<PetDefinition> GetPetsByType(PetType type);
}
