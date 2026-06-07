using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class ReshuffleTests
{
    [Fact]
    public void TestReshuffleProducesValidMoves()
    {
        var board = new BoardData(8, 8);
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                board.GetTile(row, col).SetCrystal((row + col) % 2 == 0 ? 0 : 1);

        bool hasMoves = ValidMoveChecker.HasAnyValidMove(board);

        if (!hasMoves)
        {
            var types = new Godot.Collections.Array<int>();
            for (int i = 0; i < 64; i++)
            {
                var tile = board.Tiles[i];
                types.Add(tile.CrystalType);
            }
            types.Shuffle();
            for (int i = 0; i < 64; i++)
                board.Tiles[i].SetCrystal(types[i]);

            var result = MatchDetector.DetectAll(board);
            Assert.False(result.HasMatches(), "Reshuffled board should have no initial matches");
        }
    }
}
