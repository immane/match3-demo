# Task 14: 测试实现

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/testing.md](../design/testing.md) — 完整测试策略、用例清单、文件结构 |

## 状态
- [ ] 待执行

## 依赖
- Task 02 (BoardData — 测试需要构造数据)
- Task 03 (MatchDetector, ValidMoveChecker, ScoreCalculator — 被测对象)
- Task 04 (GravitySystem, SpawnSystem — 被测对象)
- Task 08 (GameStateMachine — 集成测试需要)
- GUT 测试框架 (通过 Godot Asset Library 安装)

## 前置准备

### 安装 GUT

1. 打开 Godot 编辑器
2. AssetLib 标签 → 搜索 "GUT" → 下载安装
3. 或手动: `git clone` 到 `addons/gut/`
4. 启用: 项目设置 → Plugins → GUT → Enable

### 创建测试配置

创建 `tests/gut_config.json`:

```json
{
    "dirs": ["res://tests/unit/", "res://tests/integration/"],
    "include_subdirs": true,
    "ignore_pause": true,
    "log_level": 1,
    "disable_colors": false,
    "pre_run_script": "",
    "post_run_script": ""
}
```

## 产出文件

```
tests/
├── gut_config.json
├── unit/
│   ├── test_board_data.gd              # BoardData 测试 (6 cases)
│   ├── test_match_detector.gd          # MatchDetector 测试 (8 cases)
│   ├── test_special_tiles.gd           # 特殊水晶生成测试 (4 cases)
│   ├── test_gravity_system.gd          # GravitySystem 测试 (4 cases)
│   ├── test_valid_move_checker.gd      # ValidMoveChecker 测试 (3 cases)
│   ├── test_score_calculator.gd        # ScoreCalculator 测试 (4 cases)
│   └── test_board_generation.gd        # 初始化正确性测试 (2 cases)
└── integration/
    ├── test_swap_clear_cascade.gd      # 交换→消除→级联流程 (3 cases)
    ├── test_reshuffle.gd               # 死局→重洗流程 (1 case)
    └── test_edge_cases.gd              # 边界条件 (2 cases)
```

## 实现要求

### 通用要求

每个测试文件:
```gdscript
extends GutTest

# 每个用例: func test_<snake_case_name>():
# 每个用例开头注释说明测试目的
# 使用 assert_eq / assert_true / assert_false / assert_gt / assert_lt
# 测试数据手动构造 BoardData, 不使用 generate_initial_board (除非测试该方法本身)

func before_each():
    # 每个用例前重置状态 (如需要)

func after_each():
    # 清理 (如需要)
```

### 文件 1: test_board_data.gd

6 个用例 (参考 `design/testing.md` §4.1):

```
test_index_conversion:
  构造 BoardData(8,8)
  断言 to_index(0,0) == 0
  断言 to_index(7,7) == 63
  断言 to_index(3,5) == 3*8+5 = 29
  断言 to_row_col(29) == Vector2i(3,5)

test_swap:
  构造 BoardData, 在 (0,0) 放 RED, (0,1) 放 BLUE
  swap(0,0, 0,1)
  断言 (0,0).crystal_type == BLUE
  断言 (0,1).crystal_type == RED
  断言 (0,0).row == 0, (0,0).col == 0 (行列更新)

test_duplicate_restore:
  构造 BoardData, 填充随机数据
  data = duplicate_data()
  修改一些 tile
  restore_from_data(data)
  断言恢复到原始状态

test_is_in_bounds:
  断言 (0,0) true, (7,7) true
  断言 (-1,0) false, (0,-1) false
  断言 (8,0) false, (0,8) false

test_count_type:
  在指定位置放 5 个 RED, 3 个 BLUE
  断言 count_type(RED) == 5
  断言 count_type(BLUE) == 3
  断言 count_type(GREEN) == 0

test_generate_initial_board:
  循环 10 次:
    board.generate_initial_board()
    断言 get_empty_count() == 0
    断言 detect_all 无匹配
    断言 has_any_valid_move 为 true
```

### 文件 2: test_match_detector.gd

8 个用例 (参考 `design/testing.md` §4.2):

```gdscript
# test_horizontal_3
# test_horizontal_4
# test_horizontal_5
# test_vertical_3
# test_multiple_matches
# test_l_shape
# test_t_shape
# test_no_match
```

