class_name Tile
extends Node2D

signal tile_clicked(tile, board_position: Vector2i)

const CRYSTAL_COLORS = {
	0: Color("#ff4466"),
	1: Color("#4488ff"),
	2: Color("#44dd66"),
	3: Color("#ffcc33"),
	4: Color("#cc44ff"),
}

var tile_data = null
var board_position: Vector2i = Vector2i(-1, -1)
var visual_state: int = 0

@onready var crystal_rect: ColorRect = $CrystalRect
@onready var selection_border: ColorRect = $SelectionBorder
@onready var special_icon: ColorRect = $SpecialIcon
@onready var glow_effect: ColorRect = $GlowEffect
@onready var click_area: Area2D = $ClickArea
@onready var shader_material: ShaderMaterial = crystal_rect.material


func _ready() -> void:
	# PackedScene sub-resources are shared by default, so each tile needs its own
	# material instance or every color update will affect the whole board.
	shader_material = shader_material.duplicate()
	crystal_rect.material = shader_material
	# Tile selection is handled centrally by InputHandler based on grid position.
	# Leaving Area2D pickable here causes input to be consumed before the board
	# handler sees it, which makes swapping feel broken.
	click_area.input_pickable = false


func setup(data) -> void:
	tile_data = data
	if data:
		board_position = Vector2i(data.col, data.row)
	scale = Vector2.ONE
	_update_appearance()
	visual_state = 0
	selection_border.hide()
	special_icon.hide()
	glow_effect.hide()


func _update_appearance() -> void:
	if tile_data == null or tile_data.is_empty:
		hide()
		return

	show()
	var color = CRYSTAL_COLORS.get(tile_data.crystal_type, Color.WHITE)
	shader_material.set_shader_parameter("crystal_color", color)
	shader_material.set_shader_parameter("is_special", tile_data.is_special())
	shader_material.set_shader_parameter("special_type", tile_data.special_type)

	if tile_data.is_special():
		_set_special_icon(tile_data.special_type)


func _set_special_icon(special: int) -> void:
	special_icon.show()
	match special:
		0:
			special_icon.color = Color(1.0, 0.4, 0.2)
		1:
			special_icon.color = Color.WHITE
		2:
			special_icon.color = Color(0.2, 0.8, 1.0)
		_:
			special_icon.hide()


func select() -> void:
	visual_state = 1
	scale = Vector2(1.15, 1.15)
	selection_border.show()
	glow_effect.show()
	shader_material.set_shader_parameter("is_selected", true)


func deselect() -> void:
	visual_state = 0
	scale = Vector2.ONE
	selection_border.hide()
	glow_effect.hide()
	shader_material.set_shader_parameter("is_selected", false)


func _process(_delta: float) -> void:
	if is_instance_valid(shader_material):
		shader_material.set_shader_parameter("time_sec", Time.get_ticks_msec() / 1000.0)
