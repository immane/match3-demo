# Match-3 游戏消除检测算法研究报告

> 本文档系统性地研究 Match-3（三消）游戏中的匹配检测算法，涵盖基础检测、复杂形状识别、数据结构优化、复杂度分析及伪代码实现。

---

## 目录

1. [核心概念与数据结构](#1-核心概念与数据结构)
2. [水平匹配检测](#2-水平匹配检测)
3. [垂直匹配检测](#3-垂直匹配检测)
4. [组合检测与去重策略](#4-组合检测与去重策略)
5. [特殊形状检测](#5-特殊形状检测)
6. [Flood-Fill 检测 vs 线性扫描检测](#6-flood-fill-检测-vs-线性扫描检测)
7. [算法复杂度分析](#7-算法复杂度分析)
8. [优化数据结构](#8-优化数据结构)
9. [4连消与5连消的识别与特殊方块生成](#9-4连消与5连消的识别与特殊方块生成)
10. [匹配结果存储设计](#10-匹配结果存储设计)
11. [完整伪代码与算法流程图](#11-完整伪代码与算法流程图)
12. [参考文献](#12-参考文献)

---

## 1. 核心概念与数据结构

### 1.1 基础网格表示

Match-3 游戏通常采用二维网格表示游戏面板。最常用的数据结构：

```
// 方案 A: 二维数组（直观，适合 Godot/GDScript）
var grid: Array[Array]  # grid[row][col] = tile_type

// 方案 B: 一维平坦数组（缓存友好，性能更优）
var grid: Array[int]  # grid[row * width + col] = tile_type

// 方案 C: 带状态的结构体数组
struct Cell:
    tile_type: int        # 方块类型/颜色
    state: int            # 状态: NORMAL, MATCHED, FALLING, EMPTY
    is_matched: bool      # 当前帧是否被匹配标记
```

**推荐方案**：对于 Godot 项目，使用一维平坦数组（`PackedInt32Array` 或 `Array[int]`）以获得最佳性能，尤其是在需要频繁扫描的 8×8 或更大网格上。

### 1.2 方向定义

```
方向向量（仅上下左右，不含对角线）:
  UP    = ( 0, -1)
  DOWN  = ( 0,  1)
  LEFT  = (-1,  0)
  RIGHT = ( 1,  0)
```

---

## 2. 水平匹配检测

### 2.1 逐行线性扫描算法

核心思想：对每一行从左到右扫描，统计连续相同颜色的方块数量。

#### 伪代码

```
function detect_horizontal_matches(grid, width, height) -> List[MatchGroup]:
    matches = []

    for row in 0..height-1:
        col = 0
        while col < width:
            current_type = grid[row][col]
            if current_type == EMPTY:
                col += 1
                continue

            run_start = col
            run_length = 1

            // 向右扩展，寻找连续相同方块
            while col + run_length < width
                  AND grid[row][col + run_length] == current_type:
                run_length += 1

            // 判定是否为有效匹配（长度 >= 3）
            if run_length >= 3:
                match_positions = []
                for i in 0..run_length-1:
                    match_positions.append(Position(run_start + i, row))
                matches.append(MatchGroup(
                    type = HORIZONTAL,
                    positions = match_positions,
                    length = run_length
                ))

            col = run_start + run_length  // 跳过已扫描区域

    return matches
```

#### 算法分析

| 属性 | 值 |
|------|-----|
| 时间复杂度 | O(W × H) = O(N) |
| 空间复杂度 | O(N)，存储匹配结果 |
| 优点 | 实现简单，每格仅访问一次 |
| 缺点 | 天然只能检测直线匹配，无法直接检测交叉形状 |

### 2.2 滑动窗口优化

对于更大网格，可使用滑动窗口减少比较次数：

```
function detect_horizontal_sliding_window(grid, row) -> List[MatchGroup]:
    matches = []
    col = 0

    while col <= width - 3:  // 至少需要3格空间
        if grid[row][col] == EMPTY:
            col += 1
            continue

        if grid[row][col] == grid[row][col+1] == grid[row][col+2]:
            // 找到至少3连，继续向右扩展
            end = col + 3
            while end < width AND grid[row][end] == grid[row][col]:
                end += 1
            // 记录匹配
            matches.append(make_group(row, col, end - col))
            col = end
        else:
            col += 1

    return matches
```

---

## 3. 垂直匹配检测

### 3.1 逐列线性扫描算法

与水平检测对称，按列从上到下扫描：

```
function detect_vertical_matches(grid, width, height) -> List[MatchGroup]:
    matches = []

    for col in 0..width-1:
        row = 0
        while row < height:
            current_type = grid[row][col]
            if current_type == EMPTY:
                row += 1
                continue

            run_start = row
            run_length = 1

            while row + run_length < height
                  AND grid[row + run_length][col] == current_type:
                run_length += 1

            if run_length >= 3:
                match_positions = []
                for i in 0..run_length-1:
                    match_positions.append(Position(col, run_start + i))
                matches.append(MatchGroup(
                    type = VERTICAL,
                    positions = match_positions,
                    length = run_length
                ))

            row = run_start + run_length

    return matches
```

---

## 4. 组合检测与去重策略

### 4.1 问题描述

当一个方块同时属于水平匹配和垂直匹配时（如 L 形、T 形、+ 形的交叉点），简单的合并会导致该方块被重复计数。

### 4.2 位标记去重法

使用一个布尔矩阵（visited/matched 标记）来追踪已匹配的方块：

```
function detect_all_matches(grid, width, height) -> MatchResult:
    // 创建标记矩阵
    matched = 2D_bool_array(width, height)  // 全部初始化为 false

    // 第一步：分别检测水平和垂直匹配，标记位置
    h_matches = detect_horizontal_matches(grid)
    for each match in h_matches:
        for each pos in match.positions:
            matched[pos.y][pos.x] = true

    v_matches = detect_vertical_matches(grid)
    for each match in v_matches:
        for each pos in match.positions:
            matched[pos.y][pos.x] = true

    // 第二步：识别交叉点（同时属于水平+垂直匹配的方块）
    cross_points = []
    for each h_match in h_matches:
        for each v_match in v_matches:
            intersection = find_intersections(h_match, v_match)
            if intersection is not empty:
                cross_points.append(intersection)

    // 第三步：合并匹配组
    merged_matches = merge_connected_groups(h_matches, v_matches)

    return MatchResult(
        all_matched_positions = matched,
        match_groups = merged_matches,
        cross_points = cross_points
    )
```

### 4.3 并查集合并法

对于复杂的交叉匹配，可以使用**并查集（Union-Find / Disjoint Set Union）**：

```
function merge_with_union_find(h_matches, v_matches) -> List[MatchGroup]:
    dsu = DisjointSetUnion()  // 每个位置初始为独立集合

    // 将同行/同列相邻的匹配位置合并到同一集合
    for each match in h_matches:
        for i in 0..match.length - 2:
            dsu.union(match.positions[i], match.positions[i+1])

    for each match in v_matches:
        for i in 0..match.length - 2:
            dsu.union(match.positions[i], match.positions[i+1])

    // 按集合根节点分组
    groups = {}
    for each pos in all_matched_positions:
        root = dsu.find(pos)
        groups[root].append(pos)

    // 构建最终的匹配组
    result = []
    for each (root, positions) in groups:
        if positions.size() >= 3:
            shape = classify_shape(positions)
            result.append(MatchGroup(positions, shape))

    return result
```

### 4.4 两阶段检测法（推荐）

这是工业界最常用的方法：

```
阶段 1: 线性扫描
  - 分别执行水平扫描和垂直扫描
  - 将匹配位置标记到布尔矩阵中
  - 记录每个匹配组的长度和方向

阶段 2: 形状分类
  - 遍历布尔矩阵中被标记的位置
  - 使用 Flood-Fill 将连通区域分组
  - 根据每组的位置特征判定形状类型
```

#### 两阶段法流程图

```
┌─────────────────┐
│   开始匹配检测   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  阶段1: 线性扫描  │
│  ┌─────────────┐ │
│  │ 水平扫描     │ │──► 获取水平匹配组
│  └─────────────┘ │
│  ┌─────────────┐ │
│  │ 垂直扫描     │ │──► 获取垂直匹配组
│  └─────────────┘ │
│  ┌─────────────┐ │
│  │ 合并标记矩阵  │ │──► 布尔矩阵 matched[H][W]
│  └─────────────┘ │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  阶段2: 形状分类  │
│  ┌─────────────┐ │
│  │ Flood-Fill   │ │──► 连通区域分组
│  │ 连通区域提取  │ │
│  └─────────────┘ │
│  ┌─────────────┐ │
│  │ 形状模板匹配  │ │──► 识别 L/T/+/直线
│  └─────────────┘ │
│  ┌─────────────┐ │
│  │ 生成特殊方块  │ │──► 4连消→火箭, 5连消→彩虹
│  └─────────────┘ │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  返回匹配结果    │
└─────────────────┘
```

---

## 5. 特殊形状检测

### 5.1 形状分类总览

| 形状 | 图案 | 最小消去数 | 特殊方块 | 检测方法 |
|------|------|-----------|---------|---------|
| 3连直线 | `XXX` | 3 | 无 | 线性扫描 |
| 4连直线 | `XXXX` | 4 | 条纹方块(火箭) | 线性扫描 + 长度判断 |
| 5连直线 | `XXXXX` | 5 | 彩虹方块(彩色炸弹) | 线性扫描 + 长度判断 |
| L 形 | `XX` / `X` | 5 | 炸弹方块 | 交叉点检测 |
| T 形 | `XXX` / ` X ` | 5 | 炸弹方块 | 交叉点检测 |
| + 形(十字) | ` X ` / `XXX` | 5 | 特殊炸弹 | 交叉点 + 方向计数 |

### 5.2 L 形检测

L 形特征：某个交叉点方块同时属于一条水平匹配和一条垂直匹配。

```
function detect_L_shape(h_matches, v_matches) -> List[MatchGroup]:
    l_shapes = []

    for each h_match in h_matches:
        for each v_match in v_matches:
            // 查找交叉点
            intersection = []
            for each h_pos in h_match.positions:
                for each v_pos in v_match.positions:
                    if h_pos == v_pos:
                        intersection.append(h_pos)

            if not intersection.empty():
                // 排除纯直线（交叉点在线段端点的情况）
                // L 形的交叉点应满足：水平段和垂直段在交叉点处各有至少2个方块
                total_positions = merge_unique(h_match.positions, v_match.positions)
                
                if total_positions.size() >= 5:
                    l_shapes.append(MatchGroup(
                        type = L_SHAPE,
                        positions = total_positions,
                        pivot = intersection[0]
                    ))

    return l_shapes
```

### 5.3 T 形检测

T 形特征：一个交叉点，一条方向有 3 个方块，另一方向只有 2 个方块分别在交叉点两侧各1个。

```
function detect_T_shape(h_matches, v_matches) -> List[MatchGroup]:
    t_shapes = []

    for each cross_point in cross_points:
        h_match = find_match_containing(cross_point, h_matches)
        v_match = find_match_containing(cross_point, v_matches)

        if not (h_match AND v_match):
            continue

        // T 形判断：水平方向长度 >= 3，且交叉点在垂直方向的中间
        h_tiles_left = count_tiles_left_of(cross_point, h_match)
        h_tiles_right = count_tiles_right_of(cross_point, h_match)
        v_tiles_up = count_tiles_above(cross_point, v_match)
        v_tiles_down = count_tiles_below(cross_point, v_match)

        is_T_horizontal = (h_tiles_left >= 1 AND h_tiles_right >= 1)
                          AND (v_tiles_up >= 2 OR v_tiles_down >= 2)

        is_T_vertical = (v_tiles_up >= 1 AND v_tiles_down >= 1)
                        AND (h_tiles_left >= 2 OR h_tiles_right >= 2)

        if is_T_horizontal OR is_T_vertical:
            t_shapes.append(MatchGroup(
                type = T_SHAPE,
                positions = merge(h_match.positions, v_match.positions),
                pivot = cross_point
            ))

    return t_shapes
```

### 5.4 + 形（十字形）检测

+ 形特征：交叉点在上、下、左、右四个方向各有至少 1 个方块，且总匹配数 >= 5。

```
function detect_cross_shape(h_matches, v_matches) -> List[MatchGroup]:
    crosses = []

    for each cross_point in cross_points:
        h_match = find_match_containing(cross_point, h_matches)
        v_match = find_match_containing(cross_point, v_matches)

        if not (h_match AND v_match): continue

        h_tiles_left = count_tiles_left_of(cross_point, h_match)
        h_tiles_right = count_tiles_right_of(cross_point, h_match)
        v_tiles_up = count_tiles_above(cross_point, v_match)
        v_tiles_down = count_tiles_below(cross_point, v_match)

        // + 形：四个方向各至少 1 个
        if (h_tiles_left >= 1 AND h_tiles_right >= 1
            AND v_tiles_up >= 1 AND v_tiles_down >= 1
            AND (h_tiles_left + h_tiles_right + v_tiles_up + v_tiles_down + 1) >= 5):

            crosses.append(MatchGroup(
                type = CROSS_SHAPE,
                positions = merge(h_match.positions, v_match.positions),
                pivot = cross_point
            ))

    return crosses
```

### 5.5 通用形状识别 —— Flood-Fill 后模板匹配

最通用的方法：先用 Flood-Fill 获得连通区域，再归一化后与形状模板对比。

```
function classify_shape_by_flood_fill(positions) -> ShapeType:
    // 步骤1: 归一化坐标（减去最小坐标）
    min_x = min(p.x for p in positions)
    min_y = min(p.y for p in positions)
    normalized = set()
    for p in positions:
        normalized.insert((p.x - min_x, p.y - min_y))

    // 步骤2: 统计行列分布特征
    row_counts = {}  // 每行有多少个方块
    col_counts = {}  // 每列有多少个方块
    for (x, y) in normalized:
        row_counts[y] = row_counts.get(y, 0) + 1
        col_counts[x] = col_counts.get(x, 0) + 1

    total = positions.size()

    // 步骤3: 根据特征判定
    if row_counts.size() == 1: return HORIZONTAL_LINE   // 单行
    if col_counts.size() == 1: return VERTICAL_LINE     // 单列

    // 检测 L 形: 两条线段，一个交叉点
    if is_L_pattern(normalized): return L_SHAPE

    // 检测 T 形: 一条线段 + 一条垂直线段从中间交叉
    if is_T_pattern(normalized): return T_SHAPE

    // 检测 + 形: 交叉点在中心，四方向各至少一个
    if is_cross_pattern(normalized): return CROSS_SHAPE

    return COMPLEX_SHAPE
```

---

## 6. Flood-Fill 检测 vs 线性扫描检测

### 6.1 方法对比

| 维度 | 线性扫描 | Flood-Fill |
|------|---------|------------|
| **原理** | 逐行逐列扫描连续相同颜色 | 从种子点递归/迭代扩展连通区域 |
| **时间复杂度** | O(W × H) | O(W × H) |
| **实际常数因子** | 小（顺序访问，缓存友好） | 较大（递归/栈开销，随机访问） |
| **空间复杂度** | O(1) 额外空间（不计结果） | O(W × H) visited 数组 |
| **直线检测** | 原生支持，非常高效 | 也可检测，但冗余 |
| **复杂形状检测** | 需要后处理（两阶段法） | 原生支持连通区域 |
| **实现复杂度** | 简单 | 中等（递归版本简洁但要注意栈溢出） |
| **适用场景** | 仅需直线匹配的经典三消 | 需要任意形状匹配的游戏 |
| **缓存命中率** | 高（顺序扫描） | 低（跳跃访问） |

### 6.2 线性扫描详细分析

```
优点:
  1. 直接利用游戏规则（只有水平和垂直匹配），不做多余计算
  2. 顺序内存访问，CPU 缓存命中率高
  3. 实现极其简单，不易出错
  4. 对 8×8 网格，扫描 96 个可能位置（6×8×2），极快
  5. 可直接获取每条匹配的长度，便于区分 3/4/5 连消

缺点:
  1. 天然无法检测交叉形状，需要第二阶段处理
  2. 需要额外的去重逻辑
```

### 6.3 Flood-Fill 详细分析

```
优点:
  1. 能天然检测任意形状的连通区域
  2. 一次运行即可得到完整连通组
  3. 适合允许对角线匹配或任意形状匹配的变体

缺点:
  1. 需要维护 visited 标记数组
  2. 递归实现可能栈溢出（需要转迭代）
  3. 无法直接获取匹配方向信息
  4. 可能遗漏仅直线连通的匹配（因为本应在直线方向上）
  5. 实现更复杂，调试更难
```

### 6.4 推荐策略：两阶段混合法

在经典 Match-3 游戏中，**推荐混合使用**：

```
阶段1（线性扫描）：O(N)
  高效检测所有水平/垂直直线匹配，同时标记 matched[][] 布尔矩阵

阶段2（Flood-Fill 后处理）：仅对已标记区域运行 O(M)，M = 已匹配方块数
  将通过交叉点连接的水平匹配和垂直匹配合并为连通组
  对每个连通组进行形状分类
```

### 6.5 迭代式 Flood-Fill（避免递归栈溢出）

```
function flood_fill_iterative(matched_grid, start_x, start_y, width, height) -> List[Position]:
    if not matched_grid[start_y][start_x]:
        return []

    stack = [Position(start_x, start_y)]
    region = []

    while not stack.empty():
        pos = stack.pop()
        x, y = pos.x, pos.y

        if x < 0 OR x >= width OR y < 0 OR y >= height:
            continue
        if not matched_grid[y][x]:
            continue

        matched_grid[y][x] = false  // 消费标记，避免重复访问
        region.append(pos)

        // 4方向扩展
        stack.append(Position(x+1, y))
        stack.append(Position(x-1, y))
        stack.append(Position(x, y+1))
        stack.append(Position(x, y-1))

    return region
```

---

## 7. 算法复杂度分析

### 7.1 时间复杂度汇总

| 算法 | 最坏时间复杂度 | 平均时间复杂度 | 说明 |
|------|-------------|-------------|------|
| 水平扫描 | O(W × H) | O(W × H) | 每个格子访问1次 |
| 垂直扫描 | O(W × H) | O(W × H) | 每个格子访问1次 |
| 完整线性双扫 | O(W × H) | O(W × H) | 每个格子访问2次 |
| Flood-Fill | O(W × H) | O(M)，M = 匹配方块数 | 仅访问匹配区域 |
| 形状分类 | O(K)，K = 匹配组数 | O(K) | 常数级 |
| **两阶段混合法** | **O(W × H)** | **O(W × H)** | 实际最优 |

### 7.2 空间复杂度

| 结构 | 空间 | 说明 |
|------|------|------|
| 网格存储 | O(W × H) | 必需 |
| 匹配标记矩阵 | O(W × H) | 布尔数组或位图 |
| 匹配组列表 | O(M)，M ≤ W × H | 最坏情况全匹配 |
| Flood-Fill 栈 | O(W × H) | 最坏情况全网格 |

### 7.3 不同网格规模的实际性能估算

对于 8×8 标准网格（64 个格子）：

| 操作 | 估算开销 |
|------|---------|
| 水平扫描 | ~64 次比较 + ~48 次边界检查 |
| 垂直扫描 | ~64 次比较 + ~48 次边界检查 |
| 完整检测 | < 300 次基本操作 |
| 实际耗时 | **< 0.1 ms**（现代硬件） |

```
结论: 对于标准 8×8 网格，暴力扫描完全足够。
      除非在每帧运行数千次检测，无需过度优化。
```

### 7.4 典型游戏中的调用频率

```
游戏循环:
  玩家交换 2 个方块
    → 调用 detect_matches()           # 1次，~0.05ms
    → 消除匹配方块，播放动画           # ~250ms
    → 方块下落，填充新方块             # ~200ms
    → 调用 detect_matches()           # 1次，~0.05ms
    → (可能连锁，重复上述过程)          # 3-5次典型
  返回控制权给玩家

总计: 每个回合调用 2-10 次 detect_matches()
     总算法耗时 < 1ms，完全可忽略
```

---

## 8. 优化数据结构

### 8.1 Bitboard（位棋盘）表示

源自国际象棋编程的经典技巧，使用位运算加速检测。

```
// 每种颜色使用一个 64-bit 整数表示
// 对于 8×8 网格: 每行占 8 位，共 64 位
// bitboard[color][row] = 0b00111000  (第2-4列为该颜色)

// 检测水平 3 连:
function has_horizontal_3(bits):
    // (bits) AND (bits << 1) AND (bits << 2)
    // 如果结果非零，则存在3连
    return (bits & (bits << 1) & (bits << 2)) != 0

// 检测水平 4 连:
function has_horizontal_4(bits):
    return (bits & (bits << 1) & (bits << 2) & (bits << 3)) != 0

// 获取3连的起始位置:
function get_horizontal_3_starts(bits):
    return bits & (bits << 1) & (bits << 2)

// 示例: 对于行数据 0b00111000 (第2,3,4列为红色)
// bits        = 0b00111000
// bits << 1   = 0b01110000
// bits << 2   = 0b11100000
// AND 结果     = 0b00100000  → 起始于第2列
```

#### Bitboard 伪代码完整实现

```
const BOARD_WIDTH  = 8
const BOARD_HEIGHT = 8
const WORD_BITS    = 64

// 按颜色存储行位图和列位图
var row_bits: Array[int]   # row_bits[color][row] = bitmask
var col_bits: Array[int]   # col_bits[color][col] = bitmask

function update_bitboards(x, y, new_color):
    mask = 1 << (BOARD_WIDTH - 1 - x)  // 注意位序
    old_color = grid[y][x]

    if old_color != EMPTY:
        row_bits[old_color][y] &= ~mask
        col_bits[old_color][x] &= ~(1 << (BOARD_HEIGHT - 1 - y))

    if new_color != EMPTY:
        row_bits[new_color][y] |= mask
        col_bits[new_color][x] |= (1 << (BOARD_HEIGHT - 1 - y))

function detect_horizontal_bits(color) -> List[Match]:
    matches = []
    for row in 0..BOARD_HEIGHT-1:
        bits = row_bits[color][row]
        threes = bits & (bits << 1) & (bits << 2)
        while threes != 0:
            start = count_trailing_zeros(threes)  // CPU 指令: CTZ / BSF
            threes &= threes - 1  // 清除最低位
            // 扩展检测长度
            length = 3
            while (start + length < BOARD_WIDTH
                   AND (bits >> (BOARD_WIDTH - 1 - (start + length))) & 1):
                length += 1
            matches.append(HorizontalMatch(row, start, length, color))
    return matches
```

#### Bitboard 性能分析

| 操作 | 传统数组 | Bitboard | 加速比 |
|------|---------|----------|--------|
| 单行3连检测 | 6-8 次循环比较 | 3 条位运算指令 | ~10x |
| 全盘扫描 | 64 次元素访问 | 8 次 64-bit 操作 | ~5-8x |
| 内存占用 | 64×4 bytes = 256B | 8×8 bytes = 64B | 4x 节省 |

### 8.2 游程编码（Run-Length Encoding, RLE）

将每行/列表示为 (起始列, 长度, 颜色) 的三元组序列。

```
// 原始行: [R, R, R, G, G, B, B, B]
// RLE:    [(0, 3, R), (3, 2, G), (5, 3, B)]

// 匹配检测变为简单的长度检查:
function detect_from_rle(row_rle) -> List[Match]:
    matches = []
    for (start, length, color) in row_rle:
        if length >= 3:
            matches.append(Match(start, length, color))
    return matches
```

#### RLE 的利弊

| 优点 | 缺点 |
|------|------|
| 匹配检测变为 O(游程数)，远小于 O(W) | 每次移动需要更新 RLE |
| 适合静态或低频变化的场景 | 连锁消除时 RLE 维护成本高 |
| 天然存储连续匹配信息 | 额外内存开销 |

### 8.3 增量检测（仅在变更区域检测）

最实用的优化：不扫描整个棋盘，仅检测受影响的区域。

```
function detect_incremental(swapped_positions) -> List[Match]:
    affected_rows = set()
    affected_cols = set()

    for (x, y) in swapped_positions:
        affected_rows.insert(y)
        affected_cols.insert(x)
        // 也检查相邻位置（匹配可能延伸到交换位置之外）
        affected_rows.insert(y)
        affected_cols.insert(x - 1)
        affected_cols.insert(x + 1)

    matches = []
    for row in affected_rows:
        matches.extend(detect_horizontal_sliding_window(grid, row))
    for col in affected_cols:
        matches.extend(detect_vertical_sliding_window(grid, col))

    return remove_duplicates(matches)
```

---

## 9. 4连消与5连消的识别与特殊方块生成

### 9.1 匹配长度与特殊方块映射

在扫描过程中，匹配长度自然储存在 `MatchGroup.length` 中：

```
function generate_special_tile(match_group) -> SpecialType:
    length = match_group.length
    shape   = match_group.shape_type

    // 直线匹配
    if shape == HORIZONTAL_LINE OR shape == VERTICAL_LINE:
        if length == 4:
            return STRIPED  // 条纹方块：消除整行或整列
        if length >= 5:
            return RAINBOW  // 彩虹方块：消除所有同色方块

    // 交叉形状
    if shape == L_SHAPE OR shape == T_SHAPE:
        return BOMB  // 炸弹方块：消除 3×3 区域

    if shape == CROSS_SHAPE:
        return SUPER_BOMB  // 超级炸弹

    // 默认：3连消
    return NONE
```

### 9.2 特殊方块生成位置

```
function get_special_tile_spawn_position(match_group) -> Position:
    shape = match_group.shape_type

    if shape == HORIZONTAL_LINE:
        // 在匹配的中间位置生成
        return match_group.positions[match_group.positions.size() / 2]

    if shape == VERTICAL_LINE:
        return match_group.positions[match_group.positions.size() / 2]

    if shape == L_SHAPE OR shape == T_SHAPE OR shape == CROSS_SHAPE:
        // 在交叉点生成
        return match_group.pivot

    // 默认：最后交换的位置
    return match_group.positions[0]
```

### 9.3 完整分类逻辑

```
function classify_match_group(match_group) -> void:
    h_span = count_horizontal_span(match_group)
    v_span = count_vertical_span(match_group)
    total  = match_group.positions.size()

    // ────────── 直线判定 ──────────
    if h_span == total AND v_span == 1:
        match_group.shape_type = HORIZONTAL_LINE
    elif v_span == total AND h_span == 1:
        match_group.shape_type = VERTICAL_LINE
    // ────────── 形状判定 ──────────
    elif is_cross_pattern(match_group):
        match_group.shape_type = CROSS_SHAPE
    elif is_T_pattern(match_group):
        match_group.shape_type = T_SHAPE
    elif is_L_pattern(match_group):
        match_group.shape_type = L_SHAPE
    else:
        match_group.shape_type = IRREGULAR

    // ────────── 长度判定 ──────────
    if match_group.shape_type in [HORIZONTAL_LINE, VERTICAL_LINE]:
        match_group.match_length = max(h_span, v_span)
    else:
        match_group.match_length = total
```

#### 匹配长度 → 效果映射表

| 匹配长度 | 形状 | 特殊方块 | 消除效果 |
|---------|------|---------|---------|
| 3 | 直线 | 无 | 基础消除 |
| 4 | 直线 | 条纹方块 | 消除整行(水平4连) / 整列(垂直4连) |
| 5+ | 直线 | 彩虹方块 | 消除棋盘上所有同色方块 |
| 5+ | L形 | 炸弹方块 | 消除 3×3 范围 |
| 5+ | T形 | 炸弹方块 | 消除 3×3 范围 |
| 5+ | +形 | 超级炸弹 | 消除十字形范围(整行+整列) |

---

## 10. 匹配结果存储设计

### 10.1 核心数据结构

```
// 位置
struct Position:
    x: int
    y: int

// 匹配组
struct MatchGroup:
    type: MatchType          // HORIZONTAL, VERTICAL, L_SHAPE, T_SHAPE, CROSS_SHAPE
    positions: Array[Position]  // 所有匹配位置
    pivot: Position          // 交叉点/中心点（对于直线则为中点）
    length: int              // 匹配长度（直线为最大跨度，形状为总方块数）
    color: int               // 颜色类型

// 完整匹配结果
struct MatchResult:
    matched_cells: 2D_bool_array    // 标记哪些格子被匹配
    groups: Array[MatchGroup]       // 所有匹配组
    special_spawns: Array[SpecialSpawn]  // 需要生成特殊方块的位置
    total_matched: int              // 总消去方块数

// 特殊方块生成信息
struct SpecialSpawn:
    position: Position              // 生成位置
    special_type: SpecialType       // STRIPED, BOMB, RAINBOW, etc.
    direction: Direction            // 条纹方块的方向（HORIZONTAL / VERTICAL）
    color: int                      // 颜色
```

### 10.2 完整匹配检测与存储流程

```
function detect_matches(grid, width, height) -> MatchResult:
    result = MatchResult()
    matched = 2D_bool_array(width, height).fill(false)

    // ── 步骤1: 检测所有直线匹配 ──
    h_matches = detect_horizontal_matches(grid, width, height)
    v_matches = detect_vertical_matches(grid, width, height)

    // ── 步骤2: 填充标记矩阵 ──
    for each m in h_matches + v_matches:
        for each p in m.positions:
            matched[p.y][p.x] = true

    result.matched_cells = matched

    // ── 步骤3: Flood-Fill 连通分组 ──
    visited = 2D_bool_array(width, height).fill(false)
    all_groups = []

    for y in 0..height-1:
        for x in 0..width-1:
            if matched[y][x] AND NOT visited[y][x]:
                region = flood_fill_iterative(matched, x, y, width, height)
                for each p in region:
                    visited[p.y][p.x] = true

                // ── 步骤4: 形状分类 ──
                group = classify_match_group(region)
                all_groups.append(group)

    result.groups = all_groups

    // ── 步骤5: 确定特殊方块生成 ──
    for each group in all_groups:
        special_type = generate_special_tile(group)
        if special_type != NONE:
            spawn_pos = get_special_tile_spawn_position(group)
            result.special_spawns.append(SpecialSpawn(
                position = spawn_pos,
                special_type = special_type,
                direction = group.type if group.type in [HORIZONTAL, VERTICAL] else NONE,
                color = group.color
            ))

    result.total_matched = count_true(matched)
    return result
```

### 10.3 GDScript 实现示例（Godot 适配）

```gdscript
# match_result.gd

class_name MatchGroup
var type: int              # MatchType enum
var positions: Array       # Array[Vector2i]
var pivot: Vector2i
var match_length: int
var tile_color: int

class_name MatchResult
var matched_cells: Array          # Array[Array] of bool
var groups: Array[MatchGroup]
var special_spawns: Array         # Array[SpecialSpawn]
var total_matched: int

# 使用 PackedVector2Array 或 Typed Array 提升性能
var matched_positions: PackedVector2Array  # 比逐个存储快
```

---

## 11. 完整伪代码与算法流程图

### 11.1 整体算法流程

```
╔══════════════════════════════════════════════════╗
║           玩家交换两个方块                         ║
╚═══════════════╤══════════════════════════════════╝
                │
                ▼
╔══════════════════════════════════════════════════╗
║  Step 1: 水平扫描 detect_horizontal_matches()    ║
║  · 遍历每一行                                     ║
║  · 寻找 >= 3 的连续同色方块                       ║
║  · 记录匹配组信息                                 ║
╚═══════════════╤══════════════════════════════════╝
                │
                ▼
╔══════════════════════════════════════════════════╗
║  Step 2: 垂直扫描 detect_vertical_matches()      ║
║  · 遍历每一列                                     ║
║  · 寻找 >= 3 的连续同色方块                       ║
║  · 记录匹配组信息                                 ║
╚═══════════════╤══════════════════════════════════╝
                │
                ▼
╔══════════════════════════════════════════════════╗
║  Step 3: 合并与去重 merge_and_deduplicate()      ║
║  · 填充 matched[][] 布尔矩阵                     ║
║  · 使用 Flood-Fill 提取连通区域                   ║
╚═══════════════╤══════════════════════════════════╝
                │
                ▼
     ┌──────是否找到匹配?──────┐
     │ YES                    │ NO
     ▼                        ▼
╔══════════════╗    ╔════════════════╗
║ Step 4: 形状  ║    ║ 无匹配 → 交换  ║
║ 分类与特殊    ║    ║ 无效 → 回退    ║
║ 方块生成      ║    ╚════════════════╝
╚══════╤═══════╝
       │
       ▼
╔══════════════════════════════════════╗
║  Step 5: 消除动画 & 方块移除          ║
╚═══════════════╤══════════════════════╝
                │
                ▼
╔══════════════════════════════════════╗
║  Step 6: 重力下落 & 新方块生成        ║
╚═══════════════╤══════════════════════╝
                │
                ▼
╔══════════════════════════════════════╗
║  Step 7: 重新检测 → 循环至无匹配       ║
║  (连锁消除 Cascade/Chain)            ║
╚══════════════════════════════════════╝
```

### 11.2 核心检测算法伪代码

```
┌─────────────────────────────────────────────────────────────┐
│              match_detection_main(grid, W, H)               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  result = new MatchResult()                                │
│  matched = new bool[W][H] // 初始化为 false                 │
│                                                             │
│  // ============ 阶段 1: 直线检测 ============              │
│  for row = 0 to H-1:                                       │
│      col = 0                                                │
│      while col < W:                                         │
│          color = grid[row][col]                            │
│          if color == EMPTY: col++; continue                 │
│          len = count_run_horizontal(grid, row, col)         │
│          if len >= 3:                                       │
│              for c = col to col + len - 1:                  │
│                  matched[row][c] = true                     │
│              result.h_groups.append(                        │
│                  Group(H_LINE, row, col, len, color))       │
│          col += len                                          │
│                                                             │
│  for col = 0 to W-1:                                       │
│      row = 0                                                │
│      while row < H:                                         │
│          color = grid[row][col]                            │
│          if color == EMPTY: row++; continue                 │
│          len = count_run_vertical(grid, col, row)           │
│          if len >= 3:                                       │
│              for r = row to row + len - 1:                  │
│                  matched[r][col] = true                     │
│              result.v_groups.append(                        │
│                  Group(V_LINE, col, row, len, color))       │
│          row += len                                          │
│                                                             │
│  // ============ 阶段 2: 连通分组 & 形状分类 ============   │
│  visited = new bool[W][H]                                   │
│  for each cell (x, y):                                      │
│      if matched[y][x] AND NOT visited[y][x]:                │
│          region = flood_fill(matched, x, y)                │
│          mark_visited(visited, region)                      │
│          shape = classify_shape(region)                    │
│          special = determine_special(shape, region)         │
│          result.groups.append(Group(shape, region, ...))    │
│                                                             │
│  return result                                              │
└─────────────────────────────────────────────────────────────┘
```

### 11.3 级联消除（Cascade）状态机

```
                    ┌──────────┐
                    │ 玩家等待  │ ◄──────────────────────┐
                    │ (IDLE)   │                         │
                    └────┬─────┘                         │
                         │ 玩家交换方块                    │
                         ▼                               │
                    ┌──────────┐                         │
                    │ 交换动画  │                         │
                    │ (SWAP)   │                         │
                    └────┬─────┘                         │
                         │                               │
                         ▼                               │
                    ┌──────────┐                         │
              ┌────►│ 匹配检测  │                         │
              │     │ (CHECK)  │                         │
              │     └────┬─────┘                         │
              │          │                               │
              │     ┌────┴────┐                          │
              │     │ 有匹配?  │                          │
              │     └────┬────┘                          │
              │    YES   │   NO                          │
              │     ┌────┘                               │
              │     ▼
              │ ┌──────────┐
              │ │ 消除动画  │
              │ │ (CLEAR) │
              │ └────┬─────┘
              │      │
              │      ▼
              │ ┌──────────┐
              │ │ 重力下落  │
              │ │ (FALL)  │
              │ └────┬─────┘
              │      │
              │      ▼
              │ ┌──────────┐
              │ │ 新方块生成 │
              │ │ (SPAWN) │
              │ └────┬─────┘
              │      │
              └──────┘
                     │
                NO   │
              ┌──────┘
              │
              ▼
         ┌──────────┐                  ┌──────────┐
         │ 返回空闲  │                  │ 无效交换  │
         │ (IDLE)   │                  │ 回退动画  │
         └──────────┘                  └──────────┘
```

### 11.4 完整主循环伪代码

```
function game_loop_process_swap(pos1, pos2):
    // 1. 验证交换合法性
    if NOT is_adjacent(pos1, pos2):
        return INVALID_MOVE

    // 2. 执行交换（数据层）
    swap_tiles(grid, pos1, pos2)

    // 3. 检测匹配
    result = detect_matches(grid, W, H)

    if result.total_matched == 0:
        // 无效交换，回退
        swap_tiles(grid, pos1, pos2)
        play_invalid_animation(pos1, pos2)
        return INVALID_MOVE

    // 4. 级联消除循环
    current_result = result
    combo_count = 1

    while current_result.total_matched > 0:
        // 4a. 生成特殊方块
        spawn_special_tiles(current_result.special_spawns)

        // 4b. 播放消除动画
        play_clear_animation(current_result.matched_cells)

        // 4c. 移除已匹配方块
        clear_matched_cells(grid, current_result.matched_cells)

        // 4d. 重力和填充
        apply_gravity_and_fill(grid)
        play_fall_animation()

        // 4e. 重新检测
        current_result = detect_matches(grid, W, H)

        if current_result.total_matched > 0:
            combo_count += 1
            apply_combo_multiplier(combo_count)

    // 5. 检查死局（无可能移动）
    if NOT has_valid_moves(grid, W, H):
        shuffle_board(grid, W, H)

    // 6. 返回控制权
    return SUCCESS
```

---

## 12. 参考文献

### 12.1 在线资源

| 来源 | 标题 | URL |
|------|------|-----|
| Gamedev StackExchange | Match-three puzzle games algorithm | https://gamedev.stackexchange.com/questions/2607 |
| Gamedev StackExchange | How to detect matches in match-3 game? | https://gamedev.stackexchange.com/questions/146590 |
| Azumo Insights | The Logic Behind Match-3 Games | https://azumo.com/insights/the-logic-behind-match-3-games |
| Logic Simplified | Key Algorithmic Tricks for Match 3 Game Development | https://logicsimplified.com/newgames/key-algorithmic-tricks-for-match-3-game-development/ |
| Catlike Coding | Match 3 Tutorial (Unity/C#) | https://catlikecoding.com/unity/tutorials/prototypes/match-3/ |
| Construct 3 Forum | Detect shapes in match-3 | https://www.construct.net/en/forum/construct-2/how-do-i-18/detect-shapes-match-3-62929 |
| Reddit r/gamedev | Match-3 puzzle game algorithms | https://www.reddit.com/r/gamedev/comments/4atryv/ |

### 12.2 理论基础

| 主题 | 说明 |
|------|------|
| **Bitboard** | 源自国际象棋编程，使用64位整数 + 位运算快速检测模式。参考《Hacker's Delight》一书 |
| **Flood Fill** | 经典图遍历算法，用于确定多维数组中连通区域。时间复杂度 O(N)，标准实现使用 BFS/DFS |
| **并查集** | Union-Find Disjoint Set，用于高效合并连通组。时间复杂度接近 O(α(N))，其中 α 为阿克曼反函数 |
| **游程编码** | 无损压缩技术，将连续相同值编码为 (值, 长度) 对。在匹配检测中可将 O(W) 降至 O(R)，R 为游程数 |

### 12.3 关键设计原则总结

```
1. 数据与表现分离
   - 游戏逻辑操作纯数据结构（数组/位图）
   - 视觉层通过事件监听并渲染动画
   - 好处：可单独测试逻辑，易于更换视觉风格

2. 两阶段检测
   - 阶段1: 线性扫描（快，缓存友好）
   - 阶段2: 连通分量分析（准确，支持形状识别）

3. 渐进式优化
   - 先实现正确性（暴力扫描即可）
   - 再优化性能（仅在确实需要时）
   - 8×8 网格极其轻量，多数优化是过度工程

4. 级联处理
   - while(matches_found) 循环处理连锁消除
   - 每轮独立检测，避免状态混乱
   - 播放动画时阻止新输入

5. 死局检测
   - 每轮消除后检查是否存在可能移动
   - 若无有效移动，重新洗牌
   - 在 8×8 网格中至多检查 112 种可能交换
```

---

> **文档版本**: v1.0  
> **最后更新**: 2026-06-08  
> **适用项目**: Godot Match-3 Demo (`match3-demo`)  
> **语言**: GDScript / C# (Godot)
