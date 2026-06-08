using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Match3Demo;

public class PetCollectionService : IPetCollectionService
{
    private readonly IPetDataSource _dataSource;
    private readonly IPetEventBus _eventBus;
    private readonly IPersistentStorage? _storage;
    private PetCollection _collection = new();

    public PetCollectionService(IPetDataSource dataSource, IPetEventBus eventBus, IPersistentStorage? storage = null)
    {
        _dataSource = dataSource;
        _eventBus = eventBus;
        _storage = storage;
    }

    public PetInstance AddPet(string petDefId)
    {
        var pet = _collection.AddPet(petDefId);
        _eventBus.EmitPetAcquired(petDefId);
        _ = SaveIfAvailable();
        return pet;
    }

    public PetInstance? GetPet(string petInstanceId)
    {
        return _collection.GetPet(petInstanceId);
    }

    public List<PetInstance> GetAllOwnedPets()
    {
        return _collection.OwnedPets;
    }

    public bool HasPet(string petDefId)
    {
        return _collection.HasPet(petDefId);
    }

    public int GetDuplicateCount(string petDefId)
    {
        return _collection.GetDuplicateCount(petDefId);
    }

    public int AddXP(string petInstanceId, int amount)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return 0;

        var def = _dataSource.GetPetDefinition(pet.PetDefId);
        if (def == null) return 0;

        pet.CurrentXP += amount;
        int levelsGained = PetLevelCalculator.LevelUp(pet, def);
        if (levelsGained > 0)
        {
            _eventBus.EmitPetLeveledUp(petInstanceId, pet.Level);
        }
        _ = SaveIfAvailable();
        return levelsGained;
    }

    public bool TryEvolve(string petInstanceId)
    {
        var pet = _collection.GetPet(petInstanceId);
        if (pet == null) return false;

        var def = _dataSource.GetPetDefinition(pet.PetDefId);
        if (def?.EvolutionChain == null || def.EvolutionChain.Count == 0)
            return false;

        foreach (var step in def.EvolutionChain)
        {
            if (pet.Level >= step.RequiredLevel &&
                _collection.GetDuplicateCount(pet.PetDefId) >= step.RequiredDuplicates &&
                string.IsNullOrEmpty(step.RequiredItemId))
            {
                pet.PetDefId = step.EvolvesToDefId;
                pet.Level = 1;
                pet.CurrentXP = 0;
                _eventBus.EmitPetEvolved(petInstanceId, step.EvolvesToDefId);
                _ = SaveIfAvailable();
                return true;
            }
        }
        return false;
    }

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
        _eventBus.EmitActivePetChanged(petInstanceId);
        return true;
    }

    public string GetActivePetId()
    {
        return _collection.ActivePetId;
    }

    public async Task SaveAsync()
    {
        if (_storage == null) return;
        var saveData = new PetSaveData
        {
            OwnedPets = _collection.OwnedPets.Select(p => new PetInstanceData
            {
                Id = p.Id,
                PetDefId = p.PetDefId,
                Level = p.Level,
                CurrentXP = p.CurrentXP,
                IsFavorite = p.IsFavorite,
                Nickname = p.Nickname,
                EquippedAccessoryId = p.EquippedAccessoryId,
                AcquiredAt = p.AcquiredAt,
                Needs = p.Needs.ToSaveData()
            }).ToList(),
            ActivePetId = _collection.ActivePetId,
            MaxSlots = _collection.MaxSlots
        };
        await _storage.SaveAsync("pet_collection", saveData);
    }

    public async Task LoadAsync()
    {
        if (_storage == null) return;
        var data = await _storage.LoadAsync<PetSaveData>("pet_collection");
        if (data == null) return;

        _collection.OwnedPets = data.OwnedPets.Select(d =>
        {
            var pet = new PetInstance
            {
                Id = d.Id,
                PetDefId = d.PetDefId,
                Level = d.Level,
                CurrentXP = d.CurrentXP,
                IsFavorite = d.IsFavorite,
                Nickname = d.Nickname,
                EquippedAccessoryId = d.EquippedAccessoryId,
                AcquiredAt = d.AcquiredAt
            };
            if (d.Needs != null)
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                double elapsedSec = (nowMs - d.Needs.LastTickMs) / 1000.0;
                pet.Needs.FromSaveData(d.Needs, Math.Max(0, elapsedSec));
            }
            return pet;
        }).ToList();
        _collection.ActivePetId = data.ActivePetId;
        _collection.MaxSlots = data.MaxSlots;
    }

    private async Task SaveIfAvailable()
    {
        if (_storage != null)
            await SaveAsync();
    }
}
