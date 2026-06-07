using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class GravitySystemTests
{
    [Fact]
    public void TestSingleFall()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        var falls = GravitySystem.ApplyGravity(board);
        Assert.Single(falls);
        var f = falls[0] as GravitySystem.FallInfo;
        Assert.Equal(0, f.FromRow);
        Assert.Equal(7, f.ToRow);
        Assert.Equal(7, f.GetDistance());
        Assert.Equal(0, board.GetTile(7, 0).CrystalType);
    }

    [Fact]
    public void TestMultipleFalls()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(2, 0).SetCrystal(1);
        board.GetTile(3, 0).SetCrystal(2);
        board.GetTile(4, 0).Clear();
        board.GetTile(5, 0).SetCrystal(3);
        GravitySystem.ApplyGravity(board);
        Assert.Equal(3, board.GetTile(7, 0).CrystalType);
        Assert.Equal(2, board.GetTile(6, 0).CrystalType);
        Assert.Equal(1, board.GetTile(5, 0).CrystalType);
        Assert.Equal(0, board.GetTile(4, 0).CrystalType);
        Assert.True(board.GetTile(0, 0).IsEmpty);
    }

    [Fact]
    public void TestNoFalls()
    {
        var board = new BoardData(8, 8);
        for (int r = 0; r < 8; r++)
            board.GetTile(r, 0).SetCrystal(r % 5);
        var falls = GravitySystem.ApplyGravity(board);
        Assert.Empty(falls);
    }

    [Fact]
    public void TestEmptyColumn()
    {
        var board = new BoardData(8, 8);
        var falls = GravitySystem.ApplyGravity(board);
        Assert.Empty(falls);
        for (int r = 0; r < 8; r++)
            Assert.True(board.GetTile(r, 0).IsEmpty);
    }
}
