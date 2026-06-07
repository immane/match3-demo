# 棋盘系统设计

> 定义 8×8 网格系统、坐标映射、棋盘初始化、对象池管理。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/board_design.md](../research/board_design.md) — 尺寸对比、网格表示、坐标系统 |
| ← 研究 | [research/random_generation.md](../research/random_generation.md) — 初始棋盘生成策略 |
| ← 研究 | [research/performance.md](../research/performance.md) — 对象池性能优化 |
| ↔ 同级 | [data_models.md](data_models.md) — BoardData + TileData 定义 |
| ↔ 同级 | [crystal_shader.md](crystal_shader.md) — Tile 节点使用的 shader |
| → 任务 | [Task 06](../task/06_tile_and_pool.md) — Tile + TileManager 实现 |
| → 任务 | [Task 09](../task/09_board_and_input.md) — Board 场景集成 |

---

---

## 目录

1. [坐标系统](#1-坐标系统)
2. [棋盘初始化](#2-棋盘初始化)
3. [Tile 节点设计](#3-tile-节点设计)
4. [对象池 TileManager](#4-对象池-tilemanager)
5. [视觉布局](#5-视觉布局)

---

## 1. 坐标系统

### 1.1 网格坐标 (逻辑)

```
逻辑坐标 (grid)              屏幕坐标 (screen/pixel)
                                
 (0,0)──(0,1)──(0,2)...       (offset_x,offset_y)──────────▶ x
   │       │       │              │
 (1,0)──(1,1)──(1,2)...          │  ● (col * step + ox,
   │       │       │              │     row * step + oy)
 (2,0)──(2,1)──(2,2)...          ▼
                                 y

row: 0 → 7 (从上到下), col: 0 → 7 (从左到右)
重力方向: 从 row=0 (顶) → row=7 (底)
新生方块: 从 row=-1 (屏幕外上方) 生成, 下落
```

### 1.2 坐标转换

```gdscript
# scripts/utils/math_utils.gd

class_name GridUtils
extends RefCounted

const CELL_SIZE := 72
const CELL_SPACING := 4
const BOARD_OFFSET := Vector2(40, 120)
const CELL_STEP := CELL_SIZE + CELL_SPACING  # 76

# 网格坐标 → 屏幕像素 (格子中心)
static func grid_to_world(row: int, col: int) -> Vector2:
    return BOARD_OFFSET + Vector2(
        col * CELL_STEP + CELL_SIZE / 2.0,
        row * CELL_STEP + CELL_SIZE / 2.0
    )

# 屏幕像素 → 网格坐标 (点击检测)
static func world_to_grid(world_pos: Vector2) -> Vector2i:
    var local := world_pos - BOARD_OFFSET
    var col := int(local.x / CELL_STEP)
    var row := int(local.y / CELL_STEP)
    
    # 边界校验
    if row < 0 or row >= 8 or col < 0 or col >= 8:
        return Vector2i(-1, -1)
    return Vector2i(row, col)

# 索引 ↔ 行列
static func to_index(row: int, col: int, cols: int = 8) -> int:
    return row * cols + col

static func to_row_col(index: int, cols: int = 8) -> Vector2i:
    return Vector2i(index / cols, index % cols)
```

---

## 2. 棋盘初始化

### 2.1 初始生成 (无初始匹配)

```gdscript
# scripts/core/board_data.gd 中的方法

func generate_initial_board() -> void:
    """生成无初始 3 连匹配的棋盘"""
    var rng := RandomNumberGenerator.new()
    rng.randomize()
    
    for row in range(rows):
        for col in range(cols):
            var tile: TileData = get_tile(row, col)
            var excluded: Array[int] = []
            
            # 检查左侧 2 个, 防止水平 3 连
            if col >= 2:
                var t1 := get_tile(row, col - 1)
                var t2 := get_tile(row, col - 2)
                if t1.crystal_type == t2.crystal_type:
                    excluded.append(t1.crystal_type)
            
            # 检查上侧 2 个, 防止垂直 3 连
            if row >= 2:
                var t1 := get_tile(row - 1, col)
                var t2 := get_tile(row - 2, col)
                if t1.crystal_type == t2.crystal_type:
                    excluded.append(t1.crystal_type)
            
            # 从允许的类型中随机
            var allowed: Array[int] = []
            for t in range(num_crystal_types):
                if t not in excluded:
                    allowed.append(t)
            
            var chosen := allowed[rng.randi() % allowed.size()]
            tile.set_crystal(chosen)
```

**算法复杂度**：O(rows × cols) = O(64)，一次性扫描完成。

### 2.2 保证初始有可用移动

生成初始棋盘后，必须确保存在至少一个有效交换：

```gdscript
func ensure_has_valid_moves() -> bool:
    """检查当前棋盘是否存在至少一个有效交换"""
    var checker := ValidMoveChecker.new()
    return checker.has_any_valid_move(self)


func reshuffle_if_needed() -> void:
    """如果无可用移动, 重洗棋盘"""
    if ensure_has_valid_moves():
        return
    
    # 收集所有非空 tile 的类型
    var types: Array[int] = []
    for tile in tiles:
        if not tile.is_empty:
            types.append(tile.crystal_type)
    
    var rng := RandomNumberGenerator.new()
    rng.randomize()
    
    # 重新随机排列, 但保证无初始匹配 + 有可用移动
    var max_attempts := 100
    for attempt in range(max_attempts):
        types.shuffle()
        # 直接填充, 不考虑初始匹配 (重洗允许出现匹配)
        var idx := 0
        for tile in tiles:
            if not tile.is_empty:
                tile.crystal_type = types[idx]
                tile.special_type = SpecialType.NONE
                idx += 1
        
        if ensure_has_valid_moves():
            return
    
    # 极端情况: 全重置
    clear()
    generate_initial_board()
```

---

## 3. Tile 节点设计

### 3.1 场景结构

```
Tile (Node2D)
├── CrystalSprite (Sprite2D)         # 水晶贴图 (从图集采样)
│   └── Material: ShaderMaterial    # crystal.gdshader
├── SelectionBorder (Sprite2D)       # 选中边框 (默认隐藏)
├── SpecialIcon (Sprite2D)           # 特殊水晶图标 (默认隐藏)
├── GlowEffect (Sprite2D)            # 发光特效 (默认隐藏)
└── AnimationPlayer                  # 预留
```

### 3.2 Tile 脚本

```gdscript
# scripts/game/tile.gd

class_name Tile
extends Node2D

signal click_detected(tile: Tile)
signal animation_finished

# ---- 数据引用 ----
var tile_data: TileData = null
var board_position: Vector2i = Vector2i(-1, -1)

# ---- 视觉状态 ----
var visual_state: int = TileVisualState.IDLE

# ---- 子节点引用 ----
@onready var crystal_sprite: Sprite2D = $CrystalSprite
@onready var selection_border: Sprite2D = $SelectionBorder
@onready var special_icon: Sprite2D = $SpecialIcon
@onready var glow_effect: Sprite2D = $GlowEffect


func setup(data: TileData) -> void:
    tile_data = data
    board_position = Vector2i(data.row, data.col)
    position = GridUtils.grid_to_world(data.row, data.col)
    _update_appearance()


func _update_appearance() -> void:
    if tile_data == null or tile_data.is_empty:
        hide()
        return
    
    show()
    modulate = Color.WHITE
    scale = Vector2.ONE
    
    # 从图集设置水晶纹理区域
    var atlas := crystal_sprite.texture as AtlasTexture
    if atlas and atlas.atlas:
        var type := tile_data.crystal_type
        var region := Rect2(type * 72, 0, 72, 72)  # 每格 72px 在图集中排列
        atlas.region = region
    
    # 特殊水晶图标
    if tile_data.is_special():
        special_icon.show()
        _set_special_icon(tile_data.special_type)
    else:
        special_icon.hide()
    
    # 选中边框
    selection_border.visible = (visual_state == TileVisualState.SELECTED)


func _set_special_icon(special_type: int) -> void:
    match special_type:
        SpecialType.BOMB:
            special_icon.modulate = Color.ORANGE_RED
        SpecialType.RAINBOW:
            special_icon.modulate = Color.WHITE
        SpecialType.CROSS:
            special_icon.modulate = Color.CYAN


func select() -> void:
    visual_state = TileVisualState.SELECTED
    selection_border.show()


func deselect() -> void:
    visual_state = TileVisualState.IDLE
    selection_border.hide()


func get_world_position() -> Vector2:
    return global_position


func _on_input_event(_viewport, event: InputEvent, _shape_idx) -> void:
    if event is InputEventMouseButton and event.pressed:
        if event.button_index == MOUSE_BUTTON_LEFT:
            click_detected.emit(self)
```

---

## 4. 对象池 TileManager

```gdscript
# scripts/game/tile_manager.gd

class_name TileManager
extends Node2D

## 预加载的 Tile 场景
var tile_scene: PackedScene = preload("res://assets/scenes/tile.tscn")

## 对象池 (未使用的 tile)
var _pool: Array[Tile] = []

## 当前活跃的 tile (index → Tile)
var _active_tiles: Dictionary = {}

## 棋盘数据引用
var board_data: BoardData = null


func _ready() -> void:
    _preallocate(TILE_POOL_INITIAL)


func _preallocate(count: int) -> void:
    for i in range(count):
        var tile := tile_scene.instantiate() as Tile
        tile.visible = false
        tile.process_mode = Node.PROCESS_MODE_DISABLED
        add_child(tile)
        _pool.append(tile)


func acquire(idx: int) -> Tile:
    var tile: Tile
    if _pool.is_empty():
        tile = tile_scene.instantiate() as Tile
        add_child(tile)
    else:
        tile = _pool.pop_back()
    
    tile.visible = true
    tile.process_mode = Node.PROCESS_MODE_INHERIT
    _active_tiles[idx] = tile
    return tile


func release(idx: int) -> void:
    if not _active_tiles.has(idx):
        return
    var tile: Tile = _active_tiles[idx]
    tile.visible = false
    tile.process_mode = Node.PROCESS_MODE_DISABLED
    tile.tile_data = null
    tile.scale = Vector2.ONE
    tile.modulate = Color.WHITE
    _active_tiles.erase(idx)
    _pool.append(tile)


func release_all() -> void:
    for idx in _active_tiles.keys():
        release(idx)


func get_active_tile(idx: int) -> Tile:
    return _active_tiles.get(idx, null)


func create_tile(data: TileData, idx: int) -> Tile:
    var tile := acquire(idx)
    tile.setup(data)
    return tile


func refresh_from_data() -> void:
    """根据 board_data 同步所有 tile 节点"""
    # 移除数据中已空的 tile
    for idx in _active_tiles.keys():
        var data := board_data.tiles[idx]
        if data.is_empty:
            release(idx)
    
    # 为有数据的 tile 创建/更新节点
    for i in range(board_data.tiles.size()):
        var data := board_data.tiles[i]
        if data.is_empty:
            continue
        if not _active_tiles.has(i):
            var tile := create_tile(data, i)
            tile.board_position = Vector2i(data.row, data.col)
        else:
            _active_tiles[i]._update_appearance()
```
---

## 5. 视觉布局

### 5.1 尺寸参数

```
屏幕设计分辨率: 720 × 1280 (竖屏移动端基准)

棋盘可视区域:
┌──────────────────────────────┐
│          padding_top          │
│  ┌────────────────────────┐  │
│p │ ┌──┐┌──┐┌──┐┌──┐┌──┐  │p │
│a │ │  ││  ││  ││  ││  │  │a │
│d │ └──┘└──┘└──┘└──┘└──┘  │d │
│d │ ┌──┐┌──┐┌──┐┌──┐┌──┐  │d │
│_ │ │  ││  ││  ││  ││  │  │_ │
│l │ └──┘└──┘└──┘└──┘└──┘  │r │
│e │     ... 8 rows ...     │i │
│f │ ┌──┐┌──┐┌──┐┌──┐┌──┐  │g │
│t │ │  ││  ││  ││  ││  │  │h │
│  │ └──┘└──┘└──┘└──┘└──┘  │t │
│  └────────────────────────┘  │
│         padding_bottom        │
└──────────────────────────────┘
```

### 5.2 参数配置

| 参数 | 值 | 说明 |
|------|-----|------|
| CELL_SIZE | 72px | 水晶方块边长 |
| CELL_SPACING | 4px | 方块间距 |
| CELL_STEP | 76px | 方块布局间距 |
| BOARD_OFFSET_X | 40px | 棋盘左边距 |
| BOARD_OFFSET_Y | 120px | 棋盘顶部偏移 (给 HUD 留空间) |
| BOARD_WIDTH | 604px | 8 × 76 - 4 = 604 |
| BOARD_HEIGHT | 604px | 8 × 76 - 4 = 604 |

### 5.3 棋盘背景绘制

```gdscript
# 在 Board 节点的 _draw() 中
func _draw() -> void:
    var colors := [Color("#3a3a5c"), Color("#2e2e4a")]
    for row in range(GRID_ROWS):
        for col in range(GRID_COLS):
            var pos := GridUtils.grid_to_world(row, col) 
            pos -= Vector2(CELL_SIZE / 2, CELL_SIZE / 2)
            var rect := Rect2(pos, Vector2(CELL_SIZE, CELL_SIZE))
            var color := colors[(row + col) % 2]
            draw_rect(rect, color)
            
            # 边缘光晕
            var glow_color := Color.WHITE
            glow_color.a = 0.03
            draw_rect(rect.grow(2), glow_color, false, 1.0)
```

---

## 附录: 关键设计决策

| 决策 | 理由 |
|------|------|
| 方块 72px | 移动端可见, 8 列不溢出 720px 宽度 |
| 间距 4px | 足够区分方块但不浪费空间 |
| 棋盘居中 | 自适应不同屏幕比例 |
| 对象池预分配 80 个 | 64 个基础 + 16 个缓冲 (生成/动画中) |
| 一维数组存储 | 比二维数组快 2-3 倍, 批量操作友好 |
| 受限随机生成 | 一次扫描完成, 比 "生成后修复" 更简单 |
