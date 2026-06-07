# Task 07: Autoload 单例

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture.md](../design/architecture.md) — Autoload 设计、信号清单 |
| ↖ 设计 | [design/data_models.md](../design/data_models.md) — GameData 数据字段 |

## 状态
- [ ] 待执行

## 依赖
- Task 02 (enums, constants)
- Task 01 (project.godot 中已注册 Autoload 名称)

## 产出文件
```
scripts/autoload/game_data.gd       # GameData Autoload
scripts/autoload/event_bus.gd       # EventBus Autoload
```

## 实现要求

### game_data.gd

参考 `docs/design/architecture.md` §3.1:

```gdscript
extends Node

# 玩家数据
var high_score: int = 0
var current_score: int = 0
var current_combo: int = 0
var best_combo: int = 0
var moves_remaining: int = 30

# 设置
var music_enabled: bool = true
var sfx_enabled: bool = true
var particle_quality: int = 1  # 0=低, 1=中, 2=高

# 平台检测
var is_mobile: bool = false
var is_web: bool = false

func _ready():
    _detect_platform()

func _detect_platform():
    is_web = OS.get_name() == "Web"
    if is_web:
        # 检测移动端 (需要用 JavaScriptBridge 或 user agent 检测)
        pass

func reset_level():
    current_score = 0
    current_combo = 0
    moves_remaining = 30

func add_score(points: int):
    current_score += points
    if current_score > high_score:
        high_score = current_score
    EventBus.score_changed.emit(current_score, points)

func use_move():
    moves_remaining -= 1
    EventBus.moves_changed.emit(moves_remaining)
    if moves_remaining <= 0:
        EventBus.game_over.emit()

func update_combo(combo: int):
    current_combo = combo
    if combo > best_combo:
        best_combo = combo
    EventBus.combo_updated.emit(combo)
```

### event_bus.gd

参考 `docs/design/architecture.md` §3.2:

```gdscript
extends Node

# ---- 棋盘事件 ----
signal board_initialized
signal tile_selected(tile: Node2D, pos: Vector2i)
signal tile_deselected
signal swap_requested(from: Vector2i, to: Vector2i)
signal swap_completed(valid: bool)
signal swap_invalid

# ---- 匹配事件 ----
signal matches_found(matches: Array)
signal tiles_cleared(positions: Array)
signal special_tile_spawned(pos: Vector2i, type: int)
signal cascade_triggered(depth: int)

# ---- 分数事件 ----
signal score_changed(new_score: int, delta: int)
signal combo_updated(combo: int)
signal moves_changed(remaining: int)

# ---- 游戏状态事件 ----
signal game_state_changed(old_state: int, new_state: int)
signal game_paused
signal game_resumed
signal level_complete
signal game_over

# ---- 特效事件 ----
signal play_effect(effect_name: String, pos: Vector2)
signal screen_shake(intensity: float, duration: float)

# ---- UI 事件 ----
signal show_floating_text(text: String, pos: Vector2, color: Color)
```

**重要**: 所有信号只声明, 不需要实现逻辑。Autoload 注册名为 "EventBus" 和 "GameData"(已在 project.godot 的 `[autoload]` 段设置)。

## 验收标准
- GameData 在游戏启动时自动初始化
- EventBus 所有信号已声明
- `GameData.add_score()` 正确触发 `EventBus.score_changed`
- `GameData.use_move()` 正确触发 `EventBus.moves_changed` 并在 0 时触发 `game_over`
- 其他脚本可以通过 `EventBus.xxx.emit(...)` 和 `EventBus.xxx.connect(...)` 使用

## 注意
- Autoload 在 `project.godot` 中注册即可, Godot 自动在启动时创建实例
- GameData 的 `_detect_platform()` 可以留空 (后续添加)
- 信号参数类型要准确 (int/bool/String/Array 等)
