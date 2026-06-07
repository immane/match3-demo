using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class BoardDataTests
{
    [Fact]
    public void TestIndexConversion()
    {
        var board = new BoardData(8, 8);
        Assert.Equal(0, board.GetIndex(0, 0));
        Assert.Equal(63, board.GetIndex(7, 7));
        Assert.Equal(29, board.GetIndex(3, 5));
        var rc = board.RowCol(29);
        Assert.Equal(new Vector2I(5, 3), rc); // (col=5, row=3)
    }

    [Fact]
    public void TestSwap()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);  // RED
        board.GetTile(0, 1).SetCrystal(1);  // BLUE
        board.Swap(0, 0, 0, 1);
        Assert.Equal(1, board.GetTile(0, 0).CrystalType);
        Assert.Equal(0, board.GetTile(0, 1).CrystalType);
        Assert.Equal(0, board.GetTile(0, 0).Row);
        Assert.Equal(0, board.GetTile(0, 0).Col);
    }

    [Fact]
    public void TestDuplicateRestore()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(1, 1).SetCrystal(1);
        var data = board.DuplicateData();
        board.GetTile(0, 0).Clear();
        board.RestoreFromData(data);
        Assert.Equal(0, board.GetTile(0, 0).CrystalType);
        Assert.Equal(1, board.GetTile(1, 1).CrystalType);
    }

    [Fact]
    public void TestIsInBounds()
    {
        var board = new BoardData(8, 8);
        Assert.True(board.IsInBounds(0, 0));
        Assert.True(board.IsInBounds(7, 7));
        Assert.False(board.IsInBounds(-1, 0));
        Assert.False(board.IsInBounds(0, -1));
        Assert.False(board.IsInBounds(8, 0));
        Assert.False(board.IsInBounds(0, 8));
    }

    [Fact]
    public void TestCountType()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 1).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(0);
        board.GetTile(0, 3).SetCrystal(0);
        board.GetTile(0, 4).SetCrystal(0);
        board.GetTile(1, 0).SetCrystal(1);
        board.GetTile(1, 1).SetCrystal(1);
        board.GetTile(1, 2).SetCrystal(1);
        Assert.Equal(5, board.CountType(0));
        Assert.Equal(3, board.CountType(1));
        Assert.Equal(0, board.CountType(2));
    }

    [Fact]
    public void TestClearBoard()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.Clear();
        Assert.Equal(64, board.GetEmptyCount());
    }
}
