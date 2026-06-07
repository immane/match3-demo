class_name FloatingTextSpawner
extends Control

func _ready() -> void:
	EventBus.show_floating_text.connect(_spawn_floating_text)


func _spawn_floating_text(text: String, world_pos: Vector2, color: Color = Color.WHITE) -> void:
	var label=Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 28)
	label.add_theme_color_override("font_color", color)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.position = world_pos
	add_child(label)

	var tween=create_tween()
	tween.set_parallel(true)
	tween.tween_property(label, "position:y", world_pos.y - 80.0, 0.8)\
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(label, "modulate:a", 0.0, 0.8)
	tween.tween_property(label, "scale", Vector2(1.3, 1.3), 0.3)\
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	await tween.finished
	label.queue_free()
