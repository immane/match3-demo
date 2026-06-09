using Godot;

namespace Match3Demo;

public partial class HUD : CanvasLayer
{
	private Label _scoreLabel;
	private Label _bestScoreLabel;
	private Label _comboLabel;
	private Label _movesLabel;
	private Label _timerLabel;
	private Label _currencyLabel;
	private Button _pauseButton;
	private ProgressBar _timerBar;
	private PanelContainer _topBar;
	private PanelContainer _bottomBar;

	private int _displayedScore;

	public override void _Ready()
	{
		AddToGroup("hud");
		BuildUI();

		EventBus.Instance.ScoreChanged += OnScoreChanged;
		EventBus.Instance.ComboUpdated += OnComboUpdated;
		EventBus.Instance.MovesChanged += OnMovesChanged;
		EventBus.Instance.TimeChanged += OnTimeChanged;
		EventBus.Instance.CurrencyChanged += OnCurrencyChanged;
	}

	public override void _ExitTree()
	{
		EventBus.Instance.ScoreChanged -= OnScoreChanged;
		EventBus.Instance.ComboUpdated -= OnComboUpdated;
		EventBus.Instance.MovesChanged -= OnMovesChanged;
		EventBus.Instance.TimeChanged -= OnTimeChanged;
		EventBus.Instance.CurrencyChanged -= OnCurrencyChanged;
	}

