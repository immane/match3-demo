using Godot;

namespace Match3Demo;

public partial class Main : Node2D
{
    private Control _titleScreen;
    private Control _pauseMenu;
    private Control _gameOverPanel;
    private Board _board;
    private CanvasLayer _hud;
    private Camera2D _camera;
    private Timer _countdownTimer;

    public override void _Ready()
    {
        _titleScreen = GetNode<Control>("UILayer/TitleScreen");
        _pauseMenu = GetNode<Control>("UILayer/PauseMenu");
        _gameOverPanel = GetNode<Control>("UILayer/GameOverPanel");
        _board = GetNode<Board>("Board");
        _hud = GetNode<CanvasLayer>("HUD");
        _camera = GetNode<Camera2D>("Camera2D");

        AddToGroup("main");

        _countdownTimer = new Timer();
        _countdownTimer.WaitTime = 1.0f;
        _countdownTimer.Timeout += OnCountdownTick;
        AddChild(_countdownTimer);

        DisplayServer.WindowSetTitle("Match3 Crystal Demo");

        _titleScreen.Connect("GameStarted", Callable.From(OnGameStarted));
        EventBus.Instance.GameOver += OnGameOver;

        _titleScreen.Show();
        _hud.Hide();
        _pauseMenu.Hide();
        _gameOverPanel.Hide();
        _board.SetInteractionEnabled(false);

        if (_board.HasNode("ScreenShake"))
        {
            var ss = _board.GetNode<ScreenShake>("ScreenShake");
            ss.Camera = _camera;
        }
    }

    private void OnGameStarted()
    {
        _titleScreen.Hide();
        _hud.Show();
        GameData.Instance.ResetLevel();
        _board.ResetBoard();
        _board.SetInteractionEnabled(true);
        StartCountdown();
    }

    private void OnGameOver()
    {
        _board.SetInteractionEnabled(false);
        _countdownTimer.Stop();
        _gameOverPanel.Show();
    }

    private void OnCountdownTick()
    {
        GameData.Instance.TimeRemaining -= 1f;
        EventBus.Instance.EmitSignal(EventBus.SignalName.TimeChanged, GameData.Instance.TimeRemaining);

        if (GameData.Instance.TimeRemaining <= 0f)
        {
            _countdownTimer.Stop();
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameOver);
        }
    }

    public void StartCountdown()
    {
        GameData.Instance.TimeRemaining = 30f;
        EventBus.Instance.EmitSignal(EventBus.SignalName.TimeChanged, GameData.Instance.TimeRemaining);
        _countdownTimer.Start();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GameOver -= OnGameOver;
    }
}
