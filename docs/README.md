# Match-3 Demo 文档索引

> Godot 4.x 水晶消消乐 — 完整设计文档体系

---

## 文档层次

```
docs/
├── README.md              ← 你在这里 (总索引)
├── research/              ← 层1: 行业研究 (理论)
├── design/                ← 层2: 系统设计 (方案)
└── task/                  ← 层3: 实现任务 (执行)
```

| 层次 | 目录 | 性质 | 粒度 | 作用 |
|------|------|------|------|------|
| L1 研究 | `research/` | 理论/参考 | 行业综述、算法对比 | 回答 "别人怎么做" |
| L2 设计 | `design/` | 方案/规格 | Godot 具体实现 | 回答 "我们怎么做" |
| L3 任务 | `task/` | 执行/分解 | 单文件/单类 | 回答 "谁先做, 依赖谁" |

---

## L1: Research (行业研究)

| # | 文件 | 研究主题 | 对应设计文档 |
|---|------|---------|------------|
| 1 | [game_rules.md](research/game_rules.md) | 三消核心规则、特殊方块、计分 | [data_models](design/data_models.md) [ui_hud](design/ui_hud.md) |
| 2 | [board_design.md](research/board_design.md) | 棋盘尺寸、网格表示、坐标系统 | [board_system](design/board_system.md) |
| 3 | [match_algorithm.md](research/match_algorithm.md) | 匹配检测算法、Flood-Fill、Bitboard | [match_system](design/match_system.md) |
| 4 | [gravity.md](research/gravity.md) | 重力下落、动画时序、障碍物处理 | [gravity_cascade](design/gravity_cascade.md) |
| 5 | [cascade.md](research/cascade.md) | 级联循环、连锁反应、连击系统 | [gravity_cascade](design/gravity_cascade.md) |
| 6 | [state_machine.md](research/state_machine.md) | 状态机设计、动画驱动、输入锁定 | [state_machine](design/state_machine.md) [architecture](design/architecture.md) |
| 7 | [random_generation.md](research/random_generation.md) | 随机策略、死局检测、Bag 系统 | [board_system](design/board_system.md) [match_system](design/match_system.md) |
| 8 | [performance.md](research/performance.md) | WebGL 优化、Shader、对象池、WASM | [crystal_shader](design/crystal_shader.md) [board_system](design/board_system.md) |
| 9 | [future_work.md](research/future_work.md) | 关卡系统、障碍物、存档、多平台 | (后续版本) |
| 10 | [godot4_syntax.md](research/godot4_syntax.md) | Godot 4.x GDScript 语法 & API 速查 | 全部 Design / Task 文件 |

---

## L2: Design (系统设计)

| # | 文件 | 设计主题 | 依赖 Research | 驱动 Tasks |
|---|------|---------|-------------|-----------|
| 1 | [architecture.md](design/architecture.md) | 整体架构、场景树、信号流、文件结构 | state_machine, cascade | 01, 07, 09, 13 |
| 2 | [data_models.md](design/data_models.md) | 枚举、常量、TileData、BoardData、MatchResult | game_rules, board_design | 02, 03, 04 |
| 3 | [board_system.md](design/board_system.md) | 坐标系统、初始化、Tile 节点、对象池 | board_design, random_gen, performance | 06, 09 |
| 4 | [match_system.md](design/match_system.md) | 两阶段匹配检测、形状分类、死局检测 | match_algorithm | 03 |
| 5 | [gravity_cascade.md](design/gravity_cascade.md) | 重力下落、方块生成、级联循环 | gravity, cascade | 04, 08 |
| 6 | [state_machine.md](design/state_machine.md) | 14 状态定义、转换、输入锁定、暂停 | state_machine | 08 |
| 7 | [crystal_shader.md](design/crystal_shader.md) | 几何水晶 shader (切面/菲涅尔/色散) | performance | 05, 06 |
| 8 | [input_animation.md](design/input_animation.md) | 输入处理、Tween 动画、粒子/震动特效 | board_design, state_machine | 09, 10 |
| 9 | [ui_hud.md](design/ui_hud.md) | HUD 布局、分数公式、连击倍率、配色 | game_rules | 11, 12 |
| 10 | [testing.md](design/testing.md) | 测试金字塔、37个用例、GUT框架、CI集成 | architecture, match_system, gravity_cascade | 14 |

---

## L3: Tasks (实现任务)

### 依赖图

