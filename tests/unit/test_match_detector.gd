extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const MatchDetector = preload("res://scripts/core/match_detector.gd")
const MatchResult = preload("res://scripts/core/match_result.gd")

func test_horizontal_3() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    assert_eq(result.total_matched, 3)
    assert_eq(result.groups.size(), 1)

func test_horizontal_4() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(0)
    board.get_tile(0, 3).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    assert_eq(result.total_matched, 4)

func test_horizontal_5() -> void:
    var board = BoardData.new(8, 8)
    for c in range(5):
        board.get_tile(0, c).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    assert_eq(result.total_matched, 5)

func test_vertical_3() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(1)
    board.get_tile(1, 0).set_crystal(1)
    board.get_tile(2, 0).set_crystal(1)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    assert_eq(result.total_matched, 3)

func test_multiple_matches() -> void:
    var board = BoardData.new(8, 8)
    # Horizontal 3 on row 0
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(0)
    # Horizontal 3 on row 2
    board.get_tile(2, 3).set_crystal(1)
    board.get_tile(2, 4).set_crystal(1)
    board.get_tile(2, 5).set_crystal(1)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    assert_eq(result.total_matched, 6)

func test_l_shape() -> void:
    var board = BoardData.new(8, 8)
    # Vertical 3 on col 0
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(1, 0).set_crystal(0)
    board.get_tile(2, 0).set_crystal(0)
    # Horizontal on row 2
    board.get_tile(2, 0).set_crystal(0)
    board.get_tile(2, 1).set_crystal(0)
    board.get_tile(2, 2).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())

func test_no_match() -> void:
    var board = BoardData.new(8, 8)
    # Alternating pattern - no 3 in a row
    for row in range(8):
        for col in range(8):
            board.get_tile(row, col).set_crystal((row + col) % 5)
    var result = MatchDetector.detect_all(board)
    assert_false(result.has_matches())
    assert_eq(result.total_matched, 0)
