using Godot;

namespace Match3Demo;

public partial class PetLayer : Node2D
{
	private PetActor? _activePet;

	public override void _Ready()
	{
		EventBus.Instance.ActivePetChanged += OnActivePetChanged;
	}

	public override void _ExitTree()
	{
		EventBus.Instance.ActivePetChanged -= OnActivePetChanged;
	}

	public void SpawnActivePet()
	{
		var petService = ServiceInitializer.Instance?.GetService<IPetCollectionService>();
		var petDs = ServiceInitializer.Instance?.GetService<IPetDataSource>();
		if (petService == null || petDs == null) return;

		var activeId = petService.GetActivePetId();
		if (string.IsNullOrEmpty(activeId))
		{
			var allPets = petService.GetAllOwnedPets();
			if (allPets.Count == 0) return;
			activeId = allPets[0].Id;
			petService.SetActivePet(activeId);
		}

		var pet = petService.GetPet(activeId);
		if (pet == null) return;
		var def = petDs.GetPetDefinition(pet.PetDefId);
		if (def == null) return;

		RemoveActivePet();

		_activePet = new PetActor();
		_activePet.Name = "PetActor";
		_activePet.Setup(pet, def, showBars: true);

		// Walk area: lower portion of screen, below the board grid area
		float gridW = GridUtils.GridCols * GridUtils.CellStep;
		float gridH = GridUtils.GridRows * GridUtils.CellStep;
		float centerX = GridUtils.OffsetX + gridW / 2f;
		float bottomY = GridUtils.OffsetY + gridH + 80f;
		Position = new Vector2(centerX - gridW / 2f, bottomY - 100f);
		_activePet.Position = new Vector2(gridW / 2f, 60f);
		_activePet.SetWalkArea(
			new Vector2(20, 0),
			new Vector2(gridW - 20, 140)
		);

		AddChild(_activePet);
	}

	public void RemoveActivePet()
	{
		if (_activePet != null)
		{
			_activePet.QueueFree();
			_activePet = null;
		}
	}

	private void OnActivePetChanged(string petInstanceId)
	{
		SpawnActivePet();
	}
}
