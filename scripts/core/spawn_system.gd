class_name SpawnSystem
extends RefCounted


static func fill_empty(board) -> Array:
	var spawns: Array = []
	var rng=RandomNumberGenerator.new()
	rng.randomize()

	for col in range(board.cols):
		for row in range(board.rows):
			var tile=board.get_tile(row, col)
			if not tile.is_empty:
				continue

			var crystal_type=rng.randi_range(0, 4)

			var info=SpawnInfo.new()
			info.row = row
			info.col = col
			info.crystal_type = crystal_type
			spawns.append(info)

			tile.set_crystal(crystal_type)

	return spawns


class SpawnInfo:
	extends RefCounted

	var row: int = 0
	var col: int = 0
	var crystal_type: int = -1

	func get_enter_offset() -> Vector2:
		return Vector2(0, float(-(row + 1)) * 76.0)
