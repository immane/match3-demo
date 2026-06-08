# Task 10: 动画控制器 + 特效系统

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/input_animation.md](../design/input_animation.md) — Tween 动画编排、粒子/震动/浮动文字 |

## 状态
- [x] 已完成

## 依赖
- Task 06 (Tile, TileManager)
- Task 07 (EventBus)
- Task 09 (Board 场景结构, ScreenShake/ParticleController 占位)

## 产出文件
```
scripts/game/animation_controller.gd    # AnimationController
scripts/fx/particle_controller.gd       # ParticleController
scripts/fx/screen_shake.gd              # ScreenShake
scripts/ui/floating_text.gd             # FloatingTextSpawner
```

## 实现要求

### animation_controller.gd

参考 `docs/design/input_animation.md` §2:

```gdscript
class_name AnimationController
extends Node2D

signal swap_finished
signal clear_finished
signal fall_finished
signal spawn_finished

var board_data: BoardData
var tile_manager: TileManager

func play_swap(tile_a: Tile, tile_b: Tile, duration: float)
func play_clear(matched_positions: Array[Vector2i])
func play_falling(falls: Array[FallInfo])
func play_spawn(spawns: Array[SpawnInfo])
func play_reshuffle_animation()
```

关键实现:

**play_swap**: 创建 Tween, 同时移动两个 tile 到对方位置。`tween.finished` 时 emit `swap_finished`

**play_clear**: 
- 对每个匹配的 position, 获取对应 tile
- Tween: scale→ZERO + modulate.a→0, 时长 CLEAR_DURATION
- 半途添加 callback 生成粒子 `particle_ctrl.spawn_match_particles(world_pos, crystal_type)`
- 完成后回收所有 tile: `tile_manager.release(idx)`
- emit `clear_finished`

**play_falling**:
- 对每个 FallInfo, 获取对应 tile
- Tween: position 移动到 target (grid_to_world 计算)
- 时长使用 `fall.get_duration()`
- emit `fall_finished`

**play_spawn**:
- 对每个 SpawnInfo: 调用 `tile_manager.create_tile(data, idx)`
- 设置初始 position 在目标上方 (target.y - CELL_SIZE*2)
- scale=0.3, modulate.a=0
- Tween: position→target, scale→1, modulate.a→1
- 每列有 0.03s 延迟形成波浪效果
- emit `spawn_finished`

### particle_controller.gd

参考 `docs/design/input_animation.md` §3.1:

```gdscript
class_name ParticleController
extends Node2D

@onready var match_particles: GPUParticles2D
@onready var combo_particles: GPUParticles2D
@onready var special_particles: GPUParticles2D

const CRYSTAL_PARTICLE_COLORS = {...}

func spawn_match_particles(world_pos: Vector2, crystal_type: int)
func spawn_combo_particles(world_pos: Vector2, combo_level: int)
func spawn_special_activation(pos: Vector2, special_type: int)
```

实现:
- `spawn_match_particles`: 动态创建 `GPUParticles2D`, 设置 `one_shot=true`, `amount` 根据 particle_quality, 设置粒子颜色, `emitting=true`, 1 秒后 `queue_free`
- 使用 `duplicate()` 复制 process_material 避免影响原始节点
- 移动端: amount = 8, 桌面: amount = 20

### screen_shake.gd

参考 `docs/design/input_animation.md` §3.2:

```gdscript
class_name ScreenShake
extends Node

@export var camera: Camera2D

var _shake_intensity: float
var _shake_duration: float
var _shake_timer: float
var _original_camera_pos: Vector2

func _ready(): EventBus.screen_shake.connect(_on_screen_shake)
func _on_screen_shake(intensity, duration)
func _process(delta):  # 震动衰减, 恢复原位置
```

### floating_text.gd

参考 `docs/design/input_animation.md` §3.3:

```gdscript
class_name FloatingTextSpawner
extends Control

var floating_text_scene: PackedScene

func _ready(): EventBus.show_floating_text.connect(_spawn_floating_text)
func _spawn_floating_text(text, world_pos, color)
    # 创建 Label, Tween: y上移 + alpha淡出 + scale放大
```

## 验收标准
- AnimationController: 四种动画(queue_free已消除tile, 下落, 生成)正确播放
- 动画完成后正确发射 finished 信号
- ParticleController: 粒子颜色/数量根据参数正确
- ScreenShake: 收到 signal 时正确震动, 衰减结束恢复原位
- FloatingText: 文字正确上浮+淡出+销毁
- 所有 await 后检查 is_inside_tree()

## 注意
- AnimationController 的 `play_clear` 需要能访问 `particle_ctrl` 和 `tile_manager`
- 所有 Tween 使用 `create_tween()` (Godot 4 风格, 不是 `Tween.new()`)
- 粒子节点使用 `one_shot=true` + 自动 `queue_free`, 不需要对象池
- ScreenShake 依赖场景中有 Camera2D 节点
