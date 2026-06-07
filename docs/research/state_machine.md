# Match-3 游戏状态机设计研究

## 目录

1. [概述](#概述)
2. [核心游戏状态定义](#核心游戏状态定义)
3. [状态转换图](#状态转换图)
4. [FSM 实现模式对比](#fsm-实现模式对比)
5. [基于 Enum 的 FSM 实现](#基于-enum-的-fsm-实现)
6. [基于节点类的 FSM 实现](#基于节点类的-fsm-实现)
7. [动画驱动的状态转换](#动画驱动的状态转换)
8. [棋盘锁定与输入管理](#棋盘锁定与输入管理)
9. [暂停状态处理](#暂停状态处理)
10. [游戏结束 / 关卡完成](#游戏结束--关卡完成)
11. [重置与重启流程](#重置与重启流程)
12. [Godot 4.x 特定模式](#godot-4x-特定模式)
13. [完整状态机代码示例](#完整状态机代码示例)
14. [参考资料](#参考资料)

---

## 概述

在 Match-3 游戏中，状态机是整个游戏逻辑的核心骨架。游戏在不同阶段需要执行完全不同的逻辑，且必须保证同一时间只能处于一个状态。例如：

- 玩家正在选择棋子时，不应同时进行匹配检测
- 动画播放期间，必须阻止玩家输入
- 棋子下落未完成时，不应触发新一轮的匹配检测

状态机的设计决定了游戏的可维护性、可扩展性以及 Bug 的发生率。

### 为什么 Match-3 游戏必须有状态机

Match-3 游戏有一个**自动循环**特性：玩家只需做一次交换，随后的过程（匹配检测 → 消除 → 下落 → 生成 → 再检测）是自动进行的。这个过程构成了一个**级联循环（Cascading Loop）**，但本质上依然是线性状态转换。

如果用 `boolean` 标志位（如 `is_swapping`, `is_checking`, `is_falling`）来管理这些阶段，会出现以下问题：

1. **非法状态组合**：多个标志可能同时为 `true`，导致逻辑混乱
2. **代码分散**：同一个状态的逻辑散布在多个函数中
3. **难以扩展**：添加新状态需要修改多处代码

参考 Robert Nystrom 在 *Game Programming Patterns* 中的论述：

> "当你有多个标志位，且同时只有一个为 `true` 时，这正是你需要 `enum` 的暗示。"

---

## 核心游戏状态定义

### 状态 Enum 定义

```gdscript
# board_state.gd
enum BoardState {
    IDLE,              # 空闲：等待玩家选择棋子
    SELECTED,           # 已选择：已选中一个棋子，等待选择第二个
    SWAPPING,           # 交换中：两个棋子正在执行交换动画
    SWAP_BACK,          # 反向交换：无效交换，棋子退回原位
    CHECKING_MATCHES,   # 匹配检测：扫描棋盘查找匹配
    CLEARING,           # 消除中：播放消除动画，移除匹配棋子
    FALLING,            # 下落中：上方棋子向下填补空位
    SPAWNING,           # 生成中：在顶部生成新棋子
    CASCADE_CHECK,      # 级联检测：判断是否需要再次进入匹配检测
    PAUSED,             # 暂停
    LEVEL_COMPLETE,     # 关卡完成
    GAME_OVER,          # 游戏结束
    RESETTING,          # 重置中：棋盘重新初始化
}
```

### 各状态职责说明

| 状态 | 职责 | 接受输入 | 持续时间 |
|------|------|----------|----------|
| `IDLE` | 等待玩家触摸/点击第一个棋子 | 是 | 直到玩家选择棋子 |
| `SELECTED` | 高亮当前选中棋子，等待选择第二个 | 是 | 直到选择第二个或取消 |
| `SWAPPING` | 播放两个棋子位置交换的 Tween 动画 | 否 | ~0.25 秒 |
| `SWAP_BACK` | 无效交换时，棋子退回原位的回弹动画 | 否 | ~0.2 秒 |
| `CHECKING_MATCHES` | 扫描整个棋盘寻找 ≥3 连的匹配 | 否 | 瞬时（同步计算） |
| `CLEARING` | 播放消除动画（缩放 + 淡出），更新分数 | 否 | ~0.15-0.3 秒 |
| `FALLING` | 播放棋子下落的 Tween 动画 | 否 | ~0.2 秒 |
| `SPAWNING` | 在空位生成新棋子并播放入场动画 | 否 | ~0.2-0.3 秒 |
| `CASCADE_CHECK` | 决定下次转换的分支状态 | 否 | 瞬时 |
| `PAUSED` | 游戏暂停，遮罩覆盖，停止所有动画 | 否 | 直到玩家恢复 |
| `LEVEL_COMPLETE` | 显示完成界面，播放胜利动画 | 否 | 直到玩家点击继续 |
| `GAME_OVER` | 显示失败界面，播放失败动画 | 否 | 直到玩家点击重试 |
| `RESETTING` | 清空棋盘，重新生成初始布局 | 否 | ~0.5 秒 |

---

## 状态转换图

### 主循环状态转换

```
                          ┌──────────────────────────────────────────────┐
                          │                                              │
                          ▼                                              │
    ┌──────────┐     ┌──────────┐     ┌──────────────┐     ┌──────────┐ │
    │          │ 点击 │          │ 点击 │              │     │          │ │
    │   IDLE   ├────►│ SELECTED ├────►│   SWAPPING   │     │  SWAP_   │ │
    │          │     │          │     │              │     │  BACK    │ │
    └──────────┘     └────┬─────┘     └──────┬───────┘     └────┬─────┘ │
          ▲               │                  │                    │      │
          │               │ 取消选择          │ 交换有效?           │ 否   │
          │               ▼                  │  ├── 是 ──────────►│      │
          │          ┌──────────┐            │  │                 │      │
          │          │          │            │  │ 交换无效 ──────►│      │
          │          │   IDLE   │◄───────────┘  │                 │      │
          │          │          │               ▼                 │      │
          │          └──────────┘     ┌──────────────────┐        │      │
          │                           │  CHECKING_        │        │      │
          │                           │  MATCHES          │        │      │
          │                           └────────┬─────────┘        │      │
          │                                    │                    │      │
          │                          ┌─────────┴─────────┐         │      │
          │                          ▼ 找到匹配?         │         │      │
          │                    ┌───────────┐     ┌───────┴──┐      │      │
          │                    │  CLEARING │     │  IDLE    │      │      │
          │                    └─────┬─────┘     └──────────┘      │      │
          │                          │                              │      │
          │                          ▼                              │      │
          │                    ┌───────────┐                        │      │
          │                    │  FALLING  │                        │      │
          │                    └─────┬─────┘                        │      │
          │                          │                              │      │
          │                          ▼                              │      │
          │                    ┌───────────┐                        │      │
          │                    │  SPAWNING │                        │      │
          │                    └─────┬─────┘                        │      │
          │                          │                              │      │
          │                          ▼                              │      │
          │                  ┌───────────────┐                      │      │
          │                  │  CASCADE_      │                      │      │
          │                  │  CHECK         │                      │      │
          │                  └───┬───────┬───┘                      │      │
          │                      │       │                           │      │
          │              有匹配? │       │ 无匹配                     │      │
          │                      │       │                           │      │
          │               CHECKING_     IDLE ◄───────────────────────┘      │
          │               MATCHES
          │                  │
          └──────────────────┘
              (循环继续)
```

### 全局状态转换

```
                    ┌─────────┐
          ┌────────►│ PAUSED  │──────────┐
          │ 暂停    └─────────┘ 恢复     │
          │                              │
    ┌─────┴─────┐                 ┌──────▼──────┐
    │           │    通关条件      │             │
    │  IDLE ◄───┼────────────────►│  LEVEL_     │
    │  (及其他)  │                 │  COMPLETE   │
    └─────┬─────┘                 └──────┬──────┘
          │                              │
          │ 失败条件                      │ 继续/重试
          │                              │
    ┌─────▼──────┐                ┌──────▼──────┐
    │            │    重试        │             │
    │  GAME_OVER ├───────────────►│  RESETTING  │
    │            │                │             │
    └────────────┘                └──────┬──────┘
                                        │
                                        ▼
                                   ┌─────────┐
                                   │  IDLE   │
                                   └─────────┘
```

---

## FSM 实现模式对比

在 Godot 4 中实现有限状态机有三种主流方式：

### 1. 基于 Enum + match 语句

**适用场景**：状态数量较少（≤10 个），转换逻辑固定

**优点**：
- 代码集中，易于阅读和调试
- 零额外开销，性能最优
- 快速原型开发

**缺点**：
- 状态超过 10 个时，match 语句会变得臃肿
- 状态代码无法复用
- 不易与其他对象共享状态逻辑

**Godot 核心模式示例**：

```gdscript
enum State { IDLE, RUN, JUMP, FALL }
var current_state: State = State.IDLE

func _physics_process(delta: float) -> void:
    match current_state:
        State.IDLE:
            _process_idle(delta)
        State.RUN:
            _process_run(delta)
        # ...
```

### 2. 基于节点类（State Pattern）

**适用场景**：复杂角色/实体，状态逻辑较多，需要复用

**优点**：
- 每个状态独立脚本，封装性好
- 可在编辑器中可视化状态结构
- 通过 `@export` 轻松配置状态转换关系
- 状态可在不同实体间复用

**缺点**：
- 代码总量更多，文件更多
- 导航代码需要跨文件跳转
- 状态间共享数据需要额外设计

### 3. 层级状态机（Hierarchical State Machine / HSM）

**适用场景**：状态可被分组归类，存在共享行为

**核心思想**：
- 定义**超状态（Super State）**和**子状态（Sub State）**
- 子状态未处理的事件向上冒泡到超状态
- 相当于面向对象中的继承关系

**Match-3 举例**：

```
BoardState (根)
├── PlayerControl (超状态 - 允许玩家输入)
│   ├── IDLE
│   └── SELECTED
├── AutoSequence (超状态 - 自动序列，锁定输入)
│   ├── SWAPPING
│   ├── CHECKING_MATCHES
│   ├── CLEARING
│   ├── FALLING
│   ├── SPAWNING
│   └── CASCADE_CHECK
├── PAUSED
├── LEVEL_COMPLETE
└── GAME_OVER
```

在 `AutoSequence` 超状态中统一处理 `_unhandled_input()` 返回 `false` 以阻止输入冒泡。

### 选择建议

对于 Match-3 游戏：

> **推荐使用 .Enum + match。语句模式作为第一步**。理由是 Match-3 的状态逻辑是严格的线性序列（IDLE → SELECTED → SWAPPING → ...），状态转换非常固定，不需要复杂的可配置性。如果后续需要更复杂的特性（如基于关卡的不同的消除规则），可以再重构为节点类模式。

---

## 基于 Enum 的 FSM 实现

### 基础结构

```gdscript
# board_state_machine.gd
class_name BoardStateMachine
extends Node

enum State {
    IDLE,
    SELECTED,
    SWAPPING,
    SWAP_BACK,
    CHECKING_MATCHES,
    CLEARING,
    FALLING,
    SPAWNING,
    CASCADE_CHECK,
    PAUSED,
    LEVEL_COMPLETE,
    GAME_OVER,
    RESETTING,
}

# 使用 setter 模式，确保状态变化时执行 entry/exit 逻辑
var current_state: State = State.IDLE:
    set(value):
        if value == current_state:
            return
        _exit_state(current_state)
        var previous = current_state
        current_state = value
        _enter_state(previous)

# 输入锁定标志
var is_input_locked: bool = false

# 信号
signal state_changed(previous: State, current: State)
signal move_made(from: Vector2i, to: Vector2i)


func _enter_state(from: State) -> void:
    state_changed.emit(from, current_state)

    match current_state:
        State.IDLE:
            is_input_locked = false
        State.SELECTED:
            is_input_locked = false
        State.SWAPPING:
            is_input_locked = true
            _do_swap()
        State.SWAP_BACK:
            is_input_locked = true
            _do_swap_back()
        State.CHECKING_MATCHES:
            is_input_locked = true
            _check_matches()
        State.CLEARING:
            is_input_locked = true
            _do_clearing()
        State.FALLING:
            is_input_locked = true
            _do_falling()
        State.SPAWNING:
            is_input_locked = true
            _do_spawning()
        State.CASCADE_CHECK:
            is_input_locked = true
            _do_cascade_check()
        State.PAUSED:
            is_input_locked = true
            _on_pause_enter()
        State.LEVEL_COMPLETE:
            is_input_locked = true
            _on_level_complete()
        State.GAME_OVER:
            is_input_locked = true
            _on_game_over()
        State.RESETTING:
            is_input_locked = true
            _do_reset_board()


func _exit_state(to: State) -> void:
    pass  # 清理当前状态的资源


func _unhandled_input(event: InputEvent) -> void:
    if is_input_locked:
        return  # 阻止输入冒泡

    match current_state:
        State.IDLE:
            _handle_input_idle(event)
        State.SELECTED:
            _handle_input_selected(event)
```

### 核心转换逻辑

```gdscript
# 交换完成后的动画回调
func _on_swap_tween_finished() -> void:
    match current_state:
        State.SWAPPING:
            # 检查交换后的棋子位置是否存在有效匹配
            if _would_create_match():
                current_state = State.CHECKING_MATCHES
            else:
                current_state = State.SWAP_BACK
        State.SWAP_BACK:
            current_state = State.IDLE
```

---

## 基于节点类的 FSM 实现

### 状态基类

```gdscript
# state.gd
class_name BoardState
extends Node

## 状态完成时发出，携带下一个状态的名称和可选数据
signal finished(next_state: String, data: Dictionary)

## 进入状态时调用
func enter(previous_state: String, data := {}) -> void:
    pass

## 离开状态时调用
func exit() -> void:
    pass

## 输入处理
func handle_input(_event: InputEvent) -> void:
    pass

## 每帧更新
func update(_delta: float) -> void:
    pass
```

### 状态机管理器

```gdscript
# board_state_machine.gd
class_name BoardStateMachine
extends Node

@export var initial_state: BoardState

@onready var state: BoardState = initial_state

func _ready() -> void:
    for child in get_children():
        if child is BoardState:
            child.finished.connect(_on_state_finished)

    await owner.ready
    state.enter("")


func _on_state_finished(next_state: String, data := {}) -> void:
    if not has_node(next_state):
        push_error("状态 " + next_state + " 不存在")
        return

    state.exit()
    state = get_node(next_state) as BoardState
    state.enter(state.name, data)


func _unhandled_input(event: InputEvent) -> void:
    state.handle_input(event)


func _process(delta: float) -> void:
    state.update(delta)
```

### 具体状态示例：IdleState

```gdscript
# states/idle_state.gd
class_name IdleState
extends BoardState

const SELECTED := "SelectedState"

@export var selected_state: BoardState


func enter(_previous_state: String, _data := {}) -> void:
    owner.show_idle_cursor()


func handle_input(event: InputEvent) -> void:
    if event is InputEventMouseButton and event.pressed:
        var tile := _get_tile_at(event.position)
        if tile and tile.is_selectable:
            finished.emit(SELECTED, {"tile": tile})
```

### 场景结构

```
Board (Node2D)
├── BoardStateMachine (Node)
│   ├── IdleState
│   ├── SelectedState
│   ├── SwappingState
│   ├── CheckingMatchesState
│   ├── ClearingState
│   ├── FallingState
│   ├── SpawningState
│   ├── CascadeCheckState
│   ├── PausedState
│   ├── LevelCompleteState
│   └── GameOverState
├── TileGrid (Node2D)
│   ├── Tile0
│   ├── Tile1
│   └── ...
└── UILayer (CanvasLayer)
```

---

## 动画驱动的状态转换

Match-3 游戏的一个核心特点是：**状态转换由动画完成驱动，而非直接由帧逻辑驱动**。在消除结束后，必须等待所有棋子的消除动画完成，才能进入下落阶段。

### Godot 4 的 Tween + await 模式

```gdscript
# 在 BoardStateMachine 中

func _do_clearing() -> void:
    # 创建 Tween
    var tween := create_tween()
    tween.set_parallel(true)  # 所有消除动画并行播放

    for tile in matched_tiles:
        tween.tween_property(tile, "modulate:a", 0.0, 0.15)
        tween.tween_property(tile, "scale", Vector2.ZERO, 0.2)

    # 等待所有动画完成后再进入下一个状态
    await tween.finished
    matched_tiles.clear()
    current_state = State.FALLING


func _do_falling() -> void:
    var tween := create_tween()
    tween.set_parallel(false)  # 下落可以逐个执行或并行

    var has_fallen := false

    for col in range(board_width):
        for row in range(board_height):
            var tile := grid[row][col]
            if tile and tile.needs_to_fall():
                has_fallen = true
                # 每个棋子下落 0.2 秒
                tween.tween_property(tile, "position", tile.target_position, 0.2)

    if not has_fallen:
        current_state = State.CASCADE_CHECK
        return

    await tween.finished
    current_state = State.SPAWNING
```

### 使用信号连接方式

如果不适合用 `await`（例如希望保持同步代码风格），可以使用 `finished` 信号：

```gdscript
func _do_swapping() -> void:
    var tween := create_tween()
    tween.tween_property(tile_a, "position", tile_b.position, 0.25)
    tween.tween_property(tile_b, "position", tile_a.position, 0.25)
    tween.finished.connect(_on_swap_finished)


func _on_swap_finished() -> void:
    current_state = State.CHECKING_MATCHES
```

### 动画时间参数参考

以下时间参数来自行业实践（参考 Azumo 的 Match-3 开发指南）：

```
| 动画类型      | 推荐时长   | 缓动曲线    |
|--------------|-----------|------------|
| 交换动画      | 0.20-0.25s | Ease.InOut |
| 消除动画      | 0.15-0.20s | Ease.Out   |
| 下落动画      | 0.15-0.25s | Ease.In    |
| 新棋子生成    | 0.20-0.30s | Ease.Out   |
| 无效交换回弹  | 0.15-0.20s | Ease.InOut |
```

**注意**：动画时间必须与状态机的检查节奏保持一致。例如，如果下落动画为 0.2 秒，但在 0.15 秒时就进行了匹配检测，则会检测到尚未到达目标位置的棋子，导致错误匹配。

---

## 棋盘锁定与输入管理

### 锁定机制

在 `SWAPPING` 到 `CASCADE_CHECK` 之间的所有状态（即整个自动处理序列），都必须锁定玩家输入。这通过 `is_input_locked` 标志位实现：

```gdscript
# 在 _unhandled_input 的入口统一判断
func _unhandled_input(event: InputEvent) -> void:
    if is_input_locked:
        return  # 吞掉输入事件，阻止其向父节点冒泡

    # 以下仅在 IDLE / SELECTED 时执行
    match current_state:
        State.IDLE:
            _handle_input_idle(event)
        State.SELECTED:
            _handle_input_selected(event)
```

### 哪些状态锁定输入

```
  自由输入 ──────────────── 锁定输入 ────────────────── 自由输入
     │                          │                           │
  IDLE, SELECTED    SWAPPING → CHECKING → CLEARING    IDLE, SELECTED
                         → FALLING → SPAWNING
                         → CASCADE_CHECK
                         → SWAP_BACK
```

**用层级状态机的思路**：将 `IDLE` 和 `SELECTED` 归入 `PlayerControl` 超状态，`SWAPPING` 到 `CASCADE_CHECK` 归入 `AutoSequence` 超状态。在 `AutoSequence` 的 `handle_input()` 中直接 `return`，无需在每个子状态重复判断。

### Godot 中阻止输入的其他方式

```gdscript
# 方式 1：使用 set_process_input(false)
func _enter_state(from: State) -> void:
    match current_state:
        State.SWAPPING:
            set_process_input(false)  # 完全禁用此节点的输入处理
        State.IDLE:
            set_process_input(true)

# 方式 2：使用 GUI 阻挡层
# 在 AutoSequence 期间显示一个全屏透明的 Control 节点
# 设置 mouse_filter = MOUSE_FILTER_STOP
```

---

## 暂停状态处理

暂停是一个**叠加状态**，可以从任何非结束状态进入，恢复后应回到原状态。

### 使用栈保存暂停前状态

```gdscript
var state_before_pause: State = State.IDLE


func toggle_pause() -> void:
    if current_state == State.PAUSED:
        _resume()
    else:
        _pause()


func _pause() -> void:
    if current_state in [State.GAME_OVER, State.LEVEL_COMPLETE, State.RESETTING]:
        return  # 结束状态不允许暂停

    state_before_pause = current_state
    current_state = State.PAUSED


func _resume() -> void:
    if current_state != State.PAUSED:
        return
    current_state = state_before_pause
```

**注意**：实际上这不是一个真正的 Pushdown Automaton（下推自动机），因为暂停不改变游戏逻辑的栈深度。如果需要支持多级暂停（例如暂停时打开设置菜单，设置菜单里又有子菜单），则应使用完整的栈结构。

### 暂停时的处理

```gdscript
func _enter_state(from: State) -> void:
    # ...
    match current_state:
        State.PAUSED:
            get_tree().paused = true
            _show_pause_overlay()

func _exit_state(to: State) -> void:
    match to:
        State.PAUSED:
            get_tree().paused = false  # 不会执行到（当前状态是 PAUSED）
```

**关键点**：使用 `get_tree().paused = true` 可以暂停整个场景树（包括所有 Tween 和定时器），是 Godot 推荐的暂停方式。但 `_unhandled_input` 在暂停时仍然会触发（属于 `process_mode` 为 `when_paused` 的节点），需要在暂停覆盖层中处理输入以支持"继续"按钮。

---

## 游戏结束 / 关卡完成

这两个状态是**终态**，进入后玩家必须做出明确操作才能离开。

### 关卡完成

```gdscript
func _on_level_complete() -> void:
    # 显示完成界面
    ui_layer.show_level_complete(
        score: current_score,
        stars: calculate_stars(current_score),
    )

    # 播放完成音效
    audio_player.play("level_complete")

    # 可选：播放一个庆祝动画序列
    var tween := create_tween()
    # ... 动画序列 ...
    await tween.finished

    # 等待玩家点击"下一关"
```

### 游戏结束

```gdscript
func _on_game_over() -> void:
    ui_layer.show_game_over(
        score: current_score,
        reason: game_over_reason,  # "步数耗尽" / "时间耗尽"
    )
```

### 转换条件

```gdscript
# 在 IDLE 状态每次进入时检查（或通过信号监听分数/步数变化）

func _check_end_conditions() -> void:
    if moves_remaining <= 0 and current_score < target_score:
        current_state = State.GAME_OVER
    elif current_score >= target_score:
        current_state = State.LEVEL_COMPLETE
```

---

## 重置与重启流程

重置需要做三件事：
1. 清空当前棋盘
2. 重新生成初始布局
3. 确保初始布局中不存在自动匹配

### 重置状态流程

```gdscript
func _do_reset_board() -> void:
    # 1. 移除所有现有棋子（可带销毁动画）
    _clear_all_tiles()

    # 2. 短暂等待确保清理完成
    await get_tree().create_timer(0.1).timeout

    # 3. 生成新棋子
    _generate_initial_board()

    # 4. 检查初始棋盘是否有匹配
    var initial_matches := find_all_matches()
    while not initial_matches.is_empty():
        # 如果有初始匹配，重新洗牌
        # 注意：这里是 `_shuffle`，不是启动完整的状态循环
        _shuffle_board()
        initial_matches = find_all_matches()

    # 5. 重置所有分数和计数器
    current_score = 0
    moves_remaining = max_moves

    # 6. 回到 IDLE
    current_state = State.IDLE
```

### 重启调用链

```
GAME_OVER ──► 玩家点击"重试" ──► RESETTING ──► IDLE
LEVEL_COMPLETE ──► 玩家点击"下一关" ──► RESETTING ──► IDLE
```

---

## Godot 4.x 特定模式

### 1. await 用于异步动画流程

Godot 4 的 `await` 关键字使得**线性书写异步动画序列**成为可能。这是 Match-3 状态机中最强大的工具：

```gdscript
func _run_auto_sequence() -> void:
    # 交换
    await _animate_swap()
    # 整个级联循环
    while true:
        # 检测匹配
        var matches := _find_all_matches()
        if matches.is_empty():
            break  # 无匹配，退出循环回到 IDLE
        # 消除
        await _animate_clear(matches)
        # 下落
        await _animate_fall()
        # 生成
        await _animate_spawn()
    # 循环结束，回到空闲
    current_state = State.IDLE
```

这种写法将原本分散在多个回调中的级联循环逻辑集中到了一处，大幅提高了可读性。

### 2. Tween 的创建与使用

```gdscript
# Godot 4 的新 Tween API
func _animate_swap() -> void:
    var tween := create_tween()

    # 设置缓动曲线（全局）
    tween.set_ease(Tween.EASE_IN_OUT)
    tween.set_trans(Tween.TRANS_QUAD)

    # 添加动画属性
    tween.tween_property(tile_a, "position", tile_b.global_position, 0.25)

    # 并行运行另一个动画
    tween.set_parallel(true)
    tween.tween_property(tile_b, "position", tile_a.global_position, 0.25)

    # 等待完成
    await tween.finished


# 带回调的链式 Tween
func _animate_clear_with_particles(matches: Array) -> void:
    var tween := create_tween()

    for tile in matches:
        # 链式调用：先缩放 → 再淡出
        tween.tween_property(tile, "scale", Vector2.ZERO, 0.15)
        tween.parallel().tween_property(tile, "modulate:a", 0.0, 0.15)

        # 在缩放开始时生成粒子
        tween.tween_callback(
            Callable(self, "_spawn_particles").bind(tile.global_position)
        )

    await tween.finished
```

### 3. 信号驱动的状态转换

使用信号（Signal）可以让状态机解耦：

```gdscript
# 定义全局信号
signal score_updated(new_score: int)
signal moves_updated(remaining: int)
signal board_stabilized
signal match_found(matches: Array)
signal no_match_found


# 在 Enter 时连接
func _enter_state(from: State) -> void:
    match current_state:
        State.CASCADE_CHECK:
            match_found.connect(_on_match_found)
            no_match_found.connect(_on_no_match)
            _do_cascade_check()

func _exit_state(to: State) -> void:
    match current_state:
        State.CASCADE_CHECK:
            match_found.disconnect(_on_match_found)
            no_match_found.disconnect(_on_no_match)
```

### 4. setter 函数实现 enter/exit 钩子

利用 GDScript 的 `set` 属性，可以在状态赋值时自动触发进入/离开逻辑：

```gdscript
var current_state: State = State.IDLE:
    set(value):
        if value == current_state:
            return
        var previous := current_state
        current_state = value       # 实际赋值
        _exit_state(previous)        # exit 用 previous 判断
        _enter_state(value)          # enter 用新值
        state_changed.emit(previous, value)
```

### 5. 处理 await 中的节点销毁问题

当使用 `await tween.finished` 时，如果节点在 Tween 完成前被销毁（如场景切换），`await` 会永远挂起。解决方案：

```gdscript
func _animate_and_transition() -> void:
    var tween := create_tween()
    # ... tween 设置 ...

    # 使用 is_inside_tree() 保护
    await tween.finished

    if not is_inside_tree():
        return  # 节点已被销毁，安全退出

    current_state = State.CHECKING_MATCHES
```

### 6. 并发状态机

Match-3 游戏中可以存在多个平行状态机：

```gdscript
# 主游戏状态机管理棋盘逻辑
var board_fsm: BoardStateMachine

# UI 状态机管理界面（与棋盘逻辑并行）
var ui_fsm: UIStateMachine

# 可以同时独立运行
func _ready() -> void:
    board_fsm.state_changed.connect(_on_board_state_changed)
    ui_fsm.state_changed.connect(_on_ui_state_changed)
```

例如，棋盘处于 `CLEARING` 状态时，UI 可以同时更新分数动画——这些是独立的、不互斥的逻辑。

---

## 完整状态机代码示例

以下是一个可直接在 Godot 4 中使用的完整 Match-3 状态机实现概要：

```gdscript
# board_state_machine.gd
class_name BoardStateMachine
extends Node

enum State {
    IDLE, SELECTED,
    SWAPPING, SWAP_BACK,
    CHECKING_MATCHES, CLEARING, FALLING, SPAWNING, CASCADE_CHECK,
    PAUSED, LEVEL_COMPLETE, GAME_OVER, RESETTING,
}

signal state_changed(prev: State, curr: State)
signal score_changed(new_score: int)

var current_state: State = State.IDLE:
    set(v):
        if v == current_state:
            return
        var prev := current_state
        current_state = v
        _exit(prev)
        _enter(v)
        state_changed.emit(prev, v)

var selected_tile: Tile = null
var board: Array = []       # 2D 数组 [row][col]
var matched_tiles: Array = []
var current_score: int = 0


func _enter(to: State) -> void:
    match to:
        State.IDLE:
            selected_tile = null

        State.SELECTED:
            selected_tile.play_select_animation()

        State.SWAPPING:
            _animate_swap()

        State.SWAP_BACK:
            _animate_swap_back()

        State.CHECKING_MATCHES:
            matched_tiles = _find_all_matches()

        State.CLEARING:
            _animate_clear()

        State.FALLING:
            _animate_fall()

        State.SPAWNING:
            _animate_spawn()

        State.CASCADE_CHECK:
            matched_tiles = _find_all_matches()

        State.PAUSED:
            get_tree().paused = true

        State.RESETTING:
            _reset()

func _exit(_from: State) -> void:
    pass


func _unhandled_input(event: InputEvent) -> void:
    if event is InputEventMouseButton and event.pressed:
        var pos := event.position
        match current_state:
            State.IDLE:
                var tile := _get_tile_at(pos)
                if tile:
                    selected_tile = tile
                    current_state = State.SELECTED

            State.SELECTED:
                var tile := _get_tile_at(pos)
                if tile == selected_tile:
                    selected_tile = null
                    current_state = State.IDLE
                elif tile and _is_adjacent(selected_tile, tile):
                    _swap_tiles(selected_tile, tile)
                    current_state = State.SWAPPING
                else:
                    selected_tile = null
                    current_state = State.IDLE

func _process(_delta: float) -> void:
    match current_state:
        State.CHECKING_MATCHES:
            if matched_tiles.is_empty():
                current_state = State.SWAP_BACK
            else:
                current_score += _calculate_score(matched_tiles)
                score_changed.emit(current_score)
                current_state = State.CLEARING

        State.CASCADE_CHECK:
            if matched_tiles.is_empty():
                current_state = State.IDLE
            else:
                current_score += _calculate_score(matched_tiles)
                score_changed.emit(current_score)
                current_state = State.CLEARING


func _animate_swap() -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    tween.tween_property(_swap_tile_a, "position", _swap_tile_b.position, 0.25)
    tween.tween_property(_swap_tile_b, "position", _swap_tile_a.position, 0.25)
    await tween.finished
    if not is_inside_tree(): return
    current_state = State.CHECKING_MATCHES


func _animate_swap_back() -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    tween.tween_property(_swap_tile_a, "position", _swap_tile_a.original_position, 0.2)
    tween.tween_property(_swap_tile_b, "position", _swap_tile_b.original_position, 0.2)
    await tween.finished
    if not is_inside_tree(): return
    current_state = State.IDLE


func _animate_clear() -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    for tile in matched_tiles:
        tween.tween_property(tile, "scale", Vector2.ZERO, 0.15)
        tween.tween_property(tile, "modulate:a", 0.0, 0.15)
    await tween.finished
    if not is_inside_tree(): return
    for tile in matched_tiles:
        tile.queue_free()
    matched_tiles.clear()
    current_state = State.FALLING


func _animate_fall() -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    var needs_fall := false
    for row in range(board.size()):
        for col in range(board[row].size()):
            var tile := board[row][col]
            if tile and tile.original_position != tile.position:
                needs_fall = true
                tween.tween_property(tile, "position", tile.original_position, 0.2)
    if not needs_fall:
        current_state = State.SPAWNING
        return
    await tween.finished
    if not is_inside_tree(): return
    current_state = State.SPAWNING


func _animate_spawn() -> void:
    _spawn_new_tiles()
    var tween := create_tween()
    tween.set_parallel(true)
    for tile in _newly_spawned_tiles:
        tile.modulate.a = 0.0
        tile.scale = Vector2.ZERO
        tween.tween_property(tile, "modulate:a", 1.0, 0.25)
        tween.tween_property(tile, "scale", Vector2.ONE, 0.25)
    await tween.finished
    if not is_inside_tree(): return
    _newly_spawned_tiles.clear()
    current_state = State.CASCADE_CHECK


func _reset() -> void:
    _clear_board()
    _generate_board()
    await get_tree().create_timer(0.3).timeout
    if not is_inside_tree(): return
    current_state = State.IDLE
```

---

## 总结与建议

### Match-3 状态机设计要点

1. **严格的顺序性**：Match-3 的核心循环是 `IDLE → SELECTED → SWAPPING → CHECKING → CLEARING → FALLING → SPAWNING → CASCADE_CHECK`，这是一个确定的序列，不需要复杂的转换图。

2. **动画是关键路径**：状态转换不应由帧逻辑驱动，而应由动画完成事件驱动。`await tween.finished` 是 Godot 4 中最简洁的实现方式。

3. **输入锁定是必须的**：在 `SWAPPING` 到 `CASCADE_CHECK` 之间的所有状态，必须完全锁定玩家输入。

4. **级联循环的终止条件**：每次 `CASCADE_CHECK` 时重新扫描整个棋盘，只有当没有任何 ≥3 连的匹配时，才回到 `IDLE`。

5. **模型与视图分离**：棋盘数据（grid 数组）是 Model，棋子节点是 View。状态机只关心 Model 的变化，View 通过 Tween 随后跟上的动画表现。这样即使跳过动画（快速模式下），游戏逻辑依然正确。

6. **暂停使用栈存储**：暂停是叠加状态，用变量保存"暂停前的状态"，恢复时还原。

7. **防御性 await**：所有 `await` 之后都应检查 `is_inside_tree()`，防止节点已被销毁导致协程挂起。

### 优先级

| 优先级 | 实现 |
|--------|------|
| 1 | Enum + match 的基础 FSM |
| 2 | `await tween.finished` 的动画驱动转换 |
| 3 | `is_input_locked` 输入锁定 |
| 4 | 暂停 / 恢复机制 |
| 5 | GAME_OVER / LEVEL_COMPLETE 终态处理 |
| 6 | 日后根据需要重构为节点类 FSM 或 HSM |

---

## 参考资料

1. **Game Programming Patterns - State** (Robert Nystrom)
   https://gameprogrammingpatterns.com/state.html
   - FSM 理论基础、State Pattern、HSM、Pushdown Automata

2. **GDQuest - Make a Finite State Machine in Godot 4** (Nathan Lovato)
   https://gdquest.com/tutorial/godot/design-patterns/finite-state-machine/
   - Godot 4 中 Enum FSM 和 Node FSM 的完整实现

3. **The Shaggy Dev - Starter State Machines in Godot 4** (Jason McCollum)
   https://shaggydev.com/2023/10/08/godot-4-state-machines/
   - Enum 状态管理和 Node 状态机的对比

4. **Azumo - The Logic Behind Match-3 Games**
   https://azumo.com/insights/the-logic-behind-match-3-games
   - Match-3 核心循环、级联机制、动画时序、模型-视图分离

5. **Stack Overflow - How to Wait for Tweens in Godot 4**
   https://stackoverflow.com/questions/76841950/how-to-wait-for-tweens-in-godot-4
   - Godot 4 `create_tween()` + `await tween.finished` 模式

6. **Godot Forum - Enum State Machines**
   https://forum.godotengine.org/t/enum-state-machines/96638
   - 社区对 Enum FSM 模式的讨论和最佳实践
