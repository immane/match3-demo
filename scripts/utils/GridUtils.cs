using Godot;

namespace Match3Demo;

public static class GridUtils
{
    public const int CellSize = 72;
    public const int CellSpacing = 4;
    public const int CellStep = 76;
    public const int OffsetX = 40;
    public const int OffsetY = 120;

    public static Vector2 GridToWorld(int row, int col)
    {
        return new Vector2(
            OffsetX + col * CellStep + CellSize / 2f,
            OffsetY + row * CellStep + CellSize / 2f
        );
    }

    public static Vector2I WorldToGrid(Vector2 worldPos)
    {
        int col = Mathf.FloorToInt((worldPos.X - OffsetX) / CellStep);
        int row = Mathf.FloorToInt((worldPos.Y - OffsetY) / CellStep);

        if (col < 0 || col >= 8 || row < 0 || row >= 8)
            return new Vector2I(-1, -1);

        return new Vector2I(col, row);
    }

    public static int ToIndex(int row, int col, int pCols = 8)
    {
        return row * pCols + col;
    }

    public static Vector2I ToRowCol(int index, int pCols = 8)
    {
        return new Vector2I(index % pCols, index / pCols);
    }
}
