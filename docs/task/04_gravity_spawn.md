# Task 04: 重力系统 + 生成系统

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/gravity_cascade.md](../design/gravity_cascade.md) — GravitySystem、SpawnSystem、FallInfo、SpawnInfo |

## 状态
- [x] 已完成

## 依赖
- Task 02 (数据结构: BoardData, TileData)
  - 新建文件: `scripts/core/gravity_system.gd`
  - 新建文件: `scripts/core/spawn_system.gd`

## 产出文件
```
scripts/core/gravity_system.gd      # 新建
scripts/core/spawn_system.gd        # 新建
```

## 实现要求

### gravity_system.gd

参考 `docs/design/gravity_cascade.md` §1:

```
GravitySystem (RefCounted):
├── apply_gravity(board: BoardData) -> Array[FallInfo]
└── _process_column(board: BoardData, col: int) -> Array[FallInfo]

FallInfo (内部类/同级类, RefCounted):
├── from_row: int
├── to_row: int
├── col: int
├── distance: int (getter)
├── tile_data: TileData
└── get_duration() -> float
```

算法核心:
1. 遍历每列
2. 对每列: 从底部 (row=7) 向上扫描
3. 使用 `write_row` 指针 (初始 = rows-1)
4. 遇到非空格子: 如果 read_row != write_row, 记录 FallInfo, swap(read ↔ write), write_row--
5. 扫描完毕后, write_row 之上的所有格子 set `clear()`
6. 返回所有 FallInfo

`get_duration()`: `FALL_DURATION_BASE + distance * FALL_DURATION_PER_ROW`

### spawn_system.gd

参考 `docs/design/gravity_cascade.md` §2:

```
SpawnSystem (RefCounted):
└── fill_empty(board: BoardData) -> Array[SpawnInfo]

SpawnInfo (内部类/同级类, RefCounted):
├── row: int
├── col: int
├── crystal_type: int
└── get_enter_offset() -> Vector2
```

算法:
1. 遍历每列
2. 找到所有空格 (is_empty == true)
3. 对每个空格: 随机生成 crystal_type (0..NUM_CRYSTAL_TYPES-1), 调用 `tile.set_crystal(type)`
4. 返回所有 SpawnInfo

随机使用 `RandomNumberGenerator.new().randi_range(0, num_types-1)`

## 验收标准
- GravitySystem 正确处理单列下落和多列同时下落
- 下落后的空位全部在顶部 (row 小的位置)
- FallInfo 的 distance 和 duration 计算正确
- SpawnSystem 正确填充所有空格
- 随机水晶颜色在有效范围内

## 注意
- 纯逻辑类, 不依赖 Godot Node
- 使用 `const` 引用 constants.gd 中的动画时长
- 不使用 Tween (动画由 AnimationController 处理)
