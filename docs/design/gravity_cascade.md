# 重力与级联系统设计

> 定义方块消除后的下落、填充和级联循环逻辑。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/gravity.md](../research/gravity.md) — 下落算法、动画时序 |
| ← 研究 | [research/cascade.md](../research/cascade.md) — 级联循环、连击倍率 |
| ↔ 同级 | [match_system.md](match_system.md) — 匹配检测触发下落 |
| ↔ 同级 | [state_machine.md](state_machine.md) — 级联循环在状态机中的位置 |
| ↔ 同级 | [input_animation.md](input_animation.md) — 下落/spawn 动画实现 |
| → 任务 | [Task 04](../task/04_gravity_spawn.md) — GravitySystem + SpawnSystem 实现 |
| → 任务 | [Task 08](../task/08_state_machine.md) — 在状态机中集成级联循环 |

---

---

## 目录

1. [重力系统](#1-重力系统)
2. [方块生成系统](#2-方块生成系统)
3. [级联循环](#3-级联循环)
4. [与状态机的集成](#4-与状态机的集成)

---

## 1. 重力系统

### 1.1 设计原则

重力作用于每一**列**，从上到下 (row 0→7) 依次处理。方块下落时，所有空格被上方方块填补，最终所有空位移到列顶部。

```
交换 → 消除后:          重力处理后:           生成后:
┌───┬───┬───┐          ┌───┬───┬───┐        ┌───┬───┬───┐
│ A │ B │ C │          │   │   │   │        │ F │ D │ J │
├───┼───┼───┤          ├───┼───┼───┤        ├───┼───┼───┤
│   │ B │ C │          │ A │ B │ C │        │ A │ B │ C │
├───┼───┼───┤    →     ├───┼───┼───┤   →    ├───┼───┼───┤
│   │ D │   │          │   │ D │   │        │ E │ D │ H │
├───┼───┼───┤          ├───┼───┼───┤        ├───┼───┼───┤
│ E │   │   │          │   │   │   │        │ G │   │ K │
└───┴───┴───┘          └───┴───┴───┘        └───┴───┴───┘

 清除: A(0,0), B(0,1), D(3,1)    下落: A从row0→row1      生成: F-G 在col0
                                    B从row1→row2           D在col1
                                    E从row3→row0           J-K在col2
```

### 1.2 GravitySystem 实现

```gdscript
# scripts/core/gravity_system.gd

class_name GravitySystem
extends RefCounted


## 一键执行重力: 每列独立处理, 返回每个方块的下落信息
static func apply_gravity(board: BoardData) -> Array[FallInfo]:
    var falls: Array[FallInfo] = []
    
    for col in range(board.cols):
        falls.append_array(_process_column(board, col))
    
    return falls


## 处理单列下落
static func _process_column(board: BoardData, col: int) -> Array[FallInfo]:
    var column_falls: Array[FallInfo] = []
    
    # 从底部向上扫描, 将非空格子移到最底部
    var write_row := board.rows - 1  # 写入指针, 从底部开始
    
    for read_row in range(board.rows - 1, -1, -1):
        var tile := board.get_tile(read_row, col)
        
        if not tile.is_empty:
            if read_row != write_row:
                # 记录下落信息
                var info := FallInfo.new()
                info.from_row = read_row
                info.to_row = write_row
                info.col = col
                info.tile_data = tile
                column_falls.append(info)
                
                # 数据层移动
                board.swap(read_row, col, write_row, col)
            
            write_row -= 1
    
    # 标记 write_row 之上的所有格子为空
    for row in range(write_row, -1, -1):
        board.get_tile(row, col).clear()
    
    return column_falls
```

### 1.3 FallInfo 数据结构

```gdscript
# scripts/core/gravity_system.gd

class_name FallInfo
extends RefCounted

## 起始行
var from_row: int = -1

## 目标行
var to_row: int = -1

## 列
var col: int = -1

## 下落距离 (行数)
var distance: int:
    get:
        return to_row - from_row

## 对应的 tile 数据引用
var tile_data: TileData = null

## 建议的动画时长
func get_duration() -> float:
    return FALL_DURATION_BASE + distance * FALL_DURATION_PER_ROW
    # 例如: 0.1 + 3 * 0.08 = 0.34s
```

---

## 2. 方块生成系统

### 2.1 SpawnSystem 实现

```gdscript
# scripts/core/spawn_system.gd

class_name SpawnSystem
extends RefCounted


## 填充所有空位, 返回新生方块列表
static func fill_empty(board: BoardData) -> Array[SpawnInfo]:
    var spawns: Array[SpawnInfo] = []
    var rng := RandomNumberGenerator.new()
    rng.randomize()
    
    for col in range(board.cols):
        # 从顶向下找到所有空格
        var empty_rows: Array[int] = []
        for row in range(board.rows):
            var tile := board.get_tile(row, col)
            if tile.is_empty:
                empty_rows.append(row)
        
        if empty_rows.is_empty():
            continue
        
        # 空格有 n 个, 从底部向上填充 (row 大的先填, 因为它们在下方)
        # 实际上是: 最下面的空格应该用"从上方掉下来的最下方"方块填充
        # 简单方法: 批量生成后用 gravity 再处理一次
        # 更好方法: 空格从下往上, 新方块也从下往上分配
        
        # 从顶部开始填充 (row 小的先填)
        for row in empty_rows:
            var tile := board.get_tile(row, col)
            var crystal_type := rng.randi() % board.num_crystal_types
            tile.set_crystal(crystal_type)
            tile.row = row
            tile.col = col
            
            var info := SpawnInfo.new()
            info.row = row
            info.col = col
            info.crystal_type = crystal_type
            spawns.append(info)
    
    return spawns
```

### 2.2 SpawnInfo 数据结构

```gdscript
class_name SpawnInfo
extends RefCounted

var row: int = -1        # 目标行
var col: int = -1        # 目标列
var crystal_type: int = 0

# 建议的动画入场偏移 (从屏幕外上方)
func get_enter_offset() -> Vector2:
    return Vector2(
        col * (CELL_SIZE + CELL_SPACING) + BOARD_OFFSET_X + CELL_SIZE / 2.0,
        -CELL_SIZE  # 从屏幕上方外进入
    )
```

---

## 3. 级联循环

### 3.1 基本流程

```
一次有效交换
    │
    ▼
┌─────────────────────────────────────┐
│             级联循环开始              │
│                                     │
│  combo = 1                          │
│  while true:                        │
│      matches = detect_all(board)    │
│      if not matches: break          │
│                                     │
│      → 生成特殊水晶                    │
│      → 播放消除动画 (await)           │
│      → 清除已匹配方块                  │
│      → 应用重力 (await 动画)           │
│      → 填充空位 (await 动画)           │
│      → combo += 1                    │
│                                     │
│  循环结束 → 检查死局 → IDLE           │
└─────────────────────────────────────┘
```

### 3.2 关键安全措施

```gdscript
# 级联循环保护
const MAX_CASCADE_LOOPS := 20

var cascade_count := 0

while cascade_count < MAX_CASCADE_LOOPS:
    var result := MatchDetector.detect_all(board)
    if not result.has_matches():
        break
    
    cascade_count += 1
    
    # ... 处理消除/下落/填充 ...
    
    if not is_inside_tree():
        return  # 节点已销毁, 安全退出
```

### 3.3 连锁分数倍率

```gdscript
# 在 GameData 中
func add_score(points: int, combo: int) -> void:
    var multiplier := combo  # 连锁 N → 倍率 ×N
    var final_points := points * multiplier
    current_score += final_points
```

---

## 4. 与状态机的集成

级联循环与状态机紧密配合:

```gdscript
# scripts/game/state_machine.gd 中的级联处理

func _run_cascade() -> void:
    var cascade_depth := 0
    
    while cascade_depth < MAX_CASCADE_LOOPS:
        var result := MatchDetector.detect_all(board_data)
        
        if not result.has_matches():
            # 级联结束, 检查死局
            current_state = GameState.CHECK_VALID
            return
        
        cascade_depth += 1
        
        # CLEARING: 处理特殊水晶 + 播放消除动画
        current_state = GameState.CLEARING
        await _do_clearing(result)
        
        # FALLING: 重力下落
        current_state = GameState.FALLING
        await _do_falling()
        
        # SPAWNING: 填充新方块
        current_state = GameState.SPAWNING
        await _do_spawning()
        
        # CASCADE_CHECK: 信号通知
        EventBus.cascade_triggered.emit(cascade_depth)
        
        # 循环回到顶部 → 重新检测匹配
    
    # 超出最大级联次数, 强制回 IDLE
    current_state = GameState.IDLE
```

### 状态转换路径

```
SWAPPING → CHECKING_MATCHES → CLEARING → FALLING → SPAWNING
                                                       │
                                               ┌───────┘
                                               ▼
                                        CASCADE_CHECK
                                        │           │
                                    有匹配?       无匹配?
                                        │           │
                                   CLEARING      CHECK_VALID
                                   (循环)         │
                                              有移动?  无?
                                                 │     │
                                               IDLE  RESHUFFLE
                                                        │
                                                      IDLE
```

---

## 附录: 与动画系统的接口

级联系统与 `AnimationController` 的协作接口：

```gdscript
# GravitySystem 返回 → AnimationController 消费
func _do_falling() -> void:
    var falls: Array[FallInfo] = GravitySystem.apply_gravity(board_data)
    
    if falls.is_empty():
        return  # 无下落, 跳过动画
    
    EventBus.tiles_cleared.emit(falls)
    # AnimationController 监听此信号, 执行 Tween + await finished

# SpawnSystem 返回 → AnimationController 消费
func _do_spawning() -> void:
    var spawns: Array[SpawnInfo] = SpawnSystem.fill_empty(board_data)
    
    if spawns.is_empty():
        return
    
    # 为新生成的 tile 创建节点
    tile_manager.create_tiles_from_spawns(spawns)
    # AnimationController 播放入场动画 + await
```
