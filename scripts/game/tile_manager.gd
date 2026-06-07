class_name TileManager
extends Node2D

@export var tile_scene: PackedScene

var _pool: Array = []
var _active_tiles: Dictionary = {}


func _ready() -> void:
	if tile_scene == null:
		tile_scene = preload("res://assets/scenes/tile.tscn")
	_preallocate()


func _preallocate() -> void:
	for i in range(80):
		var tile = _create_pooled_tile()
		_pool.append(tile)


func _create_pooled_tile():
	var tile = tile_scene.instantiate()
	tile.hide()
	tile.process_mode = Node.PROCESS_MODE_DISABLED
	return tile


func acquire(index: int):
	if _active_tiles.has(index):
		return _active_tiles[index]

	var tile
	if not _pool.is_empty():
		tile = _pool.pop_back()
	else:
		tile = _create_pooled_tile()

	tile.show()
	tile.process_mode = Node.PROCESS_MODE_INHERIT
	add_child(tile)
	_active_tiles[index] = tile
	return tile


func release(index: int) -> void:
	if not _active_tiles.has(index):
		return
	var tile = _active_tiles[index]
	_active_tiles.erase(index)
	if tile.has_method("deselect"):
		tile.deselect()
	if "tile_data" in tile:
		tile.tile_data = null
	tile.position = Vector2.ZERO
	tile.scale = Vector2.ONE
	tile.modulate = Color.WHITE
	tile.hide()
	tile.process_mode = Node.PROCESS_MODE_DISABLED
	remove_child(tile)
	_pool.append(tile)


func release_all() -> void:
	for index in _active_tiles.keys():
		release(index)
	_active_tiles.clear()


func get_active_tile(index: int):
	return _active_tiles.get(index)


func refresh_from_data(board_data) -> void:
	var to_release: Array[int] = []
	for index in _active_tiles.keys():
		var td = board_data.tiles[index]
		if td.is_empty:
			to_release.append(index)

	for index in to_release:
		release(index)

	for index in range(board_data.cols * board_data.rows):
		var td = board_data.tiles[index]
		if td.is_empty:
			if _active_tiles.has(index):
				release(index)
			continue

		var tile = get_active_tile(index)
		if tile == null:
			tile = acquire(index)

		tile.setup(td)
		var pos = board_data.row_col(index)
		tile.board_position = pos
		tile.position = GridUtils.grid_to_world(pos.y, pos.x)
