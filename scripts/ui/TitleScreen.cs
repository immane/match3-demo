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
		AddToGroup("title_screen");

		MouseFilter = MouseFilterEnum.Stop;

		_startButton = GetNode<Button>("VBoxContainer/StartButton");
		_highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
		_titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
		_subtitleLabel = GetNode<Label>("VBoxContainer/SubtitleLabel");

		_titleLabel.AddThemeFontSizeOverride("font_size", 96);
		_titleLabel.AddThemeColorOverride("font_color", new Color("f1db6dff"));

		_subtitleLabel.AddThemeFontSizeOverride("font_size", 36);
		_subtitleLabel.AddThemeColorOverride("font_color", new Color("cccccc"));

		_highScoreLabel.AddThemeFontSizeOverride("font_size", 32);
		_highScoreLabel.AddThemeColorOverride("font_color", new Color(1, 0.843137f, 0));

		_startButton.AddThemeFontSizeOverride("font_size", 30);
		_startButton.Pressed += OnStartPressed;

		// Pet/Gacha buttons are added by Main via AddTitleButtons()

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

	public void AddExtraButtons(Main main)
	{
		var vbox = GetNode<VBoxContainer>("VBoxContainer");

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 16);
		vbox.AddChild(spacer);

		var petBtn = new Button();
		petBtn.Text = "PETS";
		petBtn.CustomMinimumSize = new Vector2(0, 50);
		petBtn.AddThemeFontSizeOverride("font_size", 20);
		petBtn.Pressed += () =>
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
			Hide();
			main.ShowPetCollection();
		};
		vbox.AddChild(petBtn);

		var spacer2 = new Control();
		spacer2.CustomMinimumSize = new Vector2(0, 8);
		vbox.AddChild(spacer2);

		var gachaBtn = new Button();
		gachaBtn.Text = "GACHA";
		gachaBtn.CustomMinimumSize = new Vector2(0, 50);
		gachaBtn.AddThemeFontSizeOverride("font_size", 20);
		gachaBtn.Pressed += () =>
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
			Hide();
			main.ShowGachaUI();
		};
		vbox.AddChild(gachaBtn);

		vbox.OffsetBottom += 130;
	}
}
