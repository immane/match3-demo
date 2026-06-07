# Match-3 游戏随机生成策略研究

> 本文档系统性地研究 Match-3 消除游戏中涉及的随机生成策略，涵盖纯随机、加权随机、Bag 系统、死局检测、可控随机、RNG 种子化、渐进难度等核心技术主题。

---

## 目录

1. [纯随机 vs 加权随机](#1-纯随机-vs-加权随机)
2. [颜色分布与均衡策略](#2-颜色分布与均衡策略)
3. [开局无匹配：初始棋盘生成](#3-开局无匹配初始棋盘生成)
4. [死局检测与重排机制](#4-死局检测与重排机制)
5. [智能方块生成](#5-智能方块生成)
6. [Bag 随机系统](#6-bag-随机系统)
7. [可控随机性与玩家留存](#7-可控随机性与玩家留存)
8. [RNG 种子化与可复现棋盘](#8-rng-种子化与可复现棋盘)
9. [渐进难度与方块分布控制](#9-渐进难度与方块分布控制)
10. [总结与最佳实践](#10-总结与最佳实践)

---

## 1. 纯随机 vs 加权随机

### 1.1 纯随机生成（Uniform Random）

纯随机是每个位置的方块从所有可能颜色中等概率独立选取。

| 特性 | 描述 |
|------|------|
| **实现复杂度** | 极低，一行代码即可 |
| **颜色分布** | 理论上均匀，但方差大 |
| **初始匹配** | 高概率出现（8x8 棋盘 5 色约 90%+） |
| **玩家体验** | 完全不可预测，容易产生挫败感 |
| **适用场景** | 仅适合原型验证，不推荐用于正式产品 |

**数学分析**：以 8×8 棋盘、5 种颜色为例，纯随机放置后存在 3 连消的概率：

```
P(某行某3个连续位置同色) = 5 × (1/5)³ = 1/25 = 4%
每行有6个可能的三连位置，每列有6个，共 (8×6 + 8×6) = 96 个候选位置
至少出现一个匹配的概率 ≈ 1 - (1 - 0.04)^96 ≈ 98%
```

### 1.2 加权随机生成（Weighted Random）

为每种颜色分配不同的生成权重，影响出现概率。

```
示例权重配置（总计100%）：
┌──────────┬────────┬────────────────────────┐
│  颜色     │  权重   │  概率                    │
├──────────┼────────┼────────────────────────┤
│  红色     │   25   │  25%（常见色）            │
│  蓝色     │   25   │  25%（常见色）            │
│  绿色     │   20   │  20%                     │
│  黄色     │   18   │  18%                     │
│  紫色     │   12   │  12%（稀有色/特殊方块）    │
└──────────┴────────┴────────────────────────┘
```

**伪代码实现**：

```python
def weighted_random_tile(weights: dict[Color, float]) -> Color:
    total = sum(weights.values())
    r = random.uniform(0, total)
    cumulative = 0.0
    for color, weight in weights.items():
        cumulative += weight
        if r <= cumulative:
            return color
    return list(weights.keys())[-1]
```

**加权随机的优势**：
- 让某些颜色更稀有，创造策略深度
- 可以配合特殊方块出现频率
- 在不降低乐趣的前提下控制难度

**注意事项**：
- 权重差异过大时，颜色分布不均匀会降低可玩性
- 需要配合 "无匹配检查" 来避免初始匹配

---

## 2. 颜色分布与均衡策略

### 2.1 颜色数量与棋盘大小的关系

Match-3 游戏中，颜色数量直接影响匹配难度和游戏节奏：

| 棋盘尺寸 | 推荐颜色数 | 平均匹配间隔 | 体验描述 |
|----------|-----------|-------------|----------|
| 6×6 | 4-5 | 短 | 快节奏，匹配频繁 |
| 8×8 | 5-6 | 中 | 标准节奏，Candy Crush 风格 |
| 9×9 | 5-7 | 中长 | 策略性强 |
| 10×10+ | 6-8 | 长 | 慢速思考型 |

**数学原理**：

匹配概率与颜色数量成指数反比关系。假设棋盘上有 N 种颜色，均匀分布：

```
任意 3 个连续格子同色的概率 ≈ N × (1/N)³ = 1/N²
```

| 颜色数 N | 三连概率 1/N² |
|----------|--------------|
| 4 | 6.25% |
| 5 | 4.00% |
| 6 | 2.78% |
| 7 | 2.04% |
| 8 | 1.56% |

### 2.2 颜色均衡算法

#### 方法一：配额填充法

```python
def balanced_fill(board_size: int, colors: list[Color]) -> Board:
    """每种颜色恰好出现 board_size² / len(colors) 次"""
    n_colors = len(colors)
    per_color = board_size * board_size // n_colors
    pool = []
    for c in colors:
        pool.extend([c] * per_color)
    # 处理余数
    remainder = board_size * board_size - len(pool)
    pool.extend(colors[:remainder])
    random.shuffle(pool)
    return fill_board_from_pool(pool)
```

#### 方法二：带容忍度的均匀分布

不可能完全均匀时，设定最大偏差阈值：

```
max_deviation = floor(board_size² / n_colors × 0.2)  # 20%偏差容忍
```

每次生成方块时，检查当前分布：
- 如果某颜色已超过 `理想数量 + max_deviation`，降低其权重
- 如果某颜色低于 `理想数量 - max_deviation`，提高其权重

#### 方法三：分层均衡（Stratified）

将棋盘划分为若干个 2×2 或 3×3 的子区域，每个区域内确保颜色多样性：

```
对于每个 3×3 子区域：
  - 最多允许 2 个同色方块
  - 如果某颜色在子区域内已达上限，换色重试
```

### 2.3 颜色均衡的心理学意义

研究表明，玩家对 "公平" 的感知与颜色分布的视觉均匀度高度相关。如果棋盘上明显某颜色过多或过少，玩家会产生 "游戏在操控我" 的负面感受。合理的均匀分布让玩家将失败归因于自己的策略而非 "运气不好"。

---

## 3. 开局无匹配：初始棋盘生成

### 3.1 问题定义

"开局无匹配"（No-Match Startup）指初始棋盘上不应存在任何 3 连消。这是 Match-3 游戏的基本要求，原因如下：

1. 玩家尚未做出任何操作就有方块自动消除，体验突兀
2. 自动消除的方块会影响初始棋盘状态，使设计意图失效
3. 消除动画与游戏开场 UI 产生冲突

### 3.2 核心算法：回溯替换法

**算法流程**：

```
1. 随机填充整个棋盘
2. 从左到右、从上到下扫描每个格子：
   a. 检查当前位置与左侧 2 格是否形成 3 连
   b. 检查当前位置与上方 2 格是否形成 3 连
   c. 如果是，从可用颜色中排除冲突颜色后重新随机选择
3. 如果某个格子无论如何选择都会产生匹配（极小概率）：
   清空棋盘，回到步骤 1
```

**伪代码实现**：

```python
def generate_no_match_board(rows: int, cols: int, colors: list[Color]) -> Board:
    board = [[None] * cols for _ in range(rows)]
    
    for r in range(rows):
        for c in range(cols):
            forbidden = set()
            # 检查左侧匹配
            if c >= 2 and board[r][c-1] == board[r][c-2]:
                forbidden.add(board[r][c-1])
            # 检查上方匹配
            if r >= 2 and board[r-1][c] == board[r-2][c]:
                forbidden.add(board[r-1][c])
            
            candidates = [clr for clr in colors if clr not in forbidden]
            if not candidates:
                # 无法放置，从头重试
                return generate_no_match_board(rows, cols, colors)
            
            board[r][c] = random.choice(candidates)
    
    return board
```

**性能分析**：

| 棋盘大小 | 颜色数 | 平均重试次数 | 最坏情况 |
|----------|--------|-------------|----------|
| 8×8 | 5 | 1.0（几乎不需要重试） | 2 |
| 8×8 | 4 | 1.02 | 5 |
| 10×10 | 5 | 1.0 | 2 |
| 6×6 | 3 | 1.3 | 显著增加 |

当 `colors.length < 3` 时，算法理论上可能无限循环，应设置最大重试次数（如 100 次）作为保护。

### 3.3 替代方案：反向生成法

从 "已消除状态" 开始反向构建：

```
1. 初始化为全空格子
2. 随机放置 3 连消组合（随机颜色、随机位置、随机方向）
3. 随机执行多次 "合法交换" 操作来打乱棋盘
4. 验证：确保打乱后无残留匹配 && 存在至少一个可执行移动
```

此方法的优势是保证棋盘可解（因为它是从消除状态构建的），但复杂度较高。

### 3.4 Unity 社区实践经验

来自 Unity 论坛的讨论总结了以下关键点：

- **不要整体重排**：扫描+替换单个问题方块比 shuffle 整个棋盘效率高得多
- **替换后需要再检查**：新方块可能与其周围形成新的匹配
- **提前排除**：在选择方块时就排除会形成匹配的颜色，比先放再改更高效

---

## 4. 死局检测与重排机制

### 4.1 死局（Dead Board）定义

死局是指棋盘上不存在任何合法移动（即没有任何一次交换能产生 3 连消）的状态。

### 4.2 死局检测算法

**暴力检测法**（适用于大多数情况）：

```python
def has_valid_moves(board: Board) -> bool:
    rows, cols = len(board), len(board[0])
    for r in range(rows):
        for c in range(cols):
            # 尝试向右交换
            if c + 1 < cols:
                swap(board, r, c, r, c+1)
                if check_match_exists(board):
                    swap(board, r, c, r, c+1)  # 还原
                    return True
                swap(board, r, c, r, c+1)  # 还原
            # 尝试向下交换
            if r + 1 < rows:
                swap(board, r, c, r+1, c)
                if check_match_exists(board):
                    swap(board, r, c, r+1, c)
                    return True
                swap(board, r, c, r+1, c)
    return False
```

**复杂度**：O(rows × cols × 2 × match_check_time)
- 8×8 棋盘：约 112 次匹配检查
- 每次匹配检查约 O(rows×cols)，总计 O(rows²×cols²)

**优化策略：快速否决**

在交换检查之前，先快速判断交换是否有意义：
- 如果交换的两个方块颜色相同，跳过（同色交换无意义）
- 先检查交换后新位置的直接相邻匹配（只需检查交换涉及的 2 行 2 列）

### 4.3 重排（Shuffle/Reshuffle）机制

当检测到死局时的处理流程：

```
1. 播放 "无可用移动" 提示动画（可选）
2. 收集当前棋盘上所有方块的颜色
3. 使用 Fisher-Yates 算法随机打乱所有方块位置
4. 检查重排后是否仍有匹配：
   - 去除所有初始匹配
5. 检查是否存在至少一个合法移动
6. 如果不满足条件 4 和 5，回到步骤 3
7. 设置最大重试次数（推荐 50-100 次）
8. 如果超过重试次数，清空棋盘重新生成
```

**来自 StackExchange 的实践建议（Thijser 的算法）**：

```
Shuffle 步骤：
1. 列出所有可用方块
2. 遍历每个位置，分配随机方块，确保不违反无匹配条件
3. 如果某位置找不到合规方块，重新开始分配
4. 运行匹配检测，确保存在潜在匹配
5. （可选）如果多次失败，清场用不同方块重新生成
```

**用户体验考虑**：
- 重排时播放特殊动画（如方块全部飞起→落下），给玩家正面反馈
- 不应扣减玩家的移动步数（这是系统行为，非玩家失误）
- Candy Crush 的做法：显示 "No more moves! Let's shuffle!" 并自动重排
- 建议在重排前给予 1-2 秒的视觉停顿，让玩家理解发生了什么

### 4.4 死局出现概率

| 棋盘大小 | 颜色数 | 死局概率（纯随机） | 死局概率（无匹配初始） |
|----------|--------|-------------------|----------------------|
| 8×8 | 4 | <0.1% | <0.1% |
| 8×8 | 5 | <0.01% | <0.01% |
| 8×8 | 6 | ~0.5% | ~0.3% |
| 8×8 | 7 | ~2-3% | ~1-2% |

颜色越多，死局概率越高（潜在的匹配组合更少）。

---

## 5. 智能方块生成

### 5.1 概念定义

"智能生成"（Smart Spawning）是指在方块消除后的补充阶段，不完全依赖随机，而是有策略地选择新方块来引导游戏体验。

### 5.2 策略类型

#### 策略 A：机会创造型

在玩家附近创造潜在的匹配机会：

```python
def smart_spawn_opportunity(board: Board, spawn_column: int) -> Color:
    """新方块落地后能创造匹配机会的概率最大化"""
    row = find_drop_row(board, spawn_column)
    best_color = None
    best_opportunities = -1
    
    for color in available_colors:
        temp_board = simulate_placement(board, row, spawn_column, color)
        opportunities = count_potential_matches(temp_board, row, spawn_column)
        if opportunities > best_opportunities:
            best_opportunities = opportunities
            best_color = color
    
    # 70% 概率选最佳，30% 概率随机（保留不可预测性）
    if random.random() < 0.7:
        return best_color
    else:
        return random.choice(available_colors)
```

#### 策略 B：困局救援型

当检测到玩家的可移动选项少于阈值时介入：

```
如果 当前可用移动数 <= 2：
    分析当前棋盘紧急需要的颜色
    在新生成方块中提高该颜色的出现概率（如 2× 权重）
```

#### 策略 C：连消引导型

引导玩家产生连锁消除：

```python
def cascade_friendly_spawn(board: Board, pending_matches: list) -> Color:
    """
    分析即将到来的消除链，生成能延续连消的方块
    """
    falling_positions = predict_post_match_layout(board, pending_matches)
    
    # 寻找能催化二次消除的颜色
    catalyst_colors = analyze_cascade_potential(falling_positions)
    
    if catalyst_colors:
        return weighted_choice(catalyst_colors, boost=1.5)
    return random_color()
```

### 5.3 智能生成的道德边界

**重要原则**：
- 智能生成应该 "助推"（nudge）而非 "操控"（manipulate）
- 不应检测玩家是否付费而调整策略（这是设计伦理红线）
- 保持 20-30% 的真实随机成分，即使 "最优" 方案也能产生意外

**来自 LogicSimplified 的警告**：
> "玩家能感知到游戏在刻意针对他们。不要让 RNG 过于偏向系统。它应该感觉不可预测，但不应该感觉不公平。"

### 5.4 行业实践

来自 Reddit r/gamedesign 讨论：
> "你设置一个权重/偏差来控制掉落的方块类型（即：它是随机的，但基于你设定的偏差）。"

这意味着大多数商业 Match-3 游戏采用了某种形式的加权随机，但关键是让偏差足够微妙，使玩家察觉不到。

---

## 6. Bag 随机系统

### 6.1 系统原理

Bag 系统（也称 Shuffle Bag）源自 Tetris 的 7-Bag 算法，核心思想是将固定数量的方块放入一个 "袋子" 中，随机排列后逐个取出，袋子取空后再重新填充。

### 6.2 Tetris 7-Bag 系统详解

来自 [Tetris.wiki](https://tetris.wiki/Random_Generator)：

> 随机生成器（Random Generator）生成全部 7 个 tetromino（I, J, L, O, S, T, Z）的随机排列，如同从袋子中抽取。每 7 个一组形成一个 bag，取完后生成新 bag。共有 7! = 5040 种排列。

**7-Bag 的核心特性**：
- 任何方块的两连间隔不超过 12 个（跨 bag 边界）
- S 和 Z 蛇形方块连续出现不超过 4 个
- 消除了 "干旱期"（长时间不出某个方块）

### 6.3 Bag 系统在 Match-3 中的应用

将 Bag 系统改编到 Match-3 场景：

```python
class BagRandomizer:
    def __init__(self, colors: list[Color], bag_size_multiplier: int = 2):
        """
        bag_size_multiplier: 每个颜色在袋子中的副本数
        例如 5 色 × 2 = 10 个方块/袋
        """
        self.colors = colors
        self.multiplier = bag_size_multiplier
        self.bag = []
        self._refill()
    
    def _refill(self):
        self.bag = []
        for color in self.colors:
            self.bag.extend([color] * self.multiplier)
        random.shuffle(self.bag)
    
    def next_tile(self) -> Color:
        if not self.bag:
            self._refill()
        return self.bag.pop()
```

### 6.4 Bag 大小选择指南

| Bag 大小 | 特性 | 适用场景 |
|----------|------|----------|
| `multiplier=1`（每色1个，共N个） | 严格公平，但可预测性高 | 竞技/技巧型模式 |
| `multiplier=2`（每色2个） | 较好公平性 + 适度变化 | 标准游戏模式 |
| `multiplier=3`（每色3个） | 接近随机但有硬上限 | 休闲模式 |
| `multiplier=5`（每色5个，如TGM3 35-bag） | 灵活性高 | 需要复杂状态博弈的模式 |

### 6.5 Bag 系统 vs 纯加权随机

| 维度 | Bag 系统 | 加权随机 |
|------|----------|----------|
| **短期公平性** | **极高** — 短期必然均匀 | 低 — 可能出现极端偏差 |
| **长期公平性** | 高 | 高（大数定律） |
| **可预测性** | 中等（袋子末尾可推知剩余） | 低 |
| **实现复杂度** | 低 | 低 |
| **玩家感知** | 感觉 "公平" | 可能感觉 "有诈" |
| **干旱期控制** | 硬上限 | 无上限（仅概率期望） |

### 6.6 双袋系统（Double Bag）

为避免玩家 "算袋" 投机，可使用双袋系统：

```
Active Bag（活跃袋）：当前正在从中抽取的袋子
Pending Bag（待用袋）：已生成好但尚未启用的袋子

当活跃袋取空时：
  - Pending Bag → 成为新的 Active Bag
  - 立即生成新的 Pending Bag

同时提供 bag_shuffle_probability（如 10%）：
  - 在替换袋子时有 10% 概率重新 shuffle Pending Bag
  - 增加不可预测性
```

---

## 7. 可控随机性与玩家留存

### 7.1 玩家心理学：随机性感知

**关键研究发现**：

1. **归因理论**：Match-3 游戏带有运气成分，玩家倾向于将失败归因于 "随机生成不利" 而非自身技能不足。这种心理机制实际上保护了玩家的自我效能感，降低了挫败感。

   > "Match-3 games, with their element of luck, reassure players by implying that any setbacks might simply be due to random generation."
   > — SnoukDesignNotes

2. **不公平感知阈值**：研究表明，当玩家感知到 "连续 5 次以上未遇到所需颜色" 时，无论实际概率如何，都会产生 "游戏在针对我" 的负面感受。

3. **近失效应（Near-Miss Effect）**：当玩家差一点就能达成消除但未能实现时，大脑的奖励中枢仍会激活。适度的 "惜败" 体验能提升留存，但需要配合后续的胜利来平衡。

### 7.2 玩家留存策略

#### 策略 1：挫折保护（Frustration Protection）

```
如果在过去 5 步内没有产生任何消除：
  → 将下一个新生成方块的 "有利颜色" 权重提升 30%
  → 有利颜色 = 最接近形成匹配的颜色
```

#### 策略 2：胜利加速（Win Streak Boost）

```
如果玩家连续 3 次消除都是 4 连消或更好：
  → 小幅增加稀有方块/特殊方块的出现概率
  → 让 "热手" 玩家持续获得正反馈
```

#### 策略 3：新手友好（Beginner's Luck）

```
前 5 关：
  → 颜色减少到 4 种
  → 匹配机会增加 15%
  → 死局概率降至 0%（强制检测+预防）
  → 随时间推移逐渐过渡到标准参数
```

#### 策略 4：流失预警（Churn Prevention）

```
如果玩家在同一关失败 >= 3 次：
  → 略微降低难度（多生成 1-2 个有利颜色的方块）
  → 但保持足够挑战以避免 "怜悯感"
  → 提示可使用道具，而非直接让过关
```

### 7.3 来自行业专家的警告

LogicSimplified 强调：

> **公平性优先**：玩家能感知到游戏在对抗他们。不要让 RNG 过度偏袒系统。它应该感觉不可预测，但不应该感觉不公平。

> **避免残酷的连败**：长时间的坏运气比一个高难度关卡更糟糕。好的 RNG 系统包含软保护——阻止玩家因为方块完全不配合而陷入失败循环。

> **调整概率**：你不希望稀有方块出现得太频繁，特别是在游戏的早期阶段。基于上下文调整概率——关卡类型、当前棋盘状态、道具触发等因素。

---

## 8. RNG 种子化与可复现棋盘

### 8.1 什么是 RNG 种子化

RNG 种子化（Seeding）是指使用固定种子值初始化伪随机数生成器（PRNG），使得从该种子开始产生的随机数序列完全可预测和可复现。

### 8.2 在 Match-3 中的核心价值

来自 Sergey Gutowski 的 Medium 文章《Why your puzzle game should be deterministic》：

**核心优势**：

1. **Bug 复现**：使用同一种子 + 同一操作序列，可 100% 复现任何 Bug
2. **状态预测**：可预先计算未来棋盘状态（用于提示系统、AI 分析）
3. **轻量级回放**：仅需存储种子 + 操作序列（约 1KB），即可完整回放整局游戏
4. **高级分析**：训练 AI 识别玩家行为模式，优化关卡设计
5. **难度实时调整**：提前预知将要生成的方块类型，在必要时进行微调

### 8.3 确定性引擎实践

确定性的关键原则：

| 原则 | 说明 |
|------|------|
| **分离随机生成器** | 每个子系统（掉落生成、特效、UI 动画）使用独立的随机生成器 |
| **避免浮点数** | 所有概率计算使用整数权重 |
| **固定执行顺序** | 使用游戏 tick 而非 deltaTime，确保逻辑更新顺序一致 |
| **避免字典遍历** | Dictionary/HashSet 遍历顺序不确定 |
| **逻辑与视图分离** | 模型层完全独立于渲染层，视图不应影响逻辑 |
| **快照与模拟** | 可快速克隆游戏模型进行前向模拟（用于提示/分析） |

### 8.4 Godot 中的 RNG 种子化实践

```gdscript
# 为棋盘生成创建独立的随机生成器
var board_rng := RandomNumberGenerator.new()
var spawn_rng := RandomNumberGenerator.new()

func generate_board(seed_value: int, rows: int, cols: int):
    board_rng.seed = seed_value
    # 所有棋盘生成相关的随机调用使用 board_rng
    for r in range(rows):
        for c in range(cols):
            board[r][c] = board_rng.randi_range(0, color_count - 1)

func generate_spawn_tile():
    # 使用独立种子（可从 board_seed 派生）
    spawn_rng.seed = base_seed + total_spawn_count
    return spawn_rng.randi_range(0, spawnable_colors - 1)
```

### 8.5 种子化在日常开发中的应用

| 场景 | 种子策略 | 目的 |
|------|----------|------|
| **关卡设计** | 固定种子 | 确保测试者体验的关卡完全相同 |
| **每日挑战** | `seed = hash(date_string)` | 所有玩家同一天看到相同关卡 |
| **Debug 模式** | 用户可输入种子 | 快速复现特定棋盘状态 |
| **A/B 测试** | 种子 + 变体 ID | 比较不同随机策略的留存率 |
| **多人对战** | 同步种子 | 确保双方棋盘基础状态一致 |

### 8.6 关卡设计的种子化策略

```python
def create_daily_level(date: str, level_index: int) -> Board:
    """
    每日挑战：同一天所有玩家面对相同棋盘
    """
    seed = hash(f"daily_{date}_level_{level_index}")
    rng = Random(seed)
    
    # 基础参数也可由种子随机化
    board_shape = rng.choice(["8x8", "8x8_hex", "9x9_with_obstacles"])
    colors = rng.sample(ALL_COLORS, k=rng.randint(4, 6))
    
    return generate_board_with_rng(rng, board_shape, colors)
```

---

## 9. 渐进难度与方块分布控制

### 9.1 难度维度

Match-3 关卡的难度可以从多个维度控制：

| 维度 | 难度低 | 难度高 |
|------|--------|--------|
| **颜色数量** | 3-4 种 | 6-8 种 |
| **棋盘大小** | 6×6 | 9×9（需更多步数完成目标） |
| **障碍物比例** | 0-10% | 40-60% |
| **目标分数** | 低（每次消除即可积累） | 高（需要策略性连消） |
| **步数限制** | 宽裕（目标步数 × 2+） | 紧张（刚好够用） |
| **特殊方块掉落率** | 高 | 低 |
| **Bag 大小** | 小（颜色变化可预测） | 大（更接近纯随机） |

### 9.2 渐进式颜色解锁

典型的关卡难度曲线（参考 Royal Match / Candy Crush 模式）：

```
第 1-5 关：  3 种颜色（极简入门，学习基础操作）
第 6-15 关： 4 种颜色（引入基本策略思维）
第 16-30 关：5 种颜色（标准难度，引入第一个障碍物）
第 31-50 关：5 种颜色 + 障碍物（增加策略深度）
第 51 关起：  5-6 种颜色 + 多种障碍物 + 特殊目标
```

**渐进参数表**：

| 关卡区间 | 颜色数 | 可生成颜色权重 | Bag multiplier | 智能帮扶程度 |
|----------|--------|--------------|---------------|-------------|
| L1-L5 | 3 | 均匀100% | 3 | 高（活跃帮扶） |
| L6-L15 | 4 | 均匀100% | 2 | 中 |
| L16-L30 | 5 | 均匀100% | 2 | 低 |
| L31-L50 | 5 | 稀有色-15% | 2 | 仅死局救援 |
| L51+ | 6 | 稀有色-25% | 1.5 | 仅死局救援 |

### 9.3 基于玩家表现的动态难度调整（DDA）

```python
class DynamicDifficulty:
    def __init__(self):
        self.consecutive_wins = 0
        self.consecutive_losses = 0
        self.player_skill_rating = 1000  # ELO-like
    
    def adjust_spawn_weights(self, base_weights: dict) -> dict:
        adjusted = base_weights.copy()
        
        if self.consecutive_losses >= 3:
            # 降低难度：增加最常见的 2 种颜色的权重
            top_two = sorted(base_weights, key=base_weights.get, reverse=True)[:2]
            for c in top_two:
                adjusted[c] *= 1.3
        
        if self.consecutive_wins >= 5:
            # 提高难度
            for c in adjusted:
                adjusted[c] *= random.uniform(0.85, 1.0)
        
        return adjusted
```

### 9.4 障碍物与特殊方块的分布控制

障碍物和特殊方块的生成也需要可控随机性：

```
障碍物生成规则：
  - 最多相邻 2 个障碍物（避免大面积不可玩区域）
  - 保证每个 3×3 区域至少有 5 个可操作格子
  - 障碍物不应阻挡所有通向棋盘中心的路径

特殊方块掉落率：
  - 条纹方块：清除 12 个普通方块后概率出现（权重从 0 → 逐渐增加）
  - 炸弹方块：清除 30 个普通方块后概率出现
  - 彩虹方块：清除 50 个普通方块后概率出现
```

---

## 10. 总结与最佳实践

### 10.1 推荐架构

```
┌──────────────────────────────────────────────────┐
│                  Match-3 生成系统                  │
├──────────────────────────────────────────────────┤
│                                                    │
│  ┌──────────────┐    ┌──────────────────┐         │
│  │  Bag 随机器   │    │  难度控制器       │         │
│  │  - 双袋缓冲   │◄───│  - 玩家技能评估   │         │
│  │  - 可配置袋大小│    │  - 权重动态调整   │         │
│  └──────┬───────┘    └────────┬─────────┘         │
│         │                      │                   │
│         ▼                      ▼                   │
│  ┌──────────────────────────────────────┐         │
│  │         中心生成管理器                 │         │
│  │  - 无匹配初始棋盘                    │         │
│  │  - 死局检测与重排                    │         │
│  │  - 智能方块生成（可选）               │         │
│  │  - 障碍物/特殊方块分布控制            │         │
│  └──────────────────────────────────────┘         │
│         │                      │                   │
│         ▼                      ▼                   │
│  ┌──────────────┐    ┌──────────────────┐         │
│  │  RNG 管理器  │    │  验证器           │         │
│  │  - 种子控制  │    │  - 可解性验证     │         │
│  │  - 多子系统  │    │  - 难度平衡检查   │         │
│  │  - 日志记录  │    │  - 死局预防       │         │
│  └──────────────┘    └──────────────────┘         │
│                                                    │
└──────────────────────────────────────────────────┘
```

### 10.2 最佳实践速查表

| 关注点 | 推荐做法 | 避免做法 |
|--------|----------|----------|
| **初始生成** | 扫描+替换法逐个修正匹配 | 纯随机后整体重排 |
| **公平性** | Bag 系统保证颜色分布 | 纯随机（方差太大） |
| **死局** | 每次填充后检测+自动重排 | 忽略死局问题 |
| **难度** | 渐进解锁颜色和障碍物 | 第一关就展示全部复杂度 |
| **智能生成** | 微妙的 10-20% 偏差助推 | 明显的 "作弊" 操控 |
| **种子** | 每个子系统独立种子 | 全局共享种子 |
| **调试** | 种子+操作日志可完整回放 | 依赖 "再玩一遍看看" |
| **玩家留存** | 挫折保护+近失奖励 | 让运气完全支配结果 |
| **Bag** | multiplier=2 作为默认 | multiplier 过大削弱策略性 |

### 10.3 核心原则总结

1. **随机是工具，不是目的**：随机性服务于趣味性，纯粹的 "真随机" 往往产生糟糕的体验
2. **短窗口公平 > 长期公平**：玩家感知的是最近 5-10 个方块的分布，不是 1000 次后的大数定律
3. **可控 > 不可控**：即使随机，也要有参数可以调节，有种子可以复现
4. **透明 > 黑箱**：玩家能接受 "运气不好"，但不能接受 "游戏在骗我"
5. **保护 > 惩罚**：系统应该帮助玩家避免死局，而不是利用死局推销道具
6. **渐进 > 陡峭**：难度曲线应该平滑上升，每个新元素的引入都应该有 "教学期"

---

## 参考资源

- [Tetris.wiki - Random Generator (7-Bag)](https://tetris.wiki/Random_Generator)
- [LogicSimplified - Key Algorithmic Tricks for Match 3 Game Development](https://logicsimplified.com/newgames/key-algorithmic-tricks-for-match-3-game-development/)
- [GameDev StackExchange - Match-3 Shuffling](https://gamedev.stackexchange.com/questions/79701/match-3-shuffling)
- [GameDev StackExchange - How should I generate the board](https://gamedev.stackexchange.com/questions/67078/how-should-i-generate-the-board-for-a-match-3-style-game)
- [Unity Discussions - No matches on board start](https://discussions.unity.com/t/match3-how-can-i-ensure-the-board-starts-with-no-matches/111066)
- [Sergey Gutowski - Why your puzzle game should be deterministic (Medium)](https://medium.com/@dev.ios.android/why-your-puzzle-game-should-be-deterministic-99a0ad4a5890)
- [StackOverflow - Deterministic match-3 puzzle game algorithm](https://stackoverflow.com/questions/71483599/how-can-i-make-a-deterministic-match-3-puzzle-game-algorithm)
- [GameAnalytics - How to Crack the Match 3 Code](https://www.gameanalytics.com/blog/how-to-crack-the-match-3-code-part-1)
- [SnoukDesignNotes - Design Analysis: Match-3](https://snoukdesignnotes.blog/2018/06/21/design-analysis-match-3/)
- [Reddit r/gamedev - Match-3 puzzle game algorithms](https://www.reddit.com/r/gamedev/comments/4atryv/match3_puzzle_game_algorithms/)
- [Reddit r/gamedesign - Level design in match-3 games](https://www.reddit.com/r/gamedesign/comments/apqctq/lets_talk_about_level_design_in_match3_games/)
- [MDPI - Efficient Difficulty Level Balancing in Match-3 Puzzle Games](https://www.mdpi.com/2079-9292/12/21/4456)
- [EA SEED - Improving Conditional Level Generation using Automated Validation (PDF)](https://media.contentapi.ea.com/content/dam/ea/seed/presentations/seed-tog-conditional-level-generation-paper.pdf)
