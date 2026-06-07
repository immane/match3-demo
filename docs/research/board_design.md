# Match-3 棋盘设计研究文档

> 本文档整理了三消游戏（Match-3）棋盘设计的核心技术要点，涵盖棋盘尺寸、网格表示、初始化策略、视觉设计、边界处理、坐标系统、布局设计、单元格数据结构以及撤销/重做支持。

---

## 目录

1. [常见棋盘尺寸与权衡](#1-常见棋盘尺寸与权衡)
2. [网格表示方式](#2-网格表示方式)
3. [棋盘初始化策略](#3-棋盘初始化策略)
4. [视觉棋盘设计](#4-视觉棋盘设计)
5. [边界处理与交换限制](#5-边界处理与交换限制)
6. [棋盘坐标系统](#6-棋盘坐标系统)
7. [棋盘布局设计](#7-棋盘布局设计)
8. [单元格数据结构](#8-单元格数据结构)
9. [棋盘状态快照与撤销/重做](#9-棋盘状态快照与撤销重做)
10. [参考资源](#10-参考资源)

---

## 1. 常见棋盘尺寸与权衡

### 1.1 尺寸对比

| 尺寸 | 单元格数 | 典型游戏 | 优点 | 缺点 |
|------|---------|---------|------|------|
| 6×6 | 36 | 部分休闲三消 | 紧凑、快速完成、适合小屏 | 匹配机会少，策略深度不足 |
| 7×7 | 49 | 部分益智三消 | 居中平衡 | 较少见，视觉居中难处理 |
| 8×8 | 64 | Bejeweled、经典三消 | 经典尺寸、匹配合适、视觉舒适 | 移动端可能稍显拥挤 |
| 9×9 | 81 | Candy Crush（部分关卡） | 匹配机会多、策略丰富 | 屏幕占用大、单局时间长 |

### 1.2 关键考量

- **6×6**：适合入门级/儿童向游戏。宝石尺寸可做大，视觉效果突出。但匹配可能性检测频率需更高（更容易出现无法匹配的死局）。
- **7×7**：奇数尺寸居中对称，适合需要中心定位的设计（如特殊道具在正中央生成）。
- **8×8**：最经典。对于 8×8 的标准网格，水平和垂直方向只有 **96 个可能的匹配位置**（每行6个位置 × 8行 + 每列6个位置 × 8列 = 96），匹配检测非常快（毫秒级）。
- **9×9**：Candy Crush 经典尺寸。81 个格子提供丰富的策略空间。但注意：行/列为奇数时，对称匹配检测逻辑更直观。

### 1.3 移动端适配建议

```
屏幕宽度 ÷ (单元格尺寸 + 间距) ≈ 可容纳列数

例如：1080px 屏幕，每格 100px + 10px 间距
→ 1080 ÷ 110 ≈ 9.8 → 最多 9 列
```

建议移动端优先选择 **8×8** 或 **7×7**，平板可扩展到 **9×9**。

---

## 2. 网格表示方式

### 2.1 二维数组（最常用）

游戏逻辑层使用纯数据数组，视觉层独立渲染。

```
// 传统二维数组
gem_type board[ROWS][COLS];

// 线性数组（性能更优）
// 索引公式：index = row * COLS + col
gem_type board[ROWS * COLS];
```

**为什么用一维线性数组？**
- C# 的二维数组实际上是锯齿数组（jagged array），一维数组配合索引运算更高效。
- GDScript 中同样推荐使用 `Array[Array]` 或直接的线性 `PackedArray`。
- 缓存友好，遍历更快。

**GDScript 示例：**
```gdscript
# 方式一：二维数组
var board: Array[Array] = []
for row in range(ROWS):
    board.append([])
    for col in range(COLS):
        board[row].append(null)

# 方式二：一维线性数组（推荐）
var board_data: Array = []
board_data.resize(ROWS * COLS)

# 访问辅助函数
func get_cell(row: int, col: int):
    return board_data[row * COLS + col]

func set_cell(row: int, col: int, value):
    board_data[row * COLS + col] = value
```

### 2.2 六边形网格

```
    / \     / \     / \
   /   \   /   \   /   \
  / 0,0 \ / 1,0 \ / 2,0 \
  \     / \     / \     /
   \   /   \   /   \   /
    \ / 0,1 \ / 1,1 \ /
     --------- --------
      \     / \     /
       \   /   \   /
        \ / 0,2 \ /
```

六边形网格的常见坐标系统：
- **偏移坐标（Offset Coordinates）**：将六边形"压扁"为矩形行列，区分奇偶行偏移。
- **轴向坐标（Axial Coordinates, q/r）**：使用三个轴中的两个（q, r），s = -q - r。
- **立方体坐标（Cube Coordinates, x/y/z）**：满足 x + y + z = 0 的三维坐标。

Godot 4 原生支持 Hex TileMap，可直接使用六边形网格。

### 2.3 不规则形状

通过二维数组 + `null`/空值标记实现：
```gdscript
# 菱形/异形棋盘：不可用位置设为 null
var shaped_board = [
    [-1, -1,  0,  0,  0, -1, -1],
    [-1,  0,  0,  0,  0,  0, -1],
    [ 0,  0,  0,  0,  0,  0,  0],
    [ 0,  0,  0,  0,  0,  0,  0],
    [ 0,  0,  0,  0,  0,  0,  0],
    [-1,  0,  0,  0,  0,  0, -1],
    [-1, -1,  0,  0,  0, -1, -1],
]
# -1 表示该位置不存在/被障碍物占据
```

---

## 3. 棋盘初始化策略

### 3.1 核心要求

棋盘初始状态**不允许有任何已存在的匹配** — 玩家应该在空白棋盘上自己创造匹配。

### 3.2 方法一：受限随机填充

```gdscript
func initialize_board_no_matches():
    for row in range(ROWS):
        for col in range(COLS):
            var excluded_types = []
            
            # 检查左侧两个是否相同 → 禁止第三个
            if col >= 2:
                if board[row][col-1] == board[row][col-2]:
                    excluded_types.append(board[row][col-1])
            
            # 检查上方两个是否相同 → 禁止第三个
            if row >= 2:
                if board[row-1][col] == board[row-2][col]:
                    excluded_types.append(board[row-1][col])
            
            # 从允许的类型中随机选择
            var allowed = TYPES.filter(func(t): return t not in excluded_types)
            board[row][col] = allowed[randi() % allowed.size()]
```

**优点**：一趟扫描即可完成，性能好。
**缺点**：可能导致后期格子无可用类型（概率极低，且可通过回溯解决）。

### 3.3 方法二：生成后修复

```gdscript
func initialize_with_fix():
    # 1. 完全随机填充
    for row in range(ROWS):
        for col in range(COLS):
            board[row][col] = randi() % NUM_TYPES
    
    # 2. 迭代修复所有已存在的匹配
    var changes_made = true
    while changes_made:
        changes_made = false
        for row in range(ROWS):
            for col in range(COLS):
                if has_match_at(row, col):
                    # 替换为不会形成匹配的随机类型
                    board[row][col] = get_safe_type(row, col)
                    changes_made = true
```

### 3.4 方法三：逆向生成（保证可解）

1. 创建空棋盘
2. 在随机位置放置已匹配的三连组
3. 通过可控的洗牌算法打散
4. 确保最终无三连匹配

### 3.5 死局检测与洗牌

初始化结束后也需要检测：**当前棋盘是否存在至少一个可行交换？**

```gdscript
func has_valid_moves() -> bool:
    for row in range(ROWS):
        for col in range(COLS):
            # 模拟与右侧交换
            if col < COLS - 1:
                swap(row, col, row, col + 1)
                if find_matches().size() > 0:
                    swap(row, col, row, col + 1)  # 换回
                    return true
                swap(row, col, row, col + 1)  # 换回
            
            # 模拟与下方交换
            if row < ROWS - 1:
                swap(row, col, row + 1, col)
                if find_matches().size() > 0:
                    swap(row, col, row + 1, col)
                    return true
                swap(row, col, row + 1, col)
    return false
```

若无可行移动，执行**洗牌**或**重新生成棋盘**。有些游戏会弹出提示"没有可用的移动了，正在重新排列"。

---

## 4. 视觉棋盘设计

### 4.1 棋盘布局参数

```
┌──────────────────────────────────────────┐
│              padding_top                  │
│  ┌────────────────────────────────────┐  │
│  │ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗    │  │
│p │ ║ 0 ║ ║ 1 ║ ║ 2 ║ ║ 3 ║ ║ 4 ║  p │  │
│a │ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝  a │  │
│d │   ← spacing →                      d │  │
│d │ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗  d │  │
│i │ ║ 5 ║ ║ 6 ║ ║ 7 ║ ║ 8 ║ ║ 9 ║  i │  │
│n │ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝  n │  │
│g │                                   g │  │
│_ │  ...更多行...                      _ │  │
│l │                                   r │  │
│e │ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗  i │  │
│f │ ║20 ║ ║21 ║ ║22 ║ ║23 ║ ║24 ║  g │  │
│t │ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝  h │  │
│  └────────────────────────────────────┘  │
│              padding_bottom              │
└──────────────────────────────────────────┘
```

### 4.2 关键视觉参数

| 参数 | 说明 | 建议值 |
|------|------|--------|
| `cell_size` | 每个格子的边长（像素） | 移动端 60-90px，桌面端 80-120px |
| `spacing` | 格子之间的间隙 | cell_size 的 5%-10%（约 4-8px） |
| `padding` | 棋盘四周的留白 | cell_size 的 50%-100% |
| `bg_tile_color_1` | 背景格子颜色1（棋盘格） | 柔和的浅色，如 `#F5E6D3` |
| `bg_tile_color_2` | 背景格子颜色2（棋盘格） | 与颜色1有轻微差异，如 `#EDD9C4` |
| `grid_line` | 网格线 | 可选用，通常背景棋盘格已代替网格线 |

### 4.3 背景格子实现

**方式一：TileMap 节点**
- 使用 Godot 的 `TileMap` 节点预制背景层
- 配置棋盘格图案（Checkerboard Pattern）
- 独立于游戏逻辑层

**方式二：自定义绘制**
```gdscript
# 在 _draw() 中绘制背景格子
func _draw():
    for row in range(ROWS):
        for col in range(COLS):
            var pos = grid_to_screen(row, col)
            var rect = Rect2(pos, Vector2(CELL_SIZE, CELL_SIZE))
            
            # 棋盘格交替颜色
            var color = LIGHT_COLOR if (row + col) % 2 == 0 else DARK_COLOR
            draw_rect(rect, color, true)
            
            # 可选：绘制边框
            draw_rect(rect, BORDER_COLOR, false, 1.0)
```

### 4.4 居中计算

```gdscript
# 计算棋盘在屏幕上的偏移量，使其居中
var board_pixel_width = COLS * (CELL_SIZE + SPACING) - SPACING
var board_pixel_height = ROWS * (CELL_SIZE + SPACING) - SPACING

var offset_x = (screen_width - board_pixel_width) / 2.0
var offset_y = (screen_height - board_pixel_height) / 2.0
```

---

## 5. 边界处理与交换限制

### 5.1 有效交换条件

在经典三消中，只能交换**相邻**（上下左右）的格子，不能斜向交换。

```
    ┌───┐
    │ N │   (-1, 0)
┌───┼───┼───┐
│ W │ C │ E │   (0,-1) 当前 (0,+1)
└───┼───┼───┘
    │ S │   (+1, 0)
    └───┘

C = 当前选中格子 (row, col)
N = 上方邻居 (row-1, col)
S = 下方邻居 (row+1, col)
W = 左侧邻居 (row, col-1)
E = 右侧邻居 (row, col+1)
```

### 5.2 边界检测

```gdscript
func is_valid_swap(row1: int, col1: int, row2: int, col2: int) -> bool:
    # 1. 确保两个位置都在棋盘内
    if not is_in_bounds(row1, col1) or not is_in_bounds(row2, col2):
        return false
    
    # 2. 确保两个位置相邻（曼哈顿距离 = 1）
    var distance = abs(row1 - row2) + abs(col1 - col2)
    if distance != 1:
        return false
    
    # 3. 确保两个位置不是障碍物
    if is_obstacle(row1, col1) or is_obstacle(row2, col2):
        return false
    
    return true

func is_in_bounds(row: int, col: int) -> bool:
    return row >= 0 and row < ROWS and col >= 0 and col < COLS
```

### 5.3 边界格子的邻居处理

对于边缘格子，可用的邻居数量会减少：

```
角落 (0,0)     → 2 个邻居（右、下）
边缘 (0,3)     → 3 个邻居（左、右、下）
内部 (3,3)     → 4 个邻居（上下左右）
```

```gdscript
# 获取有效邻居列表
func get_neighbors(row: int, col: int) -> Array:
    var neighbors = []
    var directions = [
        [-1, 0],  # 上
        [1, 0],   # 下
        [0, -1],  # 左
        [0, 1],   # 右
    ]
    for dir in directions:
        var nr = row + dir[0]
        var nc = col + dir[1]
        if is_in_bounds(nr, nc) and not is_obstacle(nr, nc):
            neighbors.append([nr, nc])
    return neighbors
```

### 5.4 交换验证流程

```
玩家交换两个宝石
        │
        ▼
┌──────────────────┐
│ 1. 是否相邻？     │──否──▶ 拒绝交换，播放抖动动画
└──────┬───────────┘
       │是
       ▼
┌──────────────────┐
│ 2. 先在数据层交换 │
│   （不播放动画）   │
└──────┬───────────┘
       │
       ▼
┌──────────────────┐
│ 3. 检测是否产生   │──否──▶ 数据层换回，拒绝交换
│    匹配？         │
└──────┬───────────┘
       │是
       ▼
┌──────────────────┐
│ 4. 播放交换动画   │
│   处理匹配消除    │
│   执行掉落填充    │
│   递归检测连锁    │
└──────────────────┘
```

---

## 6. 棋盘坐标系统

### 6.1 坐标系定义

三消游戏涉及**两个坐标系**：

| 坐标系 | 用途 | 原点位置 | 单位 |
|--------|------|---------|------|
| **逻辑坐标** (grid) | 数据数组索引 | 左上角 (0,0) | 行列号 |
| **屏幕坐标** (screen) | 渲染位置 | Godot: 左上角 (0,0) | 像素 |

```
逻辑坐标 (grid)              屏幕坐标 (screen/pixel)
                                
(0,0)──(0,1)──(0,2)         (0px,0px)──────────────▶ x
  │       │       │            │
(1,0)──(1,1)──(1,2)           │  ● (col * cell_size,
  │       │       │            │     row * cell_size)
(2,0)──(2,1)──(2,2)           ▼
                               y
```

### 6.2 坐标转换

```gdscript
# 逻辑坐标 → 屏幕坐标（格子左上角）
func grid_to_screen(row: int, col: int) -> Vector2:
    return Vector2(
        BOARD_OFFSET_X + col * (CELL_SIZE + SPACING),
        BOARD_OFFSET_Y + row * (CELL_SIZE + SPACING)
    )

# 逻辑坐标 → 屏幕坐标（格子中心）
func grid_to_center(row: int, col: int) -> Vector2:
    var top_left = grid_to_screen(row, col)
    return top_left + Vector2(CELL_SIZE / 2.0, CELL_SIZE / 2.0)

# 屏幕坐标 → 逻辑坐标（点击/触摸位置转行列）
func screen_to_grid(screen_pos: Vector2) -> Dictionary:
    var col = int((screen_pos.x - BOARD_OFFSET_X) / (CELL_SIZE + SPACING))
    var row = int((screen_pos.y - BOARD_OFFSET_Y) / (CELL_SIZE + SPACING))
    
    # 边界检查
    if row < 0 or row >= ROWS or col < 0 or col >= COLS:
        return {"row": -1, "col": -1}
    return {"row": row, "col": col}
```

### 6.3 行列索引惯例

```
惯例（推荐）：
- row: 从上到下递增（row=0 是最顶行）
- col: 从左到右递增（col=0 是最左列）

掉落方向：宝石从 row 小 → row 大 掉落
即新宝石在 row=0（顶部）生成，向下掉落
```

---

## 7. 棋盘布局设计

### 7.1 矩形标准布局

```
┌───┬───┬───┬───┬───┬───┬───┬───┐
│   │   │   │   │   │   │   │   │
├───┼───┼───┼───┼───┼───┼───┼───┤
│   │   │   │   │   │   │   │   │
├───┼───┼───┼───┼───┼───┼───┼───┤
│   │   │   │   │   │   │   │   │
├───┼───┼───┼───┼───┼───┼───┼───┤
│   │   │   │   │   │   │   │   │
└───┴───┴───┴───┴───┴───┴───┴───┘
```

最基础布局，全部格子可玩。Candy Crush、Bejeweled 均以此为基础。

### 7.2 带障碍物的布局

使用**掩码数组**（Mask Array）定义哪些格子可玩：

```gdscript
# 0 = 空位（不可玩）, 1 = 可玩格子
var level_mask = [
    [1, 1, 1, 1, 1, 1, 1, 1],
    [1, 1, 1, 1, 1, 1, 1, 1],
    [1, 1, 0, 0, 0, 0, 1, 1],  # 中间两列有障碍
    [1, 1, 0, 0, 0, 0, 1, 1],
    [1, 1, 1, 1, 1, 1, 1, 1],
    [1, 1, 1, 1, 1, 1, 1, 1],
]
```

**常见障碍类型**：
- **冰层（Frosting）**：需要相邻匹配消除
- **巧克力（Chocolate）**：每回合自动扩散
- **笼子（Cages）**：需要匹配多次才能打破
- **蜂蜜（Honey）**：需要相邻匹配
- **铁块（Iron）**：不可摧毁的永久障碍

### 7.3 异形布局

```
菱形布局:                    十字形布局:
    ┌───┐                    ┌───┬───┬───┐
    │   │                    │   │   │   │
┌───┼───┼───┐            ┌───┼───┼───┼───┼───┐
│   │   │   │            │   │   │   │   │   │
├───┼───┼───┼───┐        ├───┼───┼───┼───┼───┤
│   │   │   │   │        │   │   │   │   │   │
└───┼───┼───┼───┘        └───┼───┼───┼───┼───┘
    │   │   │                │   │   │   │
    └───┴───┘                └───┴───┴───┘
```

通过掩码实现。掉落逻辑需特殊处理：如果下方格子是不可玩的障碍，宝石应在此停止或跳过障碍。

### 7.4 多层/分层设计

一些游戏（如 Royal Match）使用分层结构：
- 上层有可匹配的宝石
- 下层有需要收集的物品
- 消除上层宝石后，才能接触到下层物品

---

## 8. 单元格数据结构

### 8.1 每个单元格需要存储的数据

```gdscript
class CellData:
    var gem_type: int = -1           # 宝石类型（0-N，-1 表示空）
    var row: int = 0                 # 行索引
    var col: int = 0                 # 列索引
    var state: int = STATE_NORMAL    # 状态标志位
    var special_type: int = -1       # 特殊类型（无=-1, 条纹=0, 炸弹=1, 彩虹=2）
    var is_obstacle: bool = false    # 是否为障碍物
    var obstacle_hp: int = 0         # 障碍物剩余耐久度
    var visual_node: Node = null     # 关联的视觉节点引用（弱引用）
    var match_group_id: int = -1     # 匹配组ID（用于连锁检测）
```

### 8.2 状态枚举

```gdscript
enum CellState {
    STATE_IDLE = 0,       # 空闲，等待玩家操作
    STATE_SELECTED = 1,   # 被选中
    STATE_MATCHED = 2,    # 已被匹配，等待消除
    STATE_FALLING = 3,    # 正在掉落
    STATE_SPAWNING = 4,   # 正在生成
    STATE_SWAPPING = 5,   # 正在交换动画中
    STATE_LOCKED = 6,     # 被锁定（不可交互）
}
```

### 8.3 宝石类型设计

```gdscript
enum GemType {
    TYPE_RED = 0,
    TYPE_BLUE = 1,
    TYPE_GREEN = 2,
    TYPE_YELLOW = 3,
    TYPE_PURPLE = 4,
    TYPE_ORANGE = 5,
    # 通常 5-7 种基本类型
}

enum SpecialType {
    SPECIAL_NONE = -1,
    SPECIAL_STRIPED_H = 0,   # 水平条纹（消除整行）
    SPECIAL_STRIPED_V = 1,   # 垂直条纹（消除整列）
    SPECIAL_BOMB = 2,        # 炸弹（消除3×3区域）
    SPECIAL_RAINBOW = 3,     # 彩虹/变色球（消除一种颜色全部）
    SPECIAL_FISH = 4,        # 鱼（随机消除一个目标）
}
```

### 8.4 特殊宝石的生成逻辑

| 匹配形状 | 匹配数量 | 生成特殊宝石 | 消除效果 |
|---------|---------|------------|---------|
| 一行直线 | 4 | 条纹宝石 | 整行/整列 |
| 一列直线 | 4 | 条纹宝石 | 整行/整列 |
| L 形或 T 形 | 5+ | 炸弹 | 3×3 区域 |
| 一行直线 | 5 | 彩虹球 | 全部同色 |

检测 L 形/T 形：一个宝石同时位于**横向匹配**和**纵向匹配**的交点位置。

```gdscript
func evaluate_special_tile(matches: Array) -> Dictionary:
    for match in matches:
        if match.size() >= 5:
            # 检测是否是 L/T 形
            for cell in match:
                if is_in_horizontal_match(cell) and is_in_vertical_match(cell):
                    return {"type": SPECIAL_BOMB, "position": cell}
            # 5 个直线
            return {"type": SPECIAL_RAINBOW, "position": match[match.size() / 2]}
        elif match.size() == 4:
            return {"type": SPECIAL_STRIPED_H, "position": match[match.size() / 2]}
```

---

## 9. 棋盘状态快照与撤销/重做

### 9.1 设计模式选择

| 模式 | 描述 | 适用场景 |
|------|------|---------|
| **Memento 模式** | 保存整个棋盘状态快照 | 棋盘状态完整、撤销次数不多 |
| **Command 模式** | 记录每个操作及逆向操作 | 操作种类多、需要细粒度撤销 |
| **混合模式** | 快照 + 命令结合 | 推荐方式 |

### 9.2 快照数据结构

```gdscript
class BoardSnapshot:
    var grid_data: Array          # 棋盘数据（gem_type 的平面数组）
    var special_data: Array       # 特殊宝石类型数组
    var obstacle_data: Array      # 障碍物数据数组
    var score: int                # 当前分数
    var moves_remaining: int      # 剩余步数
    var combo_count: int          # 连锁计数
    
    func to_dict() -> Dictionary:
        return {
            "grid": grid_data.duplicate(),
            "specials": special_data.duplicate(),
            "obstacles": obstacle_data.duplicate(),
            "score": score,
            "moves": moves_remaining,
            "combo": combo_count,
        }
    
    func from_dict(data: Dictionary):
        grid_data = data["grid"]
        special_data = data["specials"]
        obstacle_data = data["obstacles"]
        score = data["score"]
        moves_remaining = data["moves"]
        combo_count = data["combo"]
```

### 9.3 快照最小化

完整棋盘快照可能过大。**最小化快照**只存储与上次的差异：

```gdscript
class CompactSnapshot:
    var operations: Array = []  # 记录一系列操作
    
    class SwapOperation:
        var row1: int
        var col1: int
        var row2: int
        var col2: int
        var removed_cells: Array  # 被消除的单元格列表
        var spawned_cells: Array  # 新生成的单元格列表
    
    func record_swap(op: SwapOperation):
        operations.append(op)
    
    func undo_last() -> SwapOperation:
        if operations.is_empty():
            return null
        return operations.pop_back()
```

### 9.4 撤销实现（Command 模式）

```gdscript
class UndoRedoSystem:
    var undo_stack: Array = []   # 撤销栈
    var redo_stack: Array = []   # 重做栈
    const MAX_HISTORY = 50       # 最大历史记录数
    
    func record_action(swap_info: Dictionary):
        # 保存前后状态差异
        var action = {
            "swap": swap_info,
            "snapshot_before": capture_minimal_state(),  # 只保存受影响区域
        }
        undo_stack.append(action)
        redo_stack.clear()  # 新操作清空重做栈
        
        if undo_stack.size() > MAX_HISTORY:
            undo_stack.pop_front()
    
    func undo() -> bool:
        if undo_stack.is_empty():
            return false
        
        var action = undo_stack.pop_back()
        redo_stack.append(action)
        
        # 恢复棋盘到操作前状态
        restore_minimal_state(action["snapshot_before"])
        return true
    
    func redo() -> bool:
        if redo_stack.is_empty():
            return false
        
        var action = redo_stack.pop_back()
        undo_stack.append(action)
        
        # 重新执行交换
        execute_swap(action["swap"])
        return true
```

### 9.5 快照优化建议

1. **只保存差异**：不是每次保存整个 8×8 棋盘，只记录本次操作影响到的单元格。
2. **深拷贝数组**：使用 `duplicate(true)` 确保修改快照不影响原数据。
3. **限制栈深度**：移动端建议 10-20 步历史，桌面端可扩展到 50-100 步。
4. **序列化为字典**：方便存档/读档时持久化。

```gdscript
# 轻量级状态捕获：只保存关键数据
func capture_compact_snapshot() -> Dictionary:
    var snapshot = {}
    # 完整棋盘类型快照（值类型数组，拷贝快）
    snapshot["types"] = board_data.duplicate()
    # 特殊宝石位置（通常只有少数几个）
    snapshot["specials"] = {}
    for i in range(board_data.size()):
        if special_data[i] != SPECIAL_NONE:
            snapshot["specials"][i] = special_data[i]
    return snapshot
```

### 9.6 适合 Match-3 的推荐方案

推荐使用 **Command + 轻量快照混合**：

- 每次有效交换记录一个 Command（包含交换的起始/目标位置）
- Command 的 `undo()` 方法不仅恢复棋盘数据，还反向播放消除/掉落
- 对于连锁消除，一次性记录整个连锁周期为一个 Command
- 进阶：使用**增量快照**（只记录变化的格子索引 → 变化前的值）

---

## 10. 参考资源

### 10.1 经典实现参考

| 资源 | 描述 |
|------|------|
| [CS50 Match-3 Lecture](https://www.youtube.com/watch?v=jNOjPpanOBM) | 哈佛CS50 三消游戏开发讲座 |
| [Catlike Coding - Match 3](https://catlikecoding.com/unity/tutorials/prototypes/match-3/) | Unity 三消原型教程（深度） |
| [Azumo - The Logic Behind Match-3](https://azumo.com/insights/the-logic-behind-match-3-games) | 三消核心逻辑详解 |
| [Logic Simplified - Key Algorithms](https://logicsimplified.com/newgames/key-algorithmic-tricks-for-match-3-game-development/) | 三消关键算法汇总 |

### 10.2 Godot 相关

| 资源 | 描述 |
|------|------|
| [KidsCanCode Grid Movement](https://kidscancode.org/godot_recipes/4.x/2d/grid_movement/index.html) | Godot 网格移动教程 |
| [Godot Hex Grid Tutorial](https://www.youtube.com/watch?v=1qmXFIJU1QE) | Godot 4 六边形网格 |
| [Match-3 Godot YouTube Series](https://www.youtube.com/playlist?list=PL4vbr3u7UKWqwQlvwvgNcgDL1p_3hcNn2) | Godot 三消完整系列 |

### 10.3 问题讨论

| 资源 | 描述 |
|------|------|
| [GameDev SE - Board Generation](https://gamedev.stackexchange.com/questions/67078/) | 棋盘生成算法讨论 |
| [Unity Forums - No Starting Matches](https://discussions.unity.com/t/match3-how-can-i-ensure-the-board-starts-with-no-matches/111066) | 初始无匹配实现 |
| [Reddit - Undo System](https://www.reddit.com/r/gamedev/comments/1gwdrb3/) | 网格游戏撤销系统讨论 |

---

> **编写日期**：2026-06-08
> **语言**：中文 / Chinese
> **适用引擎**：Godot 4.x（GDScript），概念适用于所有引擎
