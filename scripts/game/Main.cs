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
    private PetShowcase _petShowcase;
    private GachaBannerUI _gachaUI;
    private PetLayer _petLayer;

    public override void _Ready()
    {
        _titleScreen = GetNode<Control>("UILayer/TitleScreen");
        _pauseMenu = GetNode<Control>("UILayer/PauseMenu");
        _gameOverPanel = GetNode<Control>("UILayer/GameOverPanel");
        _petShowcase = GetNode<PetShowcase>("UILayer/PetShowcase");
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

        // Add pet/gacha buttons to title screen
        if (_titleScreen is TitleScreen ts)
            ts.AddExtraButtons(this);

        if (_board.HasNode("ScreenShake"))
        {
            var ss = _board.GetNode<ScreenShake>("ScreenShake");
            ss.Camera = _camera;
        }

        // Load gacha UI from scene
        var gachaScene = GD.Load<PackedScene>("res://assets/scenes/gacha_banner_ui.tscn");
        _gachaUI = gachaScene.Instantiate<GachaBannerUI>();
        _gachaUI.Hide();
        GetNode<CanvasLayer>("UILayer").AddChild(_gachaUI);

        // Create pet layer (world-space, sibling of Board)
        _petLayer = new PetLayer();
        _petLayer.Name = "PetLayer";
        AddChild(_petLayer);
    }

    private void OnGameStarted()
    {
        _titleScreen.Hide();
        _hud.Show();
        GameData.Instance.ResetLevel();
        _board.ResetBoard();
        _board.SetInteractionEnabled(true);
        StartCountdown();
        SpawnPetActor();
    }

    private void OnGameOver()
    {
        _board.SetInteractionEnabled(false);
        _countdownTimer.Stop();
        _gameOverPanel.Show();
        RemovePetActor();
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

	public void ShowPetCollection()
	{
		_board.Hide();
		_hud.Hide();
		_petLayer.Hide();
		_petShowcase.Show();
		_petShowcase.SpawnPets();
	}

	public void HidePetCollection()
	{
		_petShowcase.Hide();
		_board.Show();
		_hud.Show();
		_petLayer.Show();
		_titleScreen.Show();
	}

	public void ShowGachaUI()
	{
		_board.Hide();
		_hud.Hide();
		_petLayer.Hide();
		_gachaUI.Show();
	}

	public void HideGachaUI()
	{
		_gachaUI.Hide();
		_board.Show();
		_hud.Show();
		_petLayer.Show();
		_titleScreen.Show();
	}

    private void SpawnPetActor()
    {
        _petLayer.SpawnActivePet();
    }

    private void RemovePetActor()
    {
        _petLayer.RemoveActivePet();
    }

    private void OnActivePetChanged(string petInstanceId)
    {
        // Handled by PetLayer
    }
}
