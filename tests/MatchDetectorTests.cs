using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class MatchDetectorTests
{
    [Fact]
    public void TestHorizontal3()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 1).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        Assert.Equal(3, result.TotalMatched);
        Assert.Single(result.Groups);
    }

    [Fact]
    public void TestHorizontal4()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 1).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(0);
        board.GetTile(0, 3).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        Assert.Equal(4, result.TotalMatched);
    }

    [Fact]
    public void TestHorizontal5()
    {
        var board = new BoardData(8, 8);
        for (int c = 0; c < 5; c++)
            board.GetTile(0, c).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        Assert.Equal(5, result.TotalMatched);
    }

    [Fact]
    public void TestVertical3()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(1);
        board.GetTile(1, 0).SetCrystal(1);
        board.GetTile(2, 0).SetCrystal(1);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        Assert.Equal(3, result.TotalMatched);
    }

    [Fact]
    public void TestMultipleMatches()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 1).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(0);
        board.GetTile(2, 3).SetCrystal(1);
        board.GetTile(2, 4).SetCrystal(1);
        board.GetTile(2, 5).SetCrystal(1);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        Assert.Equal(6, result.TotalMatched);
    }

    [Fact]
    public void TestLShape()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(1, 0).SetCrystal(0);
        board.GetTile(2, 0).SetCrystal(0);
        board.GetTile(2, 1).SetCrystal(0);
        board.GetTile(2, 2).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
    }

    [Fact]
    public void TestNoMatch()
    {
        var board = new BoardData(8, 8);
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                board.GetTile(row, col).SetCrystal((row + col) % 5);
        var result = MatchDetector.DetectAll(board);
        Assert.False(result.HasMatches());
        Assert.Equal(0, result.TotalMatched);
    }
}
