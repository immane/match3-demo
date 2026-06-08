# 游戏状态机设计

> 基于 Godot 4 Enum + match 模式, 实现线性游戏状态转换。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/10_godot4_syntax.md](../research/10_godot4_syntax.md) — Godot 4.x await/Tween API |
| ← 研究 | [research/06_state_machine.md](../research/06_state_machine.md) — FSM 模式对比、动画驱动、await 模式 |
| ← 研究 | [research/05_cascade.md](../research/05_cascade.md) — 级联循环状态转换流程 |
| ↔ 同级 | [architecture.md](architecture.md) — 整体架构中的状态机角色 |
| ↔ 同级 | [gravity_cascade.md](gravity_cascade.md) — 级联循环状态转换细节 |
| ↔ 同级 | [input_animation.md](input_animation.md) — 动画驱动转换实现 |
| → 任务 | [Task 08](../task/08_state_machine.md) — GameStateMachine 实现 |

---

---

## 目录

1. [状态定义](#1-状态定义)
2. [状态转换图](#2-状态转换图)
3. [StateMachine 实现](#3-statemachine-实现)
4. [输入锁定策略](#4-输入锁定策略)
5. [动画驱动转换](#5-动画驱动转换)
6. [终态处理](#6-终态处理)

---

## 1. 状态定义

```gdscript
# scripts/game/state_machine.gd

enum GameState {
    IDLE             = 0,   # 等待玩家选择方块
    SELECTED         = 1,   # 已选中第一个方块
    SWAPPING         = 2,   # 交换动画中
    SWAP_BACK        = 3,   # 无效交换回退
    CHECKING_MATCHES = 4,   # 检测匹配
    CLEARING         = 5,   # 消除动画中
    FALLING          = 6,   # 下落动画中
    SPAWNING         = 7,   # 填充新方块
    CASCADE_CHECK    = 8,   # 级联判定
    CHECK_VALID      = 9,   # 死局检测
    RESHUFFLING      = 10,  # 重洗棋盘
    PAUSED           = 11,  # 暂停
    GAME_OVER        = 12,  # 游戏结束
    RESETTING        = 13,  # 重置棋盘
}
```

### 各状态职责表

| 状态 | 职责 | 接受输入 | 持续时间 |
|------|------|----------|---------|
| `IDLE` | 等待玩家选择方块 | 是 | 直到选择 |
| `SELECTED` | 高亮选中方块, 等待第二个选择 | 是 | 直到选择/取消 |
| `SWAPPING` | Tween 交换动画 | 否 | ~0.2s |
| `SWAP_BACK` | 无效交换回退动画 | 否 | ~0.15s |
| `CHECKING_MATCHES` | 同步检测匹配 | 否 | 瞬时 |
| `CLEARING` | 消除动画 + 分数更新 | 否 | ~0.2s |
| `FALLING` | 下落动画 | 否 | ~0.1-0.5s |
| `SPAWNING` | 新方块入场动画 | 否 | ~0.25s |
| `CASCADE_CHECK` | 级联判定分支 | 否 | 瞬时 |
| `CHECK_VALID` | 死局检测 | 否 | 瞬时 |
| `RESHUFFLING` | 重洗动画 | 否 | ~0.5s |
| `PAUSED` | 停止所有动画 | 否 | 直到恢复 |
| `GAME_OVER` | 显示结束界面 | 否 | 直到重试 |
| `RESETTING` | 清空+重新生成 | 否 | ~0.5s |

---

## 2. 状态转换图

```
                          ┌──────────────────────────────────────────┐
                          │                                          │
                          ▼                                          │
    ┌──────────┐     ┌──────────┐     ┌──────────────┐               │
    │          │ 点击 │          │ 点击 │              │               │
    │   IDLE   ├────►│ SELECTED ├────►│   SWAPPING   │               │
    │          │     │          │     │              │               │
    └────▲─────┘     └────┬─────┘     └──────┬───────┘               │
         │                │                  │                        │
         │                │ 取消选择           │ 交换有效?              │
         │                ▼                  ├── 是 ────────────────┐ │
         │           ┌──────────┐            │                      │ │
         │           │          │            │ 交换无效              │ │
         │           │   IDLE   │◄───────────┘                      │ │
         │           │          │               │                    │ │
         │           └──────────┘               ▼                    │ │
         │                          ┌──────────────────┐            │ │
         │                          │  CHECKING_        │            │ │
         │                          │  MATCHES          │            │ │
         │                          └────────┬─────────┘            │ │
         │                                   │                      │ │
         │                         ┌─────────┴─────────┐            │ │
         │                         ▼ 找到匹配?         │            │ │
         │                    ┌───────────┐     ┌──────┴──┐         │ │
         │                    │  CLEARING │     │ IDLE    │         │ │
         │                    └─────┬─────┘     └─────────┘         │ │
         │                          │                                │ │
         │                          ▼                                │ │
         │                    ┌───────────┐                          │ │
         │                    │  FALLING  │                          │ │
         │                    └─────┬─────┘                          │ │
         │                          │                                │ │
         │                          ▼                                │ │
         │                    ┌───────────┐                          │ │
         │                    │  SPAWNING │                          │ │
         │                    └─────┬─────┘                          │ │
         │                          │                                │ │
         │                          ▼                                │ │
         │                  ┌───────────────┐                        │ │
         │                  │  CASCADE_      │                        │ │
         │                  │  CHECK         │                        │ │
         │                  └───┬───────┬───┘                        │ │
         │                      │       │                             │ │
         │              有匹配? │       │ 无匹配                       │ │
         │                      │       │                             │ │
         │               CLEARING       CHECK_VALID                   │ │
         │                  │           │         │                   │ │
         │                  │     有移动?│         │ 无移动             │ │
         │                  │           │         │                   │ │
         │                  │         IDLE    RESHUFFLE               │ │
         │                  │                    │                    │ │
         └──────────────────┘                    IDLE                 │ │
              (循环继续)                          ▲                   │ │
                                                  └───────────────────┘ │
                                                                        │
        全局转换:                                                       │
        IDLE/SELECTED → PAUSED ⇄ 任何活跃状态                            │
        PAUSED → GAME_OVER (放弃)                                       │
        PAUSED → RESETTING (重试)                                       │
```

---

## 3. StateMachine 实现

```gdscript
# scripts/game/state_machine.gd

class_name GameStateMachine
extends Node

signal state_changed(previous: GameState, current: GameState)
signal cascade_loop_started
signal cascade_loop_ended

## 当前状态 (使用 setter 触发 enter/exit)
var current_state: GameState = GameState.IDLE:
    set(value):
        if value == current_state:
            return
        var previous := current_state
        current_state = value
        _exit_state(previous)
        _enter_state(value)
        state_changed.emit(previous, value)
        EventBus.game_state_changed.emit(previous, value)

# ---- 引用 ----
var board_data: BoardData
var tile_manager: TileManager
var anim_controller: AnimationController

# ---- 状态数据 ----
var selected_tile: Tile = null
var selected_pos: Vector2i = Vector2i(-1, -1)
var swap_from: Vector2i
var swap_to: Vector2i
var cascade_depth: int = 0
var moves_used: int = 0
var state_before_pause: GameState = GameState.IDLE


func _enter_state(new_state: GameState) -> void:
    match new_state:
        GameState.IDLE:
            selected_tile = null
            selected_pos = Vector2i(-1, -1)
        
        GameState.SELECTED:
            pass  # tile 已由输入处理设置
        
        GameState.SWAPPING:
            _execute_swap()
        
        GameState.SWAP_BACK:
            _execute_swap_back()
        
        GameState.CHECKING_MATCHES:
            _do_check_matches()
        
        GameState.CLEARING:
            pass  # 由 _run_cascade 驱动
        
        GameState.FALLING:
            pass  # 由 _run_cascade 驱动
        
        GameState.SPAWNING:
            pass  # 由 _run_cascade 驱动
        
        GameState.CASCADE_CHECK:
            _do_cascade_check()
        
        GameState.CHECK_VALID:
            _do_check_valid()
        
        GameState.RESHUFFLING:
            _do_reshuffle()
        
        GameState.PAUSED:
            get_tree().paused = true
        
        GameState.GAME_OVER:
            EventBus.game_over.emit()
        
        GameState.RESETTING:
            _do_reset()


func _exit_state(from: GameState) -> void:
    pass


# ============ 核心状态逻辑 ============

func _execute_swap() -> void:
    # 数据层交换
    board_data.swap(swap_from.x, swap_from.y, swap_to.x, swap_to.y)
    
    # 播放动画
    var tile_a := tile_manager.get_active_tile(board_data.index(swap_from.x, swap_from.y))
    var tile_b := tile_manager.get_active_tile(board_data.index(swap_to.x, swap_to.y))
    
    anim_controller.play_swap(tile_a, tile_b, SWAP_DURATION)
    await anim_controller.swap_finished
    
    if not is_inside_tree():
        return
    
    # 检测匹配 → 决定下一状态
    var result := MatchDetector.detect_all(board_data)
    if result.has_matches():
        moves_used += 1
        EventBus.moves_changed.emit(GameData.moves_remaining - moves_used)
        cascade_depth = 1
        _process_match_result(result)
    else:
        current_state = GameState.SWAP_BACK


func _execute_swap_back() -> void:
    board_data.swap(swap_from.x, swap_from.y, swap_to.x, swap_to.y)
    
    var tile_a := tile_manager.get_active_tile(board_data.index(swap_from.x, swap_from.y))
    var tile_b := tile_manager.get_active_tile(board_data.index(swap_to.x, swap_to.y))
    
    anim_controller.play_swap(tile_a, tile_b, SWAP_BACK_DURATION)
    await anim_controller.swap_finished
    
    if not is_inside_tree():
        return
    
    EventBus.swap_invalid.emit()
    current_state = GameState.IDLE


func _do_check_matches() -> void:
    var result := MatchDetector.detect_all(board_data)
    if result.has_matches():
        _process_match_result(result)
    else:
        current_state = GameState.IDLE


func _do_cascade_check() -> void:
    cascade_depth += 1
    var result := MatchDetector.detect_all(board_data)
    if result.has_matches():
        EventBus.cascade_triggered.emit(cascade_depth)
        _process_match_result(result)
    else:
        current_state = GameState.CHECK_VALID


func _process_match_result(result: MatchResult) -> void:
    # 生成特殊水晶
    for spawn in result.special_spawns:
        var tile := board_data.get_tile(spawn.position.x, spawn.position.y)
        tile.special_type = spawn.special_type
        EventBus.special_tile_spawned.emit(spawn.position, spawn.special_type)
    
    EventBus.matches_found.emit(result.groups)
    
    # CLEARING (由 cascade loop 协同处理)
    current_state = GameState.CLEARING


func _do_check_valid() -> void:
    var checker := ValidMoveChecker.new()
    if checker.has_any_valid_move(board_data):
        current_state = GameState.IDLE
    else:
        current_state = GameState.RESHUFFLING


func _do_reshuffle() -> void:
    EventBus.show_floating_text.emit("No valid moves!", 
                                     Vector2(360, 640),
                                     Color.ORANGE)
    await get_tree().create_timer(0.3).timeout
    
    board_data.reshuffle_if_needed()
    tile_manager.refresh_from_data()
    
    await get_tree().create_timer(0.3).timeout
    
    if not is_inside_tree():
        return
    current_state = GameState.IDLE


func _do_reset() -> void:
    tile_manager.release_all()
    board_data.clear()
    board_data.generate_initial_board()
    board_data.ensure_has_valid_moves()
    tile_manager.refresh_from_data()
    moves_used = 0
    GameData.reset_level()
    
    await get_tree().create_timer(0.3).timeout
    
    if not is_inside_tree():
        return
    current_state = GameState.IDLE
```

---

## 4. 输入锁定策略

```gdscript
## 检查当前状态是否允许玩家输入
func is_input_allowed() -> bool:
    match current_state:
        GameState.IDLE, GameState.SELECTED:
            return true
        _:
            return false
```

### 输入锁定对照表

```
   自由输入 ───────────────── 锁定输入 ──────────────── 自由输入
      │                            │                         │
   IDLE, SELECTED     SWAPPING → SWAP_BACK → CHECK    IDLE, SELECTED
                      → CLEARING → FALLING → SPAWN
                      → CASCADE_CHECK → CHECK_VALID
                      → RESHUFFLING
                      → PAUSED
                      → GAME_OVER
                      → RESETTING
```

---

## 5. 动画驱动转换

所有需要动画的转换都使用 `await tween.finished` 模式:

```gdscript
# AnimationController 接口
class_name AnimationController
extends Node

signal swap_finished
signal clear_finished
signal fall_finished
signal spawn_finished


func play_swap(tile_a: Tile, tile_b: Tile, duration: float) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    var pos_a := tile_a.position
    var pos_b := tile_b.position
    tween.tween_property(tile_a, "position", pos_b, duration)
    tween.tween_property(tile_b, "position", pos_a, duration)
    tween.finished.connect(func(): swap_finished.emit())


func play_clear(matched_positions: Array[Vector2i]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    
    for pos in matched_positions:
        var tile := tile_manager.get_active_tile(board_data.index(pos.x, pos.y))
        if tile:
            tween.tween_property(tile, "scale", Vector2.ZERO, CLEAR_DURATION)
            tween.tween_property(tile, "modulate:a", 0.0, CLEAR_DURATION)
    
    tween.finished.connect(func(): clear_finished.emit())


func play_falling(falls: Array[FallInfo]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    
    for fall in falls:
        var idx := board_data.index(fall.to_row, fall.col)
        var tile := tile_manager.get_active_tile(idx)
        if tile:
            var target := GridUtils.grid_to_world(fall.to_row, fall.col)
            var duration := fall.get_duration()
            tween.tween_property(tile, "position", target, duration)\
                 .set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_QUAD)
    
    tween.finished.connect(func(): fall_finished.emit())


func play_spawn(spawns: Array[SpawnInfo]) -> void:
    var tween := create_tween()
    tween.set_parallel(true)
    
    for spawn in spawns:
        var idx := board_data.index(spawn.row, spawn.col)
        var tile := tile_manager.create_tile(
            board_data.get_tile(spawn.row, spawn.col), idx)
        
        # 从屏幕外上方进入
        tile.position = GridUtils.grid_to_world(spawn.row, spawn.col)
        tile.position.y = -CELL_SIZE
        tile.modulate.a = 0.0
        tile.scale = Vector2(0.5, 0.5)
        
        var target := GridUtils.grid_to_world(spawn.row, spawn.col)
        tween.tween_property(tile, "position", target, SPAWN_DURATION)\
             .set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BOUNCE)
        tween.tween_property(tile, "modulate:a", 1.0, SPAWN_DURATION)
        tween.tween_property(tile, "scale", Vector2.ONE, SPAWN_DURATION)
    
    tween.finished.connect(func(): spawn_finished.emit())
```

### 动画时间参数

| 动画 | 时长 | 缓动曲线 |
|------|------|---------|
| 交换 | 0.2s | Ease InOut Quad |
| 无效交换回退 | 0.15s | Ease InOut Quad |
| 消除 | 0.2s | Ease Out Quad |
| 下落 (1格) | 0.1s | Ease In Quad |
| 下落 (N格) | 0.1 + N×0.08s | Ease In Quad |
| 新方块生成 | 0.25s | Ease Out Bounce |
| 重洗 | 0.5s | Ease InOut |

---

## 6. 终态处理

### 6.1 GAME_OVER

触发条件: `moves_remaining <= 0` (步数限制模式)

```gdscript
func _check_game_over() -> void:
    if GameData.moves_remaining <= 0:
        current_state = GameState.GAME_OVER
```

### 6.2 RESETTING → IDLE

```gdscript
func restart_game() -> void:
    current_state = GameState.RESETTING
    # _do_reset() 完成后自动切换到 IDLE
```

### 6.3 暂停/恢复

```gdscript
func toggle_pause() -> void:
    if current_state == GameState.PAUSED:
        _resume()
    elif current_state in [GameState.GAME_OVER, GameState.RESETTING]:
        return  # 不能在这些状态暂停
    else:
        _pause()

func _pause() -> void:
    state_before_pause = current_state
    current_state = GameState.PAUSED
    EventBus.game_paused.emit()

func _resume() -> void:
    if current_state != GameState.PAUSED:
        return
    get_tree().paused = false
    current_state = state_before_pause
    EventBus.game_resumed.emit()
```

---

## 附录: 防御性编程

```gdscript
# 所有 await 之后必须检查节点有效性
await some_tween.finished
if not is_inside_tree():
    return  # 节点已被销毁

# 状态 setter 中防止重复
var current_state: GameState = GameState.IDLE:
    set(value):
        if value == current_state:
            return  # 已经是目标状态, 跳过
        ...
```
