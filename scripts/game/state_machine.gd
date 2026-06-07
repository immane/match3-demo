class_name GameStateMachine
extends Node

const Enums = preload("res://scripts/utils/enums.gd")

signal state_changed(previous: int, current: int)

var board_data
var tile_manager

# State data
var selected_tile = null
var swap_from: Vector2i = Vector2i(-1, -1)
var swap_to: Vector2i = Vector2i(-1, -1)
var cascade_depth: int = 0
var moves_used: int = 0
var state_before_pause: int = -1

var current_state: int = 0:
	set(value):
		if value == current_state:
			return
		var prev = current_state
		_exit_state(prev)
		current_state = value
		_enter_state(value)
		state_changed.emit(prev, value)
		EventBus.game_state_changed.emit(prev, value)


func initialize() -> void:
	if board_data == null:
		board_data = BoardData.new(8, 8, 5)

	selected_tile = null
	swap_from = Vector2i(-1, -1)
	swap_to = Vector2i(-1, -1)
	cascade_depth = 0
	moves_used = 0
	state_before_pause = -1
	current_state = -1

	_initial_board_generation()


func _initial_board_generation() -> void:
	for index in range(board_data.cols * board_data.rows):
		var pos = board_data.row_col(index)
		var tile = board_data.tiles[index]
		tile.row = pos.y
		tile.col = pos.x
		tile.clear()

	_generate_no_match_board()

	await get_tree().process_frame

	tile_manager.refresh_from_data(board_data)

	if not ValidMoveChecker.has_any_valid_move(board_data):
		_reshuffle()
		return

	current_state = Enums.GameState.IDLE
	EventBus.board_initialized.emit()


func restart_game() -> void:
	if get_tree().paused:
		get_tree().paused = false

	current_state = Enums.GameState.RESETTING
	initialize()


func _generate_no_match_board() -> void:
	var rng = RandomNumberGenerator.new()
	rng.randomize()

	for row in range(board_data.rows):
		for col in range(board_data.cols):
			var tile = board_data.get_tile(row, col)
			var forbidden: Array[int] = []

			if col >= 2:
				var left1 = board_data.get_tile(row, col - 1)
				var left2 = board_data.get_tile(row, col - 2)
				if not left1.is_empty and not left2.is_empty and left1.crystal_type == left2.crystal_type:
					forbidden.append(left1.crystal_type)

			if row >= 2:
				var up1 = board_data.get_tile(row - 1, col)
				var up2 = board_data.get_tile(row - 2, col)
				if not up1.is_empty and not up2.is_empty and up1.crystal_type == up2.crystal_type:
					forbidden.append(up1.crystal_type)

			var allowed: Array[int] = []
			for t in range(5):
				if not forbidden.has(t):
					allowed.append(t)

			if allowed.is_empty():
				tile.set_crystal(rng.randi_range(0, 4))
			else:
				tile.set_crystal(allowed[rng.randi_range(0, allowed.size() - 1)])


func _reshuffle() -> void:
	current_state = Enums.GameState.RESHUFFLING

	var types: Array[int] = []
	for i in range(board_data.cols * board_data.rows):
		var tile = board_data.tiles[i]
		if not tile.is_empty:
			types.append(tile.crystal_type)

	types.shuffle()
	var idx = 0
	for i in range(board_data.cols * board_data.rows):
		var tile = board_data.tiles[i]
		if not tile.is_empty and idx < types.size():
			tile.set_crystal(types[idx])
			idx += 1

	var result = MatchDetector.detect_all(board_data)
	if result.has_matches():
		_generate_no_match_board()

	tile_manager.refresh_from_data(board_data)

	if not ValidMoveChecker.has_any_valid_move(board_data):
		_generate_no_match_board()
		tile_manager.refresh_from_data(board_data)

	current_state = Enums.GameState.IDLE


func is_input_allowed() -> bool:
	return current_state == Enums.GameState.IDLE or current_state == Enums.GameState.SELECTED


func on_tile_clicked(tile) -> void:
	if not is_input_allowed():
		return

	match current_state:
		Enums.GameState.IDLE:
			clear_selection()
			selected_tile = tile
			tile.select()
			EventBus.tile_selected.emit(tile, tile.board_position)
			current_state = Enums.GameState.SELECTED

		Enums.GameState.SELECTED:
			if tile == selected_tile:
				selected_tile.deselect()
				selected_tile = null
				EventBus.tile_deselected.emit()
				current_state = Enums.GameState.IDLE
				return

			var pos_a = selected_tile.board_position
			var pos_b = tile.board_position
			var dx = abs(pos_a.x - pos_b.x)
			var dy = abs(pos_a.y - pos_b.y)

			if (dx + dy) == 1:
				swap_from = pos_a
				swap_to = pos_b
				selected_tile.deselect()
				selected_tile = null
				EventBus.swap_requested.emit(swap_from, swap_to)
				current_state = Enums.GameState.SWAPPING
				await _execute_swap()
			else:
				selected_tile.deselect()
				selected_tile = tile
				tile.select()
				EventBus.tile_selected.emit(tile, tile.board_position)


