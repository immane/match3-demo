using System.Collections.Generic;
using System.Threading.Tasks;
using Match3Demo;

namespace Match3Demo.Tests;

public class FakePetCollectionService : IPetCollectionService
{
    private readonly PetCollection _collection = new();

    public PetInstance AddPet(string petDefId)
    {
        return _collection.AddPet(petDefId);
    }

    public PetInstance? GetPet(string petInstanceId)
        => _collection.GetPet(petInstanceId);

    public List<PetInstance> GetAllOwnedPets()
        => _collection.OwnedPets;

    public bool HasPet(string petDefId)
        => _collection.HasPet(petDefId);

    public int GetDuplicateCount(string petDefId)
        => _collection.GetDuplicateCount(petDefId);

    public int AddXP(string petInstanceId, int amount) => 0;
    public bool TryEvolve(string petInstanceId) => false;

    public bool SetFavorite(string petInstanceId, bool isFavorite)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        pet.IsFavorite = isFavorite;
        return true;
    }

    public bool SetNickname(string petInstanceId, string nickname)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        pet.Nickname = nickname;
        return true;
    }

    public bool SetActivePet(string petInstanceId)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;
        _collection.ActivePetId = petInstanceId;
        return true;
    }

    public string GetActivePetId() => _collection.ActivePetId;
    public Task SaveAsync() => Task.CompletedTask;
    public Task LoadAsync() => Task.CompletedTask;
}