```
Phase 1 ─── 最大并行 ──────────────────────────────
│
├── Task 01 ─┬─ project.godot + 目录结构
│            │  └─ [设计: architecture]
│            │
├── Task 02 ─┼─ enums, constants, TileData, BoardData
│            │  └─ [设计: data_models]
│            │
├── Task 03 ─┼─ MatchDetector, ValidMoveChecker, ScoreCalculator
│            │  └─ [设计: match_system]
│            │
├── Task 04 ─┼─ GravitySystem, SpawnSystem
│            │  └─ [设计: gravity_cascade]
│            │
└── Task 05 ─┼─ crystal.gdshader × 3 (完全独立)
               └─ [设计: crystal_shader]
                    │
Phase 2 ─── 依赖 Phase 1 ──────────────────────────
│                   │
├── Task 06 ────────┤─ Tile.tscn, Tile.gd, TileManager
│                   │  └─ [设计: board_system, crystal_shader]
│                   │
├── Task 07 ────────┤─ GameData + EventBus Autoload
│                   │  └─ [设计: data_models, architecture]
│                   │
└── Task 08 ────────┘─ GameStateMachine (14 状态)
                       └─ [设计: state_machine, gravity_cascade]
                            │
Phase 3 ─── 依赖 Phase 2 ──────────────────────────
│                           │
├── Task 09 ────────────────┤─ Board.tscn, Board.gd, InputHandler
│                           │  └─ [设计: board_system, input_animation]
│                           │
├── Task 10 ────────────────┤─ AnimationController, 粒子/震动/浮动文字
│                           │  └─ [设计: input_animation]
│                           │
├── Task 11 ────────────────┤─ HUD.tscn, HUD.gd
│                           │  └─ [设计: ui_hud]
│                           │
└── Task 12 ────────────────┘─ TitleScreen, PauseMenu, GameOverPanel
                               └─ [设计: ui_hud]
                                    │
Phase 4 ─── 最终组装 + 验证 ───────────────────────
│                                   │
├── Task 13 ────────────────────────┤─ main.tscn + export_presets.cfg
│                                   │  └─ [设计: architecture]
│                                   │
└── Task 14 ────────────────────────┘─ 37 个 GUT 测试用例 (单元+集成)
                                       └─ [设计: testing]
```
(注: Task 14 可在 Phase 2 完成后部分开始，与 Phase 3/4 并行)
```

### 任务清单

| # | 文件 | 产出 | 可并行 |
|---|------|------|--------|
| 01 | [01_project_setup.md](task/01_project_setup.md) | project.godot, 目录结构 | ✅ Phase 1 |
| 02 | [02_core_data.md](task/02_core_data.md) | enums, constants, TileData, BoardData | ✅ Phase 1 |
| 03 | [03_match_detector.md](task/03_match_detector.md) | MatchDetector, ValidMoveChecker, ScoreCalculator | ✅ Phase 1 |
| 04 | [04_gravity_spawn.md](task/04_gravity_spawn.md) | GravitySystem, SpawnSystem | ✅ Phase 1 |
| 05 | [05_crystal_shaders.md](task/05_crystal_shaders.md) | 3× .gdshader | ✅ Phase 1 |
| 06 | [06_tile_and_pool.md](task/06_tile_and_pool.md) | Tile.tscn, Tile.gd, TileManager | ✅ Phase 2 |
| 07 | [07_autoloads.md](task/07_autoloads.md) | GameData.gd, EventBus.gd | ✅ Phase 2 |
| 08 | [08_state_machine.md](task/08_state_machine.md) | GameStateMachine.gd | ✅ Phase 2 |
| 09 | [09_board_and_input.md](task/09_board_and_input.md) | board.tscn, board.gd, input_handler.gd, math_utils.gd | ✅ Phase 3 |
| 10 | [10_animation_effects.md](task/10_animation_effects.md) | AnimationController, 粒子, 震动, 浮动文字 | ✅ Phase 3 |
| 11 | [11_ui_hud.md](task/11_ui_hud.md) | hud.tscn, hud.gd | ✅ Phase 3 |
| 12 | [12_ui_menus.md](task/12_ui_menus.md) | TitleScreen, PauseMenu, GameOverPanel | ✅ Phase 3 |
| 13 | [13_main_scene_export.md](task/13_main_scene_export.md) | main.tscn, export_presets.cfg | 最终 |
| 14 | [14_testing.md](task/14_testing.md) | 37 个 GUT 测试用例 (单元+集成) | Phase 2 后可开始 |

---

## 快速导航

### 按关注点

| 关注点 | Research | Design | Task |
|--------|----------|--------|------|
| 游戏规则 | game_rules.md | data_models.md, ui_hud.md | 02 |
| 棋盘和坐标 | board_design.md | board_system.md | 06, 09 |
| 匹配检测 | match_algorithm.md | match_system.md | 03 |
| 下落和引力 | gravity.md | gravity_cascade.md | 04 |
| 级联和连锁 | cascade.md | gravity_cascade.md | 08 |
| 状态机 | state_machine.md | state_machine.md | 08 |
| 视觉效果 | - | crystal_shader.md | 05, 10 |
| 测试与验证 | future_work.md | testing.md | 14 |
| 性能优化 | performance.md | crystal_shader.md, board_system.md | 05, 06 |
| 随机和公平 | random_generation.md | match_system.md, board_system.md | 02, 03 |
| 未来扩展 | future_work.md | - | - |

### 按文件名 (字母序)

| 文件名 | 所在目录 |
|--------|---------|
| architecture.md | design |
| board_design.md | research |
| board_system.md | design |
| cascade.md | research |
| crystal_shader.md | design |
| data_models.md | design |
| future_work.md | research |
| game_rules.md | research |
| gravity.md | research |
| gravity_cascade.md | design |
| input_animation.md | design |
| match_algorithm.md | research |
| match_system.md | design |
| performance.md | research |
| random_generation.md | research |
| state_machine.md | research + design (同名不同文件) |
| ui_hud.md | design |

---

## 术语对照

| 术语 | 英文 | 说明 |
|------|------|------|
| 三消 | Match-3 | 交换相邻方块, 3 连消除 |
| 水晶 | Crystal / Gem | 棋盘上的彩色方块 |
| 级联 | Cascade / Chain | 消除→下落→新匹配的连锁反应 |
| 特殊方块 | Special Tile | 4连/5连/L形生成的增强方块 |
| 炸弹 | Bomb | 消除 3×3 范围 |
| 彩虹 | Rainbow | 消除某颜色全部 |
| 十字 | Cross | 消除整行+整列 |
| 死局 | Deadlock / Stalemate | 棋盘无有效交换 |
| 重洗 | Reshuffle | 检测到死局后的自动重排 |
| 对象池 | Object Pool | 复用 Tile 节点, 避免频繁创建/销毁 |
