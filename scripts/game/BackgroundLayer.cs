using Godot;

namespace Match3Demo;

public partial class BackgroundLayer : Node2D
{
	private static readonly Color LightColor = new(0.227f, 0.227f, 0.361f, 0.85f);
	private static readonly Color DarkColor = new(0.18f, 0.18f, 0.29f, 0.85f);
	private const int GridCols = 8;
	private const int GridRows = 8;

	public override void _Ready()
	{
	}

	public void Redraw()
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		int cellSize = GridUtils.CellSize;
		for (int row = 0; row < GridRows; row++)
		{
			for (int col = 0; col < GridCols; col++)
			{
				Vector2 pos = GridUtils.GridToWorld(row, col) - new Vector2(cellSize / 2f, cellSize / 2f);
				var rect = new Rect2(pos, new Vector2(cellSize, cellSize));
				Color bgColor = (row + col) % 2 == 0 ? LightColor : DarkColor;
				DrawRect(rect, bgColor);
				DrawRect(rect.Grow(2.0f), new Color(1, 1, 1, 0.03f), false, 1.0f);
			}
		}
	}
}