	private void BuildUI()
	{
		var viewW = GetViewport().GetVisibleRect().Size.X;

		// ── Top bar ──
		_topBar = new PanelContainer();
		_topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_topBar.OffsetBottom = 56;
		AddChild(_topBar);

		var topStyle = new StyleBoxFlat();
		topStyle.BgColor = new Color(0.06f, 0.06f, 0.12f, 0.92f);
		topStyle.CornerRadiusBottomLeft = 12;
		topStyle.CornerRadiusBottomRight = 12;
		_topBar.AddThemeStyleboxOverride("panel", topStyle);

		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 8);
		topRow.Alignment = BoxContainer.AlignmentMode.Center;
		topRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 10);
		_topBar.AddChild(topRow);

		// Score block
		var scoreBox = new VBoxContainer();
		scoreBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		topRow.AddChild(scoreBox);
		_scoreLabel = MakeLabel("0", 22, new Color(1f, 0.85f, 0.2f));
		scoreBox.AddChild(_scoreLabel);
		_bestScoreLabel = MakeLabel("BEST 0", 14, new Color(0.6f, 0.6f, 0.6f));
		scoreBox.AddChild(_bestScoreLabel);

		// Separator
		topRow.AddChild(MakeSeparator());

		// Moves block
		var movesBox = new VBoxContainer();
		movesBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		topRow.AddChild(movesBox);
		_movesLabel = MakeLabel("30", 24, new Color(1f, 1f, 1f));
		movesBox.AddChild(_movesLabel);
		var movesCaption = MakeLabel("MOVES", 12, new Color(0.5f, 0.5f, 0.6f));
		movesBox.AddChild(movesCaption);

		// Separator
		topRow.AddChild(MakeSeparator());

		// Timer block
		var timerBox = new VBoxContainer();
		timerBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		topRow.AddChild(timerBox);
		_timerLabel = MakeLabel("30", 24, new Color(0.4f, 0.9f, 1f));
		timerBox.AddChild(_timerLabel);
		var timerCaption = MakeLabel("TIMER", 12, new Color(0.5f, 0.5f, 0.6f));
		timerBox.AddChild(timerCaption);

		// Separator
		topRow.AddChild(MakeSeparator());

		// Currency block
		var currBox = new VBoxContainer();
		currBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		topRow.AddChild(currBox);
		_currencyLabel = MakeLabel("0", 22, new Color(1f, 0.85f, 0.2f));
		currBox.AddChild(_currencyLabel);
		var currCaption = MakeLabel("COINS", 12, new Color(0.5f, 0.5f, 0.6f));
		currBox.AddChild(currCaption);

		// Pause button
		_pauseButton = new Button();
		_pauseButton.Text = "II";
		_pauseButton.CustomMinimumSize = new Vector2(40, 36);
		_pauseButton.AddThemeFontSizeOverride("font_size", 18);
		_pauseButton.Flat = true;
		_pauseButton.Pressed += OnPausePressed;
		topRow.AddChild(_pauseButton);

		// ── Timer bar ──
		_timerBar = new ProgressBar();
		_timerBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_timerBar.OffsetTop = 58;
		_timerBar.OffsetBottom = 64;
		_timerBar.MaxValue = 30;
		_timerBar.Value = 30;
		_timerBar.ShowPercentage = false;
		var barStyle = new StyleBoxFlat();
		barStyle.BgColor = new Color(0.4f, 0.9f, 1f, 0.6f);
		_timerBar.AddThemeStyleboxOverride("fill", barStyle);
		var barBg = new StyleBoxFlat();
		barBg.BgColor = new Color(0f, 0f, 0f, 0.3f);
		_timerBar.AddThemeStyleboxOverride("background", barBg);
		AddChild(_timerBar);

		// ── Combo (center screen, shown only when active) ──
		_comboLabel = new Label();
		_comboLabel.Hide();
		_comboLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_comboLabel.VerticalAlignment = VerticalAlignment.Center;
		_comboLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_comboLabel.OffsetTop = 130;
		_comboLabel.OffsetBottom = 180;
		_comboLabel.OffsetLeft = -200;
		_comboLabel.OffsetRight = 200;
		_comboLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_comboLabel);

		UpdateDisplay();
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var l = new Label();
		l.Text = text;
		l.HorizontalAlignment = HorizontalAlignment.Center;
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		return l;
	}

	private static VSeparator MakeSeparator()
	{
		var s = new VSeparator();
		s.CustomMinimumSize = new Vector2(2, 36);
		s.AddThemeColorOverride("separator", new Color(1f, 1f, 1f, 0.15f));
		return s;
	}

	private void UpdateDisplay()
	{
		_scoreLabel.Text = $"{_displayedScore}";
		_bestScoreLabel.Text = $"BEST {GameData.Instance.HighScore}";
		_movesLabel.Text = $"{GameData.Instance.MovesRemaining}";
		_timerLabel.Text = $"{Mathf.CeilToInt(GameData.Instance.TimeRemaining)}";
		_timerBar.Value = GameData.Instance.TimeRemaining;
		_currencyLabel.Text = $"{GameData.Instance.GetCurrencyBalance("soft_currency")}";
	}

	private void OnScoreChanged(int newScore, int delta)
	{
		_displayedScore = newScore;
		_scoreLabel.Text = $"{newScore}";
		_bestScoreLabel.Text = $"BEST {GameData.Instance.HighScore}";
		if (delta > 0)
		{
			var pos = new Vector2(GetViewport().GetVisibleRect().Size.X / 2f, 200);
			EventBus.Instance.EmitSignal(EventBus.SignalName.ShowFloatingText, $"+{delta}", pos, new Color(1f, 0.85f, 0.2f));
		}
	}

	private void OnComboUpdated(int combo)
	{
		if (combo <= 1) { _comboLabel.Hide(); return; }
		string text = combo switch { 2 => "COMBO x2", 3 => "COMBO x3", 4 => "AMAZING x4", 5 => "INCREDIBLE x5", _ => $"INSANE x{combo}!" };
		_comboLabel.Text = text;
		_comboLabel.AddThemeFontSizeOverride("font_size", 42);
		_comboLabel.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0));
		_comboLabel.Show();
		_comboLabel.Scale = new Vector2(2f, 2f);
		var t = CreateTween();
		t.TweenProperty(_comboLabel, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	private void OnMovesChanged(int remaining)
	{
		_movesLabel.Text = $"{remaining}";
		_movesLabel.AddThemeColorOverride("font_color",
			remaining <= 5 ? new Color("ff4444") :
			remaining <= 10 ? new Color("ff8800") :
			new Color(1, 1, 1));
	}

	private void OnTimeChanged(float remaining)
	{
		int s = Mathf.CeilToInt(remaining);
		_timerLabel.Text = $"{s}";
		_timerBar.Value = remaining;
		var tColor = s <= 5 ? new Color("ff4444") : s <= 10 ? new Color("ff8800") : new Color(0.4f, 0.9f, 1f);
		_timerLabel.AddThemeColorOverride("font_color", tColor);
		(_timerBar.GetThemeStylebox("fill") as StyleBoxFlat)!.BgColor = new Color(tColor, 0.7f);
	}

	private void OnCurrencyChanged(string currencyId, int newBalance, int delta)
	{
		if (currencyId != "soft_currency") return;
		_currencyLabel.Text = $"{newBalance}";
	}

	private void OnPausePressed()
	{
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
		var sm = GetTree().GetFirstNodeInGroup("state_machine") as GameStateMachine;
		sm?.TogglePause();
	}

	private string GetCurrencyText()
	{
		int b = GameData.Instance.GetCurrencyBalance("soft_currency");
		return b >= 1000 ? $"C: {b / 1000f:F1}K" : $"C: {b}";
	}
}
