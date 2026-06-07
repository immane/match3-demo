extends Node

# Player data
var high_score: int = 0
var current_score: int = 0
var current_combo: int = 0
var best_combo: int = 0
var moves_remaining: int = 30

# Settings
var music_enabled: bool = true
var sfx_enabled: bool = true
var particle_quality: int = 1

# Platform detection
var is_mobile: bool = false
var is_web: bool = false


func _ready() -> void:
    _detect_platform()


func _detect_platform() -> void:
    var os_name=OS.get_name()
    is_web = os_name == "Web"
    if is_web:
        # Mobile detection via JavaScriptBridge can be added later
        pass


func reset_level() -> void:
    current_score = 0
    current_combo = 0
    moves_remaining = 30

    EventBus.score_changed.emit(current_score, 0)
    EventBus.combo_updated.emit(current_combo)
    EventBus.moves_changed.emit(moves_remaining)


func add_score(points: int) -> void:
    current_score += points
    if current_score > high_score:
        high_score = current_score
    EventBus.score_changed.emit(current_score, points)


func use_move() -> void:
    moves_remaining -= 1
    EventBus.moves_changed.emit(moves_remaining)
    if moves_remaining <= 0:
        EventBus.game_over.emit()


func update_combo(combo: int) -> void:
    current_combo = combo
    if combo > best_combo:
        best_combo = combo
    EventBus.combo_updated.emit(combo)
