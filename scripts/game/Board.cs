using Godot;

namespace Match3Demo;

public partial class Board : Node2D
{
    private static readonly Color LightColor = new("3a3a5c");
    private static readonly Color DarkColor = new("2e2e4a");
    private const int GridCols = 8;
    private const int GridRows = 8;
    private const int CellSize = 72;
    private const int OffsetX = 40;
    private const int OffsetY = 120;
    private const int CellStep = 76;

    private Node2D _tileLayer;
    private Node2D _effectLayer;
    private Node2D _backgroundLayer;
    private InputHandler _inputHandler;

    public GameStateMachine StateMachine { get; private set; }
    public BoardData BoardData { get; private set; }
    public TileManager TileManager { get; private set; }
    public bool InteractionEnabled { get; set; }
    private bool _boardReady;

    public override async void _Ready()
    {
        _tileLayer = GetNode<Node2D>("TileLayer");
        _effectLayer = GetNode<Node2D>("EffectLayer");
        _backgroundLayer = GetNode<Node2D>("BackgroundLayer");
        _inputHandler = GetNode<InputHandler>("InputHandler");
        StateMachine = GetNode<GameStateMachine>("GameStateMachine");

        BoardData = new BoardData(8, 8, 5);
        AddToGroup("board");

        TileManager = new TileManager();
        _tileLayer.AddChild(TileManager);

        _inputHandler.ProcessMode = ProcessModeEnum.Disabled;
        StateMachine.BoardData = BoardData;
        StateMachine.TileManager = TileManager;
        StateMachine.StateChanged += OnStateChanged;
        StateMachine.AddToGroup("state_machine");

        QueueRedraw();

        await ToSignal(GetTree(), "process_frame");
        StateMachine.Initialize();
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

    public override void _Draw()
    {
        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                Vector2 pos = GridUtils.GridToWorld(row, col) - new Vector2(CellSize / 2.0f, CellSize / 2.0f);
                var rect = new Rect2(pos, new Vector2(CellSize, CellSize));
                Color bgColor = (row + col) % 2 == 0 ? LightColor : DarkColor;
                DrawRect(rect, bgColor);
                DrawRect(rect.Grow(2.0f), new Color(1, 1, 1, 0.03f), false, 1.0f);
            }
        }
    }
}
