# 🐱 猫咪三消

> 基于 Godot 4.6 .NET + C# 开发的可爱猫咪主题三消益智游戏

<p align="center">
  <img src="https://img.shields.io/badge/Godot-4.6-blue?logo=godot-engine" alt="Godot 4.6">
  <img src="https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/language-C%23-green?logo=csharp" alt="C#">
  <img src="https://img.shields.io/badge/license-MIT-orange" alt="MIT">
</p>

---

## ✨ 特性

- **8×8 棋盘**，5 种颜色的可爱猫咪（红、蓝、绿、黄、紫）
- **点击交换** — 点一只猫选中，再点相邻的猫交换位置
- **三消检测** — 支持横向、纵向、L 形、T 形、十字形匹配
- **级联连击** — 消除后下落补新，新匹配触发连锁反应
- **特殊方块** — 炸弹（4连）、彩虹（5连）、十字（L/T形）
- **计分系统** — 连击倍率累加
- **全动画** — 交换滑动、消除缩小淡出、掉落弹跳、生成坠入
- **动态棋盘缩放** — 自动适配任意窗口大小
- **暂停/继续** — 带暂停菜单
- **游戏结束面板** — 显示分数、支持重来
- **对象池管理** — 高性能复用 Tile 节点

---

## 🎮 玩法

1. 标题页点击 **PLAY** 开始
2. **点一只猫** 选中（会放大 + 发光）
3. **再点相邻的猫**（上下左右）交换
4. 交换后能凑 3 个同色 → 消除得分！
5. 上方掉落补位 → 新猫生成 → 连锁消除！
6. 总共 **30 步**，争取最高分！

---

## 🏗 架构

```
scripts/
├── autoload/          # 全局单例
│   ├── EventBus.cs    # 信号总线（19 个信号）
│   └── GameData.cs    # 分数、步数、设置
├── core/              # 纯逻辑（无引擎依赖）
│   ├── BoardData.cs   # 8×8 棋盘 + CellData
│   ├── MatchDetector.cs   # 横/纵/洪水填充检测
│   ├── MatchResult.cs     # 匹配组 + 特殊生成
│   ├── GravitySystem.cs   # 按列重力下落
│   ├── SpawnSystem.cs     # 随机填充
│   ├── ScoreCalculator.cs # 分数 + 连击计算
│   └── ValidMoveChecker.cs # 死局检测
├── game/              # 场景节点
│   ├── Board.cs           # 网格绘制 + 输入处理
│   ├── GameStateMachine.cs # 14 状态状态机
│   ├── Tile.cs            # 猫咪贴图显示
│   ├── TileManager.cs     # 对象池管理
│   ├── AnimationController.cs # Tween 动画
│   ├── Main.cs            # 主场景控制器
│   └── InputHandler.cs    # （已废弃，Board 直接处理输入）
├── ui/                # UI 画面
│   ├── TitleScreen.cs
│   ├── HUD.cs             # 分数、步数、连击
│   ├── PauseMenu.cs
│   ├── GameOverPanel.cs
│   └── FloatingTextSpawner.cs
├── fx/                # 视觉特效
│   ├── ParticleController.cs
│   └── ScreenShake.cs
└── utils/
    ├── Enums.cs       # GameState, SpecialType, MatchShape...
    ├── Constants.cs   # 网格尺寸、动画时长
    └── GridUtils.cs   # 坐标转换 + 动态布局
```

---

## 🧪 测试

10 个 xUnit 测试文件覆盖核心逻辑：

| 文件 | 测试内容 |
|------|---------|
| `Tests/BoardDataTests.cs` | 索引换算、交换、边界、清空 |
| `Tests/BoardGenerationTests.cs` | 无初始匹配生成、全填充 |
| `Tests/MatchDetectorTests.cs` | 横/纵/L形/无匹配 |
| `Tests/GravitySystemTests.cs` | 单个/多个/无下落、空列 |
| `Tests/ScoreCalculatorTests.cs` | 行分数、连击倍率 |
| `Tests/SpecialTilesTests.cs` | 炸弹、彩虹、十字生成 |
| `Tests/ValidMoveCheckerTests.cs` | 有效交换检测 |
| `Tests/EdgeCasesTests.cs` | 级联保护、空棋盘 |
| `Tests/ReshuffleTests.cs` | 重洗逻辑 |
| `Tests/SwapClearCascadeTests.cs` | 完整交换→消除→下落→生成周期 |

```bash
dotnet build   # 编译（0 错误 0 警告）
dotnet test    # 运行测试（需要 .NET 8 运行时）
```

---

## 🚀 快速开始

### 环境要求
- **Godot 4.6 .NET 版**（[下载](https://godotengine.org/download/)）
- **.NET 8.0 SDK**（Godot .NET 自带，或单独安装）

### 打开项目
1. 克隆本仓库
2. 打开 **Godot .NET** 编辑器
3. 点击 **导入** → 选择 `project.godot` 文件
4. 等待 C# 编译（首次约 30 秒）
5. 按 **F5** 运行

### 运行测试
```bash
dotnet build
dotnet test
```

---

## 🎨 素材说明

猫咪 SVG 为自定义矢量图形，位于 `assets/textures/cats/` 目录。

---

## 📄 许可证

MIT — 自由使用、修改、分享！
