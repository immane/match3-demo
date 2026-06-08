using System.Collections.Generic;
using System.Threading.Tasks;

namespace Match3Demo;

public interface IPetCollectionService
{
    PetInstance AddPet(string petDefId);
    PetInstance? GetPet(string petInstanceId);
    List<PetInstance> GetAllOwnedPets();
    bool HasPet(string petDefId);
    int GetDuplicateCount(string petDefId);
    int AddXP(string petInstanceId, int amount);
    bool TryEvolve(string petInstanceId);
    bool SetFavorite(string petInstanceId, bool isFavorite);
    bool SetNickname(string petInstanceId, string nickname);
    bool SetActivePet(string petInstanceId);
    string GetActivePetId();
    Task SaveAsync();
    Task LoadAsync();
}
