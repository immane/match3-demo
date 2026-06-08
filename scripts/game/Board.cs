using Godot;

namespace Match3Demo;

public partial class Board : Node2D
{
	private const int GridCols = 8;
	private const int GridRows = 8;

	private Node2D _tileLayer;
	private Node2D _effectLayer;
	private BackgroundLayer _backgroundLayer;
	private InputHandler _inputHandler;
	private AnimationController _animController;

	public GameStateMachine StateMachine { get; private set; }
	public BoardData BoardData { get; private set; }
	public TileManager TileManager { get; private set; }
	public bool InteractionEnabled { get; set; }
	private bool _boardReady;

	public override async void _Ready()
	{
		_tileLayer = GetNode<Node2D>("TileLayer");
		_effectLayer = GetNode<Node2D>("EffectLayer");
		_backgroundLayer = GetNode<BackgroundLayer>("BackgroundLayer");
		_inputHandler = GetNode<InputHandler>("InputHandler");
		StateMachine = GetNode<GameStateMachine>("GameStateMachine");
		_animController = GetNode<AnimationController>("AnimationController");

		BoardData = new BoardData(8, 8, 5);
		AddToGroup("board");

		TileManager = new TileManager();
		_tileLayer.AddChild(TileManager);

		_inputHandler.ProcessMode = ProcessModeEnum.Disabled;
		_animController.BoardData = BoardData;
		_animController.TileManager = TileManager;
		StateMachine.BoardData = BoardData;
		StateMachine.TileManager = TileManager;
		StateMachine.AnimController = _animController;
		StateMachine.StateChanged += OnStateChanged;
		StateMachine.AddToGroup("state_machine");

		RecalculateLayout();
		GetTree().Root.SizeChanged += RecalculateLayout;
	}

	private void RecalculateLayout()
	{
		var viewportSize = GetViewportRect().Size;
		GridUtils.Configure(GridCols, GridRows, viewportSize);

		// Center the board so its grid center is at world origin (camera target)
		int totalW = (GridCols - 1) * GridUtils.CellStep + GridUtils.CellSize;
		int totalH = (GridRows - 1) * GridUtils.CellStep + GridUtils.CellSize;
		float gridCenterX = GridUtils.OffsetX + totalW / 2f;
		float gridCenterY = GridUtils.OffsetY + totalH / 2f;
		Position = new Vector2(-gridCenterX, -gridCenterY);

		TileManager?.RefreshPositions(BoardData);
		_backgroundLayer?.Redraw();
	}

	private void OnStateChanged(int prev, int current)
	{
		if (current == (int)GameState.IDLE)
			_boardReady = true;
	}

	public void ResetBoard()
	{
		if (GetTree().Paused)
			GetTree().Paused = false;
		_boardReady = false;
		TileManager.ReleaseAll();
		StateMachine.Initialize();
	}

	public void SetInteractionEnabled(bool enabled)
	{
		InteractionEnabled = enabled;
		if (!enabled)
			_boardReady = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!InteractionEnabled)
			return;
		if (!_boardReady)
			return;
		if (!IsInstanceValid(StateMachine) || !StateMachine.IsInputAllowed())
			return;
		if (@event is not InputEventMouseButton)
			return;
		var mouseBtn = (InputEventMouseButton)@event;
		if (mouseBtn.ButtonIndex != MouseButton.Left || !mouseBtn.Pressed)
			return;

		Vector2 localPos = GetLocalMousePosition();
		Vector2I gridPos = GridUtils.WorldToGrid(localPos);

		if (gridPos.X < 0 || gridPos.Y < 0)
		{
			StateMachine.ClearSelection();
			return;
		}

		int index = GridUtils.ToIndex(gridPos.Y, gridPos.X);
		var tile = TileManager.GetActiveTile(index);
		if (tile != null)
			StateMachine.OnTileClicked(tile);
		else
			StateMachine.ClearSelection();
	}

	public override void _ExitTree()
	{
		GetTree().Root.SizeChanged -= RecalculateLayout;
		if (StateMachine != null)
			StateMachine.StateChanged -= OnStateChanged;
	}
}
