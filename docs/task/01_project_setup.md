# Task 01: 项目初始化

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture.md](../design/architecture.md) — 项目架构、文件结构 |

## 状态
- [ ] 待执行

## 依赖
- 无 (Phase 1, 可与其他 Phase 1 任务并行)

## 产出文件
```
match3-demo/
├── project.godot
├── .gitignore                    # 已存在, 无需修改
├── assets/
│   ├── shaders/
│   │   └── .gitkeep
│   ├── textures/
│   │   └── .gitkeep
│   ├── scenes/
│   │   └── .gitkeep
│   ├── fonts/
│   │   └── .gitkeep
│   └── audio/
│       └── .gitkeep
├── scripts/
│   ├── autoload/
│   │   └── .gitkeep
│   ├── core/
│   │   └── .gitkeep
│   ├── game/
│   │   └── .gitkeep
│   ├── ui/
│   │   └── .gitkeep
│   ├── fx/
│   │   └── .gitkeep
│   └── utils/
│       └── .gitkeep
```

## 实现要求

### project.godot
创建 Godot 4.x 项目配置文件, 包含以下关键设置:

```ini
[application]
config/name = "Match3 Demo"
config/description = "A crystal-themed match-3 puzzle demo"
config/version = "0.1.0"
run/main_scene = "res://assets/scenes/main.tscn"

[autoload]
GameData = "*res://scripts/autoload/game_data.gd"
EventBus = "*res://scripts/autoload/event_bus.gd"

[rendering]
renderer/rendering_method = "gl_compatibility"
textures/canvas_textures/default_texture_filter = 5
rendering/2d/batching = true
rendering/2d/batching_synchronous = false

[display]
window/size/viewport_width = 720
window/size/viewport_height = 1280
window/stretch/mode = "canvas_items"
window/stretch/aspect = "expand"

[audio]
general/default_playback_type.web = 0

[input_devices]
pointing/emulate_touch_from_mouse = true
```

### .gitkeep 文件
在所有空目录中放置 `.gitkeep` 文件, 确保目录结构被 git 跟踪。

## 验收标准
- `project.godot` 存在且可被 Godot 4.x 编辑器打开
- 所有目录结构已创建
- `renderer/rendering_method = "gl_compatibility"` 已设置 (WebGL 导出必需)
- 两个 Autoload 已注册 (GameData, EventBus)
- 项目名称和描述已设置
