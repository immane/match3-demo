using Godot;

namespace Match3Demo;

public partial class PauseMenu : Control
{
	private Label _pauseLabel;
	private Button _resumeButton;
	private Button _restartButton;
	private Button _quitButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.WhenPaused;
		MouseFilter = MouseFilterEnum.Ignore;

		_pauseLabel = GetNode<Label>("VBoxContainer/PauseLabel");
		_resumeButton = GetNode<Button>("VBoxContainer/ResumeButton");
		_restartButton = GetNode<Button>("VBoxContainer/RestartButton");
		_quitButton = GetNode<Button>("VBoxContainer/QuitButton");

		EventBus.Instance.GamePaused += OnGamePaused;
		EventBus.Instance.GameResumed += OnGameResumed;

		_pauseLabel.AddThemeFontSizeOverride("font_size", 56);
		_pauseLabel.AddThemeColorOverride("font_color", new Color("ffd700"));

		_resumeButton.AddThemeFontSizeOverride("font_size", 24);
		_restartButton.AddThemeFontSizeOverride("font_size", 24);
		_quitButton.AddThemeFontSizeOverride("font_size", 24);

		_resumeButton.Pressed += OnResumePressed;
		_restartButton.Pressed += OnRestartPressed;
		_quitButton.Pressed += OnQuitPressed;
	}

	private void OnGamePaused()
	{
		MouseFilter = MouseFilterEnum.Stop;
		Show();
	}

	private void OnGameResumed()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Hide();
	}

	private void OnResumePressed()
	{
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
		var sm = GetTree().GetFirstNodeInGroup("state_machine") as GameStateMachine;
		sm?.TogglePause();
	}

	private void OnRestartPressed()
	{
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
		var board = GetTree().GetFirstNodeInGroup("board") as Board;
		var main = GetTree().GetFirstNodeInGroup("main") as Main;
		GameData.Instance.ResetLevel();
		board?.ResetBoard();
		main?.StartCountdown();
		MouseFilter = MouseFilterEnum.Ignore;
		Hide();
	}

	private void OnQuitPressed()
	{
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayEffect, "ui_click", Vector2.Zero);
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	public override void _ExitTree()
	{
		EventBus.Instance.GamePaused -= OnGamePaused;
		EventBus.Instance.GameResumed -= OnGameResumed;
	}
}
