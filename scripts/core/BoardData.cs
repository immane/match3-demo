using Godot;

namespace Match3Demo;

public partial class BoardData
{
    public int Cols = 8;
    public int Rows = 8;
    public CellData[] Tiles = System.Array.Empty<CellData>();
    public int NumCrystalTypes = 5;

    public BoardData(int pCols = 8, int pRows = 8, int pTypes = 5)
    {
        Cols = pCols;
        Rows = pRows;
        NumCrystalTypes = pTypes;
        int total = Cols * Rows;
        Tiles = new CellData[total];
        for (int i = 0; i < total; i++)
            Tiles[i] = new CellData();
    }

    public CellData GetTile(int pRow, int pCol)
    {
        return Tiles[pRow * Cols + pCol];
    }

    public void SetTile(int pRow, int pCol, CellData tile)
    {
        tile.Row = pRow;
        tile.Col = pCol;
        Tiles[pRow * Cols + pCol] = tile;
    }

    public int GetIndex(int pRow, int pCol)
    {
        return pRow * Cols + pCol;
    }

    public Vector2I RowCol(int pIndex)
    {
        return new Vector2I(pIndex % Cols, pIndex / Cols);
    }

    public bool IsInBounds(int pRow, int pCol)
    {
        return pRow >= 0 && pRow < Rows && pCol >= 0 && pCol < Cols;
    }

    public void Swap(int pRow1, int pCol1, int pRow2, int pCol2)
    {
        int idx1 = GetIndex(pRow1, pCol1);
        int idx2 = GetIndex(pRow2, pCol2);
        var temp = Tiles[idx1];
        Tiles[idx1] = Tiles[idx2];
        Tiles[idx2] = temp;
        Tiles[idx1].Row = pRow1;
        Tiles[idx1].Col = pCol1;
        Tiles[idx2].Row = pRow2;
        Tiles[idx2].Col = pCol2;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> DuplicateData()
    {
        var data = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var tile in Tiles)
        {
            var dict = new Godot.Collections.Dictionary();
            dict["type"] = tile.CrystalType;
            dict["special"] = tile.SpecialType;
            dict["empty"] = tile.IsEmpty;
            data.Add(dict);
        }
        return data;
    }

    public void RestoreFromData(Godot.Collections.Array<Godot.Collections.Dictionary> data)
    {
        for (int i = 0; i < Tiles.Length; i++)
        {
            Tiles[i].CrystalType = (int)data[i]["type"];
            Tiles[i].SpecialType = (int)data[i]["special"];
            Tiles[i].IsEmpty = (bool)data[i]["empty"];
        }
    }

    public void Clear()
    {
        foreach (var tile in Tiles)
            tile.Clear();
    }

    public int CountType(int pType)
    {
        int count = 0;
        foreach (var tile in Tiles)
        {
            if (!tile.IsEmpty && tile.CrystalType == pType)
                count++;
        }
        return count;
    }

    public int GetEmptyCount()
    {
        int count = 0;
        foreach (var tile in Tiles)
        {
            if (tile.IsEmpty)
                count++;
        }
        return count;
    }

    public class CellData
    {
        public int CrystalType = -1;
        public int SpecialType = -1;
        public int Row = -1;
        public int Col = -1;
        public bool IsEmpty = true;
        public bool IsLocked = false;
        public int LockHp = 0;

        public CellData(int pType = -1, int pSpecial = -1)
        {
            CrystalType = pType;
            SpecialType = pSpecial;
            IsEmpty = pType < 0;
        }

        public void Clear()
        {
            CrystalType = -1;
            SpecialType = -1;
            IsEmpty = true;
        }

        public void SetCrystal(int pType, int pSpecial = -1)
        {
            CrystalType = pType;
            SpecialType = pSpecial;
            IsEmpty = false;
        }

        public bool IsNormal()
        {
            return !IsEmpty && SpecialType == -1;
        }

        public bool IsSpecial()
        {
            return !IsEmpty && SpecialType != -1;
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "EMPTY";
            string s = CrystalType.ToString();
            if (SpecialType == 0)
                s += "B";
            else if (SpecialType == 1)
                s += "R";
            else if (SpecialType == 2)
                s += "C";
            return s;
        }
    }

    public class MoveRecord
    {
        public Vector2I From;
        public Vector2I To;
        public Godot.Collections.Array<Godot.Collections.Dictionary> Snapshot;
        public int ScoreGained = 0;
    }
}
