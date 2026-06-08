# UI 与分数系统设计

> 定义 HUD 布局、分数计算、连击倍率和 UI 交互。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/01_game_rules.md](../research/01_game_rules.md) — 计分系统、连锁倍率模型 |
| ↔ 同级 | [match_system.md](match_system.md) — ScoreCalculator 分数公式 |
| ↔ 同级 | [state_machine.md](state_machine.md) — 分数/连击/步数事件流 |
| → 任务 | [Task 11](../task/11_ui_hud.md) — HUD 场景 + 脚本实现 |
| → 任务 | [Task 12](../task/12_ui_menus.md) — 菜单界面实现 |

---

---

## 目录

1. [HUD 布局](#1-hud-布局)
2. [分数计算](#2-分数计算)
3. [HUD 脚本实现](#3-hud-脚本实现)
4. [菜单界面](#4-菜单界面)

---

## 1. HUD 布局

### 1.1 设计分辨率布局

```
720 × 1280 (竖屏移动端基准)

┌──────────────────────────────┐
│          SCORE: 12800        │  ← HUD 顶部
│       COMBO: x3              │
│       MOVES: 18 / 30         │
├──────────────────────────────┤
│                              │
│  ┌──┐┌──┐┌──┐┌──┐┌──┐     │
│  │  ││  ││  ││  ││  │     │  ← 棋盘区域
│  └──┘└──┘└──┘└──┘└──┘     │     (604×604, 居中)
│  ...                         │
│  ┌──┐┌──┐┌──┐┌──┐┌──┐     │
│  │  ││  ││  ││  ││  │     │
│  └──┘└──┘└──┘└──┘└──┘     │
│                              │
├──────────────────────────────┤
│  [ ⏸ Pause ]                │  ← 暂停按钮
└──────────────────────────────┘
```

### 1.2 节点树

```
HUD (CanvasLayer)
├── TopPanel (PanelContainer)
│   ├── HBoxContainer
│   │   ├── ScoreSection (VBoxContainer)
│   │   │   ├── ScoreLabel          # "SCORE: 0"
│   │   │   └── BestScoreLabel      # "BEST: 0"
│   │   ├── ComboSection (VBoxContainer)
│   │   │   ├── ComboLabel          # "COMBO: -"
│   │   │   └── ComboBar (ProgressBar)
│   │   └── MovesSection (VBoxContainer)
│   │       └── MovesLabel          # "MOVES: 30"
├── PauseButton (Button)             # 暂停按钮
├── FloatingTextLayer (Control)      # 浮动文字层
├── PauseMenu (Control, 隐藏)
│   ├── ColorRect (半透明背景)
│   ├── ResumeButton
│   └── QuitButton
└── GameOverPanel (Control, 隐藏)
    ├── ColorRect (半透明背景)
    ├── TitleLabel ("GAME OVER")
    ├── FinalScoreLabel
    ├── BestScoreLabel
    └── RetryButton
```

---

## 2. 分数计算

### 2.1 分数公式

```gdscript
# 基础分数
const SCORE_3 = 30      # 3 连
const SCORE_4 = 60      # 4 连
const SCORE_5 = 100     # 5 连
const SCORE_PER_EXTRA = 50  # 超过 5 连每多一个 +50

# 特殊水晶消除分数
const SCORE_BOMB = 150
const SCORE_RAINBOW = 200
const SCORE_CROSS = 180
```

### 2.2 分数计算器

```gdscript
# scripts/core/score_calculator.gd

class_name ScoreCalculator
extends RefCounted


## 计算一个匹配组的分数 (不含连击倍率)
static func calculate_group_score(group: MatchGroup) -> int:
    var base := 0
    
    match group.shape:
        MatchShape.H_LINE, MatchShape.V_LINE:
            match group.match_length:
                3: base = SCORE_3
                4: base = SCORE_4
                5: base = SCORE_5
                _: base = SCORE_5 + (group.match_length - 5) * SCORE_PER_EXTRA
        
        MatchShape.L_SHAPE, MatchShape.T_SHAPE:
            base = SCORE_CROSS
        
        MatchShape.CROSS:
            base = SCORE_CROSS + 50
    
    return base


## 计算所有匹配组的总分 (不含连击倍率)
static func calculate_total(match_result: MatchResult) -> int:
    var total := 0
    for group in match_result.groups:
        total += calculate_group_score(group)
    return total


## 应用连击倍率
static func apply_combo(base_score: int, combo_depth: int) -> int:
    return base_score * combo_depth
```

### 2.3 连击倍率表

| 连锁深度 | 倍率 | 显示文字 |
|---------|------|---------|
| 1 (首次) | ×1 | - |
| 2 | ×2 | "Combo x2" |
| 3 | ×3 | "Combo x3" |
| 4 | ×4 | "Amazing x4" |
| 5 | ×5 | "Incredible x5" |
| 6+ | ×N | "INSANE xN!" |

---

## 3. HUD 脚本实现

```gdscript
# scripts/ui/hud.gd

class_name HUD
extends CanvasLayer

@onready var score_label: Label = $TopPanel/ScoreSection/ScoreLabel
@onready var best_score_label: Label = $TopPanel/ScoreSection/BestScoreLabel
@onready var combo_label: Label = $TopPanel/ComboSection/ComboLabel
@onready var moves_label: Label = $TopPanel/MovesSection/MovesLabel
@onready var pause_menu: Control = $PauseMenu
@onready var game_over_panel: Control = $GameOverPanel

var _score_tween: Tween = null
var _displayed_score: int = 0


func _ready() -> void:
    # 订阅事件
    EventBus.score_changed.connect(_on_score_changed)
    EventBus.combo_updated.connect(_on_combo_updated)
    EventBus.moves_changed.connect(_on_moves_changed)
    EventBus.game_over.connect(_on_game_over)
    
    # 初始化显示
    _update_score_display()
    _update_moves_display()
    combo_label.hide()


func _on_score_changed(new_score: int, delta: int) -> void:
    _animate_score(new_score, delta)
    _update_best_score()


func _animate_score(target: int, delta: int) -> void:
    if _score_tween and _score_tween.is_running():
        _score_tween.kill()
    
    _score_tween = create_tween()
    _score_tween.tween_method(
        func(v: int): score_label.text = "SCORE: %d" % v,
        _displayed_score, target, 0.3
    ).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_QUAD)
    
    # 立即更新内部值
    _displayed_score = target
    
    # 显示浮动分数变化
    if delta > 0:
        EventBus.show_floating_text.emit(
            "+%d" % delta,
            score_label.global_position + Vector2(40, 10),
            Color.WHITE
        )


func _update_score_display() -> void:
    score_label.text = "SCORE: %d" % GameData.current_score


func _update_best_score() -> void:
    if GameData.current_score > GameData.high_score:
        GameData.high_score = GameData.current_score
    best_score_label.text = "BEST: %d" % GameData.high_score


func _on_combo_updated(combo: int) -> void:
    if combo <= 1:
        combo_label.hide()
        return
    
    combo_label.show()
    
    var text: String
    match combo:
        2: text = "Combo x2"
        3: text = "Combo x3"
        4: text = "Amazing x4"
        5: text = "Incredible x5"
        _: text = "INSANE x%d!" % combo
    
    combo_label.text = text
    
    # 弹出动画
    var tween := create_tween()
    combo_label.scale = Vector2(2.0, 2.0)
    combo_label.modulate.a = 1.0
    tween.tween_property(combo_label, "scale", Vector2.ONE, 0.3)\
         .set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK)
    
    # 连击文字颜色逐渐变暖
    combo_label.add_theme_color_override("font_color",
        Color(1.0, 1.0 - combo * 0.1, 0.3))


func _on_moves_changed(remaining: int) -> void:
    moves_label.text = "MOVES: %d" % remaining
    
    # 剩余步数少时变色警告
    if remaining <= 5:
        moves_label.add_theme_color_override("font_color", Color.RED)
    elif remaining <= 10:
        moves_label.add_theme_color_override("font_color", Color.ORANGE)
    else:
        moves_label.add_theme_color_override("font_color", Color.WHITE)


func _update_moves_display() -> void:
    moves_label.text = "MOVES: %d" % GameData.moves_remaining


# ---- 暂停 ----
func _on_pause_pressed() -> void:
    var sm := _get_state_machine()
    if sm:
        sm.toggle_pause()
    pause_menu.visible = not pause_menu.visible


# ---- 游戏结束 ----
func _on_game_over() -> void:
    game_over_panel.show()
    game_over_panel.get_node("FinalScoreLabel").text = \
        "SCORE: %d" % GameData.current_score
    game_over_panel.get_node("BestScoreLabel").text = \
        "BEST: %d" % GameData.high_score


func _on_retry_pressed() -> void:
    game_over_panel.hide()
    var sm := _get_state_machine()
    if sm:
        sm.current_state = GameState.RESETTING


func _get_state_machine() -> GameStateMachine:
    return get_tree().get_first_node_in_group("state_machine")
```

---

## 4. 菜单界面

### 4.1 标题界面 (TitleScreen)

```gdscript
# scripts/ui/title_screen.gd

class_name TitleScreen
extends Control

signal game_started


func _ready() -> void:
    $StartButton.pressed.connect(_on_start_pressed)
    $SettingsButton.pressed.connect(_on_settings_pressed)
    _update_high_score()


func _update_high_score() -> void:
    $HighScoreLabel.text = "BEST: %d" % GameData.high_score


func _on_start_pressed() -> void:
    hide()
    game_started.emit()


func _on_settings_pressed() -> void:
    $SettingsPanel.show()
```

### 4.2 暂停菜单

```gdscript
# scripts/ui/pause_menu.gd

class_name PauseMenu
extends Control


func _ready() -> void:
    EventBus.game_paused.connect(show)
    EventBus.game_resumed.connect(hide)
    hide()
    
    $ResumeButton.pressed.connect(_on_resume)
    $RestartButton.pressed.connect(_on_restart)
    $QuitButton.pressed.connect(_on_quit)


func _on_resume() -> void:
    var sm := _get_state_machine()
    if sm:
        sm.toggle_pause()


func _on_restart() -> void:
    hide()
    var sm := _get_state_machine()
    if sm:
        sm.current_state = GameState.RESETTING


func _on_quit() -> void:
    # 回到标题界面
    get_tree().paused = false
    get_tree().reload_current_scene()


func _get_state_machine() -> GameStateMachine:
    return get_tree().get_first_node_in_group("state_machine")
```

---

## 附录: UI 配色方案

| 元素 | 颜色 | 说明 |
|------|------|------|
| 背景 | `#1a1a2e` → `#16213e` | 深紫蓝色渐变 |
| 棋盘格 1 | `#3a3a5c` | 深灰紫 |
| 棋盘格 2 | `#2e2e4a` | 更深的灰紫 |
| 文字主色 | `#ffffff` | 白色 |
| 文字强调 | `#ffd700` | 金色 (分数/连击) |
| 文字警告 | `#ff4444` | 红色 (低步数) |
| 按钮 | `#5c4a7a` | 紫色按钮 |
| 按钮按下 | `#7a5c9a` | 浅紫色按钮 |
| 面板背景 | `rgba(0,0,0,0.7)` | 半透明黑 (菜单遮罩) |

### 字体规格

| 用途 | 字体大小 | 字重 |
|------|---------|------|
| SCORE 标签 | 22px | Bold |
| 分数值 | 32px | Bold |
| COMBO | 28px | Bold |
| MOVES | 22px | Bold |
| 浮动文字 | 20px | Bold |
| 菜单标题 | 48px | Bold |
| 按钮文字 | 24px | Regular |
