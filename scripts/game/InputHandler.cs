using Godot;

namespace Match3Demo;

public partial class InputHandler : Node
{
    [Signal] public delegate void TileClickedEventHandler(Node2D tile);
    [Signal] public delegate void EmptySpaceClickedEventHandler();

    public BoardData BoardData { get; set; }
    public TileManager TileManager { get; set; }

    public override void _Input(InputEvent @event)
    {
        var sm = GetTree().GetFirstNodeInGroup("state_machine") as GameStateMachine;
        if (sm != null && !sm.IsInputAllowed())
            return;

        Vector2 clickPos = Vector2.Zero;
        bool isClick = false;

        if (@event is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
            {
                clickPos = mouseBtn.Position;
                isClick = true;
            }
        }

        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                clickPos = touch.Position;
                isClick = true;
            }
        }

        if (isClick)
        {
            Control hoveredControl = GetViewport().GuiGetHoveredControl();
            if (hoveredControl != null)
                return;
            ProcessClick(clickPos);
        }
    }

    private void ProcessClick(Vector2 screenPos)
    {
        Vector2I gridPos = GridUtils.WorldToGrid(screenPos);

        if (gridPos.X < 0 || gridPos.Y < 0)
        {
            EmitSignal(SignalName.EmptySpaceClicked);
            return;
        }

        if (TileManager == null || BoardData == null)
            return;

        int index = GridUtils.ToIndex(gridPos.Y, gridPos.X);
        var tile = TileManager.GetActiveTile(index);

        if (tile != null)
            EmitSignal(SignalName.TileClicked, tile);
        else
            EmitSignal(SignalName.EmptySpaceClicked);
    }
}
