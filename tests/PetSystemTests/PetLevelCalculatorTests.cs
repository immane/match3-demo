using Xunit;
using Match3Demo;

namespace Match3Demo.Tests;

public class PetLevelCalculatorTests
{
    [Fact]
    public void XPForLevel_Level1_ReturnsCorrectBase()
    {
        Assert.Equal(10, PetLevelCalculator.XPForLevel(1));
    }

    [Fact]
    public void XPForLevel_Level10_ReturnsLargerValue()
    {
        Assert.True(PetLevelCalculator.XPForLevel(9) > PetLevelCalculator.XPForLevel(1));
    }

    [Fact]
    public void XPForLevel_Level0_ReturnsZero()
    {
        Assert.Equal(0, PetLevelCalculator.XPForLevel(0));
    }

    [Fact]
    public void TotalXPForLevel_Level10_EqualsSumOfPrevious()
    {
        int manualSum = 0;
        for (int i = 1; i < 10; i++)
            manualSum += PetLevelCalculator.XPForLevel(i);
        Assert.Equal(manualSum, PetLevelCalculator.TotalXPForLevel(10));
    }

    [Fact]
    public void RarityStatMultiplier_Legendary_Returns2()
    {
        Assert.Equal(2.0f, PetLevelCalculator.RarityStatMultiplier(PetRarity.Legendary));
    }

    [Fact]
    public void RarityStatMultiplier_Common_Returns1()
    {
        Assert.Equal(1.0f, PetLevelCalculator.RarityStatMultiplier(PetRarity.Common));
    }

    [Fact]
    public void RarityStatMultiplier_Epic_ReturnsGreaterThanRare()
    {
        Assert.True(
            PetLevelCalculator.RarityStatMultiplier(PetRarity.Epic) >
            PetLevelCalculator.RarityStatMultiplier(PetRarity.Rare));
    }

    [Fact]
    public void XPFromMatch_ReturnsAtLeast1()
    {
        Assert.True(PetLevelCalculator.XPFromMatch(0, 0) >= 1);
    }

    [Fact]
    public void XPFromMatch_HigherCombo_GivesMoreXP()
    {
        int xp1 = PetLevelCalculator.XPFromMatch(100, 1);
        int xp5 = PetLevelCalculator.XPFromMatch(100, 5);
        Assert.True(xp5 > xp1);
    }

    [Fact]
    public void CanLevelUp_EnoughXP_ReturnsTrue()
    {
        var pet = new PetInstance { Level = 1, CurrentXP = 100 };
        var def = new PetDefinition { MaxLevel = 50 };
        Assert.True(PetLevelCalculator.CanLevelUp(pet, def));
    }

    [Fact]
    public void CanLevelUp_MaxLevel_ReturnsFalse()
    {
        var pet = new PetInstance { Level = 50, CurrentXP = 99999 };
        var def = new PetDefinition { MaxLevel = 50 };
        Assert.False(PetLevelCalculator.CanLevelUp(pet, def));
    }

    [Fact]
    public void LevelUp_GainsCorrectLevels()
    {
        var pet = new PetInstance { Level = 1, CurrentXP = 1000 };
        var def = new PetDefinition { MaxLevel = 50 };
        int gained = PetLevelCalculator.LevelUp(pet, def);
        Assert.True(gained > 0);
        Assert.True(pet.Level > 1);
    }
}
