namespace Match3Demo;

public record GachaRollResult(
    string RewardId,
    RewardType Type,
    PetRarity Rarity,
    GachaPityState NewPityState
);