func clear_selection() -> void:
	if selected_tile == null:
		return
	selected_tile.deselect()
	selected_tile = null
	EventBus.tile_deselected.emit()
	if current_state == Enums.GameState.SELECTED:
		current_state = Enums.GameState.IDLE


func _execute_swap() -> void:
	board_data.swap(swap_from.y, swap_from.x, swap_to.y, swap_to.x)
	tile_manager.refresh_from_data(board_data)

	await get_tree().create_timer(0.2).timeout
	if not is_inside_tree():
		return

	var result = MatchDetector.detect_all(board_data)

	if result.has_matches():
		GameData.use_move()
		EventBus.swap_completed.emit(true)
		current_state = Enums.GameState.CHECKING_MATCHES
		cascade_depth = 0
		await _run_cascade_loop()
	else:
		EventBus.swap_completed.emit(false)
		current_state = Enums.GameState.SWAP_BACK
		await _execute_swap_back()


func _execute_swap_back() -> void:
	board_data.swap(swap_from.y, swap_from.x, swap_to.y, swap_to.x)
	tile_manager.refresh_from_data(board_data)

	await get_tree().create_timer(0.15).timeout
	if not is_inside_tree():
		return

	EventBus.swap_invalid.emit()
	current_state = Enums.GameState.IDLE


func _run_cascade_loop() -> void:
	while cascade_depth < 20:
		if not is_inside_tree():
			return

		var result = MatchDetector.detect_all(board_data)
		if not result.has_matches():
			current_state = Enums.GameState.CHECK_VALID
			await _do_check_valid()
			return

		cascade_depth += 1
		var score = ScoreCalculator.calculate_total(result)
		score = ScoreCalculator.apply_combo(score, cascade_depth)
		GameData.add_score(score)
		GameData.update_combo(cascade_depth)

		current_state = Enums.GameState.CLEARING
		_process_match_result(result)
		EventBus.matches_found.emit(result.groups)
		await get_tree().create_timer(0.2).timeout
		if not is_inside_tree():
			return

		for g in result.groups:
			for pos in g.positions:
				board_data.get_tile(pos.y, pos.x).clear()

		tile_manager.refresh_from_data(board_data)
		await get_tree().process_frame

		EventBus.tiles_cleared.emit(result.get_all_positions())

		current_state = Enums.GameState.FALLING
		GravitySystem.apply_gravity(board_data)
		tile_manager.refresh_from_data(board_data)
		await get_tree().process_frame
		await get_tree().create_timer(0.3).timeout
		if not is_inside_tree():
			return

		current_state = Enums.GameState.SPAWNING
		SpawnSystem.fill_empty(board_data)
		tile_manager.refresh_from_data(board_data)
		await get_tree().process_frame
		await get_tree().create_timer(0.25).timeout
		if not is_inside_tree():
			return

		current_state = Enums.GameState.CASCADE_CHECK
		EventBus.cascade_triggered.emit(cascade_depth)

	current_state = Enums.GameState.CHECK_VALID
	await _do_check_valid()


func _process_match_result(result) -> void:
	for spawn in result.special_spawns:
		var tile = board_data.get_tile(spawn.position.y, spawn.position.x)
		if not tile.is_empty:
			tile.special_type = spawn.special_type
			EventBus.special_tile_spawned.emit(spawn.position, spawn.special_type)


func _do_check_valid() -> void:
	if not is_inside_tree():
		return

	if ValidMoveChecker.has_any_valid_move(board_data):
		current_state = Enums.GameState.IDLE
		return

	_reshuffle()


func toggle_pause() -> void:
	if current_state == Enums.GameState.PAUSED:
		_resume()
	else:
		_pause()


func _pause() -> void:
	state_before_pause = current_state
	current_state = Enums.GameState.PAUSED
	get_tree().paused = true
	EventBus.game_paused.emit()


func _resume() -> void:
	get_tree().paused = false
	current_state = state_before_pause
	state_before_pause = -1
	EventBus.game_resumed.emit()


func _enter_state(new_state: int) -> void:
	pass


func _exit_state(from_state: int) -> void:
	pass
