class_name InputHandler
extends Node

signal tile_clicked(tile)
signal empty_space_clicked

var board_data = null
var tile_manager = null


func _input(event: InputEvent) -> void:
	var sm = get_tree().get_first_node_in_group("state_machine") as GameStateMachine
	if sm and not sm.is_input_allowed():
		return

	var click_pos: Vector2 = Vector2.ZERO
	var is_click = false

	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
			click_pos = event.position
			is_click = true

	if event is InputEventScreenTouch:
		if event.pressed:
			click_pos = event.position
			is_click = true

	if is_click:
		var hovered_control: Control = get_viewport().gui_get_hovered_control()
		if hovered_control != null:
			return
		_process_click(click_pos)


func _process_click(screen_pos: Vector2) -> void:
	var grid_pos = GridUtils.world_to_grid(screen_pos)

	if grid_pos.x < 0 or grid_pos.y < 0:
		empty_space_clicked.emit()
		return

	if tile_manager == null or board_data == null:
		return

	var index = GridUtils.to_index(grid_pos.y, grid_pos.x)
	var tile = tile_manager.get_active_tile(index)

	if tile:
		tile_clicked.emit(tile)
	else:
		empty_space_clicked.emit()
