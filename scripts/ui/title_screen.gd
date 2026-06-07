class_name TitleScreen
extends Control

signal game_started

@onready var start_button: Button = $VBoxContainer/StartButton
@onready var high_score_label: Label = $VBoxContainer/HighScoreLabel
@onready var title_label: Label = $VBoxContainer/TitleLabel
@onready var subtitle_label: Label = $VBoxContainer/SubtitleLabel


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP

	title_label.add_theme_font_size_override("font_size", 64)
	title_label.add_theme_color_override("font_color", Color("#ffd700"))

	subtitle_label.add_theme_font_size_override("font_size", 24)
	subtitle_label.add_theme_color_override("font_color", Color("#aaaaaa"))

	high_score_label.add_theme_font_size_override("font_size", 20)
	high_score_label.add_theme_color_override("font_color", Color.GOLD)

	start_button.pressed.connect(_on_start_pressed)

	# Update high score display
	high_score_label.text = "BEST: %d" % GameData.high_score

	# Simple title float animation
	var tween = create_tween()
	tween.set_loops()
	tween.tween_property(title_label, "position:y", -5.0, 1.5)\
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT).as_relative()
	tween.tween_property(title_label, "position:y", 5.0, 1.5)\
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT).as_relative()


func _on_start_pressed() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	hide()
	game_started.emit()
