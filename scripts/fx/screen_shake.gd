class_name ScreenShake
extends Node

@export var camera: Camera2D

var _shake_intensity: float = 0.0
var _shake_duration: float = 0.0
var _shake_timer: float = 0.0
var _original_camera_pos: Vector2 = Vector2.ZERO


func _ready() -> void:
	EventBus.screen_shake.connect(_on_screen_shake)
	if camera:
		_original_camera_pos = camera.position


func _on_screen_shake(intensity: float, duration: float) -> void:
	_shake_intensity = intensity
	_shake_duration = duration
	_shake_timer = duration
	if camera and _shake_timer == duration:
		_original_camera_pos = camera.position


func _process(delta: float) -> void:
	if _shake_timer <= 0.0 or camera == null:
		return

	_shake_timer -= delta

	if _shake_timer <= 0.0:
		camera.position = _original_camera_pos
		return

	var progress=1.0 - (_shake_timer / _shake_duration)
	var current_intensity=_shake_intensity * (1.0 - progress)

	var offset=Vector2(
		randf_range(-current_intensity, current_intensity),
		randf_range(-current_intensity, current_intensity),
	)
	camera.position = _original_camera_pos + offset
