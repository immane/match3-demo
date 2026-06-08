# Task 09: Board 场景 + Board 脚本 + InputHandler

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/board_system.md](../design/board_system.md) — 坐标系统、初始化、视觉布局 |
| ↖ 设计 | [design/input_animation.md](../design/input_animation.md) — 输入处理、统一鼠标/触屏 |
| ↖ 设计 | [design/architecture.md](../design/architecture.md) — Board 场景树结构 |

## 状态
- [x] 已完成

## 依赖
- Task 06 (Tile, TileManager)
- Task 08 (GameStateMachine)
- Task 07 (EventBus, GameData)
- Task 01 (project.godot)

## 产出文件
```
assets/scenes/board.tscn             # Board 场景
scripts/game/board.gd                # Board 根节点脚本
scripts/game/input_handler.gd        # InputHandler
scripts/utils/math_utils.gd          # GridUtils 坐标转换工具
```

## 实现要求

### board.tscn 场景结构

参考 `docs/design/architecture.md` §2:

```
Board (Node2D) [script=board.gd]
├── BackgroundLayer (Node2D)
│   ├── BackgroundSprite (Sprite2D)
│   │   └── Material: ShaderMaterial (background.gdshader)
│   └── GridLines (Node2D)             # _draw() 绘制棋盘格
├── TileLayer (Node2D)                 # TileManager 将作为子节点添加
├── EffectLayer (Node2D)               # 粒子/特效层
├── InputHandler (Node) [script=input_handler.gd]
├── GameStateMachine (Node) [script=state_machine.gd]
├── ScreenShake (Node) [script=screen_shake.gd]
└── ParticleController (Node2D) [script=particle_controller.gd]

添加到 "state_machine" group
```

### math_utils.gd — GridUtils

参考 `docs/design/board_system.md` §1.2:

```gdscript
class_name GridUtils
extends RefCounted

# 从 constants.gd 读取
const CELL_SIZE = 72
const CELL_SPACING = 4
const CELL_STEP = 76

static func grid_to_world(row: int, col: int) -> Vector2
    # BOARD_OFFSET + Vector2(col*CELL_STEP + CELL_SIZE/2, row*CELL_STEP + CELL_SIZE/2)

static func world_to_grid(world_pos: Vector2) -> Vector2i
    # 反向计算, 边界校验: 返回 Vector2i(-1,-1) 表示无效

static func to_index(row: int, col: int, cols: int = 8) -> int
    # row * cols + col

static func to_row_col(index: int, cols: int = 8) -> Vector2i
    # Vector2i(index / cols, index % cols)
```

### input_handler.gd

参考 `docs/design/input_animation.md` §1:

```gdscript
class_name InputHandler
extends Node

signal tile_clicked(tile: Tile)
signal empty_space_clicked

var board_data: BoardData
var tile_manager: TileManager

func _input(event: InputEvent)                    # 检查 state_machine.is_input_allowed()
func _process_click(screen_pos: Vector2)          # world_to_grid → 查找 tile → emit tile_clicked
```

处理鼠标 (`InputEventMouseButton`) 和触屏 (`InputEventScreenTouch`)。

### board.gd — Board 根节点

参考 `docs/design/state_machine.md` 和 `docs/design/board_system.md`:

这是**集成脚本**, 负责协调所有子系统:

```gdscript
class_name Board
extends Node2D

@onready var tile_layer: Node2D = $TileLayer
@onready var input_handler: InputHandler = $InputHandler
@onready var state_machine: GameStateMachine = $GameStateMachine
@onready var screen_shake: ScreenShake = $ScreenShake
@onready var particle_ctrl: ParticleController = $ParticleController

var board_data: BoardData
var tile_manager: TileManager

func _ready()
    # 1. 初始化 BoardData
    # 2. 初始化 TileManager, 添加为 tile_layer 的子节点
    # 3. 连接信号: InputHandler.tile_clicked → state_machine.on_tile_clicked
    # 4. 连接 EventBus 信号
    # 5. 生成初始棋盘: board_data.generate_initial_board()
    # 6. tile_manager.refresh_from_data()
    # 7. state_machine.current_state = GameState.IDLE
    # 8. emit board_initialized

# 辅助方法
func get_world_position(row: int, col: int) -> Vector2
    # 委托给 GridUtils
```

在 `_ready()` 中还需要在 `_draw()` 绘制背景棋盘格:
```gdscript
func _draw():
    for row in GRID_ROWS:
        for col in GRID_COLS:
            var pos = GridUtils.grid_to_world(row, col) - Vector2(CELL_SIZE/2, CELL_SIZE/2)
            var rect = Rect2(pos, Vector2(CELL_SIZE, CELL_SIZE))
            var color = LIGHT_COLOR if (row+col) % 2 == 0 else DARK_COLOR
            draw_rect(rect, color)
            draw_rect(rect.grow(2), Color.WHITE * 0.03, false, 1.0)
```

棋盘格颜色:
```gdscript
const LIGHT_COLOR = Color("#3a3a5c")
const DARK_COLOR = Color("#2e2e4a")
```

## 验收标准
- board.tscn 场景可在 Godot 编辑器中打开
- GridUtils 坐标转换正确: grid(0,0) ↔ world(40+36, 120+36), (7,7) ↔ (40+7*76+36, 120+7*76+36)
- world_to_grid 边界外点击返回 -1
- InputHandler 正确发射 tile_clicked 或 empty_space_clicked
- Board._ready() 生成 8×8 无初始匹配的棋盘
- 棋盘格背景在 _draw() 中正确绘制
- state_machine 正确连接 tile_clicked 信号

## 注意
- 此任务不包含 AnimationController (在 Task 10), 暂时使用 timer 替代
- ScreenShake 和 ParticleController 可以暂时用空脚本占位 (Task 10 实现)
- Board 脚本需要在其 `_draw()` 中调用 `queue_redraw()` 在初始化后
