# Task 08: 游戏状态机

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/state_machine.md](../design/state_machine.md) — 14 状态定义、转换逻辑、级联循环 |
| ↖ 设计 | [design/gravity_cascade.md](../design/gravity_cascade.md) — 级联循环在状态机中的位置 |

## 状态
- [ ] 待执行

## 依赖
- Task 02 (enums, constants, BoardData, TileData)
- Task 03 (MatchDetector, ValidMoveChecker, ScoreCalculator)
- Task 04 (GravitySystem, SpawnSystem)
- Task 07 (EventBus, GameData)

## 产出文件
```
scripts/game/state_machine.gd       # GameStateMachine
```

## 实现要求

参考 `docs/design/state_machine.md`:

```gdscript
class_name GameStateMachine
extends Node

signal state_changed(previous: GameState, current: GameState)

# 当前状态 (使用 setter)
var current_state: GameState = GameState.IDLE:
    set(value):
        if value == current_state: return
        var prev = current_state
        current_state = value
        _exit_state(prev)
        _enter_state(value)
        state_changed.emit(prev, value)
        EventBus.game_state_changed.emit(prev, value)

# 引用
var board_data: BoardData
var tile_manager: TileManager

# 状态数据
var selected_tile: Tile = null
var swap_from: Vector2i
var swap_to: Vector2i
var cascade_depth: int = 0
var moves_used: int = 0
var state_before_pause: GameState

# 方法
func _enter_state(new_state)
func _exit_state(from_state)
func is_input_allowed() -> bool              # 仅 IDLE/SELECTED 返回 true

# 核心状态方法
func _execute_swap()                         # SWAPPING
func _execute_swap_back()                    # SWAP_BACK
func _do_check_matches()                     # CHECKING_MATCHES
func _do_cascade_check()                     # CASCADE_CHECK
func _do_check_valid()                       # CHECK_VALID
func _do_reshuffle()                         # RESHUFFLING
func _do_reset()                             # RESETTING
func _run_cascade_loop()                     # 级联主循环 (CLEARING→FALLING→SPAWNING→CASCADE_CHECK)

func toggle_pause()                          # 暂停/恢复
func _pause()
func _resume()

# 外部调用
func on_tile_clicked(tile: Tile)             # 输入处理后调用此方法
```

### 关键实现逻辑

**on_tile_clicked(tile)**:
- IDLE: 选中 tile, `selected_tile = tile`, `tile.select()`, emit tile_selected, → SELECTED
- SELECTED: 
  - 同一个→取消选择, → IDLE
  - 相邻→记录 swap_from/to, `selected_tile.deselect()`, emit swap_requested, → SWAPPING
  - 不相邻→切换选择

**_run_cascade_loop()**:
```
while cascade_depth < MAX_CASCADE_LOOPS:
    result = MatchDetector.detect_all(board)
    if no matches: → CHECK_VALID; return
    
    cascade_depth++
    score = ScoreCalculator.calculate_total(result)
    ScoreCalculator.apply_combo(score, cascade_depth)
    GameData.add_score(score)
    GameData.update_combo(cascade_depth)
    
    → CLEARING: 生成特殊水晶, emit matches_found
    await anim_finished (TODO: 由 Board 驱动)
    
    → FALLING: GravitySystem.apply_gravity(board), emit tiles_cleared
    await anim_finished
    
    → SPAWNING: SpawnSystem.fill_empty(board)
    await anim_finished
    
    EventBus.cascade_triggered.emit(cascade_depth)
    → 循环继续
```

**_execute_swap()**:
- board_data.swap(from, to)
- 通知动画 (TODO: 信号)
- await anim_finished
- result = MatchDetector.detect_all(board)
  - 有匹配: use_move, cascade_depth=1, _run_cascade_loop()
  - 无匹配: → SWAP_BACK

**注意**: Task 08 的状态机使用信号/回调与动画系统配合。由于动画系统 (AnimationController) 在 Task 10 实现, 这里可以先使用 `await get_tree().create_timer(duration).timeout` 作为临时替代。

## 验收标准
- 状态机 14 个状态全部定义
- setter 正确触发 enter/exit
- is_input_allowed() 只在 IDLE/SELECTED 返回 true
- on_tile_clicked() 处理 IDLE→SELECTED→SWAPPING 流程
- 级联循环在 MAX_CASCADE_LOOPS 处有保护
- 所有 await 后有 `is_inside_tree()` 检查
- pause/resume 正确保存/恢复状态

## 注意
- GameStateMachine 需要被添加到 Board 场景中 (Task 09)
- 与 AnimationController 的协作: 暂时使用 timer 替代动画等待
- task 文件中标注 TODO 位置, 供 Task 10 替换
