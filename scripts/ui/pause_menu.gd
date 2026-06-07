class_name PauseMenu
extends Control

@onready var pause_label: Label = $VBoxContainer/PauseLabel
@onready var resume_button: Button = $VBoxContainer/ResumeButton
@onready var restart_button: Button = $VBoxContainer/RestartButton
@onready var quit_button: Button = $VBoxContainer/QuitButton


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_WHEN_PAUSED
	mouse_filter = Control.MOUSE_FILTER_IGNORE

	EventBus.game_paused.connect(_on_game_paused)
	EventBus.game_resumed.connect(_on_game_resumed)

	pause_label.add_theme_font_size_override("font_size", 48)
	pause_label.add_theme_color_override("font_color", Color("#ffd700"))

	resume_button.pressed.connect(_on_resume_pressed)
	restart_button.pressed.connect(_on_restart_pressed)
	quit_button.pressed.connect(_on_quit_pressed)


func _on_game_paused() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	show()


func _on_game_resumed() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	hide()


func _on_resume_pressed() -> void:
	var sm = get_tree().get_first_node_in_group("state_machine")
	if sm:
		sm.toggle_pause()


func _on_restart_pressed() -> void:
	var board = get_tree().get_first_node_in_group("board")
	GameData.reset_level()
	if board:
		board.reset_board()
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	hide()


func _on_quit_pressed() -> void:
	get_tree().reload_current_scene()
