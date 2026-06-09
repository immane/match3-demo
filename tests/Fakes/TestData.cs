using System.Collections.Generic;
using Match3Demo;

namespace Match3Demo.Tests;

public static class TestData
{
    public static PetDefinition CreatePetDef(string id = "cat_01", PetRarity rarity = PetRarity.Common)
    {
        return new PetDefinition
        {
            Id = id,
            DisplayName = id,
            Type = PetType.Cat,
            Rarity = rarity,
            MaxLevel = 50
        };
    }

    public static GachaBanner CreateBanner(string id = "test", int hardPity = 10, int softPityStart = 7,
        string rateUpRewardId = "", double rateUpChanceOnSSR = 0.5)
    {
        return new GachaBanner
        {
            Id = id,
            DisplayName = "Test Banner",
            CostPerPull = 50,
            SoftPityStart = softPityStart,
            HardPityGuarantee = hardPity,
            SoftPityRateIncrease = 0.1,
            RateUpRewardId = rateUpRewardId,
            RateUpChanceOnSSR = rateUpChanceOnSSR,
            Pool = new List<GachaPoolEntry>
            {
                new() { RewardId = "common1", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 70 },
                new() { RewardId = "common2", Type = RewardType.Pet, Rarity = PetRarity.Common, Weight = 30 },
                new() { RewardId = "rare1",   Type = RewardType.Pet, Rarity = PetRarity.Rare,   Weight = 20 },
                new() { RewardId = "epic1",   Type = RewardType.Pet, Rarity = PetRarity.Epic,   Weight = 6 },
                new() { RewardId = "legend1", Type = RewardType.Pet, Rarity = PetRarity.Legendary, Weight = 2 },
            }
        };
    }
}
