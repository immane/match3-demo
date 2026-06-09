# Task 03: 匹配检测算法 + 死局检测 + 分数计算

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/match_system.md](../design/match_system.md) — 两阶段匹配检测、形状分类、死局检测 |
| ↖ 设计 | [design/ui_hud.md](../design/ui_hud.md) — ScoreCalculator 分数公式 |

## 状态
- [x] 已完成

## 依赖
- Task 02 (数据结构: BoardData, TileData, MatchResult 等)
  - 修改文件: `scripts/core/match_detector.gd` (添加算法方法)
  - 新建文件: `scripts/core/valid_move_checker.gd`
  - 新建文件: `scripts/core/score_calculator.gd`

## 产出文件
```
scripts/core/match_detector.gd          # 在已有数据结构上添加算法方法
scripts/core/valid_move_checker.gd      # 新建
scripts/core/score_calculator.gd        # 新建
```

## 实现要求

### match_detector.gd — 添加算法方法

**注意**: Task 02 已经创建了此文件并定义了 `MatchGroup`, `MatchResult`, `SpecialSpawn` 类。Task 03 需要:
1. 在文件中添加 `class_name MatchDetector extends RefCounted`
2. 实现所有 `static func` 方法

参考 `docs/design/match_system.md`:

```
MatchDetector 类方法:
├── detect_all(board: BoardData) -> MatchResult         (主入口)
├── detect_horizontal(board: BoardData) -> Array[MatchGroup]
├── detect_vertical(board: BoardData) -> Array[MatchGroup]
├── _mark_positions(flags, groups, cols)
├── _flood_fill(flags, visited, start_idx, board) -> Array[Vector2i]
├── _classify_shape(region, board) -> MatchGroup
├── _determine_special(group: MatchGroup) -> SpecialSpawn
└── _count_ones(flags: PackedByteArray) -> int
```

关键算法细节:
- **detect_horizontal**: 逐行扫描, `while col < cols`, 跳过空格, 统计连续相同颜色, `>= 3` 记录匹配组, `col += run_length`
- **detect_vertical**: 同上但逐列扫描
- **detect_all**: 先水平扫描 → 垂直扫描 → 填充 matched_flags (PackedByteArray) → flood_fill 连通分组 → 形状分类 → 特殊水晶判定
- **_flood_fill**: 迭代式 BFS (使用栈而非递归), 4 方向扩展, 访问过的标记为 0
- **_classify_shape**: 统计 row_set/col_set, 找最大行和列, 检测交叉点, 分类为 H_LINE/V_LINE/L_SHAPE/T_SHAPE/CROSS
- **_determine_special**: 4连→BOMB, 5+连→RAINBOW, L/T/CROSS→CROSS

### valid_move_checker.gd

参考 `docs/design/match_system.md` §6:

```
ValidMoveChecker (RefCounted):
├── has_any_valid_move(board: BoardData) -> bool
├── _would_match(board, r1, c1, r2, c2) -> bool
└── _quick_check(board, row, col) -> bool
```

算法: 遍历每个格子, 尝试与右/下邻居交换, 模拟后快速检查受影响的行列是否有 >=3 连, 换回。找到一个有效交换即返回 true。

### score_calculator.gd

参考 `docs/design/ui_hud.md` §2 和 `docs/design/ui_hud.md`:

```
ScoreCalculator (RefCounted):
├── calculate_group_score(group: MatchGroup) -> int
├── calculate_total(match_result: MatchResult) -> int
└── apply_combo(base_score: int, combo_depth: int) -> int
```

分数常量 (引用 constants.gd):
- SCORE_3=30, SCORE_4=60, SCORE_5=100, SCORE_PER_EXTRA=50
- SCORE_BOMB=150, SCORE_RAINBOW=200, SCORE_CROSS=180

规则:
- 直线匹配: 按长度给分
- L/T 形: SCORE_CROSS
- CROSS 形: SCORE_CROSS + 50
- 连击倍率: `base * combo_depth`

## 验收标准
- MatchDetector 可以接收 BoardData, 返回 MatchResult
- 正确检测 3/4/5 连, L/T/Cross 形状
- 特殊水晶生成位置正确 (4连→中点, L形→交叉点)
- ValidMoveChecker 正确检测死局
- ScoreCalculator 分数计算正确

## 注意
- 这三个类都是**纯逻辑类**, 继承 RefCounted, 不依赖任何 Godot Node
- 所有方法为 static, 无状态
- 使用 `const` 引用 constants.gd 中的值
- `preload("res://scripts/utils/constants.gd")` 或 `const` 引用
