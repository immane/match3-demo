using Godot;

namespace Match3Demo;

public partial class TitleScreen : Control
{
	[Signal]
	public delegate void GameStartedEventHandler();

	private Button _startButton;
	private Label _highScoreLabel;
	private Label _titleLabel;
	private Label _subtitleLabel;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;

		_startButton = GetNode<Button>("VBoxContainer/StartButton");
		_highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
		_titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
		_subtitleLabel = GetNode<Label>("VBoxContainer/SubtitleLabel");

		_titleLabel.AddThemeFontSizeOverride("font_size", 64);
		_titleLabel.AddThemeColorOverride("font_color", new Color("ffd700"));

		_subtitleLabel.AddThemeFontSizeOverride("font_size", 24);
		_subtitleLabel.AddThemeColorOverride("font_color", new Color("aaaaaa"));

		_highScoreLabel.AddThemeFontSizeOverride("font_size", 20);
		_highScoreLabel.AddThemeColorOverride("font_color", new Color(1, 0.843137f, 0));

		_startButton.Pressed += OnStartPressed;

		_highScoreLabel.Text = $"BEST: {GameData.Instance.HighScore}";

		var tween = CreateTween();
		tween.SetLoops();
		tween.TweenProperty(_titleLabel, "position:y", -5.0f, 1.5f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut).AsRelative();
		tween.TweenProperty(_titleLabel, "position:y", 5.0f, 1.5f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut).AsRelative();
	}

	private void OnStartPressed()
	{
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
		MouseFilter = MouseFilterEnum.Ignore;
		Hide();
		EmitSignal(SignalName.GameStarted);
	}
}
