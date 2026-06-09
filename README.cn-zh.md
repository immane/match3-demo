# 🐱 猫咪三消

> 基于 Godot 4.6 .NET + C# 开发的可爱猫咪主题三消益智游戏，支持宠物收集、抽卡、音效、倒计时和连锁消除

<p align="center">
  <img src="https://img.shields.io/badge/Godot-4.6-blue?logo=godot-engine" alt="Godot 4.6">
  <img src="https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/language-C%23-green?logo=csharp" alt="C#">
  <img src="https://img.shields.io/badge/license-MIT-orange" alt="MIT">
</p>

<p align="center">
  <img src="docs/images/preview-0.10-1.jpg" width="22.5%" alt="标题画面">
  <img src="docs/images/preview-0.10-2.jpg" width="22.5%" alt="游戏界面">
  <img src="docs/images/preview-0.20-2.jpg" width="22.5%" alt="宠物收藏">
  <img src="docs/images/preview-0.20-3.jpg" width="22.5%" alt="抽卡画面">
</p>

---

## ✨ 特性

- **8×8 棋盘**，5 种颜色的可爱猫咪（红、蓝、绿、黄、紫）
- **点击交换** — 点一只猫选中，再点相邻的猫交换位置
- **三消检测** — 支持横向、纵向、L 形、T 形、十字形匹配
- **级联连击** — 消除后下落补新，新匹配触发连锁反应
- **特殊方块** — 炸弹（4连）、彩虹（5连）、十字（L/T形）
- **倒计时** — 30 秒时间限制，归零自动结算
- **可爱音效** — 选中、交换、消除、连击等 16 种独立音效
- **计分系统** — 连击倍率累加，浮动分数弹出
- **全动画** — 交换滑动、消除缩小淡出、掉落弹跳、生成坠入、屏幕震动
- **动态棋盘缩放** — 自动适配任意窗口大小
- **暂停/继续** — 带暂停菜单
- **游戏结束面板** — 显示分数、支持重来
- **对象池管理** — 高性能复用 Tile 节点
- **宠物收集系统** — 收集可爱手绘宠物（猫、狗、兔子、小鸭），全屏宠物房间查看
- **宠物养成** — 喂食 & 玩耍，关注饱腹度/快乐度/精力值
- **抽卡系统** — 消耗金币抽取稀有度各异的宠物（普通/稀有/史诗/传说）
- **保底机制** — 70 抽保底稀有+，90 抽保底传说
- **货币经济** — 游戏获得金币，用于抽卡和购买宠物食物

---

## 🎮 玩法

1. 标题页点击 **PLAY** 开始
2. **点一只猫** 选中（会放大 + 发光）
3. **再点相邻的猫**（上下左右）交换
4. 交换后能凑 3 个同色 → 消除得分！
5. 上方掉落补位 → 新猫生成 → 连锁消除！
6. 在 **30 秒倒计时**内争取最高分！
7. 打开 **GACHA** 消耗金币抽取新宠物
8. 进入 **PETS** 查看收藏、喂食互动

---

## 🏗 架构

```
scripts/
├── autoload/          # 全局单例
│   ├── EventBus.cs    # 信号总线（30+ 信号：游戏 + 宠物 + 抽卡 + 货币）
│   ├── GameData.cs    # 分数、步数、计时、货币、默认宠物
│   ├── ServiceInitializer.cs  # DI 服务注册
│   └── AudioManager.cs # 音效对象池（16 种音效）
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
│   ├── BackgroundLayer.cs # 棋盘格背景
│   ├── GameStateMachine.cs # 14 状态状态机
│   ├── Tile.cs            # 猫咪贴图显示
│   ├── TileManager.cs     # 对象池管理
│   ├── AnimationController.cs # Tween 动画
│   └── Main.cs            # 主场景 + 倒计时
├── ui/                # UI 画面
│   ├── TitleScreen.cs
│   ├── HUD.cs             # 分数、步数、连击、计时、金币
│   ├── PauseMenu.cs
│   ├── GameOverPanel.cs
│   └── FloatingTextSpawner.cs
├── pets/              # 宠物系统
│   ├── data/              # PetDefinition, ResourcePetDataSource
│   ├── game/              # PetActor（spritesheet + 动画）, PetLayer
│   ├── models/            # PetInstance, PetNeeds, PetCollection
│   ├── services/          # PetCollectionService, PetCareService
│   └── ui/                # PetShowcase, PetDetailPopup
├── gacha/             # 抽卡系统
│   ├── data/              # GachaBannerResource, GachaBannerDataSource
│   ├── models/            # GachaBanner, GachaRollResult, GachaPityState
│   ├── services/          # GachaDrawService, GachaRollService, GachaPityTracker
│   └── ui/                # GachaBannerUI, RarityRevealEffect
├── currency/          # 货币系统
│   ├── models/            # CurrencyType, CurrencyBalance
│   ├── services/          # CurrencyService
│   └── ui/                # CurrencyDisplay
├── fx/                # 视觉特效
│   ├── ParticleController.cs
│   └── ScreenShake.cs
└── utils/
	├── Enums.cs       # GameState, CrystalType, SpecialType, MatchShape
	├── Constants.cs   # 网格尺寸、动画时长
	├── GridUtils.cs   # 坐标转换 + 动态布局
	├── IDataSource.cs    # 通用数据源接口
	└── IPersistentStorage.cs  # 存档接口
```

---

## 🧪 测试

10 个 xUnit 测试文件（41 项测试）覆盖核心逻辑：

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

- **猫咪 SVG** — 自定义矢量图形，位于 `assets/textures/cats/`
- **宠物 Spritesheet** — 手绘 1024×1024 九宫格表情，位于 `assets/textures/pets/`
- **音效** — 程序化可爱合成音效，位于 `assets/audio/`
- **背景图片** — `assets/images/`

---

## 📄 许可证

MIT — 自由使用、修改、分享！
