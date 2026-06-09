using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public partial class PetShowcase : Control
{
	[Export] public PackedScene? PetActorScene { get; set; }

	private Node2D _petWorld = null!;
	private readonly List<PetActor> _actors = new();
	private PetCareService? _careService;
	private IPetCollectionService? _petService;
	private IPetDataSource? _petDs;

	private PopupPanel _petPopup = null!;
	private Label _popupNameLabel = null!;
	private Label _popupRarityLabel = null!;
	private Label _popupXPLabel = null!;
	private ProgressBar _hungerBar = null!;
	private ProgressBar _happyBar = null!;
	private ProgressBar _energyBar = null!;
	private Label _hungerValue = null!;
	private Label _happyValue = null!;
	private Label _energyValue = null!;
	private Label _currencyLabel = null!;

	private PetInstance? _selectedPet;
	private PetDefinition? _selectedDef;
	private PetActor? _selectedActor;

	public override void _Ready()
	{
		_petWorld = GetNode<Node2D>("PetWorld");
		_petPopup = GetNode<PopupPanel>("PetPopup");

		var content = _petPopup.GetNode<VBoxContainer>("ContentBox");
		_popupNameLabel = content.GetNode<Label>("PopupNameLabel");
		_popupRarityLabel = content.GetNode<Label>("PopupRarityLabel");
		_popupXPLabel = content.GetNode<Label>("PopupXPLabel");
		_currencyLabel = content.GetNode<Label>("PopupCurrencyLabel");

		var needsSection = content.GetNode<VBoxContainer>("NeedsSection");
		_hungerBar = needsSection.GetNode<ProgressBar>("HungerRow/HungerBar");
		_happyBar = needsSection.GetNode<ProgressBar>("HappyRow/HappyBar");
		_energyBar = needsSection.GetNode<ProgressBar>("EnergyRow/EnergyBar");
		_hungerValue = needsSection.GetNode<Label>("HungerRow/HungerValue");
		_happyValue = needsSection.GetNode<Label>("HappyRow/HappyValue");
		_energyValue = needsSection.GetNode<Label>("EnergyRow/EnergyValue");

		// Wire food buttons
		var foodGrid = content.GetNode<GridContainer>("FoodGrid");
		var foodIds = new[] { "fish", "milk", "treat", "steak", "cake", "water" };
		int idx = 0;
		foreach (var child in foodGrid.GetChildren())
		{
			if (child is Button btn && idx < foodIds.Length)
			{
				var fid = foodIds[idx++];
				btn.Pressed += () => FeedPet(fid);
			}
		}

		content.GetNode<Button>("PlayButton").Pressed += PlayWithPet;
		content.GetNode<Button>("PopupCloseButton").Pressed += () => _petPopup.Hide();

		GetNode<Button>("TopBar/CloseButton").Pressed += () =>
		{
			var m = GetNode("/root/Main") as Main;
			m?.HidePetCollection();
		};

		// Style progress bars
		StyleBar(_hungerBar, new Color(1f, 0.35f, 0.1f));
		StyleBar(_happyBar, new Color(1f, 0.7f, 0.1f));
		StyleBar(_energyBar, new Color(0.2f, 0.65f, 1f));

		_careService = ServiceInitializer.Instance?.GetService<PetCareService>();
		_petService = ServiceInitializer.Instance?.GetService<IPetCollectionService>();
		_petDs = ServiceInitializer.Instance?.GetService<IPetDataSource>();
	}

	public override void _ExitTree()
	{
		ClearAll();
	}

	public void SpawnPets()
	{
		ClearAll();
		var pets = _petService?.GetAllOwnedPets();
		if (pets == null || pets.Count == 0) return;

		float worldW = GetViewport().GetVisibleRect().Size.X;
		float worldH = GetViewport().GetVisibleRect().Size.Y - 100f;

		int count = pets.Count;
		int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
		int rows = Mathf.CeilToInt((float)count / cols);
		float cellW = Mathf.Max(worldW / cols, 210f);
		float cellH = Mathf.Max(worldH / rows, 250f);

		for (int i = 0; i < count; i++)
		{
			var pet = pets[i];
			var def = _petDs?.GetPetDefinition(pet.PetDefId);
			if (def == null) continue;

			int col = i % cols;
			int row = i / rows;
			float cx = cellW * col + cellW / 2f;
			float cy = cellH * row + cellH / 2f;

			var actor = PetActorScene?.Instantiate<PetActor>() ?? new PetActor();
			actor.Setup(pet, def, showBars: true);
			actor.Position = new Vector2(cx, cy);
			actor.SetWalkArea(
				new Vector2(cx - cellW / 2f + 60, cy - cellH / 2f + 100),
				new Vector2(cx + cellW / 2f - 60, cy + cellH / 2f - 100)
			);

			actor.Connect(PetActor.SignalName.PetClicked, Callable.From<InputEvent>(evt =>
			{
				if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					ShowPetPopup(pet, def, actor);
			}));

			_petWorld.AddChild(actor);
			_actors.Add(actor);
		}
	}

	private void ClearAll()
	{
		foreach (var a in _actors) a.QueueFree();
		_actors.Clear();
		_selectedPet = null;
		_selectedDef = null;
		_selectedActor = null;
	}

	public override void _Process(double delta)
	{
		_careService?.TickPets(delta);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			var mpos = GetGlobalMousePosition();
			foreach (var actor in _actors)
			{
				if (actor.PetInstance == null) continue;
				if ((mpos - actor.GlobalPosition).Length() < 96f)
				{
					ShowPetPopup(actor.PetInstance, actor.PetDef!, actor);
					AcceptEvent();
					break;
				}
			}
		}
	}

	private void ShowPetPopup(PetInstance pet, PetDefinition def, PetActor actor)
	{
		_selectedPet = pet;
		_selectedDef = def;
		_selectedActor = actor;

		_popupNameLabel.Text = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : def.DisplayName;
		_popupRarityLabel.Text = $"{def.Rarity}  |  Lv.{pet.Level} / {def.MaxLevel}";
		_popupXPLabel.Text = $"XP: {pet.CurrentXP} / {pet.NextLevelXP}";

		UpdateBars();
		_currencyLabel.Text = $"Coins: {ServiceInitializer.Instance?.GetService<ICurrencyService>()?.GetBalance("soft_currency") ?? 0}";

		_petPopup.PopupCentered();
	}

	private void UpdateBars()
	{
		if (_selectedPet == null) return;
		var n = _selectedPet.Needs;
		SetBar(_hungerBar, _hungerValue, n.Hunger);
		SetBar(_happyBar, _happyValue, n.Happiness);
		SetBar(_energyBar, _energyValue, n.Energy);
		_currencyLabel.Text = $"Coins: {ServiceInitializer.Instance?.GetService<ICurrencyService>()?.GetBalance("soft_currency") ?? 0}";
	}

	private static void SetBar(ProgressBar bar, Label lbl, float val)
	{
		bar.Value = val;
		lbl.Text = $"{val:F0}%";
	}

	private void FeedPet(string foodId)
	{
		if (_selectedPet == null || _careService == null) return;
		if (_careService.Feed(_selectedPet.Id, foodId, out _))
		{
			UpdateBars();
			if (_selectedActor != null) Bounce(_selectedActor);
		}
	}

	private void PlayWithPet()
	{
		if (_selectedPet == null || _careService == null) return;
		if (_careService.Play(_selectedPet.Id, out _))
		{
			UpdateBars();
			if (_selectedActor != null) Bounce(_selectedActor);
		}
	}

	private static void Bounce(PetActor a)
	{
		var t = a.CreateTween();
		t.TweenProperty(a, "scale", new Vector2(1.25f, 1.25f), 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(a, "scale", Vector2.One, 0.18f);
	}

	private static void StyleBar(ProgressBar bar, Color c)
	{
		var fill = new StyleBoxFlat { BgColor = c };
		bar.AddThemeStyleboxOverride("fill", fill);
		var bg = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.12f) };
		bar.AddThemeStyleboxOverride("background", bg);
	}
}
