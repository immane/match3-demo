using System;

namespace Match3Demo;

public class PetInstance
{
    public string Id { get; set; } = "";
    public string PetDefId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int CurrentXP { get; set; }
    public bool IsFavorite { get; set; }
    public string Nickname { get; set; } = "";
    public string EquippedAccessoryId { get; set; } = "";
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
    public PetNeeds Needs { get; set; } = new();

    public int NextLevelXP => PetLevelCalculator.XPForLevel(Level + 1);

    public bool IsMaxLevel(PetDefinition def)
    {
        return def != null && Level >= def.MaxLevel;
    }
}
