using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public class MatchDetector
{
    private static readonly Vector2I[] DIRECTIONS = new[]
    {
        new Vector2I(0, -1), new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(-1, 0)
    };

    public static MatchResult DetectAll(BoardData board)
    {
        var result = new MatchResult();
        int size = board.Cols * board.Rows;
        result.MatchedFlags = new byte[size];

        var groups = new List<MatchResult.MatchGroup>();
        groups.AddRange(DetectHorizontal(board));
        groups.AddRange(DetectVertical(board));

        foreach (var g in groups)
        {
            foreach (var pos in g.Positions)
            {
                int idx = board.GetIndex(pos.Y, pos.X);
                result.MatchedFlags[idx] = 1;
            }
        }

        var visited = new byte[size];
        var finalGroups = new List<MatchResult.MatchGroup>();

        for (int i = 0; i < size; i++)
        {
            if (result.MatchedFlags[i] == 1 && visited[i] == 0)
            {
                var region = FloodFill(result.MatchedFlags, visited, i, board);
                if (region.Count >= 3)
                {
                    var group = ClassifyShape(region, board);
                    finalGroups.Add(group);
                }
            }
        }

        result.Groups = finalGroups;
        result.TotalMatched = CountMatched(result.MatchedFlags);

        foreach (var group in finalGroups)
        {
            var spawn = DetermineSpecial(group);
            if (spawn.SpecialType != -1)
            {
                result.SpecialSpawns.Add(spawn);
            }
        }

        return result;
    }

    private static List<MatchResult.MatchGroup> DetectHorizontal(BoardData board)
    {
        var groups = new List<MatchResult.MatchGroup>();
        for (int row = 0; row < board.Rows; row++)
        {
            int col = 0;
            while (col < board.Cols)
            {
                var tile = board.GetTile(row, col);
                if (tile.IsEmpty)
                {
                    col++;
                    continue;
                }
                int runType = tile.CrystalType;
                int runStart = col;
                col++;
                while (col < board.Cols)
                {
                    var nextTile = board.GetTile(row, col);
                    if (nextTile.IsEmpty || nextTile.CrystalType != runType)
                        break;
                    col++;
                }
                int runLength = col - runStart;
                if (runLength >= 3)
                {
                    var group = new MatchResult.MatchGroup();
                    group.Shape = 0;
                    group.CrystalType = runType;
                    group.MatchLength = runLength;
                    for (int c = runStart; c < runStart + runLength; c++)
                    {
                        group.Positions.Add(new Vector2I(c, row));
                    }
                    groups.Add(group);
                }
            }
        }
        return groups;
    }

    private static List<MatchResult.MatchGroup> DetectVertical(BoardData board)
    {
        var groups = new List<MatchResult.MatchGroup>();
        for (int col = 0; col < board.Cols; col++)
        {
            int row = 0;
            while (row < board.Rows)
            {
                var tile = board.GetTile(row, col);
                if (tile.IsEmpty)
                {
                    row++;
                    continue;
                }
                int runType = tile.CrystalType;
                int runStart = row;
                row++;
                while (row < board.Rows)
                {
                    var nextTile = board.GetTile(row, col);
                    if (nextTile.IsEmpty || nextTile.CrystalType != runType)
                        break;
                    row++;
                }
                int runLength = row - runStart;
                if (runLength >= 3)
                {
                    var group = new MatchResult.MatchGroup();
                    group.Shape = 1;
                    group.CrystalType = runType;
                    group.MatchLength = runLength;
                    for (int r = runStart; r < runStart + runLength; r++)
                    {
                        group.Positions.Add(new Vector2I(col, r));
                    }
                    groups.Add(group);
                }
            }
        }
        return groups;
    }

    private static List<Vector2I> FloodFill(byte[] flags, byte[] visited, int startIdx, BoardData board)
    {
        var region = new List<Vector2I>();
        var stack = new List<int> { startIdx };
        visited[startIdx] = 1;

        int crystalType = board.Tiles[startIdx].CrystalType;

        while (stack.Count > 0)
        {
            int currentIdx = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            var pos = board.RowCol(currentIdx);
            region.Add(pos);

            foreach (var dir in DIRECTIONS)
            {
                int nCol = pos.X + dir.X;
                int nRow = pos.Y + dir.Y;
                if (!board.IsInBounds(nRow, nCol))
                    continue;
                int nIdx = board.GetIndex(nRow, nCol);
                if (visited[nIdx] == 1)
                    continue;
                if (flags[nIdx] == 0)
                    continue;
                if (board.Tiles[nIdx].CrystalType != crystalType)
                    continue;
                visited[nIdx] = 1;
                stack.Add(nIdx);
            }
        }

        return region;
    }

    private static MatchResult.MatchGroup ClassifyShape(List<Vector2I> region, BoardData board)
    {
        var group = new MatchResult.MatchGroup();
        group.Positions = region;

        if (region.Count < 3)
            return group;

        var firstPos = region[0];
        group.CrystalType = board.GetTile(firstPos.Y, firstPos.X).CrystalType;

        int minCol = int.MaxValue;
        int maxCol = -1;
        int minRow = int.MaxValue;
        int maxRow = -1;

        var rowCounts = new Dictionary<int, int>();
        var colCounts = new Dictionary<int, int>();

        foreach (var pos in region)
        {
            minCol = Mathf.Min(minCol, pos.X);
            maxCol = Mathf.Max(maxCol, pos.X);
            minRow = Mathf.Min(minRow, pos.Y);
            maxRow = Mathf.Max(maxRow, pos.Y);
            rowCounts[pos.Y] = rowCounts.GetValueOrDefault(pos.Y, 0) + 1;
            colCounts[pos.X] = colCounts.GetValueOrDefault(pos.X, 0) + 1;
        }

        int colSpan = maxCol - minCol + 1;
        int rowSpan = maxRow - minRow + 1;
        int numRowsUsed = rowCounts.Keys.Count;
        int numColsUsed = colCounts.Keys.Count;

        int maxRowCount = 0;
        int maxRowKey = -1;
        foreach (var key in rowCounts.Keys)
        {
            if (rowCounts[key] > maxRowCount)
            {
                maxRowCount = rowCounts[key];
                maxRowKey = key;
            }
        }

        int maxColCount = 0;
        int maxColKey = -1;
        foreach (var key in colCounts.Keys)
        {
            if (colCounts[key] > maxColCount)
            {
                maxColCount = colCounts[key];
                maxColKey = key;
            }
        }

        int rowsWith3 = 0;
        foreach (var key in rowCounts.Keys)
        {
            if (rowCounts[key] >= 3)
                rowsWith3++;
        }

        int colsWith3 = 0;
        foreach (var key in colCounts.Keys)
        {
            if (colCounts[key] >= 3)
                colsWith3++;
        }

        bool isHLine = rowSpan == 1 && colSpan >= 3;
        bool isVLine = colSpan == 1 && rowSpan >= 3;

        if (isHLine)
        {
            group.Shape = 0;
            group.MatchLength = colSpan;
            group.Pivot = new Vector2I(minCol + colSpan / 2, minRow);
        }
        else if (isVLine)
        {
            group.Shape = 1;
            group.MatchLength = rowSpan;
            group.Pivot = new Vector2I(minCol, minRow + rowSpan / 2);
        }
        else if (rowsWith3 >= 2 && colsWith3 >= 2)
        {
            group.Shape = 4;
            group.Pivot = new Vector2I(maxColKey, maxRowKey);
        }
        else if (rowsWith3 >= 2)
        {
            group.Shape = 2;
            group.Pivot = new Vector2I(maxColKey, maxRowKey);
        }
        else if (colsWith3 >= 2)
        {
            group.Shape = 2;
            group.Pivot = new Vector2I(maxColKey, maxRowKey);
        }
        else if (numRowsUsed >= 2 && numColsUsed >= 2)
        {
            if (maxRowCount >= 3 && maxColCount >= 3)
            {
                group.Shape = 3;
                group.Pivot = new Vector2I(maxColKey, maxRowKey);
            }
            else
            {
                group.Shape = 2;
                group.Pivot = new Vector2I(maxColKey, maxRowKey);
            }
        }
        else
        {
            group.Shape = 0;
            group.MatchLength = region.Count;
        }

        return group;
    }

    private static MatchResult.SpecialSpawn DetermineSpecial(MatchResult.MatchGroup group)
    {
        var spawn = new MatchResult.SpecialSpawn();
        spawn.Position = group.Pivot;
        spawn.CrystalType = group.CrystalType;

        int length = group.MatchLength;

        switch (group.Shape)
        {
            case 0:
            case 1:
                if (length >= 5)
                    spawn.SpecialType = 1;
                else if (length >= 4)
                    spawn.SpecialType = 0;
                else
                    spawn.SpecialType = -1;
                break;
            case 2:
            case 3:
            case 4:
                if (group.Positions.Count >= 5)
                    spawn.SpecialType = 2;
                else
                    spawn.SpecialType = -1;
                break;
            default:
                spawn.SpecialType = -1;
                break;
        }

        return spawn;
    }

    private static int CountMatched(byte[] flags)
    {
        int count = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i] == 1)
                count++;
        }
        return count;
    }
}
