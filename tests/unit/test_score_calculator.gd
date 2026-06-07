extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const MatchDetector = preload("res://scripts/core/match_detector.gd")
const MatchResult = preload("res://scripts/core/match_result.gd")
const ScoreCalculator = preload("res://scripts/core/score_calculator.gd")

func test_score_3() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    var total = ScoreCalculator.calculate_total(result)
    assert_eq(total, 30)

func test_score_4() -> void:
    var board = BoardData.new(8, 8)
    for c in range(4):
        board.get_tile(0, c).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    var total = ScoreCalculator.calculate_total(result)
    assert_eq(total, 60)

func test_score_l_shape() -> void:
    # L shape: 3 vert + 3 horiz sharing corner
    var board = BoardData.new(8, 8)
    for r in range(3):
        board.get_tile(r, 0).set_crystal(0)
    board.get_tile(2, 1).set_crystal(0)
    board.get_tile(2, 2).set_crystal(0)
    var result = MatchDetector.detect_all(board)
    var total = ScoreCalculator.calculate_total(result)
    # L shape gives 180
    assert_eq(total, 180)

func test_combo_3x() -> void:
    var score = ScoreCalculator.apply_combo(100, 3)
    assert_eq(score, 300)

func test_combo_1x() -> void:
    var score = ScoreCalculator.apply_combo(100, 1)
    assert_eq(score, 100)
