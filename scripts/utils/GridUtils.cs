using Godot;

namespace Match3Demo;

public static class GridUtils
{
    public static int CellSize { get; private set; } = 72;
    public static int CellStep { get; private set; } = 76;
    public static int OffsetX { get; private set; } = 40;
    public static int OffsetY { get; private set; } = 120;
    public static int GridCols { get; private set; } = 8;
    public static int GridRows { get; private set; } = 8;

    public static void Configure(int cols, int rows, Vector2 boardArea)
    {
        GridCols = cols;
        GridRows = rows;
        const int spacing = 4;

        int maxW = Mathf.FloorToInt((boardArea.X - (cols - 1) * spacing) / (float)cols);
        int maxH = Mathf.FloorToInt((boardArea.Y - (rows - 1) * spacing) / (float)rows);
        int cellSize = Mathf.Min(maxW, maxH);
        cellSize = Mathf.Clamp(cellSize, 40, 120);

        CellSize = cellSize;
        CellStep = cellSize + spacing;

        int totalW = (cols - 1) * CellStep + CellSize;
        int totalH = (rows - 1) * CellStep + CellSize;
        OffsetX = Mathf.FloorToInt((boardArea.X - totalW) / 2f);
        OffsetY = Mathf.FloorToInt((boardArea.Y - totalH) / 2f);
    }

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

        if (col < 0 || col >= GridCols || row < 0 || row >= GridRows)
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
