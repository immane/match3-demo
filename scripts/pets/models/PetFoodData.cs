using System.Collections.Generic;

namespace Match3Demo;

public class PetFoodData
{
	public string FoodId { get; init; } = "";
	public string DisplayName { get; init; } = "";
	public int Cost { get; init; } = 10;
	public float HungerRestore { get; init; } = 20f;
	public float HappinessRestore { get; init; } = 5f;
	public string Emoji { get; init; } = "";

	public static readonly List<PetFoodData> AllFoods = new()
	{
		new() { FoodId = "fish",      DisplayName = "Fish",      Cost = 10,  HungerRestore = 25f, HappinessRestore = 5f,  Emoji = "\U0001F41F" },
		new() { FoodId = "milk",      DisplayName = "Milk",      Cost = 8,   HungerRestore = 18f, HappinessRestore = 8f,  Emoji = "\U0001F95B" },
		new() { FoodId = "treat",     DisplayName = "Cat Treat", Cost = 15,  HungerRestore = 10f, HappinessRestore = 25f, Emoji = "\U0001F36C" },
		new() { FoodId = "steak",     DisplayName = "Steak",     Cost = 30,  HungerRestore = 50f, HappinessRestore = 15f, Emoji = "\U0001F969" },
		new() { FoodId = "cake",      DisplayName = "Cake",      Cost = 50,  HungerRestore = 80f, HappinessRestore = 40f, Emoji = "\U0001F370" },
		new() { FoodId = "water",     DisplayName = "Water",     Cost = 5,   HungerRestore = 8f,  HappinessRestore = 2f,  Emoji = "\U0001F4A7" },
	};

	public static PetFoodData? Get(string foodId)
		=> AllFoods.Find(f => f.FoodId == foodId);
}
