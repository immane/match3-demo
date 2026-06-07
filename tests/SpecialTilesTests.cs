using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class SpecialTilesTests
{
    [Fact]
    public void TestSpecial4Bomb()
    {
        var board = new BoardData(8, 8);
        for (int c = 0; c < 4; c++)
            board.GetTile(0, c).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        var hasBomb = false;
        foreach (var spawn in result.SpecialSpawns)
        {
            if (spawn.SpecialType == (int)SpecialType.BOMB)
                hasBomb = true;
        }
        Assert.True(hasBomb, "4-in-a-row should produce BOMB special");
    }

    [Fact]
    public void TestSpecial5Rainbow()
    {
        var board = new BoardData(8, 8);
        for (int c = 0; c < 5; c++)
            board.GetTile(0, c).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        var hasRainbow = false;
        foreach (var spawn in result.SpecialSpawns)
        {
            if (spawn.SpecialType == (int)SpecialType.RAINBOW)
                hasRainbow = true;
        }
        Assert.True(hasRainbow, "5-in-a-row should produce RAINBOW special");
    }

    [Fact]
    public void TestSpecialLCross()
    {
        var board = new BoardData(8, 8);
        for (int r = 0; r < 3; r++)
            board.GetTile(r, 0).SetCrystal(0);
        for (int c = 1; c < 4; c++)
            board.GetTile(2, c).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches());
        Assert.True(result.TotalMatched > 0);
    }

    [Fact]
    public void TestNoSpecialFor3()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 1).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(0);
        var result = MatchDetector.DetectAll(board);
        Assert.Empty(result.SpecialSpawns);
    }
}
