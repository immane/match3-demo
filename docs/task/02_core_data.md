# Task 02: 核心数据定义

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/data_models.md](../design/data_models.md) — 枚举、常量、TileData、BoardData 完整规格 |

## 状态
- [ ] 待执行

## 依赖
- Task 01 (project.godot 和目录结构)

## 产出文件
```
scripts/utils/enums.gd
scripts/utils/constants.gd
scripts/core/board_data.gd           # TileData + BoardData
scripts/core/match_detector.gd       # MatchGroup + MatchResult + SpecialSpawn (仅数据结构, 不含算法)
```

## 实现要求

### enums.gd
定义所有全局枚举, 参考 `docs/design/data_models.md` §2:
- `CrystalType` enum: RED=0, BLUE=1, GREEN=2, YELLOW=3, PURPLE=4, EMPTY=-1
- `SpecialType` enum: NONE=-1, BOMB=0, RAINBOW=1, CROSS=2
- `MatchShape` enum: H_LINE=0, V_LINE=1, L_SHAPE=2, T_SHAPE=3, CROSS=4
- `GameState` enum: IDLE=0, SELECTED=1, SWAPPING=2, SWAP_BACK=3, CHECKING_MATCHES=4, CLEARING=5, FALLING=6, SPAWNING=7, CASCADE_CHECK=8, CHECK_VALID=9, RESHUFFLING=10, PAUSED=11, GAME_OVER=12, RESETTING=13
- `TileVisualState` enum: IDLE=0, SELECTED=1, SWAPPING=2, CLEARING=3, FALLING=4, SPAWNING=5
- `Direction` enum: UP=0, DOWN=1, LEFT=2, RIGHT=3

所有 enum 使用 `enum EnumName { KEY = value, ... }` 语法, 每个值有明确注释。

### constants.gd
定义全局常量, 参考 `docs/design/data_models.md` §1:
- 棋盘: GRID_COLS=8, GRID_ROWS=8, CELL_SIZE=72, CELL_SPACING=4, BOARD_OFFSET_X=40, BOARD_OFFSET_Y=120
- 水晶: NUM_CRYSTAL_TYPES=5
- 动画: SWAP_DURATION=0.2, SWAP_BACK_DURATION=0.15, CLEAR_DURATION=0.2, 等
- 分数: BASE_SCORE_3=30, BASE_SCORE_4=60, BASE_SCORE_5=100, 等
- 对象池: TILE_POOL_INITIAL=80
- 级联: MAX_CASCADE_LOOPS=20

使用 `const` 定义, 每个常量有注释说明。

### board_data.gd — TileData
参考 `docs/design/data_models.md` §3:
- 继承 `RefCounted`
- 属性: `crystal_type: int`, `special_type: int`, `row: int`, `col: int`, `is_empty: bool`, `is_locked: bool`, `lock_hp: int`
- 方法: `clear()`, `set_crystal(type, special)`, `is_normal()`, `is_special()`

### board_data.gd — BoardData
参考 `docs/design/data_models.md` §4:
- 继承 `RefCounted`
- 属性: `cols`, `rows`, `tiles: Array[TileData]` (一维数组), `num_crystal_types`
- 方法: `_init()`, `get_tile(row, col)`, `set_tile(row, col, tile)`, `index(row, col)`, `row_col(index)`, `is_in_bounds(row, col)`, `swap()`, `duplicate_data()`, `restore_from_data()`, `clear()`, `count_type()`, `get_empty_count()`

### match_detector.gd — 数据结构 (不含算法)
- `MatchGroup` (RefCounted): `shape: int`, `positions: Array[Vector2i]`, `pivot: Vector2i`, `match_length: int`, `crystal_type: int`
- `MatchResult` (RefCounted): `groups: Array[MatchGroup]`, `matched_flags: PackedByteArray`, `special_spawns: Array[SpecialSpawn]`, `total_matched: int`, `has_matches()`, `get_all_positions()`
- `SpecialSpawn` (RefCounted): `position: Vector2i`, `special_type: int`, `crystal_type: int`

## 验收标准
- 所有 enum 在 `enums.gd` 中定义, 值正确
- 所有常量在 `constants.gd` 中定义, 注释清晰
- `TileData` 类完整, 包含所有属性和方法
- `BoardData` 类完整, 一维数组正确索引
- `MatchGroup`, `MatchResult`, `SpecialSpawn` 数据结构完整

## 注意
- Task 02 只定义**数据结构**, 不包含算法逻辑 (MatchDetector 算法在 Task 03)
- 所有代码使用 GDScript, 符合 Godot 4.x 语法
- 不要添加 `class_name` 之外的 Godot 节点引用 (保持纯数据)
