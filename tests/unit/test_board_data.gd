extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")

func test_index_conversion() -> void:
    var board = BoardData.new(8, 8)
    assert_eq(board.get_index(0, 0), 0)
    assert_eq(board.get_index(7, 7), 63)
    assert_eq(board.get_index(3, 5), 29)
    var rc = board.row_col(29)
    assert_eq(rc, Vector2i(5, 3))  # (col=5, row=3)

func test_swap() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)  # RED
    board.get_tile(0, 1).set_crystal(1)  # BLUE
    board.swap(0, 0, 0, 1)
    assert_eq(board.get_tile(0, 0).crystal_type, 1)
    assert_eq(board.get_tile(0, 1).crystal_type, 0)
    assert_eq(board.get_tile(0, 0).row, 0)
    assert_eq(board.get_tile(0, 0).col, 0)

func test_duplicate_restore() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.get_tile(1, 1).set_crystal(1)
    var data = board.duplicate_data()
    board.get_tile(0, 0).clear()
    board.restore_from_data(data)
    assert_eq(board.get_tile(0, 0).crystal_type, 0)
    assert_eq(board.get_tile(1, 1).crystal_type, 1)

func test_is_in_bounds() -> void:
    var board = BoardData.new(8, 8)
    assert_true(board.is_in_bounds(0, 0))
    assert_true(board.is_in_bounds(7, 7))
    assert_false(board.is_in_bounds(-1, 0))
    assert_false(board.is_in_bounds(0, -1))
    assert_false(board.is_in_bounds(8, 0))
    assert_false(board.is_in_bounds(0, 8))

func test_count_type() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)  # RED
    board.get_tile(0, 1).set_crystal(0)
    board.get_tile(0, 2).set_crystal(0)
    board.get_tile(0, 3).set_crystal(0)
    board.get_tile(0, 4).set_crystal(0)
    board.get_tile(1, 0).set_crystal(1)  # BLUE
    board.get_tile(1, 1).set_crystal(1)
    board.get_tile(1, 2).set_crystal(1)
    assert_eq(board.count_type(0), 5)
    assert_eq(board.count_type(1), 3)
    assert_eq(board.count_type(2), 0)

func test_clear_board() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)
    board.clear()
    assert_eq(board.get_empty_count(), 64)
