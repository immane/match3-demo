# 数据模型设计

> 定义所有核心数据结构、枚举、常量，是全部系统的基础契约。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/godot4_syntax.md](../research/godot4_syntax.md) — 类型化集合、class_name、RefCounted |
| ← 研究 | [research/game_rules.md](../research/game_rules.md) — 水晶类型、特殊方块规则 |
| ← 研究 | [research/board_design.md](../research/board_design.md) — 单元格数据结构参考 |
| ↔ 同级 | [board_system.md](board_system.md) — TileData 的实际使用 |
| ↔ 同级 | [match_system.md](match_system.md) — MatchResult 的实际使用 |
| → 任务 | [Task 02](../task/02_core_data.md) — 实现 enums/constants/TileData/BoardData |
| → 任务 | [Task 03](../task/03_match_detector.md) — 实现 MatchDetector 使用这些结构 |

---

---

## 目录

1. [全局常量](#1-全局常量)
2. [核心枚举](#2-核心枚举)
3. [TileData 数据结构](#3-tiledata-数据结构)
4. [BoardData 棋盘数据](#4-boarddata-棋盘数据)
5. [MatchResult 匹配结果](#5-matchresult-匹配结果)
6. [SpecialSpawn 特殊方块生成](#6-specialspawn-特殊方块生成)
7. [MoveRecord 移动记录](#7-moverecord-移动记录)

---

## 1. 全局常量

```gdscript
# scripts/utils/constants.gd

# ---- 棋盘 ----
const GRID_COLS: int = 8
const GRID_ROWS: int = 8
const CELL_SIZE: int = 72
const CELL_SPACING: int = 4
const BOARD_OFFSET_X: int = 40
const BOARD_OFFSET_Y: int = 120
const BOARD_PIXEL_WIDTH: int = GRID_COLS * (CELL_SIZE + CELL_SPACING) - CELL_SPACING
const BOARD_PIXEL_HEIGHT: int = GRID_ROWS * (CELL_SIZE + CELL_SPACING) - CELL_SPACING

# ---- 水晶 ----
const NUM_CRYSTAL_TYPES: int = 5

# ---- 动画 ----
const SWAP_DURATION: float = 0.2
const SWAP_BACK_DURATION: float = 0.15
const CLEAR_DURATION: float = 0.2
const FALL_DURATION_BASE: float = 0.1
const FALL_DURATION_PER_ROW: float = 0.08
const SPAWN_DURATION: float = 0.25

# ---- 分数 ----
const BASE_SCORE_3: int = 30
const BASE_SCORE_4: int = 60
const BASE_SCORE_5: int = 100
const BOMB_SCORE: int = 150
const RAINBOW_SCORE: int = 200
const CROSS_SCORE: int = 180

# ---- 对象池 ----
const TILE_POOL_INITIAL: int = 80

# ---- 级联 ----
const MAX_CASCADE_LOOPS: int = 20
```

---

## 2. 核心枚举

```gdscript
# scripts/utils/enums.gd

# ---- 水晶类型 ----
enum CrystalType {
    RED    = 0,  # 红色 - 红宝石
    BLUE   = 1,  # 蓝色 - 蓝宝石
    GREEN  = 2,  # 绿色 - 翡翠
    YELLOW = 3,  # 黄色 - 黄玉
    PURPLE = 4,  # 紫色 - 紫水晶
    EMPTY  = -1, # 空位
}

# ---- 特殊水晶类型 ----
enum SpecialType {
    NONE       = -1,
    BOMB       = 0,   # 炸弹：消除 3×3 范围
    RAINBOW    = 1,   # 彩虹：消除所有同色
    CROSS      = 2,   # 十字：消除整行 + 整列
}

# ---- 匹配形状 ----
enum MatchShape {
    H_LINE   = 0,  # 水平直线
    V_LINE   = 1,  # 垂直直线
    L_SHAPE  = 2,  # L 形
    T_SHAPE  = 3,  # T 形
    CROSS    = 4,  # + 字形
}

# ---- 游戏状态 ----
enum GameState {
    IDLE             = 0,
    SELECTED         = 1,
    SWAPPING         = 2,
    SWAP_BACK        = 3,
    CHECKING_MATCHES = 4,
    CLEARING         = 5,
    FALLING          = 6,
    SPAWNING         = 7,
    CASCADE_CHECK    = 8,
    CHECK_VALID      = 9,
    RESHUFFLING      = 10,
    PAUSED           = 11,
    GAME_OVER        = 12,
    RESETTING        = 13,
}

# ---- 方块视觉状态 ----
enum TileVisualState {
    IDLE     = 0,
    SELECTED = 1,
    SWAPPING = 2,
    CLEARING = 3,
    FALLING  = 4,
    SPAWNING = 5,
}

# ---- 方向 ----
enum Direction {
    UP    = 0,
    DOWN  = 1,
    LEFT  = 2,
    RIGHT = 3,
}
```

---

## 3. TileData 数据结构

一个格子的完整逻辑数据，存储在 BoardData 的数组中。

```gdscript
# scripts/core/board_data.gd

class_name TileData
extends RefCounted

## 水晶颜色类型
var crystal_type: int = CrystalType.EMPTY

## 特殊水晶类型 (NONE 表示普通水晶)
var special_type: int = SpecialType.NONE

## 行索引 (0 = 顶部)
var row: int = -1

## 列索引 (0 = 左侧)
var col: int = -1

## 是否为空 (无水晶)
var is_empty: bool = true

## 是否被锁定 (障碍物/冰封, 暂未使用)
var is_locked: bool = false

## 锁定剩余层数 (暂未使用)
var lock_hp: int = 0


func clear() -> void:
    crystal_type = CrystalType.EMPTY
    special_type = SpecialType.NONE
    is_empty = true

func set_crystal(type: int, special: int = SpecialType.NONE) -> void:
    crystal_type = type
    special_type = special
    is_empty = false

func is_normal() -> bool:
    return not is_empty and special_type == SpecialType.NONE

func is_special() -> bool:
    return not is_empty and special_type != SpecialType.NONE

## 调试用
func _to_string() -> String:
    if is_empty:
        return "EMPTY"
    var s = str(crystal_type)
    if special_type == SpecialType.BOMB:
        s += "B"
    elif special_type == SpecialType.RAINBOW:
        s += "R"
    elif special_type == SpecialType.CROSS:
        s += "C"
    return s
```

**设计决策**：
- 使用 `RefCounted` 而非 `Resource`，更轻量
- `is_empty` 标志位提前判断，避免反复检查 `crystal_type == EMPTY`
- 每个格子独立对象，支持障碍物扩展

---

## 4. BoardData 棋盘数据

BoardData 是棋盘数据层的核心，所有逻辑操作都基于此。

```gdscript
# scripts/core/board_data.gd

class_name BoardData
extends RefCounted

var cols: int = 8
var rows: int = 8
var tiles: Array[TileData] = []  # 一维数组: tiles[row * cols + col]
var num_crystal_types: int = 5


func _init(p_cols: int = 8, p_rows: int = 8, p_types: int = 5) -> void:
    cols = p_cols
    rows = p_rows
    num_crystal_types = p_types
    tiles.resize(cols * rows)
    for i in range(cols * rows):
        tiles[i] = TileData.new()


func get_tile(row: int, col: int) -> TileData:
    return tiles[row * cols + col]


func set_tile(row: int, col: int, tile: TileData) -> void:
    tile.row = row
    tile.col = col
    tiles[row * cols + col] = tile


func index(row: int, col: int) -> int:
    return row * cols + col


func row_col(index: int) -> Vector2i:
    return Vector2i(index % cols, index / cols)


func is_in_bounds(row: int, col: int) -> bool:
    return row >= 0 and row < rows and col >= 0 and col < cols


func swap(row1: int, col1: int, row2: int, col2: int) -> void:
    var idx1 = index(row1, col1)
    var idx2 = index(row2, col2)
    var temp = tiles[idx1]
    tiles[idx1] = tiles[idx2]
    tiles[idx2] = temp
    # 更新行列信息
    tiles[idx1].row = row1
    tiles[idx1].col = col1
    tiles[idx2].row = row2
    tiles[idx2].col = col2


func duplicate_data() -> Array:
    """深拷贝 tile 类型数组 (用于快照)"""
    var data := []
    for tile in tiles:
        data.append({
            "type": tile.crystal_type,
            "special": tile.special_type,
            "empty": tile.is_empty,
        })
    return data


func restore_from_data(data: Array) -> void:
    """从快照恢复"""
    for i in range(tiles.size()):
        tiles[i].crystal_type = data[i]["type"]
        tiles[i].special_type = data[i]["special"]
        tiles[i].is_empty = data[i]["empty"]


func clear() -> void:
    for tile in tiles:
        tile.clear()


func count_type(crystal_type: int) -> int:
    var count := 0
    for tile in tiles:
        if not tile.is_empty and tile.crystal_type == crystal_type:
            count += 1
    return count


func get_empty_count() -> int:
    var count := 0
    for tile in tiles:
        if tile.is_empty:
            count += 1
    return count
```

**设计决策**：
- 一维数组 + `row * cols + col` 索引，比二维数组 `Array[Array]` 更快，内存更紧凑
- `swap()` 同时更新数据层和行列信息
- `duplicate_data()` / `restore_from_data()` 用于快照和撤销

---

## 5. MatchResult 匹配结果

```gdscript
# scripts/core/match_detector.gd

class_name MatchGroup
extends RefCounted

## 匹配形状
var shape: int = MatchShape.H_LINE

## 匹配到的所有位置
var positions: Array[Vector2i] = []

## 交叉点 (L/T/十字形时有效)
var pivot: Vector2i = Vector2i(-1, -1)

## 匹配长度 (直线匹配的连续长度)
var match_length: int = 0

## 匹配的水晶颜色
var crystal_type: int = CrystalType.EMPTY

## 总匹配数量
func size() -> int:
    return positions.size()


class_name MatchResult
extends RefCounted

## 所有匹配组
var groups: Array[MatchGroup] = []

## 布尔标记矩阵 (标记哪些位置被匹配), 大小 rows*cols
var matched_flags: PackedByteArray = PackedByteArray()

## 需要生成的特殊水晶
var special_spawns: Array[SpecialSpawn] = []

## 总消除格子数
var total_matched: int = 0


func has_matches() -> bool:
    return total_matched > 0


func get_all_positions() -> Array[Vector2i]:
    var all: Array[Vector2i] = []
    for group in groups:
        all.append_array(group.positions)
    return all
```

---

## 6. SpecialSpawn 特殊方块生成

```gdscript
# scripts/core/match_detector.gd

class_name SpecialSpawn
extends RefCounted

## 生成位置
var position: Vector2i = Vector2i()

## 特殊类型
var special_type: int = SpecialType.NONE

## 原始水晶颜色
var crystal_type: int = CrystalType.EMPTY


func _to_string() -> String:
    var names = {SpecialType.NONE: "NONE", SpecialType.BOMB: "BOMB",
                 SpecialType.RAINBOW: "RAINBOW", SpecialType.CROSS: "CROSS"}
    return "SpecialSpawn(%s at %s)" % [names.get(special_type, "?"), position]
```

### 特殊方块生成规则表

| 匹配形状 | 匹配数量 | 生成特殊水晶 | 推荐位置 |
|---------|---------|-------------|---------|
| H_LINE / V_LINE | 4 | BOMB | 匹配中点 |
| H_LINE / V_LINE | 5+ | RAINBOW | 匹配中点 |
| L_SHAPE | 5+ | CROSS | 交叉点 |
| T_SHAPE | 5+ | CROSS | 交叉点 |
| CROSS | 5+ | CROSS | 交叉点 |
| H_LINE / V_LINE | 3 | NONE | - |

---

## 7. MoveRecord 移动记录

用于撤销系统 (未来实现)。

```gdscript
# scripts/core/board_data.gd

class_name MoveRecord
extends RefCounted

## 交换的起始位置
var from: Vector2i

## 交换的目标位置
var to: Vector2i

## 交换前的棋盘快照 (紧凑格式)
var snapshot: Array

## 匹配结果
var match_result: MatchResult

## 获得的分数
var score_gained: int = 0
```

---

## 附录：数据关系图

```
BoardData (棋盘数据)
    │
    ├── tiles: Array[TileData]   (64 个 TileData)
    │     │
    │     ├── crystal_type: int   (RED/BLUE/GREEN/YELLOW/PURPLE/EMPTY)
    │     ├── special_type: int   (NONE/BOMB/RAINBOW/CROSS)
    │     ├── row: int
    │     ├── col: int
    │     └── is_empty: bool
    │
    └── 操作
          │
          ├── swap()        → 交换两个 tile 的数据
          ├── clear()       → 重置所有 tile
          ├── duplicate_data()  → 导出快照
          └── restore_from_data() → 恢复快照

MatchResult (匹配结果)
    │
    ├── groups: Array[MatchGroup]
    │     ├── shape: int         (H_LINE/V_LINE/L_SHAPE/T_SHAPE/CROSS)
    │     ├── positions: Array[Vector2i]
    │     ├── pivot: Vector2i
    │     ├── match_length: int
    │     └── crystal_type: int
    │
    ├── special_spawns: Array[SpecialSpawn]
    │     ├── position: Vector2i
    │     ├── special_type: int
    │     └── crystal_type: int
    │
    ├── matched_flags: PackedByteArray
    └── total_matched: int
```
