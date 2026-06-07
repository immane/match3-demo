class_name GravitySystem
extends RefCounted

const FALL_BASE = 0.1
const FALL_PER_ROW = 0.08


static func apply_gravity(board) -> Array:
	var all_falls: Array = []
	for col in range(board.cols):
		var falls = _process_column(board, col)
		all_falls.append_array(falls)
	return all_falls


static func _process_column(board, col: int) -> Array:
	var falls: Array = []
	var write_row = board.rows - 1

	for read_row in range(board.rows - 1, -1, -1):
		var tile = board.get_tile(read_row, col)
		if tile.is_empty:
			continue

		if read_row != write_row:
			var info = FallInfo.new()
			info.from_row = read_row
			info.to_row = write_row
			info.col = col
			info.crystal_type = tile.crystal_type
			info.special_type = tile.special_type
			falls.append(info)

			board.swap(read_row, col, write_row, col)

		write_row -= 1

	for row in range(write_row, -1, -1):
		board.get_tile(row, col).clear()

	return falls


class FallInfo:
	extends RefCounted

	var from_row: int = 0
	var to_row: int = 0
	var col: int = 0
	var crystal_type: int = -1
	var special_type: int = -1

	func get_distance() -> int:
		return to_row - from_row

	func get_duration() -> float:
		return FALL_BASE + float(get_distance()) * FALL_PER_ROW
