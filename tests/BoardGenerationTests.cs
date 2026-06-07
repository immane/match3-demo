using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class BoardGenerationTests
{
    [Fact]
    public void TestGenerateNoInitialMatches()
    {
        for (int i = 0; i < 20; i++)
        {
            var board = new BoardData(8, 8);
            GenerateNoMatch(board);
            var result = MatchDetector.DetectAll(board);
            Assert.False(result.HasMatches(), "Generated board should have no initial matches");
        }
    }

    [Fact]
    public void TestAllCellsFilled()
    {
        for (int i = 0; i < 10; i++)
        {
            var board = new BoardData(8, 8);
            GenerateNoMatch(board);
            Assert.Equal(0, board.GetEmptyCount());
        }
    }

    private static void GenerateNoMatch(BoardData board)
    {
        var rng = new Godot.RandomNumberGenerator();
        rng.Randomize();
        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Cols; col++)
            {
                var tile = board.GetTile(row, col);
                var forbidden = new Godot.Collections.Array<int>();

                if (col >= 2)
                {
                    var l1 = board.GetTile(row, col - 1);
                    var l2 = board.GetTile(row, col - 2);
                    if (!l1.IsEmpty && !l2.IsEmpty && l1.CrystalType == l2.CrystalType)
                        forbidden.Add(l1.CrystalType);
                }

                if (row >= 2)
                {
                    var u1 = board.GetTile(row - 1, col);
                    var u2 = board.GetTile(row - 2, col);
                    if (!u1.IsEmpty && !u2.IsEmpty && u1.CrystalType == u2.CrystalType)
                        forbidden.Add(u1.CrystalType);
                }

                var allowed = new Godot.Collections.Array<int>();
                for (int t = 0; t < 5; t++)
                {
                    if (!forbidden.Contains(t))
                        allowed.Add(t);
                }

                if (allowed.Count == 0)
                    tile.SetCrystal(rng.RandiRange(0, 4));
                else
                    tile.SetCrystal(allowed[rng.RandiRange(0, allowed.Count - 1)]);
            }
        }
    }
}
