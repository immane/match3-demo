extends Node

# ---- Board signals ----
signal board_initialized
signal tile_selected(tile: Node2D, pos: Vector2i)
signal tile_deselected
signal swap_requested(from: Vector2i, to: Vector2i)
signal swap_completed(valid: bool)
signal swap_invalid

# ---- Match signals ----
signal matches_found(matches: Array)
signal tiles_cleared(positions: Array)
signal special_tile_spawned(pos: Vector2i, type: int)
signal cascade_triggered(depth: int)

# ---- Score signals ----
signal score_changed(new_score: int, delta: int)
signal combo_updated(combo: int)
signal moves_changed(remaining: int)

# ---- Game state signals ----
signal game_state_changed(old_state: int, new_state: int)
signal game_paused
signal game_resumed
signal level_complete
signal game_over

# ---- Effect signals ----
signal play_effect(effect_name: String, pos: Vector2)
signal screen_shake(intensity: float, duration: float)

# ---- UI signals ----
signal show_floating_text(text: String, pos: Vector2, color: Color)
