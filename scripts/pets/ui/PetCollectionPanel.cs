using Godot;

namespace Match3Demo;

public partial class PetCollectionPanel : Control
{
	[Export] public PackedScene? PetSlotScene { get; set; }

	private GridContainer _grid = null!;
	private IPetCollectionService? _petService;
	private IPetDataSource? _petDataSource;

	public override void _Ready()
	{
		// Create UI if not from scene
		if (!HasNode("ScrollContainer"))
		{
			Size = GetViewport().GetVisibleRect().Size;
			MouseFilter = MouseFilterEnum.Stop;

			var bg = new ColorRect();
			bg.Color = new Color(0.08f, 0.08f, 0.18f, 1.0f);
			bg.Size = Size;
			AddChild(bg);

			var scroll = new ScrollContainer();
			scroll.Name = "ScrollContainer";
			scroll.Size = Size;
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			AddChild(scroll);

			_grid = new GridContainer();
			_grid.Name = "Grid";
			_grid.Columns = 4;
			scroll.AddChild(_grid);

			var closeBtn = new Button();
			closeBtn.Text = "X Close";
			closeBtn.AddThemeFontSizeOverride("font_size", 28);
			closeBtn.Position = new Vector2(Size.X - 120, 20);
			closeBtn.Size = new Vector2(100, 50);
			closeBtn.Pressed += () => {
				var m = GetTree().GetFirstNodeInGroup("main") as Main;
				m?.HidePetCollection();
				var ts = GetTree().GetFirstNodeInGroup("title_screen") as Control;
				ts?.Show();
			};
			AddChild(closeBtn);
		}
		else
		{
			_grid = GetNode<GridContainer>("ScrollContainer/Grid");
		}

		_petService = ServiceInitializer.Instance?.GetService<IPetCollectionService>();
		_petDataSource = ServiceInitializer.Instance?.GetService<IPetDataSource>();

		EventBus.Instance.PetAcquired += OnPetAcquired;
		EventBus.Instance.PetLeveledUp += OnPetLeveledUp;
		EventBus.Instance.PetEvolved += OnPetEvolved;

		RefreshGrid();
	}

	public override void _ExitTree()
	{
		EventBus.Instance.PetAcquired -= OnPetAcquired;
		EventBus.Instance.PetLeveledUp -= OnPetLeveledUp;
		EventBus.Instance.PetEvolved -= OnPetEvolved;
	}

	private void OnPetAcquired(string petDefId) => RefreshGrid();
	private void OnPetLeveledUp(string petInstanceId, int newLevel) => RefreshGrid();
	private void OnPetEvolved(string oldPetInstanceId, string newPetDefId) => RefreshGrid();

	public void RefreshGrid()
	{
		foreach (var child in _grid.GetChildren())
			child.QueueFree();

		if (_petService == null || _petDataSource == null) return;

		var ownedPets = _petService.GetAllOwnedPets();
		if (ownedPets.Count == 0)
		{
			var emptyLabel = new Label();
			emptyLabel.Text = "No pets yet!\nPlay Gacha to get pets.";
			emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			emptyLabel.AddThemeFontSizeOverride("font_size", 24);
			emptyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
			emptyLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			_grid.AddChild(emptyLabel);
			return;
		}

		foreach (var pet in ownedPets)
		{
			var def = _petDataSource.GetPetDefinition(pet.PetDefId);
			if (def == null) continue;

			PetSlot slot;
			if (PetSlotScene != null)
				slot = PetSlotScene.Instantiate<PetSlot>();
			else
				slot = new PetSlot();

			slot.CustomMinimumSize = new Vector2(140, 160);
			slot.SetPet(pet, def);
			slot.Pressed += () => ShowDetailPopup(pet, def);
			_grid.AddChild(slot);
		}
	}

	private void ShowDetailPopup(PetInstance pet, PetDefinition def)
	{
		if (_petService == null) return;
		var popup = new PetDetailPopup();
		popup.SetPetData(pet, def, _petService);
		popup.PopupCentered();
		AddChild(popup);
	}
}
