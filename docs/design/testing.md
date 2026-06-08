# 测试策略设计

> 定义测试金字塔、可测试性架构、单元测试场景和集成测试方案。
> 基于模型-视图分离架构，核心算法为纯逻辑类（RefCounted），无需 Godot 场景即可测试。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/09_future_work.md](../research/09_future_work.md) §14.5 — CI/CD 与测试策略 |
| ← 研究 | [research/03_match_algorithm.md](../research/03_match_algorithm.md) §12.3 — 数据与表现分离原则 |
| ↔ 同级 | [architecture.md](architecture.md) §5 — 模型-视图分离, 可单独测试 |
| ↔ 同级 | [data_models.md](data_models.md) — 被测数据结构 |
| ↔ 同级 | [match_system.md](match_system.md) — 被测匹配算法 |
| ↔ 同级 | [gravity_cascade.md](gravity_cascade.md) — 被测重力和生成系统 |
| ↔ 同级 | [state_machine.md](state_machine.md) — 被测状态机逻辑 |
| → 任务 | [Task 14](../task/14_testing.md) — 测试实现 |

---

## 目录

1. [测试金字塔](#1-测试金字塔)
2. [可测试性架构](#2-可测试性架构)
3. [测试框架](#3-测试框架)
4. [单元测试方案](#4-单元测试方案)
5. [集成测试方案](#5-集成测试方案)
6. [测试文件结构](#6-测试文件结构)
7. [验收标准](#7-验收标准)

---

## 1. 测试金字塔

```
          ╱  E2E  ╲              手动 / 少
         ╱  1-2 个  ╲            (整体游戏流程)
        ╱────────────╲
       ╱   集成测试    ╲          半自动 / 中
      ╱    5-8 个      ╲        (棋盘状态机完整流程)
     ╱──────────────────╲
    ╱     单元测试        ╲       全自动 / 多
   ╱      20+ 个          ╲     (纯算法类, 单个函数)
  ╱──────────────────────────╲
```

| 层级 | 数量 | 目标 | 运行时间 | 覆盖对象 |
|------|------|------|---------|---------|
| 单元 | 20+ | 纯函数正确性 | < 1s | MatchDetector, GravitySystem, SpawnSystem, ScoreCalculator, ValidMoveChecker, BoardData |
| 集成 | 5-8 | 多模块协作 | < 3s | 状态机流程, 完整一次交换→消除→下落→填充→级联 |
| E2E | 1-2 | 手动验证 | 手动 | 游戏可玩性, 视觉效果, WebGL 导出 |

---

## 2. 可测试性架构

### 2.1 为什么核心算法天然可测

由于采用了**模型-视图分离**（见 `architecture.md` §5），所有核心逻辑类：

| 类 | 继承 | 依赖 | 可单独实例化 |
|-----|------|------|------------|
| `BoardData` | RefCounted | 无 | ✅ |
| `MatchDetector` | RefCounted | `BoardData` (参数传入) | ✅ |
| `GravitySystem` | RefCounted | `BoardData` (参数传入) | ✅ |
| `SpawnSystem` | RefCounted | `BoardData` (参数传入) | ✅ |
| `ValidMoveChecker` | RefCounted | `BoardData` (参数传入) | ✅ |
| `ScoreCalculator` | RefCounted | `MatchGroup`, `MatchResult` (参数传入) | ✅ |

这些类**不依赖任何 Godot Node、场景树、Tween、纹理**。只需创建 BoardData 实例并填充数据即可测试。

### 2.2 可测试矩阵

```
测试输入: BoardData (手动构造) → 算法类 (static func) → 断言输出
                                  ↑
                                  │ 纯函数, 无副作用
                                  │ 无 Node 依赖
                                  │ 无 Tween/信号依赖
                                  ↓
                              可 100% 自动化
```

---

## 3. 测试框架

采用 **GUT (Godot Unit Test)** — Godot 生态最成熟的测试框架。

### 3.1 GUT 安装

```
方式: Godot Asset Library → 搜索 "GUT" → 安装
或: 使用 gdUnit4 (更现代的替代, 但 GUT 更稳定)
推荐: GUT v9.x+ (兼容 Godot 4.x)
```

### 3.2 GUT 基本用法

```gdscript
# tests/unit/test_match_detector.gd
extends GutTest

func test_detect_horizontal_3_in_a_row():
    var board := BoardData.new(8, 8)
    # 手动构造: 第 0 行 3 个红色
    board.get_tile(0, 0).set_crystal(CrystalType.RED)
    board.get_tile(0, 1).set_crystal(CrystalType.RED)
    board.get_tile(0, 2).set_crystal(CrystalType.RED)
    
    var result := MatchDetector.detect_horizontal(board)
    assert_eq(result.size(), 1, "应检测到 1 个水平匹配组")
    assert_eq(result[0].match_length, 3, "匹配长度应为 3")

func test_detect_all_no_match():
    var board := BoardData.new(8, 8)
    # 交替填充, 确保无 3 连
    for row in 8:
        for col in 8:
            board.get_tile(row, col).set_crystal((row + col) % 5)
    
    var result := MatchDetector.detect_all(board)
    assert_false(result.has_matches(), "交替棋盘不应有匹配")
```

### 3.3 运行测试

```bash
# 在 Godot 编辑器中:
# 1. 打开 GUT 面板 (底部栏)
# 2. 选择测试目录: res://tests/
# 3. 点击 "Run All"

# 命令行运行 (需要 Godot headless):
godot --headless -s addons/gut/gut_cmdln.gd -gdir=res://tests -gexit
```

---

## 4. 单元测试方案

### 4.1 BoardData 测试 (6 个用例)

| # | 用例名 | 场景 | 断言 |
|---|--------|------|------|
| 1 | `test_index_conversion` | 行列 ↔ 一维索引互转 | `to_index(0,0)=0`, `to_index(7,7)=63`, `to_row_col(63)=(7,7)` |
| 2 | `test_swap` | 交换两个格子 | 交换后类型/行列正确, 其他格子不受影响 |
| 3 | `test_duplicate_restore` | 快照保存和恢复 | 修改→保存→再修改→恢复→等于原始 |
| 4 | `test_is_in_bounds` | 边界检测 | (0,0) true, (-1,0) false, (8,0) false |
| 5 | `test_count_type` | 统计特定颜色数量 | 放置 5 个 RED → count_type(RED)=5 |
| 6 | `test_generate_initial_board` | 初始棋盘生成 | 64 格全非空, 无初始 3 连, 颜色在 0-4 |

### 4.2 MatchDetector 测试 (8 个用例)

| # | 用例名 | 场景 | 断言 |
|---|--------|------|------|
| 1 | `test_horizontal_3` | 第 0 行 [R,R,R,_,_,_,_,_] | detect_horizontal → 1 组, 长度 3, 类型 RED |
| 2 | `test_horizontal_4` | 第 3 行 [_,_,B,B,B,B,_,_] | detect_horizontal → 1 组, 长度 4 |
| 3 | `test_horizontal_5` | 第 5 行 [G,G,G,G,G,_,_,_] | detect_horizontal → 1 组, 长度 5 |
| 4 | `test_vertical_3` | 第 2 列 [Y,Y,Y,_,_,_,_,_] | detect_vertical → 1 组, 长度 3 |
| 5 | `test_multiple_matches` | 水平+垂直同时存在 | detect_all → 2 group, total_matched 正确 |
| 6 | `test_l_shape` | 构造 L 形匹配 | detect_all → group.shape=L_SHAPE, special=BOMB |
| 7 | `test_t_shape` | 构造 T 形匹配 | detect_all → group.shape=T_SHAPE |
| 8 | `test_no_match` | 交替棋盘 | detect_all → has_matches()=false |

### 4.3 MatchDetector — 特殊水晶生成测试 (4 个用例)

| # | 用例名 | 场景 | 生成 |
|---|--------|------|------|
| 1 | `test_special_4_bomb` | 4 连直线 → BOMB 在匹配中点 | `special_type = BOMB` |
| 2 | `test_special_5_rainbow` | 5 连直线 → RAINBOW 在匹配中点 | `special_type = RAINBOW` |
| 3 | `test_special_l_cross` | L 形 5 个 → CROSS 在交叉点 | `special_type = CROSS` |
| 4 | `test_special_t_cross` | T 形 5 个 → CROSS 在交叉点 | `special_type = CROSS` |

### 4.4 GravitySystem 测试 (4 个用例)

| # | 用例名 | 场景 | 断言 |
|---|--------|------|------|
| 1 | `test_single_fall` | 列 [R,_,_,_,_,_,_,_] (顶部一个, 其余空) | R 落到 row=7, FallInfo.distance=7 |
| 2 | `test_multiple_falls` | 列 [_,R,_,G,_,_,_,_] | R→row=7, G→row=6, fall 数量 2 |
| 3 | `test_no_falls` | 满列, 无空格 | falls 为空 |
| 4 | `test_empty_column` | 整列空 | falls 为空, 全部 is_empty |

### 4.5 ValidMoveChecker 测试 (3 个用例)

| # | 用例名 | 场景 | 断言 |
|---|--------|------|------|
| 1 | `test_has_valid_move` | 构造有可用交换的棋盘 | `has_any_valid_move() = true` |
| 2 | `test_no_valid_move` | 构造死局 | `has_any_valid_move() = false` |
| 3 | `test_swap_produces_match` | 测试单次交换是否产生匹配 | `_would_match() = true/false` |

### 4.6 ScoreCalculator 测试 (4 个用例)

| # | 用例名 | 场景 | 断言 |
|---|--------|------|------|
| 1 | `test_score_3` | 3 连直线 | `calculate_group_score() = SCORE_3 (30)` |
| 2 | `test_score_4` | 4 连直线 | `calculate_group_score() = SCORE_4 (60)` |
| 3 | `test_score_l_shape` | L 形 | `calculate_group_score() = SCORE_CROSS (180)` |
| 4 | `test_combo_3x` | 连击 3 级 | `apply_combo(100, 3) = 300` |

### 4.7 BoardData 初始化正确性测试 (2 个用例)

| # | 用例名 | 场景 | 断言 |
|---|--------|------|------|
| 1 | `test_generate_no_initial_matches` | 生成 100 次 | 每次 detect_all → has_matches=false |
| 2 | `test_generate_has_valid_move` | 生成 100 次 | 每次 has_any_valid_move=true |

---

## 5. 集成测试方案

### 5.1 完整交换→消除→级联流程 (3 个用例)

| # | 用例名 | 场景 | 验证点 |
|---|--------|------|--------|
| 1 | `test_full_swap_clear_fall_cycle` | 构造交换可产生匹配的棋盘, 执行完整流程 | 交换→match→clear→fall→spawn→idle, 分数增加 |
| 2 | `test_cascade_triggers` | 构造下落后必然产生新匹配的布局 | cascade_depth ≥ 2, combo 信号触发 |
| 3 | `test_swap_back_on_invalid` | 交换不产生匹配的两个方块 | 数据层恢复, 状态回到 IDLE |

### 5.2 死局检测→重洗流程 (1 个用例)

| # | 用例名 | 场景 | 验证点 |
|---|--------|------|------|
| 1 | `test_reshuffle_on_deadlock` | 构造死局, 触发 reshuffle | 重洗后有可用移动 |

### 5.3 边界条件测试 (2 个用例)

| # | 用例名 | 场景 | 验证点 |
|---|--------|------|------|
| 1 | `test_max_cascade_loop_protection` | 构造无限级联 (理论场景) | MAX_CASCADE_LOOPS 限制生效, 不无限循环 |
| 2 | `test_empty_board_graceful` | 空棋盘执行各种操作 | 不崩溃, 返回空结果 |

---

## 6. 测试文件结构

```
match3-demo/
├── tests/
│   ├── unit/
│   │   ├── test_board_data.gd              # BoardData 测试 (6 cases)
│   │   ├── test_match_detector.gd          # MatchDetector 测试 (8 cases)
│   │   ├── test_special_tiles.gd           # 特殊水晶生成测试 (4 cases)
│   │   ├── test_gravity_system.gd          # GravitySystem 测试 (4 cases)
│   │   ├── test_valid_move_checker.gd      # ValidMoveChecker 测试 (3 cases)
│   │   ├── test_score_calculator.gd        # ScoreCalculator 测试 (4 cases)
│   │   └── test_board_generation.gd        # 初始化正确性测试 (2 cases)
│   ├── integration/
│   │   ├── test_swap_clear_cascade.gd      # 交换→消除→级联流程 (3 cases)
│   │   ├── test_reshuffle.gd               # 死局→重洗流程 (1 case)
│   │   └── test_edge_cases.gd              # 边界条件 (2 cases)
│   └── gut_config.json                     # GUT 配置文件
```

---

## 7. 验收标准

### 7.1 覆盖率目标

| 模块 | 行覆盖率 | 分支覆盖率 |
|------|---------|-----------|
| BoardData | > 90% | > 80% |
| MatchDetector | > 90% | > 80% |
| GravitySystem | > 90% | > 80% |
| SpawnSystem | > 80% | > 70% |
| ValidMoveChecker | > 90% | > 80% |
| ScoreCalculator | 100% | 100% |

### 7.2 CI 集成 (未来)

```yaml
# .github/workflows/test.yml (未来添加)
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: gamedev-js/godot-ci@v1
        with:
          godot-version: "4.3"
      - run: godot --headless -s addons/gut/gut_cmdln.gd -gdir=res://tests -gexit
```

### 7.3 运行命令

```bash
# 本地快速运行所有单元测试
godot --headless -s addons/gut/gut_cmdln.gd -gdir=res://tests/unit -gexit

# 运行集成测试
godot --headless -s addons/gut/gut_cmdln.gd -gdir=res://tests/integration -gexit

# 运行全部
godot --headless -s addons/gut/gut_cmdln.gd -gdir=res://tests -gexit
```
