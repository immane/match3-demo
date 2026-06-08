using System;

namespace Match3Demo;

public static class PetLevelCalculator
{
    public static int XPForLevel(int level, int baseXP = 10, double exponent = 1.5)
    {
        if (level <= 0)
            return 0;
        return (int)(baseXP * Math.Pow(level, exponent));
    }

    public static int TotalXPForLevel(int targetLevel, int baseXP = 10)
    {
        int total = 0;
        for (int i = 1; i < targetLevel; i++)
            total += XPForLevel(i, baseXP);
        return total;
    }

    public static float RarityStatMultiplier(PetRarity rarity)
    {
        return rarity switch
        {
            PetRarity.Common => 1.0f,
            PetRarity.Rare => 1.3f,
            PetRarity.Epic => 1.6f,
            PetRarity.Legendary => 2.0f,
            _ => 1.0f
        };
    }

    public static int XPFromMatch(int matchScore, int comboLevel)
    {
        int baseXP = matchScore / 10;
        int comboBonus = comboLevel * 5;
        return Math.Max(1, baseXP + comboBonus);
    }

    public static bool CanLevelUp(PetInstance pet, PetDefinition def)
    {
        if (pet.IsMaxLevel(def))
            return false;
        return pet.CurrentXP >= XPForLevel(pet.Level + 1);
    }

    public static int LevelUp(PetInstance pet, PetDefinition def)
    {
        int levelsGained = 0;
        while (CanLevelUp(pet, def) && levelsGained < 10)
        {
            pet.CurrentXP -= XPForLevel(pet.Level + 1);
            pet.Level++;
            levelsGained++;
        }
        return levelsGained;
    }
}
