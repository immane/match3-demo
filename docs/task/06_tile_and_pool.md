# Task 06: Tile 场景 + Tile 脚本 + TileManager 对象池

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/board_system.md](../design/board_system.md) — Tile 节点设计、TileManager 对象池 |
| ↖ 设计 | [design/crystal_shader.md](../design/crystal_shader.md) — ShaderMaterial 参数配置 |

## 状态
- [x] 已完成

## 依赖
- Task 02 (数据结构: TileData, BoardData, enums, constants)
- Task 05 (crystal.gdshader — 被 Tile 引用)

## 产出文件
```
assets/scenes/tile.tscn                # Tile 场景
scripts/game/tile.gd                   # Tile 节点脚本
scripts/game/tile_manager.gd           # TileManager 对象池管理
```

## 实现要求

### tile.tscn 场景结构

参考 `docs/design/board_system.md` §3.1:

```
Tile (Node2D) [script=tile.gd]
├── CrystalSprite (Sprite2D)
│   └── Material: ShaderMaterial (crystal.gdshader)
├── SelectionBorder (Sprite2D)        # 默认 visible=false
├── SpecialIcon (Sprite2D)            # 默认 visible=false
└── GlowEffect (Sprite2D)             # 默认 visible=false

Node2D 配置:
- 所有 Sprite2D centered = true
- CrystalSprite texture 可以暂时留空 (后续从图集设置)
- 场景根节点面积不需要太大, 以 CELL_SIZE=72 为准
```

### tile.gd

参考 `docs/design/board_system.md` §3.2:

```gdscript
class_name Tile
extends Node2D

# 信号
signal click_detected(tile: Tile)

# 数据引用
var tile_data: TileData = null
var board_position: Vector2i

# 视觉状态
var visual_state: int

# @onready 引用
@onready var crystal_sprite: Sprite2D
@onready var selection_border: Sprite2D
@onready var special_icon: Sprite2D
@onready var glow_effect: Sprite2D
@onready var shader_material: ShaderMaterial

# 方法
func setup(data: TileData)               # 设置数据 + 更新外观
func _update_appearance()                # 根据 tile_data 更新 sprite/shader
func _set_special_icon(special_type)     # 设置特殊水晶图标颜色
func select()                            # 显示选择边框
func deselect()                          # 隐藏选择边框
func _on_input_event(viewport, event, shape_idx)  # 处理点击, emit click_detected
func _process(delta)                     # 更新 shader time_sec uniform
```

关键实现:
- `_process`: `shader_material.set_shader_parameter("time_sec", Time.get_ticks_msec() / 1000.0)`
- `_update_appearance()`: 根据 crystal_type 设置 shader 的 crystal_color (使用字典映射)
- `_set_special_icon()`: BOMB=橙红, RAINBOW=白, CROSS=青
- 使用 `_input_event` 虚方法处理点击 (需要设置 `mouse_filter = MOUSE_FILTER_STOP`)
- 根节点需要设置 `mouse_filter = MOUSE_FILTER_STOP` 和相关 input 设置

水晶颜色映射:
```gdscript
const CRYSTAL_COLORS = {
    CrystalType.RED:    Color("#ff4466"),
    CrystalType.BLUE:   Color("#4488ff"),
    CrystalType.GREEN:  Color("#44dd66"),
    CrystalType.YELLOW: Color("#ffcc33"),
    CrystalType.PURPLE: Color("#cc44ff"),
}
```

### tile_manager.gd

参考 `docs/design/board_system.md` §4:

```gdscript
class_name TileManager
extends Node2D

var tile_scene: PackedScene
var _pool: Array[Tile]
var _active_tiles: Dictionary   # int index → Tile

func _ready()                    # 预分配 TILE_POOL_INITIAL 个
func _preallocate(count: int)    # 创建并加入池
func acquire(idx: int) -> Tile   # 从池获取或新建
func release(idx: int)           # 回收 tile 到池
func release_all()               # 回收所有活跃 tile
func get_active_tile(idx: int) -> Tile
func create_tile(data: TileData, idx: int) -> Tile   # acquire + setup
func refresh_from_data()         # 根据 board_data 同步所有 tile
```

关键实现:
- `_preallocate`: 循环创建 tile_scene.instantiate(), 设置 visible=false, process_mode=DISABLED, 加入 _pool
- `acquire`: 从 _pool.pop_back() 或新建, 设置 visible=true, process_mode=INHERIT
- `release`: 重置 tile 状态, 移除 from parent (仅 remove_child, 不 queue_free)
- `refresh_from_data`: 释放空数据位置的 tile, 为有数据位置 create/update tile

## 验收标准
- tile.tscn 场景可在 Godot 编辑器中打开, 子节点结构正确
- Tile 脚本: click_detected 信号正确发射, 水晶颜色根据类型正确展示
- TileManager: acquire/release 对象复用, 预分配 80 个无泄漏
- `_active_tiles` 和 `_pool` 管理正确

## 注意
- tile.tscn 中的 CrystalSprite texture 可以留空, 后续使用程序化生成
- 如果暂时没有纹理, CrystalSprite 可以用 `ColorRect` (Godot 4: `ColorRect`) + shader 代替
- TileManager 作为 Node2D 子节点添加到 Board 场景 (在后续 Task 中)
