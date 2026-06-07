# 项目架构设计

> 基于 research 文档，定义 match3-demo 的整体架构、场景树、Autoload 和信号流。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/state_machine.md](../research/state_machine.md) — 状态机理论基础 |
| ← 研究 | [research/cascade.md](../research/cascade.md) — 级联循环和信号流参考 |
| ↔ 同级 | [data_models.md](data_models.md) — 核心数据结构 |
| ↔ 同级 | [state_machine.md](state_machine.md) — 状态机详细设计 |
| → 任务 | [Task 01](../task/01_project_setup.md) — 项目初始化 |
| → 任务 | [Task 07](../task/07_autoloads.md) — Autoload 实现 |
| → 任务 | [Task 13](../task/13_main_scene_export.md) — 主场景组装 |

---

---

## 目录

1. [技术选型](#1-技术选型)
2. [场景树结构](#2-场景树结构)
3. [Autoload 单例](#3-autoload-单例)
4. [信号流设计](#4-信号流设计)
5. [数据流架构](#5-数据流架构)
6. [文件结构](#6-文件结构)

---

## 1. 技术选型

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 引擎版本 | Godot 4.4+ | 最新稳定版，Compatibility 渲染器改进 |
| 渲染器 | Compatibility (GLES3) | WebGL 2.0 必需，无 Forward+/Mobile |
| 脚本语言 | GDScript | Godot 原生，快速原型 |
| 棋盘尺寸 | 8×8 | 经典尺寸，移动端友好 |
| 水晶种类 | 5 种颜色 | 平衡匹配难度和死局概率 |
| 状态机 | Enum + match | 状态 < 15 个，线性转换，首选方案 |
| 动画 | Tween + await | Godot 4 原生模式 |
| 网格存储 | 一维线性数组 `Array` | 缓存友好，批量操作快 |
| 导出模式 | 单线程 Web | 兼容性好，无需 COOP/COEP 头 |

---

## 2. 场景树结构

```
Main (Node2D)                              # 主场景
├── GameManager (Node)                     # 游戏管理器 (非 Autoload, 场景级)
├── Board (Node2D)                         # 棋盘根节点
│   ├── BackgroundLayer (Node2D)           # 背景层 (棋盘格 + 光效)
│   │   ├── BackgroundSprite               # 暗色背景
│   │   ├── GridLines (draw)               # 网格线
│   │   └── AmbientParticles (GPU2D)       # 浮动微光粒子
│   ├── TileLayer (Node2D)                 # 水晶方块层
│   │   └── Tiles (动态管理, 对象池)        # 水晶方块节点
│   ├── EffectLayer (Node2D)               # 特效层
│   │   ├── MatchParticles (GPU2D)         # 消除粒子
│   │   └── SelectionIndicator             # 选中高亮
│   └── InputArea (Area2D)                 # 输入区域
│       └── CollisionShape2D               # 覆盖整个棋盘
├── UILayer (CanvasLayer)                  # UI 层
│   ├── HUD (Control)                      # 游戏内 HUD
│   │   ├── ScoreLabel
│   │   ├── ComboLabel
│   │   ├── MovesLabel
│   │   └── LevelLabel
│   ├── PauseMenu (Control)                # 暂停菜单 (隐藏)
│   ├── GameOverPanel (Control)            # 结束面板 (隐藏)
│   └── TitleScreen (Control)              # 标题界面 (隐藏)
└── AudioManager (Node)                    # 音频管理
    ├── BGMPlayer
    └── SFXPool (多个 AudioStreamPlayer)
```

---

## 3. Autoload 单例

### 3.1 GameData (game_data.gd)

全局游戏数据，跨场景持久。

```gdscript
# Autoload 名称: GameData
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

func reset_level():
    current_score = 0
    current_combo = 0
    moves_remaining = 30

func add_score(points: int):
    current_score += points
    if current_score > high_score:
        high_score = current_score
```

### 3.2 EventBus (event_bus.gd)

全局事件总线，解耦系统间通信。

```gdscript
# Autoload 名称: EventBus
extends Node

# ---- 棋盘事件 ----
signal board_initialized
signal tile_selected(tile: Node2D, pos: Vector2i)
signal tile_deselected
signal swap_requested(from: Vector2i, to: Vector2i)
signal swap_completed(valid: bool)
signal swap_invalid

# ---- 匹配事件 ----
signal matches_found(matches: Array)        # Array[MatchGroup]
signal tiles_cleared(positions: Array)       # Array[Vector2i]
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

---

## 4. 信号流设计

### 4.1 一次完整交换的信号流

```
玩家点击/触摸
    │
    ▼
InputHandler._input(event)
    │
    ├─ 第一个选择 → EventBus.tile_selected(tile, pos)
    │   ├─ Board.on_tile_selected() → 高亮 tile
    │   └─ StateMachine → IDLE → SELECTED
    │
    └─ 第二个选择 → EventBus.swap_requested(from, to)
        │
        ▼
    Board._on_swap_requested(from, to)
        │
        ├─ 数据层交换 grid[from] ↔ grid[to]
        ├─ Tween 动画播放
        ├─ StateMachine → SWAPPING
        │
        await tween.finished
        │
        ▼
    MatchDetector.detect_all(grid)
        │
        ├─ 无匹配 → EventBus.swap_invalid
        │   ├─ Board._on_swap_invalid() → 回退动画
        │   └─ StateMachine → SWAP_BACK → IDLE
        │
        └─ 有匹配 → EventBus.matches_found(matches)
            │
            ▼
        Board._on_matches_found(matches)
            │
            ├─ StateMachine → CHECKING_MATCHES → CLEARING
            ├─ 生成特殊水晶 (4连=炸弹, 5连=彩虹, L/T=十字)
            ├─ Tween 消除动画
            │
            await tween.finished
            │
            ├─ EventBus.tiles_cleared
            ├─ EventBus.score_changed
            │
            ├─ StateMachine → FALLING
            ├─ GravitySystem.apply_gravity()
            ├─ Tween 下落动画
            │
            await tween.finished
            │
            ├─ StateMachine → SPAWNING
            ├─ SpawnSystem.fill_empty()
            ├─ Tween 生成动画
            │
            await tween.finished
            │
            ├─ StateMachine → CASCADE_CHECK
            └─ MatchDetector.detect_all(grid)
                │
                ├─ 有匹配 → 回到 CLEARING (连锁)
                └─ 无匹配 → StateMachine → IDLE
                    └─ Board._check_valid_moves()
                        ├─ 有可用移动 → 等待输入
                        └─ 无可用移动 → Reshuffle → IDLE
```

---

## 5. 数据流架构

### 5.1 模型-视图分离

```
┌─────────────────────────────────────────────┐
│              Model (数据层)                    │
│                                               │
│  BoardData (grid array, 1D)                  │
│  ├── tile_type: int[ROWS*COLS]               │
│  ├── special_type: int[ROWS*COLS]            │
│  └── obstacle_hp: int[ROWS*COLS]             │
│                                               │
│  MatchDetector → 纯函数, 输入 grid → 输出匹配  │
│  GravitySystem → 纯逻辑, 修改 grid 数据        │
│  ValidMoveChecker → 纯函数, 检测死局           │
└───────────────────┬─────────────────────────┘
                    │ 事件/信号
                    ▼
┌─────────────────────────────────────────────┐
│              View (表现层)                    │
│                                               │
│  TileManager → 管理 Tile 节点(对象池)          │
│  AnimationController → Tween 动画              │
│  ParticleController → 粒子特效                 │
│  UILayer → HUD 更新, 浮动文字                 │
└─────────────────────────────────────────────┘
```

### 5.2 关键设计原则

1. **数据层完全独立**：所有游戏逻辑操作纯数据结构，不引用 Node
2. **视图层消费信号**：通过 EventBus 订阅数据变化来驱动动画
3. **可单独测试**：MatchDetector、GravitySystem 可以不依赖场景进行单元测试
4. **回放友好**：只需记录数据层变化序列即可回放整局游戏
5. **快速模式可能**：可以跳过所有动画直接呈现最终状态（用于 AI 训练/测试）

---

## 6. 文件结构

```
match3-demo/
├── project.godot
├── export_presets.cfg
│
├── assets/
│   ├── shaders/
│   │   ├── crystal.gdshader              # 几何水晶折射 shader
│   │   └── background.gdshader           # 流动背景 shader
│   ├── textures/
│   │   ├── crystal_atlas.png             # 水晶纹理图集 (1024×1024)
│   │   ├── ui_atlas.png                  # UI 图集 (512×512)
│   │   └── particle_atlas.png            # 粒子精灵表 (512×512)
│   ├── scenes/
│   │   ├── main.tscn                     # 主场景
│   │   ├── board.tscn                    # 棋盘场景
│   │   └── tile.tscn                     # 水晶方块场景
│   ├── fonts/
│   │   └── default_font.ttf              # 默认字体
│   └── audio/
│       ├── bgm.ogg
│       ├── sfx_match.ogg
│       ├── sfx_swap.ogg
│       ├── sfx_swap_invalid.ogg
│       ├── sfx_combo.ogg
│       └── sfx_special.ogg
│
├── scripts/
│   ├── autoload/
│   │   ├── game_data.gd                  # Autoload: 全局数据
│   │   └── event_bus.gd                  # Autoload: 事件总线
│   ├── core/
│   │   ├── board_data.gd                 # 棋盘数据结构 (class)
│   │   ├── match_detector.gd             # 匹配检测算法 (class)
│   │   ├── gravity_system.gd             # 重力下落逻辑 (class)
│   │   ├── spawn_system.gd               # 方块生成逻辑 (class)
│   │   └── valid_move_checker.gd         # 死局检测 (class)
│   ├── game/
│   │   ├── board.gd                      # 棋盘控制 (Board 节点脚本)
│   │   ├── state_machine.gd              # 游戏状态机
│   │   ├── tile.gd                       # 水晶方块节点脚本
│   │   ├── input_handler.gd              # 输入处理
│   │   ├── tile_manager.gd               # 方块对象池管理
│   │   ├── animation_controller.gd       # 动画控制
│   │   └── special_effects.gd            # 特殊水晶效果处理
│   ├── ui/
│   │   ├── hud.gd                        # HUD 更新脚本
│   │   ├── title_screen.gd               # 标题界面
│   │   ├── pause_menu.gd                 # 暂停菜单
│   │   ├── game_over_panel.gd            # 结束面板
│   │   └── floating_text.gd              # 浮动文字
│   ├── fx/
│   │   ├── particle_controller.gd        # 粒子特效管理
│   │   └── screen_shake.gd               # 屏幕震动
│   └── utils/
│       ├── constants.gd                  # 全局常量
│       ├── enums.gd                      # 全局枚举定义
│       └── math_utils.gd                 # 数学工具函数
│
├── docs/
│   ├── research/                         # 研究报告 (已有)
│   │   ├── game_rules.md
│   │   ├── board_design.md
│   │   ├── match_algorithm.md
│   │   ├── gravity.md
│   │   ├── cascade.md
│   │   ├── state_machine.md
│   │   ├── random_generation.md
│   │   ├── performance.md
│   │   └── future_work.md
│   └── design/                           # 设计文档 (当前)
│       ├── architecture.md
│       ├── data_models.md
│       ├── board_system.md
│       ├── match_system.md
│       ├── gravity_cascade.md
│       ├── state_machine.md
│       ├── crystal_shader.md
│       ├── input_animation.md
│       └── ui_hud.md
│
└── export/
    ├── web/                              # WebGL 导出相关
    │   ├── index.html                    # 自定义 HTML shell
    │   ├── service_worker.js
    │   └── manifest.json                 # PWA manifest
    └── custom_build/                     # 自定义导出模板配置
        └── custom.py                     # SCons 编译配置
```
