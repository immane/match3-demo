using System;
using System.Collections.Generic;

namespace Match3Demo;

public class PetSaveData
{
    public List<PetInstanceData> OwnedPets { get; set; } = new();
    public string ActivePetId { get; set; } = "";
    public int MaxSlots { get; set; } = 50;
}

public class PetInstanceData
{
    public string Id { get; set; } = "";
    public string PetDefId { get; set; } = "";
    public int Level { get; set; }
    public int CurrentXP { get; set; }
    public bool IsFavorite { get; set; }
    public string Nickname { get; set; } = "";
    public string EquippedAccessoryId { get; set; } = "";
    public DateTime AcquiredAt { get; set; }
    public PetNeedsSaveData? Needs { get; set; }
}
