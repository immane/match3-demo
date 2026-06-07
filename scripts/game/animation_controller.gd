class_name AnimationController
extends Node2D

const GravitySystem = preload("res://scripts/core/gravity_system.gd")
const SpawnSystem = preload("res://scripts/core/spawn_system.gd")

signal swap_finished
signal clear_finished
signal fall_finished
signal spawn_finished

var board_data
var tile_manager


func play_swap(tile_a, tile_b, duration: float = 0.2) -> void:
	var pos_a=tile_a.position
	var pos_b=tile_b.position

	var tween=create_tween()
	tween.set_parallel(true)
	tween.tween_property(tile_a, "position", pos_b, duration)\
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(tile_b, "position", pos_a, duration)\
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN_OUT)
	await tween.finished
	swap_finished.emit()


func play_clear(positions: Array[Vector2i]) -> void:
	var tween=create_tween()
	tween.set_parallel(true)

	for pos in positions:
		var index=GridUtils.to_index(pos.y, pos.x)
		var tile=tile_manager.get_active_tile(index)
		if tile == null:
			continue

		tween.tween_property(tile, "scale", Vector2.ZERO, 0.2)\
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
		tween.tween_property(tile, "modulate:a", 0.0, 0.2)

		# Spawn particles
		_spawn_clear_particles(tile.global_position, tile.tile_data)

	await tween.finished
	if not is_inside_tree():
		return

	# Release all cleared tiles
	for pos in positions:
		var index=GridUtils.to_index(pos.y, pos.x)
		tile_manager.release(index)

	# Reset tile visual state for reuse
	for pos in positions:
		var index=GridUtils.to_index(pos.y, pos.x)
		# Tiles are already released, restore visual defaults are handled in acquire

	clear_finished.emit()


func _spawn_clear_particles(world_pos: Vector2, tile_data ) -> void:
	var p=get_parent()
	if not p or not p.has_node("ParticleController"):
		return

	var pc=p.get_node("ParticleController") as ParticleController
	if pc and tile_data:
		pc.spawn_match_particles(world_pos, tile_data.crystal_type)


func play_falling(falls: Array) -> void:
	var tween=create_tween()
	tween.set_parallel(true)

	for fall in falls:
		var fall_info=fall as GravitySystem.FallInfo
		var index=GridUtils.to_index(fall_info.to_row, fall_info.col)
		var tile=tile_manager.get_active_tile(index)
		if tile == null:
			continue

		var target_pos=GridUtils.grid_to_world(fall_info.to_row, fall_info.col)
		var duration=fall_info.get_duration()

		tween.tween_property(tile, "position", target_pos, duration)\
			.set_trans(Tween.TRANS_BOUNCE).set_ease(Tween.EASE_OUT)

	await tween.finished
	fall_finished.emit()


func play_spawn(spawns: Array) -> void:
	var tween=create_tween()
	tween.set_parallel(true)

	var col_delays: Dictionary = {}

	for spawn in spawns:
		var spawn_info=spawn as SpawnSystem.SpawnInfo
		var index=GridUtils.to_index(spawn_info.row, spawn_info.col)

		var tile=tile_manager.acquire(index)
		var tile_data=board_data.get_tile(spawn_info.row, spawn_info.col)
		tile.setup(tile_data)

		var target_pos=GridUtils.grid_to_world(spawn_info.row, spawn_info.col)
		var start_pos=target_pos + Vector2(0, -150.0)

		tile.position = start_pos
		tile.scale = Vector2(0.3, 0.3)
		tile.modulate.a = 0.0

		# Per-column wave delay
		var delay: float = col_delays.get(spawn_info.col, 0.0)
		col_delays[spawn_info.col] = delay + 0.03

		tween.tween_property(tile, "position", target_pos, 0.25)\
			.set_delay(delay)\
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		tween.tween_property(tile, "scale", Vector2.ONE, 0.25)\
			.set_delay(delay)\
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		tween.tween_property(tile, "modulate:a", 1.0, 0.18)\
			.set_delay(delay)

	await tween.finished
	spawn_finished.emit()
