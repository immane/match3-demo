# 匹配检测系统设计

> 实现两阶段匹配检测算法, 在 Godot 中作为纯数据逻辑类运行。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/03_match_algorithm.md](../research/03_match_algorithm.md) — 线性扫描、Flood-Fill、Bitboard 对比 |
| ← 研究 | [research/07_random_generation.md](../research/07_random_generation.md) — 死局检测算法 |
| ↔ 同级 | [data_models.md](data_models.md) — MatchResult、MatchGroup 数据结构 |
| ↔ 同级 | [gravity_cascade.md](gravity_cascade.md) — 匹配结果驱动级联循环 |
| → 任务 | [Task 03](../task/03_match_detector.md) — MatchDetector + ValidMoveChecker 实现 |

---

---

## 目录

1. [算法概述](#1-算法概述)
2. [阶段一: 线性扫描](#2-阶段一-线性扫描)
3. [阶段二: 连通分组与形状分类](#3-阶段二-连通分组与形状分类)
4. [特殊水晶生成判定](#4-特殊水晶生成判定)
5. [完整 MatchDetector 实现](#5-完整-matchdetector-实现)
6. [死局检测](#6-死局检测)

---

## 1. 算法概述

采用**两阶段混合法**（来自 research/03_match_algorithm.md §6.4）：

```
输入: BoardData
      │
      ▼
┌──────────────────────┐
│ 阶段1: 线性扫描       │  O(rows × cols)
│  ├── detect_horizontal │  逐行扫描 ≥3 连续
│  ├── detect_vertical   │  逐列扫描 ≥3 连续
│  └── 填充 matched_flags │  PackedByteArray
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ 阶段2: 连通分组       │  O(已匹配格子数)
│  ├── flood_fill 分组  │  迭代式 BFS
│  ├── 形状分类         │  基于行列分布特征
│  └── 生成特殊水晶判定  │  4→炸弹, 5+→彩虹, L/T→十字
└──────────┬───────────┘
           │
           ▼
输出: MatchResult
```

---

## 2. 阶段一: 线性扫描

### 2.1 水平扫描

```gdscript
# scripts/core/match_detector.gd

class_name MatchDetector
extends RefCounted


## 检测所有水平匹配 (>= 3 连)
static func detect_horizontal(board: BoardData) -> Array[MatchGroup]:
    var groups: Array[MatchGroup] = []
    
    for row in range(board.rows):
        var col := 0
        while col < board.cols:
            var tile := board.get_tile(row, col)
            
            # 跳过空格
            if tile.is_empty:
                col += 1
                continue
            
            var start_col := col
            var run_length := 1
            var crystal_type := tile.crystal_type
            
            # 向右扩展, 寻找相同颜色
            while col + run_length < board.cols:
                var next := board.get_tile(row, col + run_length)
                if next.is_empty or next.crystal_type != crystal_type:
                    break
                run_length += 1
            
            # >= 3 连 → 记录匹配
            if run_length >= 3:
                var group := MatchGroup.new()
                group.shape = MatchShape.H_LINE
                group.crystal_type = crystal_type
                group.match_length = run_length
                
                for c in range(run_length):
                    group.positions.append(Vector2i(row, start_col + c))
                
                groups.append(group)
            
            col = start_col + run_length
    
    return groups
```

### 2.2 垂直扫描

```gdscript
## 检测所有垂直匹配 (>= 3 连)
static func detect_vertical(board: BoardData) -> Array[MatchGroup]:
    var groups: Array[MatchGroup] = []
    
    for col in range(board.cols):
        var row := 0
        while row < board.rows:
            var tile := board.get_tile(row, col)
            
            if tile.is_empty:
                row += 1
                continue
            
            var start_row := row
            var run_length := 1
            var crystal_type := tile.crystal_type
            
            while row + run_length < board.rows:
                var next := board.get_tile(row + run_length, col)
                if next.is_empty or next.crystal_type != crystal_type:
                    break
                run_length += 1
            
            if run_length >= 3:
                var group := MatchGroup.new()
                group.shape = MatchShape.V_LINE
                group.crystal_type = crystal_type
                group.match_length = run_length
                
                for r in range(run_length):
                    group.positions.append(Vector2i(start_row + r, col))
                
                groups.append(group)
            
            row = start_row + run_length
    
    return groups
```

---

## 3. 阶段二: 连通分组与形状分类

### 3.1 构建标记矩阵 + 连通分组

```gdscript
## 主检测方法: 返回完整 MatchResult
static func detect_all(board: BoardData) -> MatchResult:
    var result := MatchResult.new()
    result.matched_flags.resize(board.rows * board.cols)
    result.matched_flags.fill(0)
    
    # ---- 阶段1: 线性扫描 ----
    var h_groups := detect_horizontal(board)
    var v_groups := detect_vertical(board)
    
    # 填充标记矩阵
    _mark_positions(result.matched_flags, h_groups, board.cols)
    _mark_positions(result.matched_flags, v_groups, board.cols)
    
    # ---- 阶段2: 连通分组 ----
    var visited := PackedByteArray()
    visited.resize(board.rows * board.cols)
    visited.fill(0)
    
    for i in range(result.matched_flags.size()):
        if result.matched_flags[i] == 1 and visited[i] == 0:
            var region := _flood_fill(result.matched_flags, visited, i, board)
            if region.size() >= 3:
                var group := _classify_shape(region, board)
                result.groups.append(group)
    
    result.total_matched = _count_ones(result.matched_flags)
    
    # ---- 生成特殊水晶 ----
    for group in result.groups:
        var spawn := _determine_special(group)
        if spawn != null:
            result.special_spawns.append(spawn)
    
    return result


static func _mark_positions(flags: PackedByteArray, 
                             groups: Array[MatchGroup], cols: int) -> void:
    for group in groups:
        for pos in group.positions:
            flags[pos.x * cols + pos.y] = 1


static func _count_ones(flags: PackedByteArray) -> int:
    var count := 0
    for f in flags:
        if f == 1:
            count += 1
    return count
```

### 3.2 迭代式 Flood-Fill

```gdscript
## 迭代式 BFS Flood-Fill (避免递归栈溢出)
static func _flood_fill(flags: PackedByteArray, visited: PackedByteArray,
                         start_idx: int, board: BoardData) -> Array[Vector2i]:
    var region: Array[Vector2i] = []
    var stack: Array[int] = [start_idx]
    var cols := board.cols
    var rows := board.rows
    
    while not stack.is_empty():
        var idx = stack.pop_back()
        
        if idx < 0 or idx >= flags.size():
            continue
        if visited[idx] == 1 or flags[idx] == 0:
            continue
        
        visited[idx] = 1
        region.append(Vector2i(idx / cols, idx % cols))
        
        # 4 方向扩展
        var r := idx / cols
        var c := idx % cols
        
        # 上
        if r > 0:
            stack.append(idx - cols)
        # 下
        if r < rows - 1:
            stack.append(idx + cols)
        # 左
        if c > 0:
            stack.append(idx - 1)
        # 右
        if c < cols - 1:
            stack.append(idx + 1)
    
    return region
```

### 3.3 形状分类

```gdscript
## 根据连通区域的位置特征判定形状
static func _classify_shape(region: Array[Vector2i], 
                             board: BoardData) -> MatchGroup:
    var group := MatchGroup.new()
    group.positions = region
    
    # 统计行分布和列分布
    var row_set := {}  # Dictionary[row, count]
    var col_set := {}
    var min_row := 999
    var max_row := -1
    var min_col := 999
    var max_col := -1
    
    for pos in region:
        row_set[pos.x] = row_set.get(pos.x, 0) + 1
        col_set[pos.y] = col_set.get(pos.y, 0) + 1
        min_row = mini(min_row, pos.x)
        max_row = maxi(max_row, pos.x)
        min_col = mini(min_col, pos.y)
        max_col = maxi(max_col, pos.y)
    
    var h_span := max_col - min_col + 1
    var v_span := max_row - min_row + 1
    var total := region.size()
    var pivot := Vector2i(-1, -1)
    
    # --- 直线判定 ---
    if row_set.size() == 1:
        group.shape = MatchShape.H_LINE
        group.match_length = col_set.size()
    elif col_set.size() == 1:
        group.shape = MatchShape.V_LINE
        group.match_length = row_set.size()
    else:
        # 查找交叉点 (同时属于横向和纵向匹配的方块)
        var h_match_set := {}
        var v_match_set := {}
        
        # 找最大行
        var max_row_count := 0
        var max_row_idx := -1
        for r in row_set.keys():
            var cnt: int = row_set[r]
            if cnt > max_row_count:
                max_row_count = cnt
                max_row_idx = r
        
        # 找最大列
        var max_col_count := 0
        var max_col_idx := -1
        for c in col_set.keys():
            var cnt: int = col_set[c]
            if cnt > max_col_count:
                max_col_count = cnt
                max_col_idx = c
        
        # 交叉点 = 最大行和最大列的交点
        var cross_exists := false
        for pos in region:
            if pos.x == max_row_idx and pos.y == max_col_idx:
                pivot = pos
                cross_exists = true
                break
        
        if not cross_exists:
            pivot = region[total / 2]  # fallback
        
        group.pivot = pivot
        
        # 统计交叉点四个方向的长度
        var up_count := 0
        var down_count := 0
        var left_count := 0
        var right_count := 0
        
        for pos in region:
            if pos.x == pivot.x and pos.y < pivot.y:
                left_count += 1
            elif pos.x == pivot.x and pos.y > pivot.y:
                right_count += 1
            elif pos.y == pivot.y and pos.x < pivot.x:
                up_count += 1
            elif pos.y == pivot.y and pos.x > pivot.x:
                down_count += 1
        
        # --- 形状判定 ---
        # + 形: 四个方向各 >= 1
        if up_count >= 1 and down_count >= 1 and left_count >= 1 and right_count >= 1:
            group.shape = MatchShape.CROSS
        
        # T 形: 一条线贯穿 + 另一条线从交叉点出发单侧
        elif (left_count >= 1 and right_count >= 1) and (up_count >= 2 or down_count >= 2):
            group.shape = MatchShape.T_SHAPE
        elif (up_count >= 1 and down_count >= 1) and (left_count >= 2 or right_count >= 2):
            group.shape = MatchShape.T_SHAPE
        
        # L 形: 不是一个方向贯穿
        else:
            group.shape = MatchShape.L_SHAPE
        
        group.match_length = total
    
    # 获取水晶颜色 (取任意位置的类型)
    var sample_pos := region[0]
    group.crystal_type = board.get_tile(sample_pos.x, sample_pos.y).crystal_type
    
    return group
```

---

## 4. 特殊水晶生成判定

```gdscript
## 根据匹配组判定生成什么特殊水晶, 返回 SpecialSpawn 或 null
static func _determine_special(group: MatchGroup) -> SpecialSpawn:
    if group.match_length < 4:
        return null
    
    var spawn := SpecialSpawn.new()
    spawn.crystal_type = group.crystal_type
    
    match group.shape:
        MatchShape.H_LINE, MatchShape.V_LINE:
            if group.match_length == 4:
                spawn.special_type = SpecialType.BOMB
            elif group.match_length >= 5:
                spawn.special_type = SpecialType.RAINBOW
            else:
                return null
            # 生成在匹配中点
            spawn.position = group.positions[group.match_length / 2]
        
        MatchShape.L_SHAPE, MatchShape.T_SHAPE, MatchShape.CROSS:
            spawn.special_type = SpecialType.CROSS
            spawn.position = group.pivot
        
        _:
            return null
    
    return spawn
```

### 生成位置表

| 形状 | 数量 | 特殊类型 | 生成位置 |
|------|------|---------|---------|
| H_LINE / V_LINE | 4 | BOMB | 匹配段中点 |
| H_LINE / V_LINE | 5+ | RAINBOW | 匹配段中点 |
| L_SHAPE | 5+ | CROSS | 交叉点 (pivot) |
| T_SHAPE | 5+ | CROSS | 交叉点 (pivot) |
| CROSS | 5+ | CROSS | 交叉点 (pivot) |

---

## 5. 完整 MatchDetector 实现

`MatchDetector` 类整合所有逻辑, 对外提供单一入口：

```gdscript
# scripts/core/match_detector.gd (完整文件结构)

class_name MatchDetector
extends RefCounted


static func detect_all(board: BoardData) -> MatchResult:
    # [阶段1+2 完整实现, 如上所示]


static func detect_horizontal(board: BoardData) -> Array[MatchGroup]:
    # [水平扫描实现]


static func detect_vertical(board: BoardData) -> Array[MatchGroup]:
    # [垂直扫描实现]


static func _mark_positions(...):
    # [标记矩阵填充]


static func _flood_fill(...) -> Array[Vector2i]:
    # [迭代 BFS]


static func _classify_shape(...) -> MatchGroup:
    # [形状分类]


static func _determine_special(group: MatchGroup) -> SpecialSpawn:
    # [特殊水晶判定]


static func _count_ones(flags: PackedByteArray) -> int:
    # [计数]
```

**使用方式**：

```gdscript
var board: BoardData = ...
var result: MatchResult = MatchDetector.detect_all(board)

if result.has_matches():
    print("消除 %d 个方块, %d 个特殊水晶生成" % 
          [result.total_matched, result.special_spawns.size()])
```

---

## 6. 死局检测

```gdscript
# scripts/core/valid_move_checker.gd

class_name ValidMoveChecker
extends RefCounted


## 检测棋盘是否存在至少一个有效交换
func has_any_valid_move(board: BoardData) -> bool:
    var cols := board.cols
    var rows := board.rows
    
    for row in range(rows):
        for col in range(cols):
            # 尝试向右交换
            if col < cols - 1:
                if _would_match(board, row, col, row, col + 1):
                    return true
            
            # 尝试向下交换
            if row < rows - 1:
                if _would_match(board, row, col, row + 1, col):
                    return true
    
    return false


## 模拟交换并检测是否产生匹配
func _would_match(board: BoardData, 
                   r1: int, c1: int, r2: int, c2: int) -> bool:
    var t1 := board.get_tile(r1, c1)
    var t2 := board.get_tile(r2, c2)
    
    if t1.is_empty or t2.is_empty:
        return false
    
    # 模拟交换
    board.swap(r1, c1, r2, c2)
    
    # 快速检测受影响区域 (只扫描涉及的行列)
    var has_match := _quick_check(board, r1, c1) or _quick_check(board, r2, c2)
    
    # 换回
    board.swap(r1, c1, r2, c2)
    
    return has_match


## 快速检测指定行/列是否存在 ≥3 连
func _quick_check(board: BoardData, row: int, col: int) -> bool:
    # 检查该行
    var current_type := -1
    var run := 0
    for c in range(board.cols):
        var tile := board.get_tile(row, c)
        var t := tile.crystal_type if not tile.is_empty else -2
        if t == current_type and t >= 0:
            run += 1
            if run >= 3:
                return true
        else:
            current_type = t
            run = 1
    
    # 检查该列
    current_type = -1
    run = 0
    for r in range(board.rows):
        var tile := board.get_tile(r, col)
        var t := tile.crystal_type if not tile.is_empty else -2
        if t == current_type and t >= 0:
            run += 1
            if run >= 3:
                return true
        else:
            current_type = t
            run = 1
    
    return false
```

**复杂度**：
- 8×8 棋盘: 最多 112 个可能的交换 (7×8 水平 + 7×8 垂直)
- 每次交换只需要扫描 2 行 + 2 列 = 32 个格子
- 总操作量: 112 × 32 ≈ 3584 次比较, 毫秒级完成
