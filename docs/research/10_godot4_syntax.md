# Godot 4.x GDScript Syntax & API Reference

> 为 Match-3 Crystal 项目整理的 Godot 4.x 核心 API 速查。所有代码示例均使用 Godot 4.4+ 语法。

---

## 目录

1. [GDScript 基础语法](#1-gdscript-基础语法)
2. [静态类型系统](#2-静态类型系统)
3. [信号系统](#3-信号系统)
4. [协程与 await](#4-协程与-await)
5. [Tween 动画系统](#5-tween-动画系统)
6. [TileMapLayer API](#6-tilemaplayer-api)
7. [UI 系统](#7-ui-系统)
8. [Resource 自定义资源](#8-resource-自定义资源)
9. [输入处理](#9-输入处理)
10. [场景管理](#10-场景管理)
11. [Autoload 单例](#11-autoload-单例)
12. [导出变量与 @tool](#12-导出变量与-tool)
13. [Array / Dictionary 类型化集合](#13-array--dictionary-类型化集合)
14. [Godot 3 → 4 迁移对照表](#14-godot-3--4-迁移对照表)

---

## 1. GDScript 基础语法

### 代码顺序规范

```gdscript
@tool                          # 01. @tool, @icon
class_name MyClass             # 02. class_name
extends Node                   # 03. extends
## 文档注释                    # 04. documentation comment

signal my_signal               # 05. signals
enum MyEnum { A, B }           # 06. enums
const MAX = 100                # 07. constants
static var count = 0           # 08. static vars
@export var speed := 100       # 09. @export vars
var health := 100               # 10. public vars
var _private_var := 0           #     private vars (prefixed _)
@onready var label := $Label   # 11. @onready vars

static func helper(): pass     # 12. static methods
func _init() -> void: pass     # 13. virtual methods
func _ready() -> void: pass
func _process(delta: float) -> void: pass
func do_something() -> void:   # 14. public methods
func _internal() -> void:      # 15. private methods
class InnerClass: pass         # 16. inner classes
```

### 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 文件名 | snake_case | `match3_board.gd` |
| 类名 | PascalCase | `class_name Match3Board` |
| 函数/变量 | snake_case | `func swap_tiles()` |
| 信号 | snake_case (过去式) | `signal tiles_matched` |
| 常量 | CONSTANT_CASE | `const MAX_BOARD_SIZE = 8` |
| 枚举名 | PascalCase | `enum TileType { RED, GREEN }` |
| 枚举值 | CONSTANT_CASE | `CRYSTAL_RED` |
| 私有成员 | `_` 前缀 snake_case | `var _board_data: Array` |

### 注释与文档

```gdscript
## 这是文档注释, 在编辑器中可见。
## 多行用连续 ## 表示。
##
## @param pos: 棋盘坐标
## @return: 该位置的 Tile 引用
func get_tile_at(pos: Vector2i) -> Tile:
    return board[pos.y][pos.x]
```

### 最佳实践

- 使用 `@onready` 缓存节点引用,而非在 `_ready()` 中获取
- 使用 `and` / `or` / `not` 而非 `&&` / `||` / `!`
- 多行数组和字典末尾加逗号
- 在 Project Settings 中启用 `UNTYPED_DECLARATION` 警告强制静态类型
- 所有覆写的生命周期方法中调用 `super()`

---

## 2. 静态类型系统

```gdscript
# 变量类型标注 (推荐使用 := 推断)
var health: int = 100
var velocity: Vector2 = Vector2.ZERO
var direction := Vector2(1, 0)     # 类型被推断为 Vector2
@onready var grid := $Grid as Grid  # 强转为安全类型

# 函数参数与返回类型
func get_tile_at(pos: Vector2i) -> Tile:
    return board[pos.y][pos.x]

func _process(delta: float) -> void:
    pass

# 可空类型 (Godot 4.x 中所有类型默认可为 null)
var node: Node = null  # 有效
var result: int = 0    # int 不可为 null

# class_name 注册全局类
class_name Match3Board
extends Node2D

# 内部类
class TileData:
    var crystal_type: int
    var row: int
    var col: int

    func _init(p_type: int, p_row: int, p_col: int) -> void:
        crystal_type = p_type
        row = p_row
        col = p_col
```

### 类型化集合

```gdscript
# 类型化数组
var scores: Array[int] = [100, 200, 300]
var tiles: Array[Vector2i] = []
var levels: Array[LevelData] = []

# 类型化字典
var config: Dictionary[String, int] = { "width": 8, "height": 8 }

# 循环变量类型推断
for score: int in scores:
    print(score)
```

---

## 3. 信号系统

Godot 4 的信号使用 **Callable 对象**,取代了旧的字符串-based 连接方式。

### 定义信号

```gdscript
signal score_changed(new_score: int)
signal tiles_matched(positions: Array[Vector2i])
signal game_over
signal level_completed(level: int, score: int)
```

### 连接信号

```gdscript
# Callable 连接 (类型安全, 推荐)
EventBus.score_changed.connect(_on_score_changed)
button.pressed.connect(_on_button_pressed)

# 带参数绑定
EventBus.tile_clicked.connect(_on_tile_clicked.bind(tile_position))

# 断开连接
EventBus.score_changed.disconnect(_on_score_changed)
```

### 发射信号

```gdscript
score_changed.emit(100)
tiles_matched.emit(matched_positions)
game_over.emit()
```

### 编辑器连接

在编辑器 Node 面板中可视化连接信号,会自动生成连接代码。

### CONNECT_ONE_SHOT 标志

```gdscript
# 一次性连接 (常用于动画结束后清理)
anim.animation_finished.connect(effect.queue_free, CONNECT_ONE_SHOT)
```

---

## 4. 协程与 await

`yield()` 已被移除,全面使用 `await`。

### 基本用法

```gdscript
# 等待计时器
await get_tree().create_timer(1.0).timeout

# 等待信号
await some_node.some_signal

# 等待 Tween 完成
await tween.finished

# 等待下一帧
await get_tree().process_frame
```

### Match-3 典型模式

```gdscript
# 交换动画后检测匹配
func swap_tiles(pos_a: Vector2i, pos_b: Vector2i) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    tween.tween_property(tile_a, "position", pos_b_display, 0.2)
    tween.tween_property(tile_b, "position", pos_a_display, 0.2)
    await tween.finished

    # 检测匹配
    var result := match_detector.detect_all(board_data)
    if result.matched_groups.is_empty():
        _swap_back(pos_a, pos_b)
    else:
        await _process_matches(result)

# 级联循环 (核心模式)
func _run_cascade_loop() -> void:
    var cascade_depth := 0
    while cascade_depth < MAX_CASCADE_LOOPS:
        if not is_inside_tree():
            return

        var result := match_detector.detect_all(board_data)
        if result.matched_groups.is_empty():
            break

        cascade_depth += 1
        await _do_clearing(result)
        await _do_falling()
        await _do_spawning()
```

### 安全模式

```gdscript
# 所有 await 后必须检查节点有效性
await some_tween.finished
if not is_inside_tree():
    return

# 检查节点是否已被释放
if not is_instance_valid(target_node):
    return
```

---

## 5. Tween 动画系统

Tween 从 Node 节点变为 **RefCounted 对象**,通过 `create_tween()` 创建。

### 核心 API

```gdscript
# 创建 Tween (自动开始, 默认顺序执行)
var tween := create_tween()

# 属性动画
tween.tween_property($Sprite, "modulate", Color.RED, 1.0)
tween.tween_property($Sprite, "scale", Vector2(), 1.0)
tween.tween_callback($Sprite.queue_free)

# 链式配置
tween.tween_property($Tile, "position", target_pos, 0.25)\
    .set_trans(Tween.TRANS_BACK)\
    .set_ease(Tween.EASE_OUT)

# 并行执行
tween.set_parallel(true)
tween.tween_property(tile1, "position:x", 100, 0.5)
tween.tween_property(tile2, "position:x", 100, 0.5)

# 使用 parallel() 分组
var tween := create_tween()
tween.tween_property(a, "modulate", Color.RED, 1.0)
tween.parallel().tween_property(b, "modulate", Color.BLUE, 1.0)
tween.chain().tween_callback(func(): print("done"))

# 循环
tween.set_loops(3)

# 生命周期绑定 (目标节点释放时自动停止 Tween)
tween.bind_node(self)

# 信号
tween.finished.connect(_on_finished)
tween.loop_finished.connect(func(count: int): print(count))

# 控制
tween.stop()
tween.kill()
```

### 缓动类型

| 过渡 | 描述 | 常用场景 |
|------|------|----------|
| `Tween.TRANS_LINEAR` | 线性 | 无关紧要的移动 |
| `Tween.TRANS_SINE` | 正弦平滑 | 一般过渡 |
| `Tween.TRANS_QUAD` | 二次方 | 常规动画 |
| `Tween.TRANS_BOUNCE` | 弹跳 | 落地效果 |
| `Tween.TRANS_ELASTIC` | 弹性 | 弹出效果 |
| `Tween.TRANS_BACK` | 过冲 | 强调动画 |
| `Tween.TRANS_SPRING` | 弹簧 | 摆动效果 |

| 缓出方向 | 效果 |
|-----------|------|
| `Tween.EASE_IN` | 慢开始 |
| `Tween.EASE_OUT` | 慢结束 (最常用) |
| `Tween.EASE_IN_OUT` | 两端慢 |
| `Tween.EASE_OUT_IN` | 两端快 |

### Match-3 动画示例

```gdscript
# 消除动画
func play_clear(tiles: Array[Node2D]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    for tile in tiles:
        tween.tween_property(tile, "scale", Vector2.ZERO, CLEAR_DURATION)\
            .set_trans(Tween.TRANS_BACK)\
            .set_ease(Tween.EASE_IN)
        tween.tween_property(tile, "modulate:a", 0.0, CLEAR_DURATION)
    await tween.finished

# 下落动画
func play_falling(tiles: Array[Node2D], fall_durations: Array[float]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    for i in tiles.size():
        var tile := tiles[i] as Node2D
        var target := tile.target_pos
        tween.tween_property(tile, "position", target, fall_durations[i])\
            .set_trans(Tween.TRANS_BOUNCE)\
            .set_ease(Tween.EASE_OUT)
    await tween.finished

# 生成动画 (从上方滑入 + 缩放弹出)
func play_spawn(tiles: Array[Node2D], col_delays: Array[float]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    for i in tiles.size():
        var tile := tiles[i] as Node2D
        tile.scale = Vector2.ZERO
        var target := tile.target_pos
        tween.tween_property(tile, "position", target, SPAWN_DURATION)\
            .set_delay(col_delays[i])\
            .set_trans(Tween.TRANS_QUAD)\
            .set_ease(Tween.EASE_OUT)
        tween.tween_property(tile, "scale", Vector2.ONE, SPAWN_DURATION * 0.7)\
            .set_delay(col_delays[i])\
            .set_trans(Tween.TRANS_BACK)\
            .set_ease(Tween.EASE_OUT)
    await tween.finished

# 计分动画 (tween_method)
func animate_score(from_val: int, to_val: int) -> void:
    var tween := create_tween()
    tween.tween_method(
        func(value: int): score_label.text = str(value),
        from_val, to_val, 0.5
    )
```

---

## 6. TileMapLayer API

Godot 4 中 `TileMap` 已废弃,使用 **TileMapLayer**。每个图层是独立节点。

### 多层架构

```
- Node2D ("Board")
  - TileMapLayer ("BackgroundLayer")
  - TileMapLayer ("TileLayer")
  - TileMapLayer ("EffectLayer")
```

### 常用操作

```gdscript
# 设置/获取格子
func set_cell_at(pos: Vector2i, source_id: int, atlas_coords: Vector2i) -> void:
    tilemap_layer.set_cell(pos, source_id, atlas_coords)

func get_tile_type_at(pos: Vector2i) -> int:
    var source_id := tilemap_layer.get_cell_source_id(pos)
    if source_id == -1:
        return -1  # 空格
    var atlas := tilemap_layer.get_cell_atlas_coords(pos)
    return atlas.x

func erase_cell(pos: Vector2i) -> void:
    tilemap_layer.erase_cell(pos)

# 坐标转换
func world_to_grid(world_pos: Vector2) -> Vector2i:
    return tilemap_layer.local_to_map(world_pos)

func grid_to_world(grid_pos: Vector2i) -> Vector2:
    return tilemap_layer.map_to_local(grid_pos)

# 获取所有使用中的格子
func get_all_used_cells() -> Array[Vector2i]:
    return tilemap_layer.get_used_cells()

# 清空
func clear_board() -> void:
    tilemap_layer.clear()

# 自定义数据 (存储在 TileData 中)
func get_custom_data(pos: Vector2i, property_name: String):
    var data := tilemap_layer.get_cell_tile_data(pos)
    if data:
        return data.get_custom_data(property_name)
    return null
```

### Godot 3 vs 4

| Godot 3 TileMap | Godot 4 TileMapLayer |
|-----------------|----------------------|
| `map_to_world()` | `map_to_local()` |
| `world_to_map()` | `local_to_map()` |
| `get_cell(x, y)` | `get_cell_source_id(coords)` |
| `set_cell(x, y, tile)` | `set_cell(coords, source_id, atlas_coords)` |
| 单节点多层 | 多节点每层一个 |

---

## 7. UI 系统

### Godot 4 主题 API

```gdscript
# 获取主题属性
var stylebox := button.get_theme_stylebox("normal")
var font := label.get_theme_font("font")
var color := label.get_theme_color("font_color")

# 创建主题覆写
var theme := Theme.new()
theme.set_color("font_color", "Label", Color.RED)
label.theme = theme
```

### Match-3 HUD 示例

```gdscript
extends CanvasLayer

@onready var score_label: Label = $TopPanel/ScoreLabel
@onready var combo_popup: Label = $ComboPopup

func update_score(new_score: int) -> void:
    score_label.text = str(new_score)

func show_combo(combo_count: int) -> void:
    combo_popup.text = "x%d Combo!" % combo_count
    combo_popup.show()
    var tween := create_tween()
    tween.tween_property(combo_popup, "scale", Vector2(2, 2), 0.0)
    tween.tween_property(combo_popup, "scale", Vector2.ONE, 0.3)\
        .set_trans(Tween.TRANS_ELASTIC)
    tween.tween_property(combo_popup, "modulate:a", 0.0, 0.5)\
        .set_delay(0.5)
    tween.tween_callback(combo_popup.hide)
```

### 锚点 (Anchor)

```gdscript
# 代码设置锚点
rect.anchor_left = 0.0
rect.anchor_right = 1.0
rect.anchor_top = 0.0
rect.anchor_bottom = 0.1  # 顶部 10%
```

### 最佳实践

- 使用容器节点 (VBox/HBox/GridContainer) 进行布局
- 用 CanvasLayer 包裹 UI,独立于游戏世界坐标
- 触摸友好的按钮最小 48×48px
- 优先使用 .theme 资源统一管理样式

---

## 8. Resource 自定义资源

Resource 是数据容器,不渲染不处理,仅保存数据。支持类型化属性、编辑器检查、序列化。

### 创建自定义 Resource

```gdscript
# level_data.gd
class_name LevelData
extends Resource

@export var board_width: int = 8
@export var board_height: int = 8
@export var num_tile_types: int = 4
@export var move_limit: int = 30
@export var target_score: int = 1000
@export var layouts: PackedByteArray

func _init(p_width: int = 8, p_height: int = 8) -> void:
    board_width = p_width
    board_height = p_height
```

### 使用 Resource

```gdscript
# 加载
var level := load("res://levels/level_01.tres") as LevelData

# 创建
var new_level := LevelData.new()
new_level.target_score = 500

# 保存
ResourceSaver.save(new_level, "res://levels/level_custom.tres")
```

### 最佳实践

- 使用 Resource 存储所有游戏数据 (关卡配置、Tile 定义、道具属性)
- 使用 `class_name` 让它们在编辑器中可识别
- 开发期间使用 `.tres` (文本格式, Git 友好)
- Resource 支持嵌套子资源
- 在 `_init()` 中提供默认值以便编辑器检查

---

## 9. 输入处理

### 两种方式

**轮询 (每帧检测):**
```gdscript
func _process(delta: float) -> void:
    if Input.is_action_pressed("ui_right"):
        position.x += speed * delta
```

**事件驱动 (响应输入事件):**
```gdscript
func _input(event: InputEvent) -> void:
    if event.is_action_pressed("click"):
        handle_click()
```

### Match-3 输入处理

```gdscript
extends Node2D

func _input(event: InputEvent) -> void:
    # 鼠标
    if event is InputEventMouseButton:
        if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
            _on_click(event.position)

    # 触摸
    if event is InputEventScreenTouch:
        if event.pressed:
            _on_click(event.position)

    # 拖拽 (滑动检测)
    if event is InputEventScreenDrag:
        _on_drag(event.position, event.relative)

func _on_click(screen_pos: Vector2) -> void:
    var grid_pos := tilemap_layer.local_to_map(screen_pos)
    EventBus.tile_clicked.emit(grid_pos)
```

### 滑动检测

```gdscript
var _swipe_start: Vector2

func _input(event: InputEvent) -> void:
    if event is InputEventMouseButton:
        if event.pressed:
            _swipe_start = event.position
        else:
            var swipe := event.position - _swipe_start
            if swipe.length() > 30:
                _handle_swipe(_swipe_start, swipe)
            else:
                _handle_tap(event.position)

func _handle_swipe(start: Vector2, vector: Vector2) -> void:
    var dir: Vector2i
    if abs(vector.x) > abs(vector.y):
        dir = Vector2i(1, 0) if vector.x > 0 else Vector2i(-1, 0)
    else:
        dir = Vector2i(0, 1) if vector.y > 0 else Vector2i(0, -1)
    var from := tilemap_layer.local_to_map(start)
    try_swap(from, from + dir)
```

### InputMap

在 Project Settings > InputMap 中定义动作名:
- `click` — 点击
- `swipe` — 滑动
- `pause` — 暂停

### 最佳实践

- 使用 InputMap 定义跨平台动作
- 同时支持鼠标和触摸
- 设置滑动阈值区分点击和滑动 (建议 30px)
- 开发时启用 Project Settings > "Emulate Touch From Mouse"

---

## 10. 场景管理

### 场景切换

```gdscript
# 通过文件路径
get_tree().change_scene_to_file("res://scenes/game.tscn")

# 通过预加载的 PackedScene (编译时, 推荐)
const GAME_SCENE := preload("res://scenes/game.tscn")

func start_game() -> void:
    get_tree().change_scene_to_packed(GAME_SCENE)
```

### 实例化场景

```gdscript
const TILE_SCENE := preload("res://scenes/tile.tscn")

func create_tile(pos: Vector2, type: int) -> Node2D:
    var tile := TILE_SCENE.instantiate()  # Godot 4: instantiate()
    tile.position = pos
    tile.tile_type = type
    add_child(tile)
    return tile

# 自动清理特效
func spawn_effect(pos: Vector2) -> void:
    var effect := EFFECT_SCENE.instantiate()
    effect.position = pos
    add_child(effect)
    effect.get_node("AnimationPlayer").animation_finished.connect(
        effect.queue_free, CONNECT_ONE_SHOT
    )
    effect.get_node("AnimationPlayer").play("explode")
```

### 自定义场景切换 (Autoload 方式)

```gdscript
# global.gd (autoload)
extends Node

func goto_scene(path: String) -> void:
    call_deferred("_deferred_goto_scene", path)

func _deferred_goto_scene(path: String) -> void:
    var root := get_tree().root
    for child in root.get_children():
        child.queue_free()
    var scene := load(path) as PackedScene
    root.add_child(scene.instantiate())
```

### Godot 3 vs 4

| Godot 3 | Godot 4 |
|---------|---------|
| `change_scene("path")` | `change_scene_to_file("path")` |
| `PackedScene.instance()` | `PackedScene.instantiate()` |
| `Node.filename` | `Node.scene_file_path` |

---

## 11. Autoload 单例

Project Settings > Globals > Autoload,添加脚本/场景并命名。

### GameData 示例

```gdscript
# game_data.gd (autoload)
extends Node

var high_score: int = 0
var current_score: int = 0
var combo: int = 0
var moves_remaining: int = 30

signal score_changed(new_score: int)
signal level_completed(level: int, score: int)

func add_score(amount: int) -> void:
    current_score += amount
    score_changed.emit(current_score)

func reset_level() -> void:
    current_score = 0
    combo = 0
    moves_remaining = 30

func use_move() -> void:
    moves_remaining -= 1
```

### EventBus 示例

```gdscript
# event_bus.gd (autoload)
extends Node

## Board signals
signal tile_clicked(pos: Vector2i)
signal tiles_exchanged(from_pos: Vector2i, to_pos: Vector2i)

## Match signals
signal tiles_matched(positions: Array[Vector2i])
signal special_spawned(pos: Vector2i, special_type: int)

## Score signals
signal score_changed(new_score: int)
signal combo_updated(combo: int)

## Game state signals
signal game_over
signal level_complete

## UI signals
signal show_combo(count: int)
signal show_floating_text(text: String, pos: Vector2, color: Color)
```

### 访问 Autoload

```gdscript
# 直接用名称 (在 Autoload 中注册的名称)
GameData.add_score(100)
EventBus.tile_clicked.emit(pos)
AudioManager.play_sfx(AudioManager.SOUND_MATCH)

# 通过 root 访问
get_node("/root/GameData").add_score(100)
```

### 最佳实践

- 使用 Autoload 管理: 游戏状态, 音频, 关卡数据, 存档, 场景切换
- 不要 autoload 重量级场景 (使用轻量 Node 脚本)
- 不要 free autoload (会导致崩溃)
- 使用信号解耦 Autoload 之间的通信
- 保持 Autoload 数量可控

---

## 12. 导出变量与 @tool

### 导出注解参考

```gdscript
# 基础导出
@export var speed: float = 100.0
@export var color: Color = Color.RED
@export var tile_size: Vector2i = Vector2i(32, 32)
@export var level_data: LevelData
@export var tile_scene: PackedScene

# 范围限制
@export_range(0, 10) var difficulty: int = 5
@export_range(0.0, 1.0, 0.1) var volume: float = 0.8

# 枚举
@export_enum("Red", "Green", "Blue") var crystal_type: int

# 类型安全的节点引用
@export var board: Match3Board
@export var tile_map: TileMapLayer

# 分组织
@export_group("Board Settings")
@export var board_width: int = 8
@export var board_height: int = 8

@export_subgroup("Animation")
@export var swap_duration: float = 0.2
```

### @tool 脚本

```gdscript
@tool
extends Node2D

@export var preview_size: Vector2i = Vector2i(8, 8):
    set(value):
        preview_size = value
        if Engine.is_editor_hint():
            queue_redraw()

func _draw() -> void:
    if not Engine.is_editor_hint():
        return
    # 编辑器预览绘制
    for x in preview_size.x:
        for y in preview_size.y:
            draw_rect(Rect2(x * 64, y * 64, 64, 64), Color.WHITE, false)
```

### 最佳实践

- `@tool` 放在文件第一行
- 用 `Engine.is_editor_hint()` 守卫编辑器专用代码
- 分组组织相关导出变量
- 用 `##` 注释作为编辑器提示

---

## 13. Array / Dictionary 类型化集合

### 类型化数组

```gdscript
var scores: Array[int] = [100, 200]
var tiles: Array[Vector2i] = []
var levels: Array[LevelData] = []

# 类型在写入时强制检查
scores.append(400)       # OK
# scores.append("abc")   # ERROR

# 循环变量自动类型推断
for score: int in scores:
    print(score)
```

### 常用 Array 方法

```gdscript
var arr: Array[int] = [1, 2, 3, 4, 5]

arr.size()           # 5
arr[0]               # 1
arr[-1]              # 5 (最后一个)
arr.front()          # 1
arr.back()           # 5

arr.append(6)        # [1, 2, 3, 4, 5, 6]
arr.push_front(0)    # [0, 1, 2, 3, 4, 5, 6]
arr.erase(3)         # 移除第一个值为3的元素
arr.remove_at(2)     # 移除索引2
arr.clear()          # 清空

arr.has(3)           # 是否包含
arr.find(3)          # 返回索引, -1 表示未找到

# 函数式方法
arr.map(func(x): return x * 2)           # [2, 4, 6, 8, 10]
arr.filter(func(x): return x > 2)        # [3, 4, 5]
arr.reduce(func(acc, x): return acc + x, 0)  # 15

arr.reverse()        # 原地反转
arr.shuffle()        # 原地打乱
arr.sort()           # 原地升序

arr.slice(1, 3)      # [2, 3] (end 不包含!)
arr.duplicate()      # 浅拷贝
arr.is_empty()       # 是否为空
```

### 类型化字典

```gdscript
var config: Dictionary[String, int] = { "width": 8, "height": 8 }
var lookup: Dictionary[Vector2i, Node] = {}

# Lua 风格语法 (仅字符串键)
var data = {
    width = 8,
    height = 8,
}
print(data.width)  # 点访问

# 常用操作
config["width"] = 8          # 设置
config.get("width", 0)       # 获取 (带默认值)
config.has("width")          # 是否包含键
config.keys()                 # 所有键
config.values()               # 所有值
config.erase("width")         # 移除键
config.merge({"new": 1})      # 合并
config.size()                 # 大小
```

### PackedArray (高性能)

```gdscript
var byte_data := PackedByteArray()
var int_data := PackedInt32Array()
var float_data := PackedFloat32Array()
var color_data := PackedColorArray()
var vector2_data := PackedVector2Array()
var string_data := PackedStringArray()
```

### Match-3 典型模式

```gdscript
# 1D 数组棋盘
var board_data: PackedByteArray  # 64 bytes for 8x8

# 位置查找字典
var tile_lookup: Dictionary[Vector2i, Node2D] = {}

func add_tile(pos: Vector2i, tile: Node2D) -> void:
    tile_lookup[pos] = tile

func get_tile_at(pos: Vector2i) -> Node2D:
    return tile_lookup.get(pos)

func remove_tile(pos: Vector2i) -> void:
    if tile_lookup.has(pos):
        tile_lookup[pos].queue_free()
        tile_lookup.erase(pos)

# 按类型分组匹配
func group_by_type(positions: Array[Vector2i]) -> Dictionary:
    var groups: Dictionary[int, Array] = {}
    for pos in positions:
        var t := get_tile_type(pos)
        if not groups.has(t):
            groups[t] = []
        groups[t].append(pos)
    return groups

# 方向常量
const DIRECTIONS: Array[Vector2i] = [
    Vector2i(0, -1),  # 上
    Vector2i(1, 0),   # 右
    Vector2i(0, 1),   # 下
    Vector2i(-1, 0),  # 左
]
```

---

## 14. Godot 3 → 4 迁移对照表

| 类别 | Godot 3 | Godot 4 |
|------|---------|---------|
| **协程** | `yield()` | `await` |
| **信号连接** | `signal.connect(target, "method")` | `signal.connect(callable)` |
| **信号生态** | `Object.connect()`, `is_connected()` | `signal.connect()`, `signal.is_connected()` |
| **@onready** | `onready var x = ...` | `@onready var x = ...` |
| **@tool** | `tool` | `@tool` |
| **导出** | `export var x = 5` | `@export var x = 5` |
| **导出范围** | `export(int, 0, 10) var x` | `@export_range(0, 10) var x: int` |
| **Setter/Getter** | `setget _set_x, _get_x` | `set(value): x = value` (内联) |
| **实例化** | `PackedScene.instance()` | `PackedScene.instantiate()` |
| **场景切换** | `get_tree().change_scene("path")` | `get_tree().change_scene_to_file("path")` |
| **场景路径** | `Node.filename` | `Node.scene_file_path` |
| **TileMap** | `TileMap` 节点 (单节点多层) | `TileMapLayer` 节点 (每层独立) |
| **TileMap 坐标** | `map_to_world()` / `world_to_map()` | `map_to_local()` / `local_to_map()` |
| **Tween** | `Tween` 节点, `interpolate_property()` | `Tween` RefCounted, `tween_property()` |
| **Tween 创建** | `Tween.new()` + `add_child()` | `create_tween()` |
| **主题属性** | `get_stylebox("normal")` | `get_theme_stylebox("normal")` |
| **边距** | `.margin` | `.offset` |
| **输入** | `event.is_action("name")` | `event.is_action_pressed("name")` |
| **修饰键** | `event.control` | `event.ctrl_pressed` |
| **双击** | `event.doubleclick` | `event.double_click` |
| **键盘** | `KEY_*` | `KEY_*` (相同, 已扩展) |
| **Array** | 无类型 | `Array[int]`, `Array[Node]` |
| **Array 方法** | `empty()`, `invert()`, `remove(idx)` | `is_empty()`, `reverse()`, `remove_at(idx)` |
| **Array slice** | `slice()` end 包含 | `slice()` end 不包含 |
| **Dictionary** | 无类型 | `Dictionary[String, int]` |
| **粒子** | `Particles2D` | `GPUParticles2D` |
| **渲染器** | GLES2/GLES3 | Compatibility/Mobile/Forward+ |
| **环境** | `WorldEnvironment` | `WorldEnvironment` (相同) |
| **2D 光照** | Light2D | PointLight2D |
| **shader** | `shader_type canvas_item;` | `shader_type canvas_item;` (相同) |
| **随机数** | `randi() % n` | `randi() % n` / `randf()` (相同) |
| **File** | `File` | `FileAccess` |
| **JSON** | `JSON.parse(data)` | `JSON.parse_string(data)` (静态) |

---

## 项目专用速查

### BoardData 1D 数组坐标转换

```gdscript
# 1D 索引 ↔ 2D 行列
func to_index(row: int, col: int) -> int:
    return row * BOARD_WIDTH + col

func to_row_col(index: int) -> Vector2i:
    return Vector2i(index % BOARD_WIDTH, index / BOARD_WIDTH)
```

### 二阶混合匹配检测

```gdscript
# Phase 1: 线性扫描 (O(n))
func detect_horizontal(board: BoardData) -> Array[MatchGroup]:
    var groups: Array[MatchGroup] = []
    for row in board.rows:
        var run_start := 0
        var run_type := board.get_tile(0, row).crystal_type
        for col in 1..board.cols:
            var current := board.get_tile(col, row).crystal_type
            if current != run_type or col == board.cols - 1:
                if (col - run_start) >= 3:
                    groups.append(_create_group(row, run_start, col - 1))
                run_start = col
                run_type = current
    return groups

# Phase 2: BFS 洪水填充 + 形状分类
func _flood_fill(pos: Vector2i, board: BoardData) -> MatchGroup:
    var queue: Array[Vector2i] = [pos]
    var visited: Array[Vector2i] = []
    while not queue.is_empty():
        var current := queue.pop_front()
        if current in visited:
            continue
        visited.append(current)
        for dir in DIRECTIONS:
            var neighbor := current + dir
            if is_valid(neighbor) and neighbor not in visited
                and board.get_tile_at(neighbor).crystal_type == target_type:
                queue.append(neighbor)
    return _classify_shape(visited, target_type)
```

### 状态机 setter 模式

```gdscript
@export var current_state: GameState = GameState.IDLE:
    set(value):
        if value == current_state:
            return
        _exit_state(current_state)
        current_state = value
        _enter_state(current_state)
        state_changed.emit(current_state)
```
