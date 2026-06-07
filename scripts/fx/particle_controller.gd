class_name ParticleController
extends Node2D

const CRYSTAL_PARTICLE_COLORS={
	0: Color("#ff4466"),  # RED
	1: Color("#4488ff"),  # BLUE
	2: Color("#44dd66"),  # GREEN
	3: Color("#ffcc33"),  # YELLOW
	4: Color("#cc44ff"),  # PURPLE
}


func spawn_match_particles(world_pos: Vector2, crystal_type: int) -> void:
	var particles=GPUParticles2D.new()
	particles.position = world_pos
	particles.one_shot = true
	particles.explosiveness = 1.0
	particles.lifetime = 0.6

	var quality=GameData.particle_quality
	particles.amount = 8 if quality <= 0 else 20

	var mat=ParticleProcessMaterial.new()
	mat.gravity = Vector2(0, 100)
	mat.initial_velocity_min = 40.0
	mat.initial_velocity_max = 120.0
	mat.angle_min = -180.0
	mat.angle_max = 180.0
	mat.scale_min = 0.5
	mat.scale_max = 1.5
	mat.color = CRYSTAL_PARTICLE_COLORS.get(crystal_type, Color.WHITE)
	particles.process_material = mat

	particles.emitting = true
	add_child(particles)

	get_tree().create_timer(1.5).timeout.connect(particles.queue_free)


func spawn_combo_particles(world_pos: Vector2, combo_level: int) -> void:
	var particles=GPUParticles2D.new()
	particles.position = world_pos
	particles.one_shot = true
	particles.explosiveness = 1.0
	particles.lifetime = 0.8
	particles.amount = 12 + combo_level * 4

	var mat=ParticleProcessMaterial.new()
	mat.gravity = Vector2(0, 50)
	mat.initial_velocity_min = 60.0
	mat.initial_velocity_max = 180.0
	mat.angle_min = -180.0
	mat.angle_max = 180.0
	mat.scale_min = 0.3
	mat.scale_max = 2.0

	# Combo colors get warmer with higher combo
	var r=1.0
	var g=1.0 - combo_level * 0.1
	var b=0.3
	mat.color = Color(r, g, b)
	particles.process_material = mat

	particles.emitting = true
	add_child(particles)

	get_tree().create_timer(1.5).timeout.connect(particles.queue_free)


func spawn_special_activation(pos: Vector2, special_type: int) -> void:
	var particles=GPUParticles2D.new()
	particles.position = pos
	particles.one_shot = true
	particles.explosiveness = 1.0
	particles.lifetime = 1.0
	particles.amount = 30

	var mat=ParticleProcessMaterial.new()
	mat.gravity = Vector2(0, 20)
	mat.initial_velocity_min = 80.0
	mat.initial_velocity_max = 200.0
	mat.angle_min = -180.0
	mat.angle_max = 180.0

	match special_type:
		0: mat.color = Color(1.0, 0.4, 0.2)   # BOMB: orange
		1: mat.color = Color.WHITE               # RAINBOW: white
		2: mat.color = Color(0.2, 0.8, 1.0)     # CROSS: cyan
		_: mat.color = Color.WHITE

	particles.process_material = mat
	particles.emitting = true
	add_child(particles)

	get_tree().create_timer(2.0).timeout.connect(particles.queue_free)
