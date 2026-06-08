using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public partial class PetShowcase : Control
{
	private Node2D? _petWorld;
	private readonly List<PetActor> _actors = new();
	private PetActor? _selectedActor;
	private PetInstance? _selectedPet;
	private PetDefinition? _selectedDef;
	private PetCareService? _careService;
	private IPetCollectionService? _petService;
	private IPetDataSource? _petDs;

	private Control? _sidePanel;
	private ProgressBar? _hungerBar;
	private ProgressBar? _happinessBar;
	private ProgressBar? _energyBar;
	private Label? _petNameLabel;
	private Label? _petLevelLabel;
	private Label? _currencyLabel;
	private Label? _xpLabel;
	private VBoxContainer? _foodBox;

	public override void _Ready()
	{
		Size = GetViewport().GetVisibleRect().Size;
		MouseFilter = MouseFilterEnum.Stop;

		var bg = new ColorRect();
		bg.Color = new Color(0.08f, 0.09f, 0.18f, 1f);
		bg.Size = Size;
		AddChild(bg);

		_petWorld = new Node2D();
		_petWorld.Name = "PetWorld";
		AddChild(_petWorld);

		BuildSidePanel();

		var title = new Label();
		title.Text = "My Pets";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 34);
		title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		title.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		title.OffsetBottom = 44;
		AddChild(title);

		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.AddThemeFontSizeOverride("font_size", 28);
		closeBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		closeBtn.OffsetLeft = -56;
		closeBtn.OffsetRight = 0;
		closeBtn.OffsetBottom = 44;
		closeBtn.Pressed += () =>
		{
			var m = GetNode("/root/Main") as Main;
			m?.HidePetCollection();
		};
		AddChild(closeBtn);

		_careService = ServiceInitializer.Instance?.GetService<PetCareService>();
		_petService = ServiceInitializer.Instance?.GetService<IPetCollectionService>();
		_petDs = ServiceInitializer.Instance?.GetService<IPetDataSource>();

		EventBus.Instance.PetFed += OnPetFed;
	}

	public override void _ExitTree()
	{
		EventBus.Instance.PetFed -= OnPetFed;
		ClearAll();
	}

	public void SpawnPets()
	{
		ClearAll();
		var pets = _petService?.GetAllOwnedPets();
		if (pets == null || pets.Count == 0) return;

		float worldW = Size.X * 0.58f;
		float worldH = Size.Y;

		int count = pets.Count;
		int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
		int rows = Mathf.CeilToInt((float)count / cols);
		float cellW = worldW / cols;
		float cellH = worldH / rows;

		for (int i = 0; i < count; i++)
		{
			var pet = pets[i];
			var def = _petDs?.GetPetDefinition(pet.PetDefId);
			if (def == null) continue;

			int col = i % cols;
			int row = i / rows;
			float cx = cellW * col + cellW / 2f;
			float cy = cellH * row + cellH / 2f;

			var actor = new PetActor();
			actor.Setup(pet.PetDefId, pet.Nickname, def.Rarity, pet.Level);
			actor.Position = new Vector2(cx, cy);
			actor.SetWalkArea(
				new Vector2(cx - cellW / 2f + 40, cy - cellH / 2f + 50),
				new Vector2(cx + cellW / 2f - 40, cy + cellH / 2f - 70)
			);

			var capturedPet = pet;
			var capturedDef = def;
			actor.Connect(PetActor.SignalName.PetClicked, Callable.From<InputEvent>(evt =>
			{
				if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					SelectPet(actor, capturedPet, capturedDef);
			}));

			_petWorld?.AddChild(actor);
			_actors.Add(actor);
		}

		if (_actors.Count > 0 && _petDs != null)
			SelectPet(_actors[0], pets[0], _petDs.GetPetDefinition(pets[0].PetDefId)!);
	}

	private void SelectPet(PetActor actor, PetInstance pet, PetDefinition def)
	{
		_selectedActor = actor;
		_selectedPet = pet;
		_selectedDef = def;
		RefreshInfo();
	}

	private void ClearAll()
	{
		foreach (var a in _actors) a.QueueFree();
		_actors.Clear();
		_selectedActor = null;
		_selectedPet = null;
		_selectedDef = null;
	}

	public override void _Process(double delta)
	{
		_careService?.TickPets(delta);
		if (_selectedPet != null)
			UpdateBars();
	}

	// ========== SIDE PANEL ==========

	private void BuildSidePanel()
	{
		float panelX = Size.X * 0.6f;
		float panelW = Size.X - panelX - 8f;

		_sidePanel = new Control();
		_sidePanel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
		_sidePanel.OffsetLeft = -(panelW);
		_sidePanel.OffsetBottom = 0;
		_sidePanel.MouseFilter = MouseFilterEnum.Pass;
		AddChild(_sidePanel);

		var scroll = new ScrollContainer();
		scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_sidePanel.AddChild(scroll);

		var content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 8);
		content.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		scroll.AddChild(content);

		// Info section
		var infoSection = MakeSection(content, "Info");
		_petNameLabel = MakeLabel("Select a pet", 22, Colors.White);
		infoSection.AddChild(_petNameLabel);
		_petLevelLabel = MakeLabel("", 15, new Color(0.6f, 0.6f, 0.7f));
		infoSection.AddChild(_petLevelLabel);
		_xpLabel = MakeLabel("", 14, new Color(0.3f, 0.7f, 1f));
		infoSection.AddChild(_xpLabel);

		// Needs section
		var needsSection = MakeSection(content, "Needs");
		_hungerBar = MakeBar(needsSection, "Hunger", new Color(1f, 0.5f, 0.15f));
		_happinessBar = MakeBar(needsSection, "Happy", new Color(1f, 0.8f, 0.2f));
		_energyBar = MakeBar(needsSection, "Energy", new Color(0.25f, 0.75f, 1f));

		// Currency
		_currencyLabel = MakeLabel("Coins: 0", 16, new Color(1f, 0.85f, 0.2f));
		needsSection.AddChild(_currencyLabel);

		// Food section
		var foodSection = MakeSection(content, "Feed");
		_foodBox = new VBoxContainer();
		_foodBox.AddThemeConstantOverride("separation", 4);
		foodSection.AddChild(_foodBox);

		foreach (var food in PetFoodData.AllFoods)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);
			_foodBox.AddChild(row);

			var btn = new Button();
			btn.Text = $"{food.Emoji}";
			btn.TooltipText = $"{food.DisplayName}\nHunger +{food.HungerRestore}  Happy +{food.HappinessRestore}";
			btn.CustomMinimumSize = new Vector2(42, 36);
			btn.AddThemeFontSizeOverride("font_size", 16);
			var fid = food.FoodId;
			btn.Pressed += () => FeedPet(fid);
			row.AddChild(btn);

			var info = new Label();
			info.Text = $"{food.DisplayName}  {food.Cost}c";
			info.AddThemeFontSizeOverride("font_size", 13);
			info.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
			info.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(info);
		}

		// Play
		var playSection = MakeSection(content, "Play");
		var playBtn = new Button();
		playBtn.Text = "Play With Pet";
		playBtn.CustomMinimumSize = new Vector2(0, 42);
		playBtn.AddThemeFontSizeOverride("font_size", 16);
		playBtn.Pressed += () => PlayWithPet();
		playBtn.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		playSection.AddChild(playBtn);
	}

	private static VBoxContainer MakeSection(Node parent, string title)
	{
		var panel = new PanelContainer();
		panel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		parent.AddChild(panel);

		var titleLabel = new Label();
		titleLabel.Text = title;
		titleLabel.AddThemeFontSizeOverride("font_size", 13);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		panel.AddChild(titleLabel);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		panel.AddChild(box);
		return box;
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var l = new Label();
		l.Text = text;
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		return l;
	}

	private static ProgressBar MakeBar(Node parent, string label, Color color)
	{
		var bar = new ProgressBar();
		bar.MaxValue = PetNeeds.MaxValue;
		bar.Value = PetNeeds.MaxValue;
		bar.ShowPercentage = false;
		bar.CustomMinimumSize = new Vector2(0, 16);
		bar.SizeFlagsHorizontal = Control.SizeFlags.Fill;

		var fill = new StyleBoxFlat();
		fill.BgColor = color;
		bar.AddThemeStyleboxOverride("fill", fill);
		var bg = new StyleBoxFlat();
		bg.BgColor = new Color(0.15f, 0.15f, 0.15f);
		bar.AddThemeStyleboxOverride("background", bg);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		parent.AddChild(row);

		var lbl = new Label();
		lbl.Text = label;
		lbl.CustomMinimumSize = new Vector2(54, 0);
		lbl.AddThemeFontSizeOverride("font_size", 12);
		lbl.AddThemeColorOverride("font_color", color);
		row.AddChild(lbl);
		row.AddChild(bar);

		var val = new Label();
		val.Text = "100%";
		val.CustomMinimumSize = new Vector2(34, 0);
		val.AddThemeFontSizeOverride("font_size", 12);
		val.AddThemeColorOverride("font_color", Colors.White);
		row.AddChild(val);

		return bar;
	}

	// ========== INTERACTIONS ==========

	private void RefreshInfo()
	{
		if (_selectedPet == null || _selectedDef == null) return;
		_petNameLabel!.Text = !string.IsNullOrEmpty(_selectedPet.Nickname) ? _selectedPet.Nickname : _selectedDef.DisplayName;
		_petLevelLabel!.Text = $"{_selectedDef.Rarity}  |  Lv.{_selectedPet.Level} / {_selectedDef.MaxLevel}";
		_xpLabel!.Text = $"XP: {_selectedPet.CurrentXP} / {_selectedPet.NextLevelXP}";
		UpdateBars();
		var bal = ServiceInitializer.Instance?.GetService<ICurrencyService>()?.GetBalance("soft_currency") ?? 0;
		_currencyLabel!.Text = $"Coins: {bal}";
	}

	private void UpdateBars()
	{
		if (_selectedPet == null) return;
		var n = _selectedPet.Needs;
		SetBarVal(_hungerBar!, n.Hunger);
		SetBarVal(_happinessBar!, n.Happiness);
		SetBarVal(_energyBar!, n.Energy);
	}

	private static void SetBarVal(ProgressBar bar, float value)
	{
		bar.Value = value;
		if (bar.GetParent() is HBoxContainer row && row.GetChildCount() > 2)
			if (row.GetChild(2) is Label lbl)
				lbl.Text = $"{value:F0}%";
	}

	private void FeedPet(string foodId)
	{
		if (_selectedPet == null || _careService == null) return;
		if (_careService.Feed(_selectedPet.Id, foodId, out var error))
		{
			var food = PetFoodData.Get(foodId)!;
			Notify($"+{food.HungerRestore} Hunger  +{food.HappinessRestore} Happy", Colors.Green);
			RefreshInfo();
			if (_selectedActor != null)
				BounceActor(_selectedActor);
		}
		else if (error != null)
			Notify(error, Colors.Red);
	}

	private void PlayWithPet()
	{
		if (_selectedPet == null || _careService == null) return;
		if (_careService.Play(_selectedPet.Id, out var error))
		{
			Notify("+20 Happy  -10 Energy", Colors.Cyan);
			RefreshInfo();
			if (_selectedActor != null)
				BounceActor(_selectedActor);
		}
		else if (error != null)
			Notify(error, Colors.Red);
	}

	private static void BounceActor(PetActor actor)
	{
		var t = actor.CreateTween();
		t.TweenProperty(actor, "scale", new Vector2(1.25f, 1.25f), 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(actor, "scale", Vector2.One, 0.18f);
	}

	private void OnPetFed(string petInstanceId, string foodId) => RefreshInfo();

	private void Notify(string text, Color color)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
		lbl.AddThemeConstantOverride("outline_size", 2);
		lbl.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		lbl.OffsetTop = -40;
		lbl.OffsetBottom = 0;
		AddChild(lbl);

		var t = CreateTween();
		t.TweenProperty(lbl, "modulate:a", 0f, 2f);
		t.Finished += () => lbl.QueueFree();
	}
}
