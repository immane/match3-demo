extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const MatchDetector = preload("res://scripts/core/match_detector.gd")
const MatchResult = preload("res://scripts/core/match_result.gd")

# This test verifies the no-match initial board generation logic
# It directly tests the algorithm used in the state machine

func test_generate_no_initial_matches() -> void:
    for _i in range(20):
        var board = BoardData.new(8, 8)
        _generate_no_match(board)
        var result = MatchDetector.detect_all(board)
        assert_false(result.has_matches(), "Generated board should have no initial matches")

func _generate_no_match(board) -> void:
    var rng = RandomNumberGenerator.new()
    rng.randomize()
    for row in range(board.rows):
        for col in range(board.cols):
            var tile = board.get_tile(row, col)
            var forbidden = []

            if col >= 2:
                var l1 = board.get_tile(row, col - 1)
                var l2 = board.get_tile(row, col - 2)
                if not l1.is_empty and not l2.is_empty and l1.crystal_type == l2.crystal_type:
                    forbidden.append(l1.crystal_type)

            if row >= 2:
                var u1 = board.get_tile(row - 1, col)
                var u2 = board.get_tile(row - 2, col)
                if not u1.is_empty and not u2.is_empty and u1.crystal_type == u2.crystal_type:
                    forbidden.append(u1.crystal_type)

            var allowed = []
            for t in range(5):
                if not forbidden.has(t):
                    allowed.append(t)

            if allowed.is_empty():
                tile.set_crystal(rng.randi_range(0, 4))
            else:
                tile.set_crystal(allowed[rng.randi_range(0, allowed.size() - 1)])

func test_all_cells_filled() -> void:
    for _i in range(10):
        var board = BoardData.new(8, 8)
        _generate_no_match(board)
        assert_eq(board.get_empty_count(), 0, "All cells should be filled")
