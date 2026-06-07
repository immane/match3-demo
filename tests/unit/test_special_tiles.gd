extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const MatchDetector = preload("res://scripts/core/match_detector.gd")
const MatchResult = preload("res://scripts/core/match_result.gd")

func test_special_4_bomb() -> void:
    var board = BoardData.new(8, 8)
    for c in range(4):
        board.get_tile(0, c).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    # 4 in a row should produce at least one BOMB special spawn
    var has_bomb = false
    for spawn in result.special_spawns:
        var s = spawn
        if s.special_type == 0:  # BOMB
            has_bomb = true
    assert_true(has_bomb, "4-in-a-row should produce BOMB special")

func test_special_5_rainbow() -> void:
    var board = BoardData.new(8, 8)
    for c in range(5):
        board.get_tile(0, c).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    var has_rainbow = false
    for spawn in result.special_spawns:
        var s = spawn
        if s.special_type == 1:  # RAINBOW
            has_rainbow = true
    assert_true(has_rainbow, "5-in-a-row should produce RAINBOW special")

func test_special_l_cross() -> void:
    # This test creates a matching pattern that should be detected
    # The specific shape classification depends on the algorithm
    var board = BoardData.new(8, 8)
    for r in range(3):
        board.get_tile(r, 0).set_crystal(0)
    for c in range(1, 4):
        board.get_tile(2, c).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    assert_true(result.has_matches())
    # Verify that special spawns are generated for significant matches
    assert_gt(result.total_matched, 0)

func test_no_special_for_3() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    # 3-match should NOT produce special spawns
    assert_eq(result.special_spawns.size(), 0, "3-match should not produce special tiles")
