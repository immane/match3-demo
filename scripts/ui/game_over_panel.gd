class_name GameOverPanel
extends Control

@onready var game_over_label: Label = $VBoxContainer/GameOverLabel
@onready var final_score_label: Label = $VBoxContainer/FinalScoreLabel
@onready var best_score_label: Label = $VBoxContainer/BestScoreLabel
@onready var new_record_label: Label = $VBoxContainer/NewRecordLabel
@onready var retry_button: Button = $VBoxContainer/RetryButton


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

	EventBus.game_over.connect(_on_game_over)

	game_over_label.add_theme_font_size_override("font_size", 48)
	game_over_label.add_theme_color_override("font_color", Color("#ff4444"))

	final_score_label.add_theme_font_size_override("font_size", 32)
	final_score_label.add_theme_color_override("font_color", Color.WHITE)

	best_score_label.add_theme_font_size_override("font_size", 24)
	best_score_label.add_theme_color_override("font_color", Color.GOLD)

	new_record_label.add_theme_font_size_override("font_size", 28)
	new_record_label.add_theme_color_override("font_color", Color.GOLD)

	retry_button.pressed.connect(_on_retry_pressed)


func _on_game_over() -> void:
	final_score_label.text = "SCORE: %d" % GameData.current_score
	best_score_label.text = "BEST: %d" % GameData.high_score

	if GameData.current_score >= GameData.high_score and GameData.high_score > 0:
		new_record_label.show()
	else:
		new_record_label.hide()

	mouse_filter = Control.MOUSE_FILTER_STOP
	show()


func _on_retry_pressed() -> void:
	var board = get_tree().get_first_node_in_group("board")
	GameData.reset_level()
	if board:
		board.reset_board()
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	hide()
