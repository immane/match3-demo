# Task 13: 主场景组装 + 导出配置

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture.md](../design/architecture.md) — Main 场景树结构、导出配置 |

## 状态
- [x] 已完成

## 依赖
- Task 09 (Board 场景)
- Task 11 (HUD 场景)
- Task 12 (菜单场景: TitleScreen, PauseMenu, GameOverPanel)
- Task 07 (Autoload)

## 产出文件
```
assets/scenes/main.tscn              # 主场景
export_presets.cfg                   # 导出预设
```

## 实现要求

### main.tscn 场景结构

参考 `docs/design/architecture.md` §2:

```
Main (Node2D) [script=main.gd]
├── GameManager (Node) [script=game_manager.gd]
├── Camera2D
│   current = true
├── Board (实例化 board.tscn)
│   screen_shake.camera = Camera2D
├── HUD (实例化 hud.tscn)
├── TitleScreen (实例化 title_screen.tscn)
├── PauseMenu (实例化 pause_menu.tscn)
├── GameOverPanel (实例化 game_over_panel.tscn)
└── AudioManager (Node)
```

### main.gd

```gdscript
class_name Main
extends Node2D

@onready var title_screen: TitleScreen = $TitleScreen
@onready var pause_menu: PauseMenu = $PauseMenu
@onready var game_over_panel: GameOverPanel = $GameOverPanel

func _ready()
    # 1. 设置窗口标题
    # 2. 连接信号:
    #    title_screen.game_started → _on_game_started
    #    EventBus.game_over → _on_game_over
    # 3. 显示标题界面

func _on_game_started()
    # title_screen.hide()
    # 通知 Board 开始
    # pause_button.show()

func _on_game_over()
    # game_over_panel.show()
```

### export_presets.cfg

创建 Web (HTML5) 导出预设:

```ini
[preset.0]
name = "HTML5"
platform = "Web"
runnable = true
dedicated_server = false
custom_features = ""
export_filter = "all_resources"
include_filter = ""
exclude_filter = ""
export_path = "export/web/index.html"
patches = PackedStringArray()

[preset.0.options]
custom_template/debug = ""
custom_template/release = ""
variant/extensions_support = false
variant/export_type = 1
vram_texture_compression/for_desktop = true
vram_texture_compression/for_mobile = false
html/export_icon = true
html/custom_html_shell = ""
html/head_include = ""
html/canvas_resize_policy = 2  # Adaptive
html/focus_canvas_on_start = true
html/experimental_virtual_keyboard = false
progressive_web_app/enabled = true
progressive_web_app/offline_page = ""
progressive_web_app/display = 1  # Standalone
progressive_web_app/orientation = 0  # Any
progressive_web_app/icon_144x144 = ""
progressive_web_app/icon_180x180 = ""
progressive_web_app/icon_512x512 = ""
progressive_web_app/background_color = Color(0.101961, 0.094118, 0.180392, 1)
```

关键导出选项:
- `variant/export_type = 1` (单线程 — 最佳兼容性)
- `html/canvas_resize_policy = 2` (自适应)
- `progressive_web_app/enabled = true` (PWA 支持)
- `vram_texture_compression/for_desktop = true`

可选: 创建 Android 和 iOS 预设占位 (暂不配置详细参数):

```ini
[preset.1]
name = "Android"
platform = "Android"
runnable = false

[preset.2]
name = "iOS"
platform = "iOS"
runnable = false
```

## 验收标准
- main.tscn 可在 Godot 编辑器中打开, 所有子场景正确实例化
- 启动时显示标题界面
- 点击 "PLAY" 后标题界面隐藏, 游戏开始
- 游戏结束时显示 GameOverPanel
- Web 导出预设配置正确 (单线程, PWA 启用)
- 导出路径指向 `export/web/index.html`

## 注意
- 确保 `project.godot` 中 `run/main_scene` 指向 `res://assets/scenes/main.tscn`
- Web 导出需要单线程模式 (`variant/export_type = 1`)
- PWA 配置为可选, 需要后续提供图标文件
- Android/iOS 预设为占位, 后续补充具体签名和配置
