using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class EdgeCasesTests
{
    [Fact]
    public void TestMaxCascadeLoopProtection()
    {
        var board = new BoardData(8, 8);
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                board.GetTile(row, col).SetCrystal((row + col) % 5);

        int cascadeDepth = 0;
        while (cascadeDepth < 20)
        {
            var result = MatchDetector.DetectAll(board);
            if (!result.HasMatches())
                break;
            cascadeDepth++;
            foreach (var pos in result.GetAllPositions())
                board.GetTile(pos.Y, pos.X).Clear();
            GravitySystem.ApplyGravity(board);
            SpawnSystem.FillEmpty(board);
        }

        Assert.True(cascadeDepth < 20, "Cascade loop should terminate before MAX_CASCADE_LOOPS");
    }

    [Fact]
    public void TestEmptyBoardOperations()
    {
        var board = new BoardData(8, 8);

        var result = MatchDetector.DetectAll(board);
        Assert.False(result.HasMatches());
        Assert.Equal(0, result.TotalMatched);
        Assert.Empty(result.Groups);

        var falls = GravitySystem.ApplyGravity(board);
        Assert.Empty(falls);

        var spawns = SpawnSystem.FillEmpty(board);
        Assert.Equal(64, spawns.Count);
        Assert.Equal(0, board.GetEmptyCount());
    }
}
