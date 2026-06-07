using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class SwapClearCascadeTests
{
    [Fact]
    public void TestSwapDetectionAndClear()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(0, 2).SetCrystal(0);
        board.GetTile(0, 3).SetCrystal(0);
        board.GetTile(0, 4).SetCrystal(1);
        board.GetTile(0, 5).SetCrystal(1);

        board.Swap(0, 0, 0, 1);
        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches(), "Swap should produce matches");

        var positions = result.GetAllPositions();
        foreach (var pos in positions)
            board.GetTile(pos.Y, pos.X).Clear();

        foreach (var pos in positions)
            Assert.True(board.GetTile(pos.Y, pos.X).IsEmpty, "Cleared tiles should be empty");

        GravitySystem.ApplyGravity(board);
        SpawnSystem.FillEmpty(board);

        Assert.Equal(0, board.GetEmptyCount());
    }

    [Fact]
    public void TestCascadeTrigger()
    {
        var board = new BoardData(8, 8);
        board.GetTile(0, 0).SetCrystal(0);
        board.GetTile(1, 0).SetCrystal(1);
        board.GetTile(2, 0).SetCrystal(1);
        board.GetTile(3, 0).SetCrystal(1);
        board.GetTile(4, 0).SetCrystal(2);
        board.GetTile(5, 0).SetCrystal(0);
        board.GetTile(6, 0).SetCrystal(0);
        board.GetTile(7, 0).SetCrystal(3);

        var result = MatchDetector.DetectAll(board);
        Assert.True(result.HasMatches(), "Should detect initial 3-vertical match of B");

        foreach (var pos in result.GetAllPositions())
            board.GetTile(pos.Y, pos.X).Clear();

        GravitySystem.ApplyGravity(board);
        SpawnSystem.FillEmpty(board);

        Assert.Equal(0, board.GetEmptyCount());
    }

    [Fact]
    public void TestNoCrashOnInvalidSwap()
    {
        var board = new BoardData(8, 8);
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                board.GetTile(row, col).SetCrystal((row + col) % 5);

        var snapshot = board.DuplicateData();
        board.Swap(0, 0, 0, 1);
        var result = MatchDetector.DetectAll(board);

        if (!result.HasMatches())
            board.RestoreFromData(snapshot);

        Assert.Equal(0, board.GetEmptyCount());
    }
}