每个用例需要手动构造 BoardData。构造方法: `board.get_tile(row, col).set_crystal(type)`。

### 文件 3: test_special_tiles.gd

4 个用例 (参考 `design/testing.md` §4.3):

```gdscript
# test_special_4_bomb: 4连→BOMB
# test_special_5_rainbow: 5连→RAINBOW
# test_special_l_cross: L形→CROSS
# test_special_t_cross: T形→CROSS
```

### 文件 4: test_gravity_system.gd

4 个用例 (参考 `design/testing.md` §4.4):

```gdscript
# test_single_fall: 一列, 顶部 1 个, 其余空
  设置 (0,0)=RED, 其余 empty
  falls = GravitySystem.apply_gravity(board)
  断言 falls.size() == 1
  断言 falls[0].from_row == 0
  断言 falls[0].to_row == 7
  断言 falls[0].distance == 7
  断言 board.get_tile(7, 0).crystal_type == RED

# test_multiple_falls: 一列, 多个方块交错
# test_no_falls: 满列, 无空格
# test_empty_column: 整列空
```

### 文件 5: test_valid_move_checker.gd

3 个用例 (参考 `design/testing.md` §4.5):

```gdscript
# test_has_valid_move: 构造有 exchange 能产生匹配的棋盘
  例如: [R,R,B,_,...] (交换 B 和第一个 R 旁边的位置产生 3 连)
  断言 has_any_valid_move == true

# test_no_valid_move: 构造死局
  例如: 所有相邻交换都不产生匹配的布局
  断言 has_any_valid_move == false

# test_swap_produces_match:
  测试 _would_match 对特定交换的预测
```

### 文件 6: test_score_calculator.gd

4 个用例 (参考 `design/testing.md` §4.6):

```gdscript
# test_score_3: 手动构造 3 连 MatchGroup → score == 30
# test_score_4: 4 连 → score == 60
# test_score_l_shape: L 形 → score == 180
# test_combo_3x: apply_combo(100, 3) == 300
```

### 文件 7: test_board_generation.gd

2 个用例 (参考 `design/testing.md` §4.7):

```gdscript
# test_generate_no_initial_matches: 循环 100 次, 验证无初始匹配
# test_generate_has_valid_move: 循环 100 次, 验证有可用移动
```

### 文件 8-10: 集成测试

**test_swap_clear_cascade.gd** (3 cases):

```gdscript
# test_full_swap_clear_fall_cycle:
  构造棋盘 + 状态机, 模拟一次有效交换
  验证: swap完成 → 匹配检测 → 消除 → 下落 → 填充 → 回到 IDLE
  验证分数增加

# test_cascade_triggers:
  构造下落后必然产生新匹配的布局
  例如: 消除一行后, 上方方块下落恰好形成新 3 连
  验证 cascade_depth >= 2

# test_swap_back_on_invalid:
  构造有效棋盘, 交换两个不相邻/不产生匹配的方块
  验证: 数据恢复到交换前, 状态回到 IDLE
```

**test_reshuffle.gd** (1 case):

```gdscript
# test_reshuffle_on_deadlock:
  构造死局, 强制进入 CHECK_VALID → RESHUFFLING
  验证 reshuffle 后有可用移动
```

**test_edge_cases.gd** (2 cases):

```gdscript
# test_max_cascade_loop_protection:
  构造可能产生多个级联的场景
  验证不会超过 MAX_CASCADE_LOOPS

# test_empty_board_graceful:
  空 BoardData, 执行 detect_all / apply_gravity / fill_empty
  验证返回空结果, 不崩溃
```

## 验收标准

- 所有 37 个测试用例在 GUT 中全部通过 (绿色)
- 运行 `godot --headless -s addons/gut/gut_cmdln.gd -gdir=res://tests -gexit` 返回 0
- 无 panic/crash, 所有边界条件有保护
- 每个测试文件顶部有文件级注释说明测试范围

## 注意

- GUT 测试文件**不需要**添加到 project.godot 的 Autoload 或场景中
- 测试文件只在 GUT runner 中执行
- 使用 `extends GutTest` (GUT v9 语法)
- 不要使用 `await` 测试异步代码 (集成测试中使用 `GutTest.wait_seconds` 或 `yield`)
- 确保 `res://tests/` 目录在 `.gitignore` 中不排除 (或用例外规则)
