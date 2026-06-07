# board_data.gd - Board data classes for match-3 game

class_name BoardData
extends RefCounted

var cols: int = 8
var rows: int = 8
var tiles: Array = []
var num_crystal_types: int = 5

func _init(p_cols: int = 8, p_rows: int = 8, p_types: int = 5) -> void:
	cols = p_cols
	rows = p_rows
	num_crystal_types = p_types
	tiles.resize(cols * rows)
	for i in range(cols * rows):
		tiles[i] = CellData.new()

func get_tile(p_row: int, p_col: int) -> CellData:
	return tiles[p_row * cols + p_col]

func set_tile(p_row: int, p_col: int, tile: CellData) -> void:
	tile.row = p_row
	tile.col = p_col
	tiles[p_row * cols + p_col] = tile

func get_index(p_row: int, p_col: int) -> int:
	return p_row * cols + p_col

func row_col(p_index: int) -> Vector2i:
	return Vector2i(p_index % cols, p_index / cols)

func is_in_bounds(p_row: int, p_col: int) -> bool:
	return p_row >= 0 and p_row < rows and p_col >= 0 and p_col < cols

func swap(p_row1: int, p_col1: int, p_row2: int, p_col2: int) -> void:
	var idx1=get_index(p_row1, p_col1)
	var idx2=get_index(p_row2, p_col2)
	var temp: CellData = tiles[idx1]
	tiles[idx1] = tiles[idx2]
	tiles[idx2] = temp
	tiles[idx1].row = p_row1
	tiles[idx1].col = p_col1
	tiles[idx2].row = p_row2
	tiles[idx2].col = p_col2

func duplicate_data() -> Array:
	var data: Array = []
	for tile in tiles:
		data.append({"type": tile.crystal_type, "special": tile.special_type, "empty": tile.is_empty})
	return data

func restore_from_data(data: Array) -> void:
	for i in range(tiles.size()):
		tiles[i].crystal_type = data[i]["type"]
		tiles[i].special_type = data[i]["special"]
		tiles[i].is_empty = data[i]["empty"]

func clear() -> void:
	for tile in tiles:
		tile.clear()

func count_type(p_type: int) -> int:
	var count=0
	for tile in tiles:
		if not tile.is_empty and tile.crystal_type == p_type:
			count += 1
	return count

func get_empty_count() -> int:
	var count=0
	for tile in tiles:
		if tile.is_empty:
			count += 1
	return count


class CellData:
	extends RefCounted

	var crystal_type: int = -1
	var special_type: int = -1
	var row: int = -1
	var col: int = -1
	var is_empty: bool = true
	var is_locked: bool = false
	var lock_hp: int = 0

	func _init(p_type: int = -1, p_special: int = -1) -> void:
		crystal_type = p_type
		special_type = p_special
		is_empty = p_type < 0

	func clear() -> void:
		crystal_type = -1
		special_type = -1
		is_empty = true

	func set_crystal(p_type: int, p_special: int = -1) -> void:
		crystal_type = p_type
		special_type = p_special
		is_empty = false

	func is_normal() -> bool:
		return not is_empty and special_type == -1

	func is_special() -> bool:
		return not is_empty and special_type != -1

	func _to_string() -> String:
		if is_empty:
			return "EMPTY"
		var s=str(crystal_type)
		if special_type == 0:
			s += "B"
		elif special_type == 1:
			s += "R"
		elif special_type == 2:
			s += "C"
		return s


class MoveRecord:
	extends RefCounted

	var from: Vector2i
	var to: Vector2i
	var snapshot: Array
	var score_gained: int = 0
