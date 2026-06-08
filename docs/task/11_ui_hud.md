# Task 11: HUD 界面

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/ui_hud.md](../design/ui_hud.md) — HUD 布局、分数公式、连击动画、配色方案 |

## 状态
- [x] 已完成

## 依赖
- Task 07 (EventBus, GameData)
- Task 09 (Board 场景 — HUD 将作为 main.tscn 中的 CanvasLayer)

## 产出文件
```
assets/scenes/hud.tscn              # HUD 场景
scripts/ui/hud.gd                   # HUD 脚本
```

## 实现要求

### hud.tscn 场景结构

参考 `docs/design/ui_hud.md` §1:

```
HUD (CanvasLayer) [script=hud.gd]
├── TopPanel (PanelContainer)
│   └── HBoxContainer
│       ├── ScoreSection (VBoxContainer)
│       │   ├── ScoreLabel (Label)        "SCORE: 0"
│       │   └── BestScoreLabel (Label)    "BEST: 0"
│       ├── ComboSection (VBoxContainer)
│       │   ├── ComboLabel (Label)        "COMBO: -" (初始隐藏)
│       │   └── ComboBar (ProgressBar)    (可选)
│       └── MovesSection (VBoxContainer)
│           └── MovesLabel (Label)        "MOVES: 30"
├── PauseButton (Button)                    "⏸"
├── FloatingTextLayer (Control)              # 浮动文字在此层
└── FloatingTextSpawner (Node) [script=floating_text.gd]
```

### hud.gd

参考 `docs/design/ui_hud.md` §3:

```gdscript
class_name HUD
extends CanvasLayer

@onready var score_label: Label
@onready var best_score_label: Label
@onready var combo_label: Label
@onready var moves_label: Label
@onready var pause_button: Button

var _score_tween: Tween
var _displayed_score: int = 0

func _ready()
    # 连接 EventBus 信号:
    #   score_changed → _on_score_changed
    #   combo_updated → _on_combo_updated
    #   moves_changed → _on_moves_changed
    #   game_over → _on_game_over (TODO: Task 12)
    # 初始化显示

func _on_score_changed(new_score: int, delta: int)
    # Tween 数字动画: score_label 从 _displayed_score 渐变到 new_score
    # 显示 "+delta" 浮动文字

func _on_combo_updated(combo: int)
    # combo <= 1: 隐藏
    # combo 2-5+: 显示不同文字 (Combo x2 → INSANE x8!)
    # 弹出动画: scale 2→1
    # 颜色渐变: 越深越暖

func _on_moves_changed(remaining: int)
    # 更新文字, ≤5 红色, ≤10 橙色, 其他白色

func _on_pause_pressed()
    # 触发 state_machine.toggle_pause()
```

### UI 配色

参考 `docs/design/ui_hud.md` 附录:

| 元素 | 颜色 |
|------|------|
| 文字主色 | #ffffff |
| 分数强调 | #ffd700 (金色) |
| 步数警告 | #ff4444 (红色) |
| 步数注意 | #ff8800 (橙色) |
| 连击文字 | 1.0, 1.0-combo*0.1, 0.3 |
| 面板背景 | rgba(0,0,0,0.5) |

Label 字体配置:
- 使用 `add_theme_font_size_override("font_size", size)` 设置大小
- SCORE 标签: 22px, 分数值: 32px Bold
- COMBO: 28px Bold
- MOVES: 22px Bold

## 验收标准
- HUD 在 CanvasLayer 上正确渲染, 不随相机移动
- 分数变化时数字平滑渐变 (Tween)
- 连击文字有弹出动画
- 步数 ≤5 时显示红色警告
- 暂停按钮可点击

## 注意
- HUD 是独立的 CanvasLayer, 不需要添加到 Board 场景内部
- 暂停按钮需要能访问 state_machine (通过 `get_tree().get_first_node_in_group("state_machine")`)
- 浮动文字使用 Task 10 的 FloatingTextSpawner
