extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const ValidMoveChecker = preload("res://scripts/core/valid_move_checker.gd")

func test_has_valid_move() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(1)
    board.get_tile(0, 3).set_crystal(0)
    assert_true(ValidMoveChecker.has_any_valid_move(board))

func test_has_valid_move_returns_bool() -> void:
    var board = BoardData.new(8, 8)
    var pattern = [0, 1, 2, 4, 3, 1, 2, 3]
    for row in range(8):
        for col in range(8):
            board.get_tile(row, col).set_crystal(pattern[(row + col) % pattern.size()])
    var result = ValidMoveChecker.has_any_valid_move(board)
    assert_true(result is bool)
