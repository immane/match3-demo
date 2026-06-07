# 消消乐(Match-3)游戏重力与下落机制研究

> 本文档研究了消消乐游戏中方块（Tile）被消除后，剩余方块如何通过重力作用下落填充空位，以及新方块如何生成的全部机制。
> 涵盖从经典实现（Candy Crush / Bejeweled）到现代变体（Royal Match / Homescapes）的核心设计模式。
>
> 编写语言：中文

---

## 目录

1. [核心概念：重力与级联](#1-核心概念重力与级联)
2. [重力方向](#2-重力方向)
3. [下落算法：逐列处理](#3-下落算法逐列处理)
4. [空位检测（Gap Detection）](#4-空位检测gap-detection)
5. [方块就位（Tile Settling）](#5-方块就位tile-settling)
6. [新方块生成（Spawning）](#6-新方块生成spawning)
7. [动画时序与缓动](#7-动画时序与缓动)
8. [同时下落 vs 逐列下落 vs 错开下落](#8-同时下落-vs-逐列下落-vs-错开下落)
9. [障碍物与阻挡器处理](#9-障碍物与阻挡器处理)
10. [物理引擎驱动 vs 动画驱动](#10-物理引擎驱动-vs-动画驱动)
11. [代码架构建议](#11-代码架构建议)
12. [Godot 中的实现参考](#12-godot-中的实现参考)
13. [常见问题与解决方案](#13-常见问题与解决方案)
14. [参考资料](#14-参考资料)

---

## 1. 核心概念：重力与级联

### 1.1 什么是「重力」？

在消消乐游戏中，「重力」(Gravity) 指的是：**当一些方块因匹配被消除后，其上方的方块受重力影响向下移动，填补因消除而产生的空位**。重力方向通常是从上到下（Y 轴负方向），但也存在其他方向变体。

### 1.2 什么是「级联」(Cascade)？

「级联」或「连锁」指的是：重力下落后的新布局可能产生新的匹配，新匹配被自动消除，再次触发重力下落，如此循环往复的过程。现代消消乐（如 Royal Match、Homescapes）采用 **连续级联** 模式——在任意下落帧都可能检测并触发新匹配，而不是等待所有方块完全静止。

```
玩家交换 → 匹配消除 → 重力下落 → 新方块生成 → 检测匹配
                                                    ↓ 有匹配
                                              回到「匹配消除」
                                                    ↓ 无匹配
                                              玩家恢复操作
```

### 1.3 典型处理流程

```
[交换方块] → [查找匹配] → [标记已匹配方块] → [删除已匹配方块]
     ↑                                                  ↓
     └── 无新匹配 ← [检测匹配] ← [重力下落填充] ← [生成新方块]
                         ↓ 有新匹配
                    回到「删除已匹配方块」
```

---

## 2. 重力方向

### 2.1 从上到下（最常见）

```
■□□□■    →     □□□□□
□■■□□    →     ■□□□■
■■■■□    →     □■■□□
□□□□□    →     ■■■■□
```

- **代表游戏**：Candy Crush Saga、Bejeweled、Royal Match、Toon Blast
- **实现方式**：每一列独立处理，从底部向上扫描空位，将上方方块向下移动

### 2.2 从下到上

- **代表游戏**：Tetris Battle Gaiden（某些模式）、部分物理消除游戏
- **使用场景**：反重力道具、特殊关卡设计

### 2.3 左右方向

- **代表游戏**：Dungeon Raid（斜向重力）、You Must Build a Boat
- **实现方式**：逐行处理，从右向左或从左向右填充空位

### 2.4 多方向 / 可选择方向

- 同一游戏内通过道具或关卡机制切换重力方向
- 棋盘旋转后重力方向也随之旋转
- 实现要点：将重力方向抽象为单位向量 `gravity_direction: Vector2`，在下落算法中使用该向量计算「底部」和「上方」

### 2.5 对角线下落

在某些游戏中，方块可以沿对角线方向移动来填充那些列方向无法到达的空位（例如被障碍物阻挡时）。对角线下落可以让方块绕过障碍物。

---

## 3. 下落算法：逐列处理

### 3.1 经典逐列下落算法

这是最基础、最广泛使用的算法。核心思路：**对每一列，从底部向上扫描，找到空位后将其上方的所有方块向下移动**。

```gdscript
# 伪代码：逐列下落
func collapse_columns():
    for column in range(board_width):
        for row in range(board_height):
            if board[column][row] == null:  # 发现空位
                # 从当前位置向上查找，找到第一个非空方块
                for above_row in range(row + 1, board_height):
                    if board[column][above_row] != null:
                        # 将上方方块移动到当前空位
                        move_tile(board[column][above_row], Vector2(column, row))
                        board[column][row] = board[column][above_row]
                        board[column][above_row] = null
                        break  # 跳出内层循环，继续处理下一个位置
```

### 3.2 优化的队列式下落

一次性计算所有方块的最终位置，避免多次遍历：

```gdscript
# 优化版本：使用双指针/队列
func collapse_columns_optimized():
    for column in range(board_width):
        # 「写入指针」：方块应该移动到的目标行
        var write_row = 0
        for read_row in range(board_height):
            if board[column][read_row] != null:
                if read_row != write_row:
                    # 有落差，需要移动
                    move_tile_to(board[column][read_row], column, write_row)
                    board[column][write_row] = board[column][read_row]
                    board[column][read_row] = null
                write_row += 1
        # write_row 之后的行全部为空，需要填补新方块
```

此算法的优势在于：每个方块的最终位置只会被计算一次，且能直接得知每个空列需要生成多少个新方块。

### 3.3 Catlike Coding 的实现模式

Catlike Coding 的 Match-3 教程（Unity C#）中推荐将移动抽象为 `Move` 概念：

```csharp
// 检查移动有效性
bool IsValidMove(Move move);

// 执行移动
void DoMove(Move move);

// 后续处理（包括下落和新方块）
IEnumerator DoAfterMove();
```

其中下落逻辑放在 `DoAfterMove()` 协程中逐步执行，与动画系统解耦。

---

## 4. 空位检测（Gap Detection）

### 4.1 逐格扫描法

遍历棋盘每个格子，检查 `board[x][y] == null`。简单直观但 O(n²)。

### 4.2 匹配后标记法

在 `delete_matches()` 阶段记录被删除的格子坐标列表，后续只在这些列中进行下落处理：

```gdscript
var affected_columns = []
func delete_matches():
    for x in range(board_width):
        for y in range(board_height):
            if board[x][y] != null and board[x][y].matched:
                board[x][y].queue_free()
                board[x][y] = null
                if x not in affected_columns:
                    affected_columns.append(x)
```

然后在 `collapse_columns()` 中只处理 `affected_columns` 中的列，大幅减少不必要的遍历。

### 4.3 空位链（Gap Chain）

当一个方块落下填充空位后，原本的位置又成为新的空位。算法需要正确处理这种「空位链」：

```
删除后：      第一次填充后：      最终结果：
[■]           [■]                [□] ← 需要新方块
[□]  ← 空位   [□]  ← 新空位     [■]
[■]           [■]                [■]
[□]  ← 空位   [□]  ← 新空位     [■]
[■]           [■]                [■]
```

逐行自底向上扫描可以自动处理空位链。

---

## 5. 方块就位（Tile Settling）

### 5.1 即时就位 vs 动画就位

| 方式 | 逻辑层 | 表现层 | 优缺点 |
|------|--------|--------|--------|
| 即时就位 | 方块数据立即更新 | 视觉通过动画过渡 | 逻辑简单，是最推荐的做法 |
| 动画驱动 | 方块数据随动画帧更新 | 视觉即逻辑 | 容易产生数据不一致 |

**推荐方案**：逻辑与表现分离。逻辑层（Board）立即更新所有方块位置数据；表现层（TileView）使用 Tween/动画从当前位置平滑过渡到目标位置。

### 5.2 「弹簧着陆」效果

如 Homescapes 等现代消消乐，方块落地后会有一个微小的「弹跳」效果：

- **实现方式 A（解析法）**：使用正弦衰减波模拟弹簧阻尼振荡
  ```gdscript
  # 弹簧效果：叠加正弦衰减
  func bounce_offset(t: float, amplitude: float, frequency: float, decay: float) -> float:
      return amplitude * exp(-decay * t) * sin(frequency * t)
  
  # 在总动画时长中，前 70% 用于下落，后 30% 用于弹簧着陆
  ```

- **实现方式 B（分段缓动）**：先快后慢 + 微小过冲回弹
  ```gdscript
  tween.tween_property(tile, "position", target_pos, fall_duration) \
       .set_ease(Tween.EASE_IN)  # 模拟重力加速
  # 着陆后，在 Y 轴上叠加一个小过冲
  tween.tween_property(tile, "position", 
       target_pos + Vector2(0, -overshoot_amount), 0.05) \
       .set_ease(Tween.EASE_OUT)
  tween.tween_property(tile, "position", target_pos, 0.08) \
       .set_ease(Tween.EASE_IN_OUT)  # 回弹到最终位置
  ```

### 5.3 避免「弹跳」问题

使用物理引擎时，Tile 之间的碰撞会导致持续弹跳和极慢的「稳定」过程（参见 GameDev StackExchange 讨论）。解决方案：

- 不使用物理引擎，改用动画驱动（强烈推荐）
- 若必须使用物理引擎：着陆后将方块设为 Kinematic，等待稳定后再切换回 Dynamic
- 使用 `Continuous` 碰撞检测模式（而非 `Discrete`）

---

## 6. 新方块生成（Spawning）

### 6.1 从哪里生成？

```
                  [新方块从棋盘上方进入]
                        ↓↓↓
┌─────────────────────────┐
│  □  □  □  棋盘可见区域   │
│  ■  ■  ■                │
│  ■  □  ■                │
│  ■  ■  ■                │
└─────────────────────────┘
```

- 新方块在棋盘顶部（Y 轴最大处 + 偏移量）被实例化
- 初始位置在视野外（通常是 `grid_to_pixel(x, board_height + offset)`）
- 通过动画下降到目标空格位置

### 6.2 生成算法

```gdscript
func spawn_pieces():
    for column in range(board_width):
        # 计算该列需要生成多少个新方块
        var empty_count = 0
        for row in range(board_height):
            if board[column][row] == null:
                empty_count += 1
        
        for i in range(empty_count):
            # 选择随机的方块类型
            var piece = random_piece_scene.instantiate()
            pieces_container.add_child(piece)
            
            # 计算目标位置（从下往上填充）
            var target_row = i  # 空位从底部开始
            var target_pos = grid_to_pixel(column, target_row)
            
            # 初始位置：棋盘上方
            var start_pos = grid_to_pixel(column, board_height + empty_count - i)
            piece.position = start_pos
            
            board[column][target_row] = piece
            
            # 播放下降动画
            animate_fall(piece, target_pos, fall_duration)
```

### 6.3 入场动画类型

| 类型 | 描述 | 适用场景 |
|------|------|----------|
| 直线下落 | 新方块从顶部直线下降到位 | 基础实现 |
| 带旋转下落 | 下降过程中方块旋转 360° | 表现力更强 |
| 弹跳入场 | 方块落到位后轻微弹跳 | 现代消消乐标配 |
| 缩放弹出 | 方块从小变大出现在目标位置 | 消除后再填充 |
| 延迟错开 | 同一行新方块以微小延迟依次生成 | 增强视觉层次感 |

### 6.4 初始布局时的无匹配检查

在生成初始棋盘时，需要确保不会一开始就产生三消：

```gdscript
func spawn_pieces_with_no_initial_match():
    for x in range(board_width):
        for y in range(board_height):
            var piece = random_piece()
            # 检查新生成的方块是否会与左侧/下方的方块形成匹配
            while match_at(x, y, piece.color):
                piece.queue_free()
                piece = random_piece()
            board[x][y] = piece
            pieces_container.add_child(piece)
            piece.position = grid_to_pixel(x, y)
```

---

## 7. 动画时序与缓动

### 7.1 关键时间参数

| 参数 | 典型值 | 说明 |
|------|--------|------|
| 下落速度 | 0.1s ~ 0.3s / 格 | 方块下落一个格子所需的时间 |
| 列间延迟 | 0.02s ~ 0.1s | 相邻列开始下落的延迟间隔 |
| 行间延迟 | 0.03s ~ 0.08s | 同一列中不同方块的下落延迟 |
| 匹配显示 | 0.2s ~ 0.5s | 匹配方块高亮后到删除前的等待 |
| 删除动画 | 0.15s ~ 0.3s | 方块消除动画（缩小+淡出） |
| 新方块延迟 | 0.1s ~ 0.2s | 旧方块落位后到新方块生成的间隔 |
| 级联等待 | 0.3s ~ 0.5s | 全部就位后到检测新匹配的间隔 |

### 7.2 缓动曲线（Easing Curves）

```gdscript
# 不同缓动曲线在方块下落中的使用

# 1. 重力加速下落（模拟真实重力）
tween.set_ease(Tween.EASE_IN)        # 先慢后快
tween.set_trans(Tween.TRANS_QUAD)    # 二次加速

# 2. 平滑减速着陆
tween.set_ease(Tween.EASE_OUT)       # 先快后慢
tween.set_trans(Tween.TRANS_CUBIC)   # 三次减速

# 3. 弹性着陆
tween.set_ease(Tween.EASE_OUT)
tween.set_trans(Tween.TRANS_ELASTIC) # 弹性效果（注意不要太过）

# 4. 回弹着陆
tween.set_ease(Tween.EASE_IN_OUT)
tween.set_trans(Tween.TRANS_BACK)    # 过冲后回弹

# 推荐组合：下落用 EASE_IN + QUAD，着陆弹簧用自定义正弦衰减
```

### 7.3 适应不同下落距离的时长计算

方块下落格数不同，动画时长应动态调整：

```gdscript
# 方式 A：固定每格时间
var fall_duration = distance_in_cells * TIME_PER_CELL  # e.g. 0.12s / cell

# 方式 B：使用平方根（距离越长，每格越快）
# 模拟真实的物理加速
var fall_duration = sqrt(distance_in_cells) * BASE_TIME

# 方式 C：固定最大值
var fall_duration = min(distance_in_cells * TIME_PER_CELL, MAX_FALL_TIME)
```

### 7.4 完整动画时序表

以典型消消乐一回合为例（玩家一次交换后）：

```
时间轴 (秒)
│
0.00 ─── 交换动画开始 (0.2s)
0.20 ─── 交换完成，开始检测匹配
0.25 ─── 找到匹配，匹配方块闪烁/高亮
0.55 ─── 匹配方块删除动画开始 (0.2s 缩放+淡出)
0.75 ─── 删除完成
0.80 ─── 逐列下落开始，列间延迟 0.05s
│        Column 0: 下落中...
0.85 ─── Column 1: 下落中...
0.90 ─── Column 2: 下落中...
│        每格下落耗时 0.12s
1.20 ─── 所有列下落完成
1.25 ─── 新方块生成，从上方降下 (0.3s)
1.55 ─── 新方块着陆完成
1.60 ─── 检测新匹配 ──→ 有新匹配？回到 0.25s
│                        └──→ 无新匹配，玩家恢复操作 (total: ~1.6s)
```

---

## 8. 同时下落 vs 逐列下落 vs 错开下落

### 8.1 三种下落策略对比

```
同时下落 (Simultaneous):        逐列下落 (Column by Column):       错开下落 (Staggered):
                                  Col0 Col1 Col2
T=0:  全部方块同时开始下落        T=0:  ██░░░░░░                      T=0:  同一列内上下方块也有延迟
T=1:  全部方块同时到达             T=0.05: ░░██░░░░                    T=0: ██
T=2:  不可控，视觉混乱             T=0.10: ░░░░██░░                    T=0.03: ░░██
                                  T=0.15: ░░░░░░██                    T=0.06: ░░░░██
```

| 策略 | 优点 | 缺点 | 适用场景 |
|------|------|------|----------|
| 同时下落 | 实现最简单 | 视觉混乱，不自然 | 原型/测试 |
| 逐列下落 | 视觉清晰，有节奏感 | 总时间随列数线性增长 | 经典实现 |
| 错开下落 | 最有动感，最自然 | 实现复杂 | 现代商业消消乐 |

### 8.2 错开下落（Staggered Fall）的精确实现

Homescapes 等现代消消乐采用的模式：

```gdscript
func apply_gravity_with_stagger():
    var animations = []
    var max_fall_distance = 0
    
    # 第一步：计算所有方块的移动路径（数据层面立即更新）
    for column in range(board_width):
        var write_row = 0
        for row in range(board_height):
            if board[column][row] != null:
                var target_row = write_row
                var fall_distance = row - target_row
                
                if fall_distance > 0:
                    animations.append({
                        "tile": board[column][row],
                        "from_row": row,
                        "to_row": target_row,
                        "column": column,
                        "fall_distance": fall_distance
                    })
                    max_fall_distance = max(max_fall_distance, fall_distance)
                
                board[column][target_row] = board[column][row]
                if target_row != row:
                    board[column][row] = null
                write_row += 1
    
    # 第二步：按「当前高度」排序
    # 较高的方块应该先开始下落（因为它们有更长的距离）
    animations.sort_custom(func(a, b): return a.from_row > b.from_row)
    
    # 第三步：为每个方块创建动画，带有基于其初始高度的延迟
    for anim in animations:
        var tile = anim.tile
        var target_pos = grid_to_pixel(anim.column, anim.to_row)
        var delay = (max_fall_distance - anim.from_row) * STAGGER_DELAY  # 0.03s ~ 0.06s
        var duration = anim.fall_distance * TIME_PER_CELL
        
        tween.tween_property(tile, "position", target_pos, duration) \
             .set_delay(delay) \
             .set_ease(Tween.EASE_IN) \
             .set_trans(Tween.TRANS_QUAD)
```

### 8.3 动画完成后检测匹配

```gdscript
# 在所有 Tween 完成后触发检测
tween.tween_callback(check_matches_and_loop)
# 或者使用 tween.finished 信号
tween.finished.connect(_on_all_falling_done)
```

### 8.4 连续级联的特殊考虑

现代消消乐（Royal Match 风格）与传统（Bejeweled 风格）的关键区别：

| 特性 | 传统（Bejeweled） | 现代（Royal Match） |
|------|-------------------|---------------------|
| 玩家操作时机 | 所有方块静止后才能操作 | 任何时刻都可以操作 |
| 级联触发 | 逐批等待完成 | 连续不断 |
| 下落方式 | 逐列/同时 | 高速错开 |
| 动画总时长 | 较长（1.5~3s） | 很短（0.3~1s） |

实现连续级联的关键：在方块还在移动时，就可以交换其他方块。这需要使用**状态机**来管理每对方块的动画状态。

---

## 9. 障碍物与阻挡器处理

### 9.1 障碍物类型

| 类型 | 示例 | 对重力的影响 |
|------|------|-------------|
| 不可移动障碍 | 冰块、石头、墙壁 | 重力跳过该格，上方方块绕过 |
| 半可移动障碍 | 笼子（内有方块）、锁链 | 消除后重力正常处理 |
| 可破坏障碍 | 巧克力、蜂蜜 | 消除后该格变为空位 |
| 多层障碍 | 需要多次消除 | 每消除一层，格子内容改变 |
| 传送门 / 管道 | 类似 Portal 机制 | 方块可能从其他列出现 |

### 9.2 障碍物下的下落算法

```gdscript
func collapse_columns_with_obstacles():
    for column in range(board_width):
        var write_row = 0
        for read_row in range(board_height):
            var cell = board[column][read_row]
            
            # 跳过障碍物
            if cell is Obstacle:
                # 障碍物保持原位，写入指针跳到障碍物之后
                if not cell.is_destroyable:
                    write_row = read_row + 1
                continue
            
            if cell != null and cell is Tile:
                if read_row != write_row:
                    board[column][read_row] = null
                    board[column][write_row] = cell
                write_row += 1
```

### 9.3 对角线绕过障碍物

当列方向下落被障碍物阻挡时，某些游戏允许方块沿对角线绕过：

```
下落前：          直线下落不行：     对角线绕过：
[■] [□] [□]     [■] [□] [□]       [□] [□] [□]
[■] [□] [□]     [■] [□] [□]       [■] [□] [□]
[■] [█] [□]     [□] [█] [□]       [■] [█] [□]  ← 方块绕过障碍物
[□] [□] [□]     [□] [□] [□]       [■] [□] [□]

█ = 障碍物    ■ = 方块    □ = 空位
```

对角线下落实现：
```gdscript
func try_diagonal_fall(column, row):
    # 检查左下方
    if column > 0 and board[column-1][row-1] == null:
        return Vector2(column-1, row-1)
    # 检查右下方
    if column < board_width-1 and board[column+1][row-1] == null:
        return Vector2(column+1, row-1)
    return null
```

---

## 10. 物理引擎驱动 vs 动画驱动

### 10.1 对比总结

| 方面 | 物理引擎驱动 | 动画驱动 |
|------|-------------|----------|
| 实现复杂度 | 看似简单，实则陷阱多 | 需要自己写逻辑，但可控 |
| 弹跳问题 | 严重（方块互相挤压反弹） | 完全不存在 |
| 时间可控性 | 无法精确控制结束时间 | 时长精确可控 |
| 碰撞检测 | 引擎自动处理 | 用数据层判断（grid 数组） |
| 调整难度 | 调参黑箱 | 参数直观 |
| 适用场景 | 原型、物理解谜类消除 | **几乎所有商业消消乐** |

### 10.2 为什么要避免物理引擎

GameDev StackExchange 上的一个典型案例（https://gamedev.stackexchange.com/questions/175331）：

> 开发者使用 Unity RigidBody2D + BoxCollider2D 实现方块下落。方块间的碰撞产生持续弹跳，即使将 Physics Material 的 Bounciness 设为 0 也无济于事。根本原因是物理引擎中多个动态刚体在短距离内的碰撞解析（decompression）导致无限振荡。

结论：**物理引擎不适合离散网格下落**。应使用动画驱动（Tween/Animation）或解析方程（SUVAT）。

### 10.3 解析方程法（SUVAT）

若不使用 Tween，也可直接用物理公式计算每帧位置：

```gdscript
# SUVAT 方程（匀加速运动）
# s = ut + (1/2)at²
# 在 Godot 中的实现：

var fall_start_time: float
var fall_start_pos: Vector2
var fall_end_pos: Vector2
var fall_duration: float

func _process(delta):
    var elapsed = Time.get_ticks_msec() / 1000.0 - fall_start_time
    if elapsed >= fall_duration:
        position = fall_end_pos
        return
    
    var t = elapsed / fall_duration
    var eased_t = ease(t, -2.0)  # 模拟重力加速 (EASE_IN)
    position = fall_start_pos.lerp(fall_end_pos, eased_t)
```

### 10.4 Godot Tween 的优势

Godot 4.x 的 Tween 系统天然适合消消乐的下落动画：

- **链式调用**：`tween.tween_property(...).set_delay(...).set_ease(...)`
- **并行/串行控制**：`tween.parallel()` / `tween.sequential()`
- **回调集成**：`tween.tween_callback(check_matches)`
- **可中断**：`tween.kill()` 在玩家快速操作时可中断当前动画
- **完成信号**：`tween.finished` 用于触发级联检测

### 10.5 「解析法 + 弹簧」混合方案

来自 GameDev.net 论坛讨论的一种工业级方法：

> 下落阶段：使用 SUVAT 解析方程（s = ut + ½at²）驱动位置。
> 着陆阶段：使用正弦衰减波（amplitude × e^(-decay×t) × sin(frequency×t)）驱动 Y 轴偏移。
> 着陆阶段的初始振幅基于方块到达时的速度，确保过渡平滑。

这种方法比纯 Tween 更灵活，可以实现完全自定义的物理感效果。

---

## 11. 代码架构建议

### 11.1 核心原则：逻辑与表现分离

这是 Catlike Coding 教程和 Godot Forum 讨论中一致强调的核心理念。

```
┌──────────────────────────────────────┐
│            BoardLogic (纯数据)         │
│  - board: Array[Array[TileData]]      │
│  - find_matches() → Array[Match]      │
│  - collapse_columns() → Array[Move]   │
│  - spawn_pieces() → Array[SpawnInfo]  │
│  - 完全不依赖任何渲染节点               │
└──────────────┬───────────────────────┘
               │ 数据流
               ▼
┌──────────────────────────────────────┐
│          GravitySystem (中继层)        │
│  - 接收逻辑层的 Move/Spawn 指令        │
│  - 调度动画顺序                        │
│  - 管理动画完成回调                     │
└──────────────┬───────────────────────┘
               │ 指令
               ▼
┌──────────────────────────────────────┐
│        AnimationSystem (表现层)        │
│  - 使用 Godot Tween 驱动位置            │
│  - 管理缓动曲线和延迟                   │
│  - 完全不修改游戏数据                    │
└──────────────────────────────────────┘
```

### 11.2 推荐的文件结构

```
scripts/
├── logic/
│   ├── board_state.gd        # 棋盘状态（纯数据）
│   ├── board_logic.gd        # 匹配检测、下落计算
│   ├── tile_data.gd          # 方块数据类（Resource）
│   └── match_result.gd       # 匹配结果结构
│
├── systems/
│   ├── gravity_system.gd     # 重力系统（调度层）
│   ├── match_system.gd       # 匹配检测系统
│   └── spawn_system.gd       # 新方块生成系统
│
├── animation/
│   ├── fall_animator.gd      # 下落动画控制器
│   ├── match_animator.gd     # 消除动画控制器
│   ├── spawn_animator.gd     # 生成动画控制器
│   └── animation_config.gd   # 动画配置（时间、缓动曲线）
│
├── board_controller.gd       # 主控制器，协调逻辑与动画
└── tile_view.gd              # 方块视觉节点
```

### 11.3 GravitySystem 核心接口设计

```gdscript
# gravity_system.gd
class_name GravitySystem
extends Node

signal all_settled()

# 动画参数配置
@export var fall_time_per_cell: float = 0.12
@export var column_delay: float = 0.05
@export var row_stagger_delay: float = 0.03
@export var easing_type: Tween.EaseType = Tween.EASE_IN
@export var transition_type: Tween.TransitionType = Tween.TRANS_QUAD

# 核心方法：将逻辑层的移动指令转化为动画
func apply_gravity(moves: Array[BoardLogic.MoveInfo]) -> void:
    var tween = create_tween()
    tween.set_parallel(true)  # 并行播放
    
    for move in moves:
        var tile = move.tile
        var target_pos = GridHelper.grid_to_pixel(move.to_column, move.to_row)
        var distance = move.fall_distance
        var delay = _calculate_delay(move)
        var duration = distance * fall_time_per_cell
        
        var tw = tween.tween_property(tile, "position", target_pos, duration)
        tw.set_delay(delay)
        tw.set_ease(easing_type)
        tw.set_trans(transition_type)
    
    tween.finished.connect(_on_falling_done)
```

### 11.4 状态机（State Machine）管理流程

使用状态机管理整个消除循环，推荐 Godot 4.x 的枚举状态机模式：

```gdscript
enum GameState {
    IDLE,           # 等待玩家输入
    SWAPPING,       # 交换动画中
    CHECKING,       # 检测匹配中
    MARKING,        # 标记匹配方块
    DELETING,       # 删除动画中
    COLLAPSING,     # 重力下落中
    SPAWNING,       # 新方块生成中
    CASCADE_CHECK   # 级联检测
}

var current_state: GameState = GameState.IDLE

func _process_state(current_state):
    match current_state:
        GameState.IDLE:
            # 等待玩家输入
            pass
        GameState.SWAPPING:
            # 播放交换动画
            # 动画结束后 → CHECKING
            pass
        GameState.CHECKING:
            var matches = board_logic.find_matches()
            if matches.is_empty():
                # 无匹配 → IDLE
                change_state(GameState.IDLE)
            else:
                # 有匹配 → MARKING
                change_state(GameState.MARKING)
        # ... 其他状态
```

### 11.5 逻辑/表现分离：Godot Forum 讨论的实现

Godot Forum 上一个关于消消乐架构的讨论（https://forum.godotengine.org/t/51204）提供了清晰的分离方案：

**逻辑层 (Board.gd)**：
```gdscript
class_name Board
extends Resource

var grid_dictionary: Dictionary = {}  # Vector2 : Tile

func move(first: Vector2, second: Vector2):
    # 纯数据操作，不涉及渲染
    pass

func remove_tile(position: Vector2):
    pass
```

**服务层 (GameService.gd)**：
```gdscript
class_name GameService
extends Node2D

@export var board: Board

func check_matches() -> Array:
    return []

func collapse_and_spawn():
    # 调用 Board 逻辑 + 触发动画
    pass
```

**表现层 (BoardUI.gd)**：
```gdscript
class_name BoardUI
extends Node2D

# 监听逻辑层信号，驱动视觉
func initialize():
    pass

func on_tile_moved(from: Vector2, to: Vector2):
    # 播放 Tween 动画
    pass
```

---

## 12. Godot 中的实现参考

### 12.1 Peanuts Code 教程的实现

[Peanuts Code 的 Godot 3 消消乐教程](https://www.peanuts-code.com/en/tutorials/gd0012_match3/) 提供了一个完整的基础实现，关键代码片段：

```gdscript
# 自动处理的 while 循环
while check_matches():
    find_matches()
    yield(get_tree().create_timer(0.3), "timeout")
    delete_matches()
    yield(get_tree().create_timer(0.3), "timeout")
    collapse_columns()     # 重力下落
    yield(get_tree().create_timer(0.3), "timeout")
    spawn_pieces()         # 生成新方块
    yield(get_tree().create_timer(0.3), "timeout")
```

> 注意：Godot 4.x 中 `yield` 已被 `await` 替代，上述代码需改写为 `await get_tree().create_timer(0.3).timeout`。

### 12.2 Godot 4.x Tween 用法示例

```gdscript
func animate_fall(tile: Node2D, target: Vector2, 
                   distance: int, column: int) -> Tween:
    var tween = create_tween()
    
    # 计算延迟：列间延迟 + 行间错开
    var delay = column * column_delay + distance * row_stagger_delay
    var duration = distance * fall_time_per_cell
    
    tween.tween_property(tile, "position", target, duration) \
         .set_delay(delay) \
         .set_ease(Tween.EASE_IN) \
         .set_trans(Tween.TRANS_QUAD)
    
    # 着陆弹簧效果（可选）
    if bounce_enabled:
        tween.tween_property(tile, "position:y", target.y - bounce_height, bounce_duration) \
             .set_ease(Tween.EASE_OUT)
        tween.tween_property(tile, "position:y", target.y, bounce_duration * 0.6) \
             .set_ease(Tween.EASE_IN_OUT)
    
    return tween
```

### 12.3 Godot 4.x await 异步处理

```gdscript
# Godot 4.x 中完整的异步消除循环
func _on_player_swapped():
    is_processing = true
    
    while true:
        var matches = find_all_matches()
        if matches.is_empty():
            break
        
        # 标记匹配
        mark_matches(matches)
        await get_tree().create_timer(MARK_DISPLAY_TIME).timeout
        
        # 删除匹配
        delete_matched_tiles()
        await get_tree().create_timer(DELETE_ANIM_TIME).timeout
        
        # 重力下落
        collapse_columns()
        await get_tree().create_timer(FALL_ANIM_TIME).timeout
        
        # 生成新方块
        spawn_new_pieces()
        await get_tree().create_timer(SPAWN_ANIM_TIME).timeout
    
    is_processing = false
    player_can_interact = true
```

---

## 13. 常见问题与解决方案

### 13.1 动画播放中玩家再次操作

**问题**：下落动画未完成时，玩家尝试交换方块，导致数据混乱。

**解决方案**：
```gdscript
# 使用状态锁
var can_interact: bool = true

func _on_player_swipe(start_grid, direction):
    if not can_interact:
        return  # 忽略输入
    # ... 处理交换

# 在动画开始时锁定
func start_gravity_animation():
    can_interact = false
    # ... 播放动画
    tween.finished.connect(func(): can_interact = true)
```

### 13.2 新方块生成时生成新匹配导致无限循环

**问题**：生成的新方块恰好与邻近方块形成匹配，在某些极端随机棋盘上可能造成理论上永不终止的循环。

**解决方案**：
- 最多循环次数限制（如 100 次级联后强制中断）
- 生成时检查是否与周围形成匹配，若是则换一种颜色
- 现代消消乐通常**接受**级联匹配（这是游戏乐趣的一部分），仅需确保不会无限循环（极罕见）

### 13.3 方块下落时视觉穿透

**问题**：多个方块同时下落时，由于渲染顺序问题导致方块「穿过」下方方块。

**解决方案**：
```gdscript
# 确保 z_index 随行号改变
func set_tile_z_index(tile, row):
    tile.z_index = row
    # 或使用 CanvasItem 的 z_index
    tile.z_index = board_height - row  # 底部的方块渲染在上层
```

### 13.4 Tween 数量过多导致性能问题

**问题**：大盘面（如 9x9）同时有多个方块下落时，同时创建数十个 Tween 可能导致帧率下降。

**解决方案**：
- 合并同列方块的动画到单个 Tween（减少 Tween 实例数）
- 对于简单直线下落，使用 `_process` 中的解析方程而非 Tween
- 限制同时活跃的 Tween 数量上限

---

## 14. 参考资料

| 来源 | 链接 | 关键内容 |
|------|------|----------|
| Catlike Coding Match-3 | https://catlikecoding.com/unity/tutorials/prototypes/match-3/ | 逻辑/表现分离架构；Move 抽象；hot reload |
| GameDev SE: Fill Empty Space | https://gamedev.stackexchange.com/questions/68865 | 逐列下落基础算法 |
| GameDev SE: Physics Bouncing | https://gamedev.stackexchange.com/questions/175331 | 物理引擎问题与替代方案 |
| GameDev.net: Smooth Falling | https://gamedev.net/forums/topic/702077 | 错开下落；弹簧着陆；解析法 |
| Peanuts Code: Godot Match-3 | https://www.peanuts-code.com/en/tutorials/gd0012_match3/ | Godot 3 完整实现 |
| Godot Forum: Architecture | https://forum.godotengine.org/t/51204 | 逻辑/表现分离设计 |
| GSAP Community: Stagger Timing | https://gsap.com/community/forums/topic/36161 | 动画错开时序 |
| Medium: Gravity without Swaps | https://becominghuman.ai/match-3-with-gravity-without-swaps-9b7758a8cddd | 重力算法理论基础 |
| CS50: Match-3 Lecture | https://www.youtube.com/watch?v=jNOjPpanOBM | 哈佛 CS50 游戏开发课程 |
| Canopy Games: Godot Match-3 Course | https://www.canopy.games/p/zooblocks | Godot 4.x 付费教程 |

---

> **文档版本**：v1.0  
> **最后更新**：2026-06-08  
> **编写依据**：以上所有列出的参考资料，结合 GameDev 社区实践和商业消消乐的公开行为分析。
