using Godot;

namespace Match3Demo;

public partial class HUD : CanvasLayer
{
	private Label _scoreLabel;
	private Label _bestScoreLabel;
	private Label _comboLabel;
	private Label _movesLabel;
	private Button _pauseButton;

	private int _displayedScore;

	public override void _Ready()
	{
		AddToGroup("hud");

		_scoreLabel = GetNode<Label>("TopPanel/HBoxContainer/ScoreSection/ScoreLabel");
		_bestScoreLabel = GetNode<Label>("TopPanel/HBoxContainer/ScoreSection/BestScoreLabel");
		_comboLabel = GetNode<Label>("TopPanel/HBoxContainer/ComboSection/ComboLabel");
		_movesLabel = GetNode<Label>("TopPanel/HBoxContainer/MovesSection/MovesLabel");
		_pauseButton = GetNode<Button>("PauseButton");

		EventBus.Instance.ScoreChanged += OnScoreChanged;
		EventBus.Instance.ComboUpdated += OnComboUpdated;
		EventBus.Instance.MovesChanged += OnMovesChanged;
		_pauseButton.Pressed += OnPausePressed;

		_scoreLabel.AddThemeFontSizeOverride("font_size", 22);
		_bestScoreLabel.AddThemeFontSizeOverride("font_size", 18);
		_comboLabel.AddThemeFontSizeOverride("font_size", 28);
		_movesLabel.AddThemeFontSizeOverride("font_size", 22);

		_scoreLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
		_bestScoreLabel.AddThemeColorOverride("font_color", new Color("aaaaaa"));
		_comboLabel.AddThemeColorOverride("font_color", new Color("ffaa00"));
		_movesLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));

		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		_scoreLabel.Text = $"SCORE: {_displayedScore}";
		_bestScoreLabel.Text = $"BEST: {GameData.Instance.HighScore}";
		_movesLabel.Text = $"MOVES: {GameData.Instance.MovesRemaining}";
	}

	private void OnScoreChanged(int newScore, int delta)
	{
		_displayedScore = newScore;
		_scoreLabel.Text = $"SCORE: {newScore}";
		_bestScoreLabel.Text = $"BEST: {GameData.Instance.HighScore}";

		if (delta > 0)
		{
			var pos = new Vector2(360, 200);
			EventBus.Instance.EmitSignal(EventBus.SignalName.ShowFloatingText, $"+{delta}", pos, new Color(1, 0.843137f, 0));
		}
	}

	private void OnComboUpdated(int combo)
	{
		if (combo <= 1)
		{
			_comboLabel.Hide();
			return;
		}

		string text = combo switch
		{
			2 => "Combo x2",
			3 => "Combo x3",
			4 => "Amazing x4",
			5 => "Incredible x5",
			_ => $"INSANE x{combo}!"
		};

		_comboLabel.Text = text;
		_comboLabel.Show();

		_comboLabel.Scale = new Vector2(2.0f, 2.0f);
		var tween = CreateTween();
		tween.TweenProperty(_comboLabel, "scale", Vector2.One, 0.3f)
			.SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

		float r = 1.0f;
		float g = Mathf.Clamp(1.0f - combo * 0.1f, 0.0f, 1.0f);
		float b = Mathf.Clamp(0.3f - combo * 0.05f, 0.0f, 1.0f);
		_comboLabel.AddThemeColorOverride("font_color", new Color(r, g, b));
	}

	private void OnMovesChanged(int remaining)
	{
		_movesLabel.Text = $"MOVES: {remaining}";

		if (remaining <= 5)
			_movesLabel.AddThemeColorOverride("font_color", new Color("ff4444"));
		else if (remaining <= 10)
			_movesLabel.AddThemeColorOverride("font_color", new Color("ff8800"));
		else
			_movesLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
	}

	private void OnPausePressed()
	{
		var sm = GetTree().GetFirstNodeInGroup("state_machine") as GameStateMachine;
		sm?.TogglePause();
	}
}
