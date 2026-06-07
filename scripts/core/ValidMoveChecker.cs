namespace Match3Demo;

public class ValidMoveChecker
{
    public static bool HasAnyValidMove(BoardData board)
    {
        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Cols; col++)
            {
                if (col + 1 < board.Cols)
                {
                    if (WouldMatch(board, row, col, row, col + 1))
                        return true;
                }
                if (row + 1 < board.Rows)
                {
                    if (WouldMatch(board, row, col, row + 1, col))
                        return true;
                }
            }
        }
        return false;
    }

    private static bool WouldMatch(BoardData board, int r1, int c1, int r2, int c2)
    {
        var t1 = board.GetTile(r1, c1);
        var t2 = board.GetTile(r2, c2);

        if (t1.IsEmpty || t2.IsEmpty)
            return false;

        board.Swap(r1, c1, r2, c2);

        bool hasMatch = QuickCheck(board, r1, c1) || QuickCheck(board, r2, c2);

        board.Swap(r1, c1, r2, c2);

        return hasMatch;
    }

    private static bool QuickCheck(BoardData board, int row, int col)
    {
        var tile = board.GetTile(row, col);
        if (tile.IsEmpty)
            return false;
        int t = tile.CrystalType;

        int leftCount = 0;
        int c = col - 1;
        while (c >= 0)
        {
            var ct = board.GetTile(row, c);
            if (ct.IsEmpty || ct.CrystalType != t)
                break;
            leftCount++;
            c--;
        }

        int rightCount = 0;
        c = col + 1;
        while (c < board.Cols)
        {
            var ct = board.GetTile(row, c);
            if (ct.IsEmpty || ct.CrystalType != t)
                break;
            rightCount++;
            c++;
        }

        if (1 + leftCount + rightCount >= 3)
            return true;

        int upCount = 0;
        int r = row - 1;
        while (r >= 0)
        {
            var ct = board.GetTile(r, col);
            if (ct.IsEmpty || ct.CrystalType != t)
                break;
            upCount++;
            r--;
        }

        int downCount = 0;
        r = row + 1;
        while (r < board.Rows)
        {
            var ct = board.GetTile(r, col);
            if (ct.IsEmpty || ct.CrystalType != t)
                break;
            downCount++;
            r++;
        }

        return 1 + upCount + downCount >= 3;
    }
}
