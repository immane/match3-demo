class_name HUD
extends CanvasLayer

@onready var score_label: Label = $TopPanel/HBoxContainer/ScoreSection/ScoreLabel
@onready var best_score_label: Label = $TopPanel/HBoxContainer/ScoreSection/BestScoreLabel
@onready var combo_label: Label = $TopPanel/HBoxContainer/ComboSection/ComboLabel
@onready var moves_label: Label = $TopPanel/HBoxContainer/MovesSection/MovesLabel
@onready var pause_button: Button = $PauseButton

var _displayed_score: int = 0


func _ready() -> void:
	add_to_group("hud")

	EventBus.score_changed.connect(_on_score_changed)
	EventBus.combo_updated.connect(_on_combo_updated)
	EventBus.moves_changed.connect(_on_moves_changed)
	pause_button.pressed.connect(_on_pause_pressed)

	score_label.add_theme_font_size_override("font_size", 22)
	best_score_label.add_theme_font_size_override("font_size", 18)
	combo_label.add_theme_font_size_override("font_size", 28)
	moves_label.add_theme_font_size_override("font_size", 22)

	score_label.add_theme_color_override("font_color", Color("#ffd700"))
	best_score_label.add_theme_color_override("font_color", Color("#aaaaaa"))
	combo_label.add_theme_color_override("font_color", Color("#ffaa00"))
	moves_label.add_theme_color_override("font_color", Color.WHITE)

	_update_display()


func _update_display() -> void:
	score_label.text = "SCORE: %d" % _displayed_score
	best_score_label.text = "BEST: %d" % GameData.high_score
	moves_label.text = "MOVES: %d" % GameData.moves_remaining


func _on_score_changed(new_score: int, delta: int) -> void:
	var from=_displayed_score
	_displayed_score = new_score

	var tween=create_tween()
	tween.tween_method(
		func(value: int): score_label.text = "SCORE: %d" % value,
		from, new_score, 0.5,
	)

	best_score_label.text = "BEST: %d" % GameData.high_score

	if delta > 0:
		var pos=Vector2(360, 200)
		EventBus.show_floating_text.emit("+%d" % delta, pos, Color.GOLD)


func _on_combo_updated(combo: int) -> void:
	if combo <= 1:
		combo_label.hide()
		return

	var text: String
	match combo:
		2: text = "Combo x2"
		3: text = "Combo x3"
		4: text = "Amazing x4"
		5: text = "Incredible x5"
		_: text = "INSANE x%d!" % combo

	combo_label.text = text
	combo_label.show()

	combo_label.scale = Vector2(2.0, 2.0)
	var tween=create_tween()
	tween.tween_property(combo_label, "scale", Vector2.ONE, 0.3)\
		.set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)

	var r=1.0
	var g=clamp(1.0 - combo * 0.1, 0.0, 1.0)
	var b=clamp(0.3 - combo * 0.05, 0.0, 1.0)
	combo_label.add_theme_color_override("font_color", Color(r, g, b))


func _on_moves_changed(remaining: int) -> void:
	moves_label.text = "MOVES: %d" % remaining

	if remaining <= 5:
		moves_label.add_theme_color_override("font_color", Color("#ff4444"))
	elif remaining <= 10:
		moves_label.add_theme_color_override("font_color", Color("#ff8800"))
	else:
		moves_label.add_theme_color_override("font_color", Color.WHITE)


func _on_pause_pressed() -> void:
	var sm=get_tree().get_first_node_in_group("state_machine")
	if sm:
		sm.toggle_pause()
