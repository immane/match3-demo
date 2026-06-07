extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const GravitySystem = preload("res://scripts/core/gravity_system.gd")

func test_single_fall() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)  # RED at top
    var falls = GravitySystem.apply_gravity(board)
    assert_eq(falls.size(), 1)
    var f = falls[0]
    assert_eq(f.from_row, 0)
    assert_eq(f.to_row, 7)
    assert_eq(f.get_distance(), 7)
    assert_eq(board.get_tile(7, 0).crystal_type, 0)

func test_multiple_falls() -> void:
    var board = BoardData.new(8, 8)
    board.get_tile(0, 0).set_crystal(0)  # RED
    # Empty at row 1
    board.get_tile(2, 0).set_crystal(1)  # BLUE
    board.get_tile(3, 0).set_crystal(2)  # GREEN
    board.get_tile(4, 0).clear()  # Empty
    board.get_tile(5, 0).set_crystal(3)  # YELLOW
    var falls = GravitySystem.apply_gravity(board)
    # All non-empty tiles should fall to bottom
    assert_eq(board.get_tile(7, 0).crystal_type, 3)  # YELLOW at bottom
    assert_eq(board.get_tile(6, 0).crystal_type, 2)  # GREEN
    assert_eq(board.get_tile(5, 0).crystal_type, 1)  # BLUE
    assert_eq(board.get_tile(4, 0).crystal_type, 0)  # RED
    assert_true(board.get_tile(0, 0).is_empty)

func test_no_falls() -> void:
    var board = BoardData.new(8, 8)
    for r in range(8):
        board.get_tile(r, 0).set_crystal(r % 5)
    var falls = GravitySystem.apply_gravity(board)
    assert_eq(falls.size(), 0)

func test_empty_column() -> void:
    var board = BoardData.new(8, 8)
    var falls = GravitySystem.apply_gravity(board)
    assert_eq(falls.size(), 0)
    for r in range(8):
        assert_true(board.get_tile(r, 0).is_empty)
