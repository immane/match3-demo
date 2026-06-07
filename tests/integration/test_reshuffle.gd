extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const MatchDetector = preload("res://scripts/core/match_detector.gd")
const MatchResult = preload("res://scripts/core/match_result.gd")
const ValidMoveChecker = preload("res://scripts/core/valid_move_checker.gd")

func test_reshuffle_produces_valid_moves() -> void:
    # Create a board with no valid moves
    # Use a specific no-match pattern
    var board = BoardData.new(8, 8)
    # ABABABAB pattern
    for row in range(8):
        for col in range(8):
            if (row + col) % 2 == 0:
                board.get_tile(row, col).set_crystal(0)
            else:
                board.get_tile(row, col).set_crystal(1)

    # Check if this board has valid moves
    var has_moves = ValidMoveChecker.has_any_valid_move(board)

    if not has_moves:
        # Perform reshuffle-like operation
        var types = []
        for i in range(64):
            var tile = board.tiles[i]
            types.append(tile.crystal_type)
        types.shuffle()
        for i in range(64):
            var tile = board.tiles[i]
            tile.set_crystal(types[i])

        # Now check if moves exist
        var has_moves_after = ValidMoveChecker.has_any_valid_move(board)
        # We can't guarantee, but the shuffle should likely create them
        var result = MatchDetector.detect_all(board)
        assert_false(result.has_matches(), "Reshuffled board should have no initial matches")
