using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public class SpawnSystem
{
    public static List<SpawnInfo> FillEmpty(BoardData board)
    {
        var spawns = new List<SpawnInfo>();
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int col = 0; col < board.Cols; col++)
        {
            for (int row = 0; row < board.Rows; row++)
            {
                var tile = board.GetTile(row, col);
                if (!tile.IsEmpty)
                    continue;

                int crystalType = rng.RandiRange(0, 4);

                var info = new SpawnInfo();
                info.Row = row;
                info.Col = col;
                info.CrystalType = crystalType;
                spawns.Add(info);

                tile.SetCrystal(crystalType);
            }
        }

        return spawns;
    }

    public class SpawnInfo
    {
        public int Row;
        public int Col;
        public int CrystalType = -1;

        public Vector2 GetEnterOffset()
        {
            return new Vector2(0, -(Row + 1) * 76.0f);
        }
    }
}
