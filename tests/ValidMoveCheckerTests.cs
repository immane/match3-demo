using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class ValidMoveCheckerTests
{
    [Fact]
    public void TestHasValidMove()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 1).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(1);
        board.GetTile(0, 3).SetCrystal(0);
        Assert.True(ValidMoveChecker.HasAnyValidMove(board));
    }

    [Fact]
    public void TestHasValidMoveReturnsBool()
    {
        var board = new BoardData(8, 8);
        int[] pattern = { 0, 1, 2, 4, 3, 1, 2, 3 };
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                board.GetTile(row, col).SetCrystal(pattern[(row + col) % pattern.Length]);
        var result = ValidMoveChecker.HasAnyValidMove(board);
        Assert.IsType<bool>(result);
    }
}
