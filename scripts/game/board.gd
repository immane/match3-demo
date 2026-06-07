class_name Board
extends Node2D

const Enums = preload("res://scripts/utils/enums.gd")
const LIGHT_COLOR = Color("#3a3a5c")
const DARK_COLOR = Color("#2e2e4a")
const GRID_COLS = 8
const GRID_ROWS = 8
const CELL_SIZE = 72
const OFFSET_X = 40
const OFFSET_Y = 120
const CELL_STEP = 76

@onready var tile_layer: Node2D = $TileLayer
@onready var effect_layer: Node2D = $EffectLayer
@onready var background_layer: Node2D = $BackgroundLayer
@onready var input_handler = $InputHandler
@onready var state_machine = $GameStateMachine

var board_data
var tile_manager
var interaction_enabled: bool = false
var _board_ready: bool = false


func _ready() -> void:
	board_data = BoardData.new(8, 8, 5)
	add_to_group("board")

	tile_manager = TileManager.new()
	tile_layer.add_child(tile_manager)

	input_handler.process_mode = Node.PROCESS_MODE_DISABLED
	state_machine.board_data = board_data
	state_machine.tile_manager = tile_manager
	state_machine.state_changed.connect(_on_state_changed)
	state_machine.add_to_group("state_machine")

	queue_redraw()

	await get_tree().process_frame
	state_machine.initialize()


func _on_state_changed(_prev: int, current: int) -> void:
	if current == Enums.GameState.IDLE:
		_board_ready = true


func reset_board() -> void:
	if get_tree().paused:
		get_tree().paused = false
	_board_ready = false
	tile_manager.release_all()
	state_machine.initialize()


func set_interaction_enabled(enabled: bool) -> void:
	interaction_enabled = enabled
	if not enabled:
		_board_ready = false


func _input(event: InputEvent) -> void:
	if not interaction_enabled:
		return
	if not _board_ready:
		return
	if not is_instance_valid(state_machine) or not state_machine.is_input_allowed():
		return
	if not event is InputEventMouseButton:
		return
	if event.button_index != MOUSE_BUTTON_LEFT or not event.pressed:
		return

	var local_pos = get_local_mouse_position()
	var grid_pos = GridUtils.world_to_grid(local_pos)

	if grid_pos.x < 0 or grid_pos.y < 0:
		state_machine.clear_selection()
		return

	var index = GridUtils.to_index(grid_pos.y, grid_pos.x)
	var tile = tile_manager.get_active_tile(index)
	if tile:
		state_machine.on_tile_clicked(tile)
	else:
		state_machine.clear_selection()


func _draw() -> void:
	for row in range(GRID_ROWS):
		for col in range(GRID_COLS):
			var pos = GridUtils.grid_to_world(row, col) - Vector2(CELL_SIZE / 2.0, CELL_SIZE / 2.0)
			var rect = Rect2(pos, Vector2(CELL_SIZE, CELL_SIZE))
			var bg_color = LIGHT_COLOR if (row + col) % 2 == 0 else DARK_COLOR
			draw_rect(rect, bg_color)
			draw_rect(rect.grow(2.0), Color.WHITE * 0.03, false, 1.0)
