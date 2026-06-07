# Task 12: 菜单界面

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/ui_hud.md](../design/ui_hud.md) — 暂停菜单、游戏结束面板设计 |

## 状态
- [ ] 待执行

## 依赖
- Task 07 (EventBus, GameData)
- Task 08 (GameStateMachine — pause/resume)

## 产出文件
```
assets/scenes/title_screen.tscn      # 标题界面
assets/scenes/pause_menu.tscn        # 暂停菜单
assets/scenes/game_over_panel.tscn   # 游戏结束面板
scripts/ui/title_screen.gd           # 标题界面脚本
scripts/ui/pause_menu.gd             # 暂停菜单脚本
scripts/ui/game_over_panel.gd        # 游戏结束面板脚本
```

## 实现要求

### title_screen.tscn + title_screen.gd

参考 `docs/design/ui_hud.md` §4.1:

```
TitleScreen (Control) [script=title_screen.gd]
├── ColorRect (全屏背景, #1a1a2e)
├── VBoxContainer (居中)
│   ├── TitleLabel (Label)           "MATCH 3"
│   │   font_size=64, color=#ffd700
│   ├── SubtitleLabel (Label)        "Crystal Demo"
│   │   font_size=24, color=#aaaaaa
│   ├── Spacer (Control)
│   ├── StartButton (Button)         "▶ PLAY"
│   ├── HighScoreLabel (Label)       "BEST: 0"
│   └── VersionLabel (Label)         "v0.1.0"
└── AnimationPlayer (标题文字浮动动画)
```

```gdscript
class_name TitleScreen
extends Control

signal game_started

func _ready()
    # 更新最高分显示
    # StartButton.pressed → hide() + emit game_started
```

### pause_menu.tscn + pause_menu.gd

参考 `docs/design/ui_hud.md` §4.2:

```
PauseMenu (Control, 默认隐藏) [script=pause_menu.gd]
├── ColorRect (全屏半透明遮罩, rgba(0,0,0,0.7))
└── VBoxContainer (居中)
    ├── PauseLabel (Label)           "PAUSED"
    ├── Spacer
    ├── ResumeButton (Button)        "▶ RESUME"
    ├── RestartButton (Button)       "↺ RESTART"
    └── QuitButton (Button)          "✕ QUIT"
```

```gdscript
class_name PauseMenu
extends Control

func _ready()
    # EventBus.game_paused → show
    # EventBus.game_resumed → hide
    # ResumeButton → state_machine.toggle_pause()
    # RestartButton → state_machine.current_state = RESETTING
    # QuitButton → get_tree().reload_current_scene()
```

### game_over_panel.tscn + game_over_panel.gd

```
GameOverPanel (Control, 默认隐藏) [script=game_over_panel.gd]
├── ColorRect (全屏半透明遮罩)
└── VBoxContainer (居中)
    ├── GameOverLabel (Label)        "GAME OVER"
    │   font_size=48, color=#ff4444
    ├── Spacer
    ├── FinalScoreLabel (Label)      "SCORE: 0"
    ├── BestScoreLabel (Label)       "BEST: 0"
    ├── NewRecordLabel (Label)       "★ NEW RECORD!" (默认隐藏)
    ├── Spacer
    └── RetryButton (Button)         "↺ TRY AGAIN"
```

```gdscript
class_name GameOverPanel
extends Control

func _ready()
    # EventBus.game_over → show + 更新分数显示
    # RetryButton → state_machine.current_state = RESETTING
```

**NewRecordLabel**: 当 `current_score >= high_score` 且 high_score > 0 时显示

## 验收标准
- 标题界面点击 "PLAY" → 隐藏, 通过 signal 通知 Main
- 暂停菜单响应 EventBus.game_paused/game_resumed 信号
- 暂停菜单 "RESUME" → state_machine.toggle_pause()
- 暂停菜单 "RESTART" → 重置游戏
- 游戏结束面板显示最终分数和最高分
- 新纪录时显示 "★ NEW RECORD!"
- "TRY AGAIN" → 重置游戏

## 注意
- 所有菜单为绝对定位 (Control + anchors)
- 按钮使用最小尺寸 200×50
- 暂停时需要设置 `get_tree().paused = true` (在 state_machine 中)
- 退出按钮在 Web 平台可能不可用, 留空或重定向到标题
