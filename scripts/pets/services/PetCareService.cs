using Godot;
using System;
using System.Collections.Generic;

namespace Match3Demo;

public class PetCareService
{
	private readonly IPetCollectionService _petCollection;
	private readonly ICurrencyService _currency;
	private readonly EventBus _eventBus;

	public PetCareService(IPetCollectionService petCollection, ICurrencyService currency, EventBus eventBus)
	{
		_petCollection = petCollection;
		_currency = currency;
		_eventBus = eventBus;
	}

	public bool Feed(string petInstanceId, string foodId, out string? error)
	{
		var pet = _petCollection.GetPet(petInstanceId);
		if (pet == null) { error = "Pet not found"; return false; }

		var food = PetFoodData.Get(foodId);
		if (food == null) { error = "Food not found"; return false; }

		if (!_currency.Spend("soft_currency", food.Cost, $"feed_{petInstanceId}_{foodId}"))
		{ error = "Not enough currency"; return false; }

		pet.Needs.Feed(food.HungerRestore, food.HappinessRestore);
		pet.Needs.LastTickMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		_petCollection.SaveAsync();

		_eventBus.EmitSignal(EventBus.SignalName.PetFed, petInstanceId, foodId);
		_eventBus.EmitSignal(EventBus.SignalName.PlayEffect, "match", Vector2.Zero);
		error = null;
		return true;
	}

	public bool Play(string petInstanceId, out string? error)
	{
		var pet = _petCollection.GetPet(petInstanceId);
		if (pet == null) { error = "Pet not found"; return false; }
		if (pet.Needs.Energy < 10f) { error = "Pet is too tired"; return false; }

		pet.Needs.Play(20f, 10f);
		pet.Needs.LastTickMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		_petCollection.SaveAsync();

		_eventBus.EmitSignal(EventBus.SignalName.PlayEffect, "combo", Vector2.Zero);
		error = null;
		return true;
	}

	public void TickPets(double deltaSeconds)
	{
		var pets = _petCollection.GetAllOwnedPets();
		foreach (var pet in pets)
		{
			pet.Needs.Tick(deltaSeconds);
		}
	}
}
