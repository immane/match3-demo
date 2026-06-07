using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public class GravitySystem
{
    private const float FallBase = 0.1f;
    private const float FallPerRow = 0.08f;

    public static List<FallInfo> ApplyGravity(BoardData board)
    {
        var allFalls = new List<FallInfo>();
        for (int col = 0; col < board.Cols; col++)
        {
            var falls = ProcessColumn(board, col);
            allFalls.AddRange(falls);
        }
        return allFalls;
    }

    private static List<FallInfo> ProcessColumn(BoardData board, int col)
    {
        var falls = new List<FallInfo>();
        int writeRow = board.Rows - 1;

        for (int readRow = board.Rows - 1; readRow >= 0; readRow--)
        {
            var tile = board.GetTile(readRow, col);
            if (tile.IsEmpty)
                continue;

            if (readRow != writeRow)
            {
                var info = new FallInfo();
                info.FromRow = readRow;
                info.ToRow = writeRow;
                info.Col = col;
                info.CrystalType = tile.CrystalType;
                info.SpecialType = tile.SpecialType;
                falls.Add(info);

                board.Swap(readRow, col, writeRow, col);
            }

            writeRow--;
        }

        for (int row = writeRow; row >= 0; row--)
        {
            board.GetTile(row, col).Clear();
        }

        return falls;
    }

    public class FallInfo
    {
        public int FromRow;
        public int ToRow;
        public int Col;
        public int CrystalType = -1;
        public int SpecialType = -1;

        public int GetDistance()
        {
            return ToRow - FromRow;
        }

        public float GetDuration()
        {
            return FallBase + (float)GetDistance() * FallPerRow;
        }
    }
}
