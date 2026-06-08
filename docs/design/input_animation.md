# 输入与动画系统设计

> 统一鼠标+触屏输入处理, 以及 Tween 驱动的动画编排。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/02_board_design.md](../research/02_board_design.md) — 坐标转换、边界处理 |
| ← 研究 | [research/10_godot4_syntax.md](../research/10_godot4_syntax.md) — Tween API 和输入处理参考 |
| ← 研究 | [research/06_state_machine.md](../research/06_state_machine.md) — Tween+await 动画驱动 |
| ↔ 同级 | [state_machine.md](state_machine.md) — 动画驱动状态转换 |
| ↔ 同级 | [gravity_cascade.md](gravity_cascade.md) — 下落/spawn 动画参数 |
| → 任务 | [Task 09](../task/09_board_and_input.md) — InputHandler 实现 |
| → 任务 | [Task 10](../task/10_animation_effects.md) — AnimationController + 特效实现 |

---

---

## 目录

1. [输入处理](#1-输入处理)
2. [动画控制器](#2-动画控制器)
3. [特效系统](#3-特效系统)

---

## 1. 输入处理

### 1.1 统一输入模型

鼠标和触屏使用相同的处理流程: "点击/触摸 → 选中方块 → 点击/触摸第二个 → 触发交换"。

```gdscript
# scripts/game/input_handler.gd

class_name InputHandler
extends Node2D

signal tile_clicked(tile: Tile)
signal tile_tap(tile: Tile)
signal empty_space_clicked


func _input(event: InputEvent) -> void:
    # 仅在允许输入的状态处理
    if not _get_state_machine().is_input_allowed():
        return
    
    if event is InputEventMouseButton and event.pressed:
        if event.button_index == MOUSE_BUTTON_LEFT:
            _process_click(event.position)
    
    elif event is InputEventScreenTouch and event.pressed:
        _process_click(event.position)


func _process_click(screen_pos: Vector2) -> void:
    var grid_pos := GridUtils.world_to_grid(screen_pos)
    
    if grid_pos.x < 0:
        empty_space_clicked.emit()
        return
    
    # 查找该位置的 tile
    var idx := board_data.index(grid_pos.x, grid_pos.y)
    var tile := tile_manager.get_active_tile(idx)
    
    if tile:
        tile_clicked.emit(tile)
```

### 1.2 点击状态处理

```gdscript
# 在 GameStateMachine 或 Board 中
func _on_tile_clicked(tile: Tile) -> void:
    match current_state:
        GameState.IDLE:
            # 选择第一个方块
            selected_tile = tile
            tile.select()
            EventBus.tile_selected.emit(tile, tile.board_position)
            current_state = GameState.SELECTED
        
        GameState.SELECTED:
            if tile == selected_tile:
                # 取消选择
                selected_tile.deselect()
                selected_tile = null
                EventBus.tile_deselected.emit()
                current_state = GameState.IDLE
            elif _is_adjacent(selected_tile.board_position, tile.board_position):
                # 相邻方块 → 触发交换
                selected_tile.deselect()
                swap_from = selected_tile.board_position
                swap_to = tile.board_position
                selected_tile = null
                EventBus.swap_requested.emit(swap_from, swap_to)
                current_state = GameState.SWAPPING
            else:
                # 不相邻 → 切换选择
                selected_tile.deselect()
                selected_tile = tile
                tile.select()
                EventBus.tile_selected.emit(tile, tile.board_position)
```

### 1.3 相邻判定

```gdscript
func _is_adjacent(pos1: Vector2i, pos2: Vector2i) -> bool:
    return abs(pos1.x - pos2.x) + abs(pos1.y - pos2.y) == 1
    # 曼哈顿距离 = 1  → 上下左右相邻
    # 曼哈顿距离 = 2  → 对角线 (不允许)
```

### 1.4 HTML5 触摸优化

在导出 HTML shell 中添加 meta 标签消除 300ms 触摸延迟:

```html
<meta name="viewport" content="width=device-width, initial-scale=1.0,
      maximum-scale=1.0, user-scalable=no, viewport-fit=cover">
```

---

## 2. 动画控制器

### 2.1 AnimationController 集成到 Board 脚本

```gdscript
# scripts/game/board.gd 

class_name Board
extends Node2D

# ---- 引用 ----
@onready var tile_layer: Node2D = $TileLayer
@onready var effect_layer: Node2D = $EffectLayer

var board_data: BoardData
var tile_manager: TileManager
var state_machine: GameStateMachine
var particle_ctrl: ParticleController


func _ready() -> void:
    _init_systems()
    _generate_board()
    state_machine.current_state = GameState.IDLE


func _init_systems() -> void:
    board_data = BoardData.new()
    tile_manager = TileManager.new()
    tile_manager.tile_scene = preload("res://assets/scenes/tile.tscn")
    tile_layer.add_child(tile_manager)
    state_machine = GameStateMachine.new()
    state_machine.board_data = board_data
    state_machine.tile_manager = tile_manager
    add_child(state_machine)
```

### 2.2 消除动画 (含粒子特效)

```gdscript
# 消除动画
func play_clear_animation(matched_positions: Array[Vector2i]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    
    var to_release: Array[int] = []
    
    for pos in matched_positions:
        var idx := board_data.index(pos.x, pos.y)
        var tile := tile_manager.get_active_tile(idx)
        if tile == null:
            continue
        
        # 播放消除动画
        tween.tween_property(tile, "scale", Vector2.ZERO, CLEAR_DURATION)\
             .set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_BACK)
        tween.tween_property(tile, "modulate:a", 0.0, CLEAR_DURATION)
        
        # 生成消除粒子
        var world_pos := GridUtils.grid_to_world(pos.x, pos.y)
        tween.tween_callback(
            func(): particle_ctrl.spawn_match_particles(world_pos, 
                    board_data.get_tile(pos.x, pos.y).crystal_type)
        )
        
        to_release.append(idx)
    
    await tween.finished
    if not is_inside_tree():
        return
    
    # 回收所有已消除的 tile
    for idx in to_release:
        tile_manager.release(idx)
```

### 2.3 下落动画

```gdscript
# 下落动画
func play_fall_animation(falls: Array[FallInfo]) -> void:
    if falls.is_empty():
        return
    
    var tween := create_tween()
    tween.set_parallel(true)
    
    for fall in falls:
        var idx := board_data.index(fall.to_row, fall.col)
        var tile := tile_manager.get_active_tile(idx)
        if tile == null:
            continue
        
        var target_pos := GridUtils.grid_to_world(fall.to_row, fall.col)
        var duration := fall.get_duration()
        
        tween.tween_property(tile, "position", target_pos, duration)\
             .set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_QUAD)
        
        # 更新 tile 的 board_position
        tile.board_position = Vector2i(fall.to_row, fall.col)
    
    await tween.finished
```

### 2.4 新方块入场动画

```gdscript
# 新方块入场动画
func play_spawn_animation(spawns: Array[SpawnInfo]) -> void:
    if spawns.is_empty():
        return
    
    var tween := create_tween()
    tween.set_parallel(true)
    
    for spawn in spawns:
        var data := board_data.get_tile(spawn.row, spawn.col)
        var idx := board_data.index(spawn.row, spawn.col)
        
        # 创建 tile 节点
        var tile := tile_manager.create_tile(data, idx)
        var target_pos := GridUtils.grid_to_world(spawn.row, spawn.col)
        
        # 初始位置 (屏幕上方外)
        tile.position = target_pos + Vector2(0, -CELL_SIZE * 2)
        tile.modulate.a = 0.0
        tile.scale = Vector2(0.3, 0.3)
        
        tween.tween_property(tile, "position", target_pos, SPAWN_DURATION)\
             .set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK)\
             .set_delay(spawn.col * 0.03)  # 列延迟, 形成波浪效果
        tween.tween_property(tile, "modulate:a", 1.0, SPAWN_DURATION * 0.7)
        tween.tween_property(tile, "scale", Vector2.ONE, SPAWN_DURATION)
    
    await tween.finished
```

---

## 3. 特效系统

### 3.1 粒子控制器

```gdscript
# scripts/fx/particle_controller.gd

class_name ParticleController
extends Node2D

## 消除时的粒子 (水晶碎片)
@onready var match_particles: GPUParticles2D = $MatchParticles

## 连击时的粒子
@onready var combo_particles: GPUParticles2D = $ComboParticles

## 特殊水晶激活粒子
@onready var special_particles: GPUParticles2D = $SpecialParticles

## 颜色到粒子颜色的映射
const CRYSTAL_PARTICLE_COLORS = {
    CrystalType.RED:    Color("#ff6688"),
    CrystalType.BLUE:   Color("#66aaff"),
    CrystalType.GREEN:  Color("#66ee88"),
    CrystalType.YELLOW: Color("#ffdd55"),
    CrystalType.PURPLE: Color("#dd66ff"),
}


func spawn_match_particles(world_pos: Vector2, crystal_type: int) -> void:
    # 克隆粒子节点到指定位置 (简单方案)
    # 或使用对象池管理
    var particles := GPUParticles2D.new()
    particles.process_material = match_particles.process_material.duplicate()
    particles.texture = match_particles.texture
    particles.position = world_pos
    particles.one_shot = true
    particles.amount = 20 if GameData.particle_quality >= 1 else 8
    particles.lifetime = 0.5
    particles.explosiveness = 1.0
    
    # 设置粒子颜色
    var color := CRYSTAL_PARTICLE_COLORS.get(crystal_type, Color.WHITE)
    var mat := particles.process_material as ParticleProcessMaterial
    mat.color = color
    
    add_child(particles)
    particles.emitting = true
    
    # 自动清理
    await get_tree().create_timer(1.0).timeout
    if is_instance_valid(particles):
        particles.queue_free()


func spawn_combo_particles(world_pos: Vector2, combo_level: int) -> void:
    # 连击文字弹出 (通过 EventBus → FloatingText)
    EventBus.show_floating_text.emit(
        "x%d Combo!" % combo_level,
        world_pos + Vector2(0, -40),
        Color.GOLD
    )


func spawn_special_activation(pos: Vector2, special_type: int) -> void:
    var particles := GPUParticles2D.new()
    particles.texture = special_particles.texture
    particles.position = pos
    particles.one_shot = true
    particles.amount = 40
    particles.lifetime = 0.8
    particles.explosiveness = 1.0
    
    add_child(particles)
    particles.emitting = true
    
    await get_tree().create_timer(1.5).timeout
    if is_instance_valid(particles):
        particles.queue_free()
```

### 3.2 屏幕震动

```gdscript
# scripts/fx/screen_shake.gd

class_name ScreenShake
extends Node

@export var camera: Camera2D

var _shake_intensity: float = 0.0
var _shake_duration: float = 0.0
var _shake_timer: float = 0.0
var _original_camera_pos: Vector2


func _ready() -> void:
    EventBus.screen_shake.connect(_on_screen_shake)
    if camera:
        _original_camera_pos = camera.position


func _on_screen_shake(intensity: float, duration: float) -> void:
    _shake_intensity = max(_shake_intensity, intensity)
    _shake_duration = max(_shake_duration, duration)
    _shake_timer = duration


func _process(delta: float) -> void:
    if _shake_timer <= 0.0:
        return
    
    _shake_timer -= delta
    var strength := _shake_intensity * (_shake_timer / _shake_duration)
    
    if camera:
        camera.position = _original_camera_pos + Vector2(
            randf_range(-strength, strength),
            randf_range(-strength, strength)
        )
    
    if _shake_timer <= 0.0:
        _shake_intensity = 0.0
        if camera:
            camera.position = _original_camera_pos


# 使用示例:
# EventBus.screen_shake.emit(4.0, 0.2)  # 消除震动
# EventBus.screen_shake.emit(8.0, 0.4)  # 特殊水晶爆炸震动
```

### 3.3 浮动文字

```gdscript
# scripts/ui/floating_text.gd

class_name FloatingTextSpawner
extends Control

## 浮动文字场景
var floating_text_scene: PackedScene = preload("res://assets/scenes/floating_text.tscn")


func _ready() -> void:
    EventBus.show_floating_text.connect(_spawn_floating_text)


func _spawn_floating_text(text: String, world_pos: Vector2, color: Color) -> void:
    var ft := floating_text_scene.instantiate() as Label
    add_child(ft)
    
    ft.text = text
    ft.add_theme_color_override("font_color", color)
    ft.position = world_pos + Vector2(-20, -20)
    ft.scale = Vector2(0.5, 0.5)
    
    var tween := create_tween()
    tween.set_parallel(true)
    tween.tween_property(ft, "position:y", ft.position.y - 60, 0.8)\
         .set_ease(Tween.EASE_OUT)
    tween.tween_property(ft, "modulate:a", 0.0, 0.8)\
         .set_ease(Tween.EASE_IN)
    tween.tween_property(ft, "scale", Vector2(1.2, 1.2), 0.3)\
         .set_ease(Tween.EASE_OUT)
    
    await tween.finished
    if is_instance_valid(ft):
        ft.queue_free()
```

---

## 附录: 动画时序总览

```
一次完整交换的动画时间轴:

0.00s   玩家点击第二个方块
0.00s   → SWAPPING: 两个方块交换动画 (0.2s)
0.20s   → CHECKING_MATCHES: 同步检测 (< 1ms)
0.20s   → CLEARING: 消除动画 (0.2s) + 粒子
0.40s   → FALLING: 下落动画 (0.1-0.5s, 取决于距离)
0.55s   → SPAWNING: 新方块入场 (0.25s) + 波浪延迟
0.80s   → CASCADE_CHECK: 瞬时
0.80s   → 有连锁 → 回到 CLEARING (0.2s)
1.00s   → ...循环...
1.00s   → 无连锁 → CHECK_VALID → IDLE
1.00s   玩家可以再次操作

总计: 约 1 秒 / 次有效交换 (无连锁时)
```
