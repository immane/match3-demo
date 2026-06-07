# 三消游戏级联（Cascade/Chain Reaction）系统研究

> 级联（Cascade）又称连锁反应（Chain Reaction），是三消游戏中最重要的核心机制之一。本文档全面研究级联系统的设计原理、实现方式、各主流游戏的处理方案以及性能优化策略。

---

## 目录

1. [什么是级联](#1-什么是级联)
2. [级联检测流程](#2-级联检测流程)
3. [无限级联防护](#3-无限级联防护)
4. [连击/连锁倍率系统](#4-连击连锁倍率系统)
5. [级联的视觉反馈](#5-级联的视觉反馈)
6. [特殊方块的连锁反应](#6-特殊方块的连锁反应)
7. [级联期间的性能考量](#7-级联期间的性能考量)
8. [状态机方案](#8-状态机方案)
9. [主流游戏如何处���级联](#9-主流游戏如何处理级联)
10. [代码流程：循环结构、递归 vs 迭代、异步/动画同步级联](#10-代码流程循环结构递归-vs-迭代异步动画同步级联)

---

## 1. 什么是级联

### 1.1 定义

**级联（Cascade）** 是指玩家进行一次有效交换后，匹配消除 → 方块下落填空 → 新方块从顶部出现 → 再次形成匹配 → 再次消除……这样的连锁反应过程。一次动作触发一连串自动匹配，形成一个"链"。

### 1.2 核心概念

```
玩家交换(move)
  └─ 第一次匹配消除 (cascade level 1)
      └─ 方块下落 + 新方块填入
          └─ 第二次匹配消除 (cascade level 2)  ← 自动触发
              └─ 方块下落 + 新方块填入
                  └─ 第三次匹配消除 (cascade level 3)  ← 自动触发
                      └─ ... 反复直到不再产生新匹配
```

### 1.3 级联的重要性

- **分数放大器**：级联越深，分数越高。是获取高分的核心途径
- **策略深度**：有经验的玩家会刻意在棋盘下方消���，利用下落过程中的乱流创造额外匹配
- **满足感**：级联时一连串自动爆炸是游戏最"爽"的时刻
- **随机性与技巧的平衡**：部分级联是偶然的，但高手可以有意识地在潜在重组区域制造初始匹配

---

## 2. 级联检测流程

### 2.1 标准流程

```
┌──────────────────────────────────────────┐
│              玩家输入 (Swap)              │
│   交换两个相邻方块，检查是否构成合法交换    │
│   若不合法 → 交换回原位，播放抖动动画      │
│   若合法   → 继续                          │
└───────────────────┬──────────────────────┘
                    ▼
┌──────────────────────────────────────────┐
│           MARK（标记匹配）                 │
│   扫描整个棋盘，找出所有 ≥3 的连线匹配     │
│   将匹配到的方块标记为「待消除」状态        │
│   同时识别特殊匹配形状（T型、L型、4连、5连）│
└───────────────────┬──────────────────────┘
                    ▼
┌──────────────────────────────────────────┐
│           CLEAR（消除方块）                │
│   移除所有被标记的方块                     │
│   播放消除动画（缩放+淡出+粒子效果）       │
│   生成特殊方块（如4连→火箭，5连→彩虹炸弹） │
│   计算并累加分数（含当前级联倍率）          │
└───────────────────┬──────────────────────┘
                    ▼
┌──────────────────────────────────────────┐
│           FALL（下落填充）                 │
│   逐列扫描，从底部向上找空洞               │
│   将空洞上方的方块向下移动填满空格          │
│   掉落动画（重力模拟，约0.2秒）             │
│   在顶部空位生成新的随机方块                │
│   新方块从顶部"落入"棋盘，播放跌落动画      │
└───────────────────┬──────────────────────┘
                    ▼
┌──────────────────────────────────────────┐
│           CHECK AGAIN（重新检测）          │
│   再次扫描整个棋盘，检查是否存在新的匹配    │
│   若找到新匹配 → 回到 MARK 阶段             │
│                级联计数 +1                  │
│   若没有匹配   → 流程结束，返回玩家控制     │
└──────────────────────────────────────────┘
```

### 2.2 关键细节

#### 扫描方向
通常先水平扫描每一行，再垂直扫描每一列。对于 8×8 的棋盘，只需要检查 96 个可能位置（每行 6 个 × 8 行 + 每列 6 个 × 8 列），扫描速度极快（毫秒级）。

#### 批量处理 vs 逐个处理
- **批量处理**：一次性找出所有匹配，一起消除 → 高效、视觉统一
- **逐个处理**：找到一个匹配立即消除 → 容易产生索引错乱，不推荐

#### 分步动画时序
每步之间需要等待动画完成：
- 匹配粒子效果：0.0s
- 方块缩小：0.0-0.1s（缓入）
- 方块淡出：0.05-0.15s
- 分数弹出：0.1s
- 上方方块下落：0.2s
- 新方块生成并下落：0.3s
- 下一次匹配检测：0.5s

---

## 3. 无限级联防护

### 3.1 问题

如果新生成的方块恰好总是形成新的匹配，理论上级联可以无限进行下去。在极端情况下可能导致：
- 游戏陷入死循环
- 玩家等待时间过长
- 帧率下降
- 用户体验灾难

### 3.2 防护策略

#### 策略一：最大级联次数硬限制
```
CASCADE_MAX_DEPTH = 20  // 硬限制：超过此深度强制停止
```
- 最简单粗暴的方案
- 缺点：可能截断正常的精彩连锁
- 建议值：15-30 次（正常玩法几乎不可能达到）

#### 策略二：超时保护
```
cascade_start_time = now()
每轮循环检查：
    if now() - cascade_start_time > MAX_CASCADE_DURATION (如 5秒):
        force_stop_cascade()
```
- 防止级联因等待动画而无限延长
- 更符合用户体验（玩家不会等太久）

#### 策略三：智能生成算法
在生成新方块时，检查是否会立即造成匹配：
```
func spawn_tile(column):
    var banned_types = []
    // 检查下方两个方块是否同色 → 禁止该色
    if grid[col][row+1].type == grid[col][row+2].type:
        banned_types.append(grid[col][row+1].type)
    // 检查左右邻接是否匹配
    if grid[col-1][row].type == grid[col-2][row].type:
        banned_types.append(grid[col-1][row].type)
    // 从非禁止类型中选择
    return random_type_excluding(banned_types)
```
- 业内最佳实践
- 避免"无限级联"从根本上发生
- 但仍保留合理的级联机会

#### 策略四：伪随机数种子控制
使用可预测的伪随机序列，在极端情况下（如果某局游戏级联过深），调整随机种子使其更倾向于生成"安全"方块。

#### 策略五：全局状态跟踪
```
var cascade_protection = {
    depth: 0,
    total_matches_this_turn: 0,
    max_allowed_depth: 15
}
每一步级联都递增 depth，超过限制后切换为"安全生成模式"。
```

### 3.3 推荐组合方案

**智能生成（策略三） + 最大深度硬限制（策略一）** 是最稳健的方案：
- 日常游玩：智能生成使自然级联停留在 1-5 层
- 极端情况：硬限制作为安全网
- 给玩家带来的感觉：级联"刚刚好"，既爽快又不失控

---

## 4. 连击/连锁倍率系统

### 4.1 基本公式

```
总分 = Σ (基础分 × 级联倍率)  对每次匹配
```

其中：
- **基础分** = 匹配的方块数量 × 单方块分值
- **级联倍率** = 1 + (级联层级 - 1) × 倍率增幅系数

### 4.2 Bejeweled 风格倍率

Bejeweled 系列使用简单的阶梯倍率：

| 级联层级 | 倍率 | 名称 |
|---------|------|------|
| 第1次匹配 | ×1 | 普通匹配 |
| 第2次匹配 | ×1.5 | Cascade |
| 第3次匹配 | ×2 | 2x Cascade |
| 第4次匹配 | ×3 | 3x Cascade |
| 第n次匹配(n≥4) | ×(n-1) | ... |

公式近似：`multiplier = max(1, cascade_level - 1) + 1`（起始 ×1.5）

### 4.3 Candy Crush 风格倍率

Candy Crush 有独特的分级命名系统，额外奖励：

| 消除糖果数 | 级联层级 | 命名 | 额外加分 |
|-----------|---------|------|---------|
| < 12 | 1-3 | 普通/无特殊 | +0 |
| 12-17 | ~4 | **Sweet**（甜蜜） | +60/每个糖果 |
| 18-23 | ~6 | **Tasty**（美味） | +60/每个糖果 |
| 24-29 | ~8 | **Divine**（绝妙） | +60/每个糖果 |
| 30+ | ~10+ | **Delicious**（美味绝伦） | +60/每个糖果 |

关键机制：
- 每次级联额外为每个消除糖果加 60 分
- Candy Crush Soda 中变为 20 × 级联层级
- 命名系统本身就是强大的正反馈——玩家会因为看到 "Divine!" 弹出而感到满足

### 4.4 通用连击倍率公式

以下是一个可自定义的通用倍率公式：

```
// 指数增长型（前期温和，后期爆炸）
multiplier = 1.0 + (cascade_level * 0.5)
// cascade_level=1: 1.5x, =2: 2.0x, =3: 2.5x, =4: 3.0x

// 加速增长型（奖励深级联）
multiplier = 1.0 + (cascade_level ^ 2 * 0.25)
// cascade_level=1: 1.25x, =2: 2.0x, =3: 3.25x, =4: 5.0x

// 阶梯跳跃型
multiplier = cascade_level <= 2 ? 1.0 
           : cascade_level <= 4 ? 2.0 
           : cascade_level <= 7 ? 3.0 
           : 5.0
```

### 4.5 额外奖励因子

```
最终得分 = 基础分 × 级联倍率 × 特殊方块倍率 × 难度系数
```

- **特殊方块倍率**：彩虹炸弹 ×2、火箭 ×1.5 等
- **难度系数**：关卡越难，分数倍率越高
- **连续消除奖励**：连续 N 次级联额外 ×1.1^N

### 4.6 Godot 实现示例

```gdscript
# combo_tracker.gd
class_name ComboTracker
extends Node

var cascade_level: int = 0
var total_score_gained: int = 0
var cascade_names: Array[String] = [
    "", "Nice!", "Great!", "Excellent!", "Amazing!",
    "Incredible!", "Fantastic!", "Divine!", "Delicious!"
]

func start_new_chain():
    cascade_level = 0
    total_score_gained = 0

func increment_cascade():
    cascade_level += 1

func calculate_score(base_score: int) -> int:
    var multiplier = 1.0 + (cascade_level - 1) * 0.5
    var final_score = int(base_score * multiplier)
    total_score_gained += final_score
    return final_score

func get_cascade_name() -> String:
    if cascade_level < cascade_names.size():
        return cascade_names[cascade_level]
    return "LEGENDARY!"

func get_multiplier() -> float:
    return 1.0 + (cascade_level - 1) * 0.5
```

---

## 5. 级联的视觉反馈

### 5.1 多层级视觉反馈体系

级联越深，视觉效果越强。这是一个分层次的设计：

```
┌──────────────────────────────────────────────┐
│ Level 1-2: 温和反馈                          │
│  · 简单的"pop"粒子效果                       │
│  · "Nice!" / "Good!" 文字弹出                │
│  · 轻微的音效（短促的"叮"）                  │
├──────────────────────────────────────────────┤
│ Level 3-4: 明显反馈                          │
│  · 更大更亮的粒子爆炸                         │
│  · "Great!" / "Excellent!" 文字，有缩放动画  │
│  · 屏幕轻微震动（0.5-1px 偏移）               │
│  · 音效变得更加丰富（和弦/琶音）               │
│  · 分数数字放大弹出                           │
├──────────────────────────────────────────────┤
│ Level 5-7: 强烈反馈                          │
│  · 全屏粒子/闪光效果                          │
│  · "Amazing!" / "Incredible!" 大字体，缓动    │
│  · 屏幕中等震动（2-3px 偏移）                 │
│  · 背景色调短暂变化（如偏暖色）              │
│  · 背景音乐短暂加速/变调                      │
│  · 多音效叠加                                 │
├──────────────────────────────────────────────┤
│ Level 8+: 极致反馈                           │
│  · 全屏特效（边框发光、暗角、屏幕闪光）       │
│  · "DIVINE!" / "DELICIOUS!" / "LEGENDARY!"   │
│  · 大字体带描边+投影+弹性缩放                 │
│  · 屏幕强震动（4-6px 偏移）                   │
│  · 时间短暂减速（Time Scale 0.5-0.8）         │
│  · 背景音乐暂停 + 专属音效高潮                │
│  · 角落出现特殊花纹/饰框                      │
└──────────────────────────────────────────────┘
```

### 5.2 Godot 视觉实现示例

```gdscript
# cascade_fx.gd
extends Node

@export var combo_label_template: PackedScene
@export var camera: Camera2D
@export var screen_flash: ColorRect

const SHAKE_INTENSITIES = [0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0, 6.0]
const COMBO_COLORS = [
    Color.WHITE,       # Level 1
    Color.YELLOW,      # Level 2
    Color.ORANGE,      # Level 3
    Color.RED,         # Level 4
    Color.MAGENTA,     # Level 5
    Color.PURPLE,      # Level 6
    Color.GOLD,        # Level 7
    Color.CYAN,        # Level 8+
]

func play_cascade_fx(cascade_level: int, position: Vector2):
    var idx = clamp(cascade_level - 1, 0, SHAKE_INTENSITIES.size() - 1)
    
    # 屏幕震动
    shake_camera(SHAKE_INTENSITIES[idx])
    
    # 弹出连击文字
    var label = combo_label_template.instantiate()
    label.text = get_combo_name(cascade_level)
    label.modulate = COMBO_COLORS[idx]
    label.position = position
    add_child(label)
    
    # 深度级联时闪光
    if cascade_level >= 5:
        flash_screen(cascade_level)
    
    # 深度级联时时间减速
    if cascade_level >= 8:
        slow_motion(0.6, 0.5)

func shake_camera(intensity: float):
    if camera and intensity > 0:
        var tween = create_tween()
        tween.tween_property(camera, "offset", 
            Vector2(randf_range(-intensity, intensity), 
                    randf_range(-intensity, intensity)), 0.1)
        tween.tween_property(camera, "offset", Vector2.ZERO, 0.1)

func flash_screen(level: int):
    var alpha = clamp(level * 0.05, 0.0, 0.3)
    screen_flash.modulate = Color(1, 1, 1, alpha)
    var tween = create_tween()
    tween.tween_property(screen_flash, "modulate:a", 0.0, 0.3)

func slow_motion(time_scale: float, duration: float):
    Engine.time_scale = time_scale
    await get_tree().create_timer(duration * time_scale).timeout
    Engine.time_scale = 1.0
```

### 5.3 连击文字动画曲线

```
文字弹出动画时间线 (0.0s - 1.2s):
┌────────────────────────────────────────────┐
│ 0.00s: 生成 (scale=0.5, alpha=0)          │
│ 0.05s: 弹性放大 (scale 0.5→1.3, 缓出弹性) │
│ 0.15s: 回弹稳定 (scale 1.3→1.0, 缓出)     │
│ 0.20s: 保持显示                            │
│ 0.80s: 上浮 + 淡出 (offset_y -=30, alpha→0)│
│ 1.20s: 销毁                                │
└────────────────────────────────────────────┘
```

---

## 6. 特殊方块的连锁反应

### 6.1 特殊方块生成规则

| 匹配形状 | 方块数 | 生成物 | 效果 |
|---------|-------|--------|------|
| 直线 | 4 | 条纹/火箭 | 清除整行或整列 |
| L/T 型 | 5 | 炸弹/包装糖果 | 清除 3×3 区域 |
| 直线 | 5 | 彩虹/彩色炸弹 | 清除棋盘上所有某色方块 |

### 6.2 特殊方块的连锁触发

这是级联系统中最复杂的部分。特殊方块被清除时会触发范围效果，范围效果可能激活其他特殊方块，形成"炸弹链"。

```
激活特殊方块
  └─ 执行范围效果（如消除 3×3 区域）
      └─ 该区域内包含另一个特殊方块
          └─ 该特殊方块也被激活
              └─ 再次执行范围效果
                  └─ 可能继续触发更多...
```

#### 处理策略

**方案一：顺���执行队列**
```gdscript
var activation_queue: Array[Tile] = []

func activate_special_tile(tile: Tile):
    var affected = tile.get_affected_tiles()
    for t in affected:
        t.mark_for_removal()
        if t.is_special():
            activation_queue.append(t)  # 加入队列尾部

# 执行
while activation_queue:
    var next_tile = activation_queue.pop_front()
    activate_special_tile(next_tile)
```
- 优点：简单，可预测
- 缺点：长链可能造成视觉卡顿

**方案二：层级解析**
```gdscript
var current_level_queue: Array[Tile] = []
var next_level_queue: Array[Tile] = []

func resolve_special_chain():
    while true:
        for tile in current_level_queue:
            var affected = tile.activate()
            for t in affected.get_special_tiles():
                next_level_queue.append(t)
        
        if next_level_queue.is_empty():
            break
        
        await play_activation_animation(current_level_queue)
        current_level_queue = next_level_queue
        next_level_queue = []
```
- 优点：动画可分层，视觉效果好
- 缺点：需要 await/协程管理

### 6.3 彩虹炸弹 × 彩虹炸弹（双重彩虹）

两个彩虹炸弹交换是匹配游戏中最强力的效果——清除整个棋盘：
```gdscript
func activate_double_rainbow():
    var all_tiles = get_all_tiles()
    for tile in all_tiles:
        tile.queue_destruction()  # 全部标记清除
    # 播放全屏清版特效
    play_board_wipe_effect()
    # 生成大量新方块（必然触发大量级联）
    refill_entire_board()
```

### 6.4 特殊组合处理

常用特殊方块组合效果表：

| 组合 | 效果 |
|-----|------|
| 条纹 + 条纹 | 十字消除（行列交叉） |
| 条纹 + 炸弹 | 三行三列大范围消除 |
| 炸弹 + 炸弹 | 5×5 或更大范围消除 |
| 彩虹 + 条纹 | 该色所有方块变为条纹并依次激活 |
| 彩虹 + 炸弹 | 该色所有方块变为炸弹并依次激活 |
| 彩虹 + 彩虹 | 全屏清除 |

---

## 7. 级联期间的性能考量

### 7.1 计算性能

| 操作 | 复杂度 | 说明 |
|-----|--------|------|
| 全盘匹配扫描 | O(W×H) | 每行每列扫描一次，8×8棋盘仅96个检查点 |
| 下落计算 | O(W×H) | 逐列扫描空洞，移动方块 |
| 新方块生成 | O(W) | 仅顶部一行 |

对于标准 8×8 棋盘，全套计算 < 1ms，性能瓶颈不在计算而在动画。

### 7.2 优化要点

#### 增量扫描
只扫描受影响的区域而非全盘：
```gdscript
func find_matches_incremental(affected_columns: Array[int]):
    for col in affected_columns:
        check_column(col)
        for row in range(grid_height):
            check_horizontal_from(col, row)
    # 同样只检查受影响的行
```

#### 对象池
消除和生成方块的视觉节点应使用对象池，避免每帧分配/释放：
```gdscript
var tile_pool: ObjectPool[TileRenderer]
var particle_pool: ObjectPool[GPUParticles2D]
```

#### 动画批处理
相同层级的匹配先全部识别完，再批量播放动画：
```gdscript
func resolve_cascade():
    var all_matches = find_all_matches()
    if all_matches.is_empty():
        return false
    
    # 批量执行消除动画（所有匹配同时播放）
    var tweens: Array[Tween] = []
    for match in all_matches:
        tweens.append(animate_match_removal(match))
    await all_tweens_finished(tweens)
    
    # 批量下落
    await animate_falling()
    
    return true
```

#### 避免不必要的全盘扫描
```gdscript
# 记录哪些列发生了变化
var dirty_columns: Array[int] = []

func mark_column_dirty(col: int):
    if col not in dirty_columns:
        dirty_columns.append(col)

func find_matches_optimized():
    if dirty_columns.is_empty():
        return find_all_matches()  # 首次：全盘扫描
    else:
        return find_matches_in_columns(dirty_columns)
```

### 7.3 移动端特别注意事项

- **粒子数量限制**：移动端 GPU 较弱，粒子系统单次发射不宜超过 50 个
- **Draw Call 合并**：相同材质的方块应使用 TextureAtlas，减少 Draw Call
- **避免透明混合层叠**：过多的半透明粒子叠加会严重拖慢移动端帧率
- **使用 MultiMeshInstance2D**：对于大量相似方块，MultiMesh 比单独 Sprite2D 高效一个数量级

---

## 8. 状态机方案

### 8.1 为什么需要状态机

级联流程涉及多个互斥阶段（等待输入、动画播放、匹配检测等），用状态机管理可以：
- 清晰地分离各阶段逻辑
- 防止玩家在动画播放时输入
- 便于调试和扩展
- 支持暂停/恢复

### 8.2 三消游戏状态机架构

```
                        ┌─────────┐
                        │  IDLE   │ ◄─ 等待玩家输入
                        │ (空闲)  │
                        └────┬────┘
                             │ 玩家执行交换
                             ▼
                        ┌─────────┐
                        │ SWAPPING│ 交换动画
                        │ (交换中) │
                        └────┬────┘
                             │ 动画结束
                             ▼
                 ┌─────►┌─────────┐
                 │      │ CHECKING│ 扫描匹配
                 │      │ (检测中) │
                 │      └────┬────┘
                 │           │
                 │      ┌────┴────┐
                 │      ▼         ▼
                 │  ┌─────────┐  ┌──────────┐
                 │  │MATCHING │  │ NO_MATCH │ 没有匹配
                 │  │(有匹配) │  │ (无匹配)  │──► 交换回原位
                 │  └────┬───┘  └──────────┘       │
                 │       │                          ▼
                 │       ▼                     ┌─────────┐
                 │  ┌─────────┐                │ SHUFFLE │ 无可走步时
                 │  │CLEARING │ 消除动画        │(洗牌中)│──► IDLE
                 │  │(消除中) │                └─────────┘
                 │  └────┬───┘
                 │       │
                 │       ▼
                 │  ┌─────────┐
                 │  │ FALLING │ 下落动画
                 │  │(下落中) │
                 │  └────┬───┘
                 │       │
                 │       ▼
                 │  ┌─────────┐
                 │  │SPAWNING │ 生成新方块
                 │  │(生成中) │
                 │  └────┬───┘
                 │       │
                 │       ▼
                 │  ┌─────────┐
                 └──┤CHECKING │ 级联：回到检测态
                    │(检测中) │
                    └─────────┘
```

### 8.3 Godot 状态机伪代码实现

```gdscript
# board_state_machine.gd
class_name BoardStateMachine
extends Node

enum State {
    IDLE,       # 等待玩家输入
    SWAPPING,   # 播放交换动画
    CHECKING,   # 扫描匹配
    CLEARING,   # 消除方块
    FALLING,    # 方块下落
    SPAWNING,   # 生成新方块
    NO_MATCH,   # 交换无匹配，回退
    SHUFFLING,  # 棋盘洗牌
}

var current_state: State = State.IDLE
var cascade_level: int = 0

func _ready():
    set_state(State.IDLE)

func set_state(new_state: State):
    exit_state(current_state)
    current_state = new_state
    enter_state(current_state)

func exit_state(state: State):
    match state:
        State.IDLE:
            pass
        State.CLEARING:
            pass

func enter_state(state: State):
    match state:
        State.IDLE:
            cascade_level = 0
            enable_player_input(true)
            
        State.SWAPPING:
            enable_player_input(false)
            # 播放交换动画，完成后自动进入 CHECKING
            
        State.CHECKING:
            var matches = find_all_matches()
            if matches.is_empty():
                if cascade_level == 0:
                    set_state(State.NO_MATCH)
                else:
                    # 级联结束
                    set_state(State.IDLE)
            else:
                cascade_level += 1
                emit_cascade_event(cascade_level)
                set_state(State.CLEARING)
                
        State.CLEARING:
            clear_matches()
            # 动画完成后自动进入 FALLING
            
        State.FALLING:
            drop_tiles()
            # 动画完成后自动进入 SPAWNING
            
        State.SPAWNING:
            spawn_new_tiles()
            # 动画完成后 → CHECKING (级联循环)
            set_state(State.CHECKING)
            
        State.NO_MATCH:
            swap_back()
            # 动画完成后 → IDLE
            set_state(State.IDLE)
            
        State.SHUFFLING:
            shuffle_board()
            # 完成后 → IDLE
            set_state(State.IDLE)

func handle_player_swap(tile1: Tile, tile2: Tile):
    if current_state != State.IDLE:
        return  # 忽略输入
    swap_tiles(tile1, tile2)
    set_state(State.SWAPPING)

func enable_player_input(enabled: bool):
    # 切换输入处理
    _input_enabled = enabled
```

### 8.4 状态机优势总结

| 问题 | 无状态机 | 有状态机 |
|------|---------|---------|
| 动画期间玩家输入 | 可能被处理，导致BUG | 自动忽略 |
| 调试 | 难以定位当前处于哪个阶段 | 一目了然 |
| 添加新状态 | 改动范围大 | 独立添加 |
| 级联循环 | 容易死循环或遗漏 | CHECKING→CLEARING→FALLING→SPAWNING→CHECKING 环清晰可控 |
| 暂停/恢复 | 需要手动管理所有状态 | 状态机统一处理 |

---

## 9. 主流游戏如何处理级联

### 9.1 Bejeweled（宝石迷阵）

| 方面 | 实现 |
|------|------|
| **级联倍率** | 使用 ×1 → ×1.5 → ×2 → ×3 → ... 阶梯倍率 |
| **特殊方块** | 火焰宝石（4连）、星形宝石（L/T型）、超新星宝石（5连） |
| **时间模式** | Blitz 模式下60秒倒计时，级联可增加时间 |
| **视觉** | 文字弹出 "Cascade!" "×2" 等，粒子爆炸随级联增大 |
| **级联防护** | 使用智能生成+最大深度限制 |
| **特点** | 强调速度，鼓励盲目点击底部而非仔细规划（被批评为不够策略性） |

### 9.2 Candy Crush Saga（糖果粉碎传奇）

| 方面 | 实现 |
|------|------|
| **级联倍率** | "Sweet/Tasty/Divine/Delicious" 四级命名系统，每级加60分/糖果 |
| **特殊方块** | 条纹糖果（4连）、包装糖果（L/T型）、彩色炸弹（5连） |
| **特殊组合** | 极其丰富的特殊组合效果（条纹+条纹、彩虹+条纹等） |
| **视觉** | 极其华丽：全屏效果、多个动画层叠、文字缓动、屏幕震动 |
| **级联反馈** | 弹出 "Sweet!" → "Tasty!" → "Divine!" → "Delicious!" |
| **特点** | 级联是获取高分的主要途径；棋盘故意设置为容易触发级联 |

### 9.3 Puzzle Quest（益智探险）

| 方面 | 实现 |
|------|------|
| **级联** | 级联会导致对手获得额外法力（mana），可能是负面的 |
| **策略** | 需要避免不必要的级联，防止给对手资源 |
| **独特机制** | 匹配4个以上获得额外回合；法术可操控棋盘 |
| **特点** | 将RPG战斗层叠在消除上，级联成为"双刃剑" |

### 9.4 HuniePop

| 方面 | 实现 |
|------|------|
| **操作方式** | 不交换相邻方块，而是将一行/列任意滑动 |
| **级联** | 级联概率经过精心调节，比Bejeweled更可控 |
| **策略** | 有限步数而非时间限制，鼓励规划而非手速 |
| **特点** | 解决了Bejeweled"随机性占据主导"的问题，被称为"修好了三消游戏" |

### 9.5 对比总结

| 特性 | Bejeweled | Candy Crush | Puzzle Quest | HuniePop |
|------|-----------|-------------|--------------|----------|
| 级联频率 | 高 | 很高 | 中 | 中低 |
| 级联奖励 | 倍率 | 命名 + 额外分 | 资源(gem/mana) | 倍率 |
| 时间压力 | 有 | 无（步数限制） | 无 | 无（步数限制） |
| 策略深度 | 低 | 中 | 高 | 高 |
| 级联可预见性 | 低（随机主导） | 低（随机主导） | 中 | 中高 |

---

## 10. 代码流程：循环结构、递归 vs 迭代、异步/动画同步级联

### 10.1 递归 vs 迭代

#### 递归方式（不推荐）
```gdscript
# 递归级联 —— 可能在极深层级栈溢出
func resolve_cascade_recursive(depth: int = 0):
    if depth > MAX_DEPTH:
        return
    
    var matches = find_all_matches()
    if matches.is_empty():
        return  # 基准条件
    
    process_matches(matches)
    # 如果动画是同步的，递归调用
    resolve_cascade_recursive(depth + 1)  # 栈可能溢出！
```

**问题**：
- 栈深度有限（可能达到数百层）
- 不便于动画等待
- 调试困难
- Godot GDScript 中默认递归深度有限

#### 迭代方式（推荐）
```gdscript
# 迭代级联 —— 安全且灵活
func resolve_cascade_iterative():
    var depth = 0
    
    while depth < MAX_DEPTH:
        var matches = find_all_matches()
        if matches.is_empty():
            break
        
        process_matches(matches, depth)
        depth += 1
    
    # 可选：级联结束回调
    on_cascade_finished(depth)
```

**优势**：
- 无栈溢出风险
- 易于插入 `await` 等待动画
- 循环计数天然追踪级联深度
- 易于添加超时保护

### 10.2 同步 vs 异步/动画同步

#### 同步级联（简单但无动画）
```
所有计算在单帧完成 → 直接显示最终结果
```
问题：
- 没有消除动画、下落动画
- 玩家看到的只是最终棋盘
- 失去所有"爽感"

#### 半同步（分步播放动画）
```gdscript
func resolve_cascade_with_animation():
    var depth = 0
    
    while depth < MAX_DEPTH:
        var matches = find_all_matches()
        if matches.is_empty():
            break
        
        # 1. 播放消除动画
        await animate_clearing(matches)  # 等待约0.2秒
        
        # 2. 播放下落动画
        await animate_falling()  # 等待约0.3秒
        
        # 3. 生成新方块并播放入场动画
        await animate_spawning()  # 等待约0.3秒
        
        depth += 1
    
    # 回到 IDLE 状态
    emit_signal("cascade_finished")
```

**核心**：每一步动画之间都有 `await`，让玩家看到完整的级联过程。

#### Godot 完整级联循环示例

```gdscript
# board.gd —— 级联核心循环
extends Node2D

const MAX_CASCADE_DEPTH = 20
const MAX_CASCADE_DURATION = 5.0  # 秒

var cascade_start_time: float

signal cascade_finished(total_cascades: int, total_score: int)

func start_cascade():
    cascade_start_time = Time.get_ticks_msec() / 1000.0
    var depth = 0
    
    while depth < MAX_CASCADE_DEPTH:
        # 超时防护
        var elapsed = Time.get_ticks_msec() / 1000.0 - cascade_start_time
        if elapsed > MAX_CASCADE_DURATION:
            push_warning("级联超时，强制终止于深度 %d" % depth)
            break
        
        # 扫描匹配
        var matches = match_detector.find_all_matches(grid)
        if matches.is_empty():
            break  # 级联结束
        
        depth += 1
        
        # 更新级联追踪器
        combo_tracker.increment_cascade()
        var combo_name = combo_tracker.get_cascade_name()
        
        # 播放连击视觉
        cascade_fx.play_cascade_fx(depth, Vector2(GRID_CENTER_X, GRID_CENTER_Y))
        
        # 计算分数
        var base_score = calculate_match_score(matches)
        var final_score = combo_tracker.calculate_score(base_score)
        
        # 显示分数弹出
        await show_score_popup(final_score, combo_name)
        
        # 生成特殊方块（如果需要）
        generate_special_tiles(matches)
        
        # 消除动画
        await animate_clearing(matches)
        
        # 下落
        var affected_columns = perform_gravity()
        await animate_falling(affected_columns)
        
        # 填充新方块
        var spawned = spawn_new_tiles(affected_columns)
        await animate_spawning(spawned)
        
        # 继续循环检测下一轮匹配
    
    # 级联结束
    if not has_available_moves():
        await shuffle_board()
    
    cascade_finished.emit(depth, combo_tracker.total_score_gained)

# 消除动画
func animate_clearing(matches: Array) -> void:
    var tweens: Array[Tween] = []
    for match in matches:
        for tile_pos in match.positions:
            var tile = get_tile_at(tile_pos)
            if tile:
                var tween = create_tween()
                tween.tween_property(tile, "scale", Vector2.ZERO, 0.15)
                tween.parallel().tween_property(tile, "modulate:a", 0.0, 0.1)
                tweens.append(tween)
                # 粒子效果
                spawn_particles(tile.global_position)
                tile.queue_free()
    
    # 等待所有消除动画完成
    for tween in tweens:
        await tween.finished

# 下落动画
func animate_falling(affected_columns: Array[int]) -> void:
    var tweens: Array[Tween] = []
    for col in affected_columns:
        for row in range(grid_height - 1, -1, -1):
            var tile = get_tile_at(col, row)
            if tile and tile.fall_distance > 0:
                var target_y = row * TILE_SIZE
                var tween = create_tween()
                tween.tween_property(tile, "position:y", target_y, 0.2) \
                        .set_ease(Tween.EASE_IN) \
                        .set_trans(Tween.TRANS_BOUNCE)  # 落地轻微弹跳
                tweens.append(tween)
                tile.fall_distance = 0
    
    for tween in tweens:
        await tween.finished
```

### 10.3 关键设计决策

| 决策 | 推荐方案 | 原因 |
|------|---------|------|
| 递归 vs 迭代 | **迭代** | 安全、灵活、支持await |
| 全盘扫描 vs 增量扫描 | **首次全盘，后续增量** | 性能最优 |
| 逐个动画 vs 批量动画 | **批量**（同层级并行） | 视觉效果和性能的平衡 |
| 同步 vs 异步 | **异步（await）** | 玩家需要看到动画过程 |
| 级联深度限制 | **20层硬限制 + 超时5秒** | 防死循环 |
| 方块生成时机 | **下落完成后统一生成** | 避免生成过程中被下落动画干扰 |

### 10.4 流程简化图

```
        玩家交换
           │
           ▼
    ┌──────────────┐
    │ 合法交换?     │── 否 ──► 回退动画 ──► 返回玩家
    └──────┬───────┘
           │ 是
           ▼
    ┌──────────────────────────────────────┐
    │           级联循环开始                │
    │                                      │
    │   while (depth < 20 && time < 5s):   │
    │     matches = find_matches()         │
    │     if 无匹配: break                 │
    │     depth++                          │
    │     calculate_score(depth)           │
    │     play_combo_fx(depth)             │
    │     await clear(matches)             │
    │     await fall()                     │
    │     await spawn()                    │
    │   end                                │
    └──────────────────┬───────────────────┘
                       ▼
    ┌───────────────────────────┐
    │ has_available_moves()?    │
    │   否 → shuffle_board()    │
    │   是 → 返回玩家控制        │
    └───────────────────────────┘
```

---

## 附录 A：级联设计检查清单

- [ ] 状态机实现，阻止动画期间的玩家输入
- [ ] 迭代循环（while）+ 深度限制（MAX_DEPTH ≤ 20）
- [ ] 超时保护（≥ 5秒强制中断）
- [ ] 智能方块生成（避免生成后立即形成新匹配）
- [ ] 级联倍率系统（线性、加速或阶梯型）
- [ ] 分层视觉反馈（按级联深度递增效果强度）
- [ ] 连击文字/命名系统
- [ ] 特殊方块连锁处理队列
- [ ] 无可走步检测 + 自动洗牌
- [ ] 对象池（方块、粒子、文字）
- [ ] 增量扫描优化（仅检查受影响列）
- [ ] 动画时序协调（消除0.15s → 下落0.2s → 生成0.3s → 检测）

## 附录 B：参考资料

1. Azumo - "The Logic Behind Match-3 Games: Building with Unity & C#" (2025)
2. Logic Simplified - "Key Algorithmic Tricks for Match 3 Game Development" (2025)
3. CS50's Introduction to Game Development - Match-3 Lecture (Harvard, 2018)
4. Shamus Young - "This Dumb Industry: Fixing Match 3" (2017)
5. Candy Crush Sage Wiki - Cascades
6. Candy Crush Support - "What are Sweet, Tasty, Divine and Delicious cascades?"
7. Reddit r/gamedesign - Match-3 game design discussion (2023)
8. GitHub dhairyagothi - Candy Crush Cascade Chain Reaction Issue #3228
