extends GutTest

const BoardData = preload("res://scripts/core/board_data.gd")
const MatchDetector = preload("res://scripts/core/match_detector.gd")
const MatchResult = preload("res://scripts/core/match_result.gd")
const GravitySystem = preload("res://scripts/core/gravity_system.gd")
const SpawnSystem = preload("res://scripts/core/spawn_system.gd")

func test_max_cascade_loop_protection() -> void:
    var board = BoardData.new(8, 8)
    # Generate a board that won't cause infinite cascade
    for row in range(8):
        for col in range(8):
            board.get_tile(row, col).set_crystal((row + col) % 5)

    # Simulate cascade loop
    var cascade_depth = 0
    while cascade_depth < 20:
        var result = MatchDetector.detect_all(board)
        if not result.has_matches():
            break
        cascade_depth += 1
        for pos in result.get_all_positions():
            board.get_tile(pos.y, pos.x).clear()
        GravitySystem.apply_gravity(board)
        SpawnSystem.fill_empty(board)

    assert_lt(cascade_depth, 20, "Cascade loop should terminate before MAX_CASCADE_LOOPS")

func test_empty_board_operations() -> void:
    var board = BoardData.new(8, 8)
    # All cells are empty by default

    # Match detection on empty board
    var result = MatchDetector.detect_all(board)
    assert_false(result.has_matches())
    assert_eq(result.total_matched, 0)
    assert_eq(result.groups.size(), 0)

    # Gravity on empty board
    var falls = GravitySystem.apply_gravity(board)
    assert_eq(falls.size(), 0)

    # Spawn on empty board
    var spawns = SpawnSystem.fill_empty(board)
    assert_eq(spawns.size(), 64)
    assert_eq(board.get_empty_count(), 0)
