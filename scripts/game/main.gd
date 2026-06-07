class_name Main
extends Node2D

@onready var title_screen = $TitleScreen
@onready var pause_menu = $PauseMenu
@onready var game_over_panel = $GameOverPanel
@onready var board = $Board
@onready var hud = $HUD
@onready var camera: Camera2D = $Camera2D


func _ready() -> void:
	DisplayServer.window_set_title("Match3 Crystal Demo")

	title_screen.game_started.connect(_on_game_started)
	EventBus.game_over.connect(_on_game_over)

	title_screen.show()
	hud.hide()
	pause_menu.hide()
	game_over_panel.hide()
	board.set_interaction_enabled(false)

	if board.has_node("ScreenShake"):
		var ss=board.get_node("ScreenShake") as ScreenShake
		ss.camera = camera


func _on_game_started() -> void:
	title_screen.hide()
	hud.show()
	GameData.reset_level()
	board.reset_board()
	board.set_interaction_enabled(true)


func _on_game_over() -> void:
	board.set_interaction_enabled(false)
	game_over_panel.show()
