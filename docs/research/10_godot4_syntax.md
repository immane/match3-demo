# Godot 4.x C# (.NET) Syntax & API Reference

> 为 Match-3 Crystal 项目整理的 Godot 4.x C# (.NET 8.0) 核心 API 速查。所有代码示例均使用 Godot 4.4+ C# 语法。

---

## 目录

1. [C# 基础语法与代码顺序](#1-c-基础语法与代码顺序)
2. [静态类型系统](#2-静态类型系统)
3. [信号系统](#3-信号系统)
4. [Async / Await 协程](#4-async--await-协程)
5. [Tween 动画系统](#5-tween-动画系统)
6. [UI 系统](#6-ui-系统)
7. [Resource 自定义资源](#7-resource-自定义资源)
8. [输入处理](#8-输入处理)
9. [场景管理](#9-场景管理)
10. [Autoload 单例](#10-autoload-单例)
11. [导出变量与 [Tool]](#11-导出变量与-tool)
12. [Typed Collections 类型化集合](#12-typed-collections-类型化集合)
13. [Godot 3 → 4 迁移对照表](#13-godot-3--4-迁移对照表)
14. [C# vs GDScript 常见陷阱](#14-c-vs-gdscript-常见陷阱)
15. [C# Build 系统](#15-c-build-系统)
16. [项目专用 C# 速查](#16-项目专用-c-速查)

---

## 1. C# 基础语法与代码顺序

### 代码顺序规范

```csharp
using Godot;                            // using directives (System first, then Godot)
using Godot.Collections;
using System.Collections.Generic;

namespace Match3Demo;                    // 01. file-scoped namespace

[Tool]                                   // 02. attributes ([Tool], [GlobalClass], [Icon])
[GlobalClass]
public partial class MyNode : Node2D     // 03. partial class, extends via inheritance
{
    // ── Signals ──────────────────────── 04. signals ([Signal] delegates)

    [Signal] public delegate void MySignalEventHandler(int value);
    [Signal] public delegate void ReadyEventHandler();

    // ── Constants ────────────────────── 05. constants

    private const int MAX_BOARD_SIZE = 8;
    private const float SWAP_DURATION = 0.2f;

    // ── Fields ───────────────────────── 06. fields (public → private)

    [Export] public int Speed { get; set; } = 100;
    public int Health { get; set; } = 100;
    private int _privateVar;
    private static int _staticCount;

    // ── Properties ───────────────────── 07. properties

    public BoardData BoardData { get; set; }

    private int _currentState;
    public int CurrentState
    {
        get => _currentState;
        set
        {
            if (value == _currentState) return;
            int prev = _currentState;
            ExitState(prev);
            _currentState = value;
            EnterState(value);
            EmitSignal(SignalName.StateChanged, prev, value);
        }
    }

    // ── Virtual Methods ──────────────── 08. Godot lifecycle (public override)

    public override void _Ready()
    {
        // base._Ready() is NOT required in C# (not a virtual call chain)
        // Onready equivalents use GetNode<T>() in _Ready()
        var label = GetNode<Label>("Label");
    }

    public override void _Process(double delta)
    {
        // float deltaF = (float)delta;
    }

    // ── Public Methods ───────────────── 09. public methods

    public void DoSomething() { }

    // ── Private Methods ──────────────── 10. private methods

    private void InternalHelper() { }

    // ── Nested Classes ───────────────── 11. inner classes

    public class TileData
    {
        public int CrystalType;
        public int Row;
        public int Col;

        public TileData(int type, int row, int col)
        {
            CrystalType = type;
            Row = row;
            Col = col;
        }
    }
}
```

### 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 文件名 | PascalCase (必须匹配类名) | `TileManager.cs` |
| 类名 | PascalCase | `partial class Match3Board` |
| 方法 | PascalCase | `void SwapTiles()` |
| 属性 | PascalCase | `BoardData BoardData { get; set; }` |
| 私有字段 | `_` 前缀 camelCase | `private int _boardWidth;` |
| 静态字段 | `s_` 或 `_` 前缀 | `private static int s_count;` |
| 常量 | PascalCase | `private const int MaxBoardSize = 8;` |
| 枚举类型 | PascalCase | `enum GameState { IDLE, SWAPPING }` |
| 枚举值 | PascalCase | `GameState.IDLE` |
| 信号事件 | PascalCase + EventHandler 后缀 | `TileClickedEventHandler` |
| 本地变量 | camelCase | `var targetPos = ...` |
| 参数 | `p` 前缀 camelCase | `int pRow, int pCol` |

### 注释与文档

```csharp
/// <summary>
/// Returns the Tile at the given board position.
/// </summary>
/// <param name="pos">Board coordinate (col, row)</param>
/// <returns>The Tile node at that position, or null if empty</returns>
public Tile GetTileAt(Vector2I pos)
{
    return _tileLookup.TryGetValue(pos, out var tile) ? tile : null;
}
```

### 最佳实践

- 使用 `GetNode<T>("Path")` 在 `_Ready()` 中缓存节点引用
- 所有 `[Signal]` 委托必须以 `EventHandler` 结尾
- 私有字段以 `_` 开头；不要暴露公共字段，使用属性
- 场景脚本使用 `partial class`,纯数据类则不需要
- 文件-scoped namespace (`namespace Match3Demo;` 末尾分号)
- `double delta` 参数在 `_Process` 中是 C# 规范 (Godot 4 的 C# 绑定),使用时转换 `(float)delta`
- C# 中 `_Ready()` 的子节点尚未就绪问题：用 `CallDeferred()` 或 `await ToSignal(GetTree(), "process_frame")` 等待子节点

---

## 2. 静态类型系统

C# 天生强类型，Godot 提供额外的 Godot 特有类型。

### 变量声明

```csharp
// 基础类型
int health = 100;
float speed = 100.0f;              // C# float 字面量需要 f 后缀
string name = "Player";
bool isActive = true;

// Godot 数学类型
Vector2 velocity = Vector2.Zero;
Vector2 direction = new Vector2(1.0f, 0.0f);
Vector2I cellPos = new Vector2I(3, 5);
Color tint = Colors.Red;           // 或 new Color(1, 0, 0)
Transform2D transform = Transform2D.Identity;

// Godot 引用类型
Node node = GetNode<Node>("Child");
NodePath path = "Sprite/AnimationPlayer";
StringName action = "ui_accept";
Rid resourceId;

// 使用 var 推断 (类型明确时可用)
var board = GetNode<Node2D>("Board");       // 类型为 Node2D
var gridPos = GridUtils.WorldToGrid(pos);   // 类型为 Vector2I
var tiles = new List<Tile>();               // 类型为 List<Tile>
```

### 可空类型

```csharp
// C# 引用类型默认可为 null
Tile tile = null;                   // OK - 引用类型

// Godot 可空 (GodotObject 为引用类型，可为 null)
Node node = null;                   // OK
PackedScene scene = null;           // OK

// 值类型需要显式使用 Nullable<T>
int? optionalScore = null;          // OK
bool? flag = null;                  // OK

// Godot 值类型 (Vector2, Color, Vector2I 等是 struct, 不可为 null)
Vector2 pos = Vector2.Zero;         // 不能设为 null

// 使用 TryGetValue 安全模式
if (_tileLookup.TryGetValue(pos, out var tile))
{
    tile.Select();                   // tile 不为 null
}
```

### 类型转换

```csharp
// 安全转换: as 操作符 (返回 null 如果类型不匹配)
var board = node as Board;
if (board != null)
    board.ResetBoard();

// 模式匹配 (C# 7+)
if (node is Board b)
    b.ResetBoard();

// event 模式匹配
if (@event is InputEventMouseButton mouseBtn)
{
    if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
        HandleClick(mouseBtn.Position);
}

// 强制转换 (类型 100% 匹配时)
var tile = (Tile)GetNode("Tile");

// GetNode<T> 泛型版本 (最常用)
var label = GetNode<Label>("ScoreLabel");
var board = GetNode<Board>("Board");
```

### class_name 等价物 — [GlobalClass]

```csharp
// 在 Godot 编辑器中注册为全局类
[GlobalClass]
public partial class Match3Board : Node2D
{
}

// 内部类 (不需要 partial 或 GlobalClass)
public class TileData
{
    public int CrystalType;
    public int Row;
    public int Col;

    public TileData(int type, int row, int col)
    {
        CrystalType = type;
        Row = row;
        Col = col;
    }
}
```

---

## 3. 信号系统

Godot 4 C# 使用 **`[Signal]` + delegate** 模式定义信号，使用 `+=` / `-=` 订阅，使用 `EmitSignal()` 发射。

### 定义信号 (Autoload / EventBus)

```csharp
using Godot;

namespace Match3Demo;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    // 带参数信号 — delegate 必须以 EventHandler 结尾
    [Signal] public delegate void ScoreChangedEventHandler(int newScore, int delta);
    [Signal] public delegate void TileSelectedEventHandler(Node2D tile, Vector2I pos);
    [Signal] public delegate void MatchesFoundEventHandler(Godot.Collections.Array matches);
    [Signal] public delegate void CascadeTriggeredEventHandler(int depth);

    // 无参数信号
    [Signal] public delegate void GameOverEventHandler();
    [Signal] public delegate void LevelCompleteEventHandler();

    public override void _EnterTree()
    {
        Instance = this;
    }
}
```

### 定义信号 (场景节点)

```csharp
// 在场景附加的 C# 脚本中
public partial class GameStateMachine : Node
{
    [Signal] public delegate void StateChangedEventHandler(int previous, int current);
    [Signal] public delegate void TileClickedEventHandler(Node2D tile);

    // 触发
    public void ChangeState(int newState)
    {
        int prev = _currentState;
        _currentState = newState;
        EmitSignal(SignalName.StateChanged, prev, newState);
    }
}
```

### 连接信号 (订阅)

```csharp
// C# += 语法 (推荐)
EventBus.Instance.ScoreChanged += OnScoreChanged;
EventBus.Instance.GameOver += OnGameOver;

// 按钮信号
var button = GetNode<Button>("PlayButton");
button.Pressed += OnPlayPressed;

// 自定义信号
stateMachine.StateChanged += OnStateChanged;

// 延迟连接 — 确保接收方就绪后再连接
CallDeferred("ConnectBusSignals");

private void ConnectBusSignals()
{
    EventBus.Instance.ScoreChanged += OnScoreChanged;
    EventBus.Instance.ComboUpdated += OnComboUpdated;
}
```

### 断开连接

```csharp
// C# -= 语法
EventBus.Instance.ScoreChanged -= OnScoreChanged;
EventBus.Instance.GameOver -= OnGameOver;
button.Pressed -= OnPlayPressed;

// 在 _ExitTree() 中清理 (重要，防止内存泄漏)
public override void _ExitTree()
{
    EventBus.Instance.ScoreChanged -= OnScoreChanged;
    EventBus.Instance.ComboUpdated -= OnComboUpdated;
    EventBus.Instance.MovesChanged -= OnMovesChanged;
}
```

### 发射信号

```csharp
// Autoload 信号
EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, 100, 50);
EventBus.Instance.EmitSignal(EventBus.SignalName.GameOver);

// 本节点信号
EmitSignal(SignalName.StateChanged, prevState, newState);
EmitSignal(SignalName.TileClicked, tile);

// 多参数信号
EventBus.Instance.EmitSignal(EventBus.SignalName.ShowFloatingText,
    $"+{points}", pos, new Color(1, 0.843137f, 0));
```

### CONNECT_ONE_SHOT 等价物

```csharp
// C# 不支持内置 CONNECT_ONE_SHOT，使用 lambda + 手动断开
void Handler(int score, int delta)
{
    _scoreLabel.Text = $"SCORE: {score}";
    EventBus.Instance.ScoreChanged -= Handler;  // 一次性
}

EventBus.Instance.ScoreChanged += Handler;

// 或者让动画节点的 AnimationFinished 信号在回调中 queue_free
// 注意：C# 中需要使用 async void 模式
public async void PlayAndFree(AnimationPlayer player)
{
    player.Play("explode");
    await ToSignal(player, AnimationPlayer.SignalName.AnimationFinished);
    if (IsInstanceValid(this))
        QueueFree();
}
```

### 编辑器连接

在 Godot 编辑器 Node 面板中可视化连接信号，会在 `.cs` 文件中生成对应的 `+=` 连接代码。编译后即可在编辑器中看到信号列表。

---

## 4. Async / Await 协程

Godot 4 C# 直接使用 C# 原生 `async` / `await` 配合 Godot 的 `ToSignal()` 实现协程。

### 基本用法

```csharp
// 等待计时器 — 使用 ToSignal 等待 SceneTreeTimer
await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

// 等待信号
await ToSignal(someNode, "signal_name");

// 等待 Tween 完成
await ToSignal(tween, Tween.SignalName.Finished);

// 等待下一帧
await ToSignal(GetTree(), "process_frame");

// 等待物理帧
await ToSignal(GetTree(), "physics_frame");
```

### async void vs async Task

```csharp
// async void — 用于 _Ready() 和 fire-and-forget 事件处理
public override async void _Ready()
{
    await ToSignal(GetTree(), "process_frame");
    InitializeBoard();
}

// async Task — 用于可等待的内部逻辑 (链式调用时可跟踪完成)
private async System.Threading.Tasks.Task RunCascadeLoop()
{
    var result = MatchDetector.DetectAll(BoardData);
    if (result.HasMatches())
        await ProcessMatchesAsync(result);
}

// async void wrapper — 从 UI 事件触发
public async void OnButtonPressed()
{
    await PlayAnimationAsync();
}
```

### 级联循环 (核心模式)

```csharp
private async System.Threading.Tasks.Task RunCascadeLoop()
{
    while (CascadeDepth < 20)
    {
        if (!IsInsideTree())
            return;

        var result = MatchDetector.DetectAll(BoardData);
        if (!result.HasMatches())
            break;

        CascadeDepth++;
        int score = ScoreCalculator.CalculateTotal(result);
        score = ScoreCalculator.ApplyCombo(score, CascadeDepth);
        GameData.Instance.AddScore(score);

        EventBus.Instance.EmitSignal(EventBus.SignalName.CascadeTriggered, CascadeDepth);

        // 消除动画
        var posArray = new Godot.Collections.Array<Vector2I>();
        foreach (var pos in result.GetAllPositions())
            posArray.Add(pos);
        await AnimController.PlayClearAsync(posArray);

        if (!IsInsideTree()) return;

        // 下落动画
        var falls = GravitySystem.ApplyGravity(BoardData);
        if (falls.Count > 0)
            await AnimController.PlayFallingAsync(falls);

        if (!IsInsideTree()) return;

        // 生成动画
        var spawns = SpawnSystem.FillEmpty(BoardData);
        if (spawns.Count > 0)
            await AnimController.PlaySpawnAsync(spawns);

        if (!IsInsideTree()) return;
    }

    await DoCheckValid();
}
```

### 交换动画后检测匹配

```csharp
private async System.Threading.Tasks.Task ExecuteSwap()
{
    int idxA = BoardData.GetIndex(SwapFrom.Y, SwapFrom.X);
    int idxB = BoardData.GetIndex(SwapTo.Y, SwapTo.X);
    var tileA = TileManager.GetActiveTile(idxA);
    var tileB = TileManager.GetActiveTile(idxB);

    // 动画
    if (tileA != null && tileB != null)
        await AnimController.PlaySwapAsync(tileA, tileB, 0.15f);

    // 数据交换
    BoardData.Swap(SwapFrom.Y, SwapFrom.X, SwapTo.Y, SwapTo.X);
    TileManager.RefreshFromData(BoardData);

    if (!IsInsideTree()) return;

    // 检测匹配
    var result = MatchDetector.DetectAll(BoardData);
    if (result.HasMatches())
    {
        GameData.Instance.UseMove();
        CurrentState = (int)GameState.CHECKING_MATCHES;
        CascadeDepth = 0;
        await RunCascadeLoop();
    }
    else
    {
        await ExecuteSwapBack();
    }
}
```

### 安全模式

```csharp
// 每个 await 后必须检查节点有效性
await ToSignal(tween, Tween.SignalName.Finished);
if (!IsInsideTree())
    return;

// 检查节点是否有效 (C# 有 GC, 但 GodotObject 需要 IsInstanceValid)
if (!IsInstanceValid(targetNode))
    return;

// 检查 Godot 原生对象是否已被释放
if (!GodotObject.IsInstanceValid(targetNode))
    return;
```

---

## 5. Tween 动画系统

Tween 从 Node 变为 **RefCounted 对象**，通过 `CreateTween()` 创建。

### 核心 API

```csharp
// 创建 Tween (自动开始，默认顺序执行)
var tween = CreateTween();

// 属性动画
tween.TweenProperty(sprite, "modulate", Colors.Red, 1.0);
tween.TweenProperty(sprite, "scale", Vector2.Zero, 1.0);
tween.TweenCallback(Callable.From(sprite.QueueFree));

// 链式配置
tween.TweenProperty(tile, "position", targetPos, 0.25f)
    .SetTrans(Tween.TransitionType.Back)
    .SetEase(Tween.EaseType.Out);

// 并行执行
tween.SetParallel(true);
tween.TweenProperty(tile1, "position:x", 100.0, 0.5f);
tween.TweenProperty(tile2, "position:x", 100.0, 0.5f);

// 使用 Parallel() 分组
var tw = CreateTween();
tw.TweenProperty(a, "modulate", Colors.Red, 1.0);
tw.Parallel().TweenProperty(b, "modulate", Colors.Blue, 1.0);
tw.Chain().TweenCallback(Callable.From(() => GD.Print("done")));

// 循环
tween.SetLoops(3);

// 生命周期绑定 (目标节点释放时自动停止 Tween)
tween.BindNode(this);

// 等待完成
await ToSignal(tween, Tween.SignalName.Finished);

// 控制
tween.Stop();
tween.Kill();
```

### 缓动类型

| 过渡 | 描述 | 常用场景 |
|------|------|----------|
| `Tween.TransitionType.Linear` | 线性 | 无关紧要的移动 |
| `Tween.TransitionType.Sine` | 正弦平滑 | 一般过渡 |
| `Tween.TransitionType.Quad` | 二次方 | 常规动画 |
| `Tween.TransitionType.Bounce` | 弹跳 | 落地效果 |
| `Tween.TransitionType.Elastic` | 弹性 | 弹出效果 |
| `Tween.TransitionType.Back` | 过冲 | 强调动画 |
| `Tween.TransitionType.Spring` | 弹簧 | 摆动效果 |

| 缓出方向 | 效果 |
|-----------|------|
| `Tween.EaseType.In` | 慢开始 |
| `Tween.EaseType.Out` | 慢结束 (最常用) |
| `Tween.EaseType.InOut` | 两端慢 |
| `Tween.EaseType.OutIn` | 两端快 |

### Match-3 动画示例

```csharp
// 消除动画
public async System.Threading.Tasks.Task PlayClearAsync(Godot.Collections.Array<Vector2I> positions)
{
    var tween = CreateTween();
    tween.SetParallel(true);

    foreach (var pos in positions)
    {
        int index = GridUtils.ToIndex(pos.Y, pos.X);
        var tile = TileManager.GetActiveTile(index);
        if (tile == null) continue;

        tween.TweenProperty(tile, "scale", Vector2.Zero, 0.2f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(tile, "modulate:a", 0.0, 0.15f);
    }

    await ToSignal(tween, Tween.SignalName.Finished);
    if (!IsInsideTree()) return;

    foreach (var pos in positions)
    {
        int index = GridUtils.ToIndex(pos.Y, pos.X);
        TileManager.Release(index);
    }
}

// 下落动画
public async System.Threading.Tasks.Task PlayFallingAsync(List<GravitySystem.FallInfo> falls)
{
    var tween = CreateTween();
    tween.SetParallel(true);

    foreach (var fallInfo in falls)
    {
        int index = GridUtils.ToIndex(fallInfo.ToRow, fallInfo.Col);
        var tile = TileManager.GetActiveTile(index);
        if (tile == null) continue;

        Vector2 targetPos = GridUtils.GridToWorld(fallInfo.ToRow, fallInfo.Col);

        tween.TweenProperty(tile, "position", targetPos, fallInfo.GetDuration())
            .SetTrans(Tween.TransitionType.Bounce)
            .SetEase(Tween.EaseType.Out);
    }

    await ToSignal(tween, Tween.SignalName.Finished);
}

// 生成动画 (从上方滑入 + 缩放弹出)
public async System.Threading.Tasks.Task PlaySpawnAsync(List<SpawnSystem.SpawnInfo> spawns)
{
    var tween = CreateTween();
    tween.SetParallel(true);

    var colDelays = new Godot.Collections.Dictionary<int, float>();

    foreach (var spawnInfo in spawns)
    {
        int index = GridUtils.ToIndex(spawnInfo.Row, spawnInfo.Col);
        var tile = TileManager.Acquire(index);
        var tileData = BoardData.GetTile(spawnInfo.Row, spawnInfo.Col);
        tile.Setup(tileData);

        Vector2 targetPos = GridUtils.GridToWorld(spawnInfo.Row, spawnInfo.Col);
        Vector2 startPos = targetPos + new Vector2(0, -150.0f);

        tile.Position = startPos;
        tile.Scale = new Vector2(0.3f, 0.3f);
        tile.Modulate = new Color(tile.Modulate.R, tile.Modulate.G, tile.Modulate.B, 0.0f);

        float delay = colDelays.TryGetValue(spawnInfo.Col, out float d) ? d : 0.0f;
        colDelays[spawnInfo.Col] = delay + 0.03f;

        tween.TweenProperty(tile, "position", targetPos, 0.25f)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(tile, "scale", Vector2.One, 0.25f)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(tile, "modulate:a", 1.0, 0.18f)
            .SetDelay(delay);
    }

    await ToSignal(tween, Tween.SignalName.Finished);
}
```

---

## 6. UI 系统

### Godot 4 主题 API

```csharp
// 获取主题属性
var stylebox = button.GetThemeStylebox("normal");
var font = label.GetThemeFont("font");
var color = label.GetThemeColor("font_color");

// 代码覆写主题属性 (无需单独 Theme)
label.AddThemeFontSizeOverride("font_size", 26);
label.AddThemeColorOverride("font_color", new Color("ffd700"));

// 创建主题
var theme = new Theme();
theme.SetColor("font_color", "Label", Colors.Red);
label.Theme = theme;
```

### Match-3 HUD 示例

```csharp
public partial class HUD : CanvasLayer
{
    private Label _scoreLabel;
    private Label _comboLabel;

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("TopPanel/HBoxContainer/ScoreSection/ScoreLabel");
        _comboLabel = GetNode<Label>("TopPanel/HBoxContainer/ComboSection/ComboLabel");

        EventBus.Instance.ScoreChanged += OnScoreChanged;
        EventBus.Instance.ComboUpdated += OnComboUpdated;
    }

    private void OnScoreChanged(int newScore, int delta)
    {
        _scoreLabel.Text = $"SCORE: {newScore}";

        if (delta > 0)
        {
            var pos = new Vector2(360, 200);
            EventBus.Instance.EmitSignal(EventBus.SignalName.ShowFloatingText,
                $"+{delta}", pos, new Color(1, 0.843137f, 0));
        }
    }

    private void OnComboUpdated(int combo)
    {
        if (combo <= 1)
        {
            _comboLabel.Hide();
            return;
        }

        string text = combo switch
        {
            2 => "Combo x2",
            3 => "Combo x3",
            4 => "Amazing x4",
            5 => "Incredible x5",
            _ => $"INSANE x{combo}!"
        };

        _comboLabel.Text = text;
        _comboLabel.Show();

        _comboLabel.Scale = new Vector2(2.0f, 2.0f);
        var tween = CreateTween();
        tween.TweenProperty(_comboLabel, "scale", Vector2.One, 0.3f)
            .SetTrans(Tween.TransitionType.Elastic)
            .SetEase(Tween.EaseType.Out);

        float g = Mathf.Clamp(1.0f - combo * 0.1f, 0.0f, 1.0f);
        float b = Mathf.Clamp(0.3f - combo * 0.05f, 0.0f, 1.0f);
        _comboLabel.AddThemeColorOverride("font_color", new Color(1.0f, g, b));
    }

    public override void _ExitTree()
    {
        EventBus.Instance.ScoreChanged -= OnScoreChanged;
        EventBus.Instance.ComboUpdated -= OnComboUpdated;
    }
}
```

### 锚点 (Anchor)

```csharp
// 代码设置锚点 — C# 使用 Offset 而 margin 已改为 offset
control.OffsetLeft = 0;
control.OffsetTop = 0;
control.OffsetRight = 640;
control.OffsetBottom = 48;

// Anchor 属性
control.AnchorLeft = 0.0f;
control.AnchorRight = 1.0f;
control.AnchorTop = 0.0f;
control.AnchorBottom = 0.1f;  // 顶部 10%
```

### 最佳实践

- 使用容器节点 (VBox/HBox/GridContainer) 进行布局
- 用 CanvasLayer 包裹 UI，独立于游戏世界坐标
- 触摸友好的按钮最小 48×48px
- 使用 `AddThemeFontSizeOverride()` / `AddThemeColorOverride()` 快速覆写主题
- 在 `_ExitTree()` 中清理信号订阅防止内存泄漏
- C# 中控件的 `SizeFlagsHorizontal` = `Control.SizeFlags.ShrinkCenter` 等

---

## 7. Resource 自定义资源

Resource 是数据容器，不渲染不处理，仅保存数据。支持导出属性、序列化、编辑器预览。

### 创建自定义 Resource

```csharp
using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class LevelData : Resource
{
    [Export] public int BoardWidth { get; set; } = 8;
    [Export] public int BoardHeight { get; set; } = 8;
    [Export] public int NumTileTypes { get; set; } = 4;
    [Export] public int MoveLimit { get; set; } = 30;
    [Export] public int TargetScore { get; set; } = 1000;
    [Export] public byte[] Layouts { get; set; }  // PackedByteArray

    // 无参数构造函数 (编辑器需要)
    public LevelData() : this(8, 8) { }

    public LevelData(int width, int height)
    {
        BoardWidth = width;
        BoardHeight = height;
    }
}
```

### 使用 Resource

```csharp
// 加载 (GD.Load<T> 泛型)
var level = GD.Load<LevelData>("res://levels/level_01.tres");

// 创建
var newLevel = new LevelData();
newLevel.TargetScore = 500;

// 保存
ResourceSaver.Save(newLevel, "res://levels/level_custom.tres");

// 使用 ResourceLoader
var level = ResourceLoader.Load<LevelData>("res://levels/level_01.tres");
```

### 最佳实践

- 使用 `[GlobalClass]` 让资源在编辑器中可创建和识别
- 开发期间使用 `.tres` (文本格式，Git 友好)
- Resource 支持嵌套子资源
- 提供无参数构造函数用于编辑器
- `[Export]` 属性自动出现在 Inspector 中

---

## 8. 输入处理

### 两种方式

**轮询 (每帧检测):**

```csharp
public override void _Process(double delta)
{
    if (Input.IsActionPressed("ui_right"))
        Position += new Vector2(speed * (float)delta, 0);
}
```

**事件驱动 (响应输入事件):**

```csharp
public override void _Input(InputEvent @event)
{
    if (@event.IsActionPressed("click"))
        HandleClick();
}
```

### Match-3 输入处理

```csharp
public partial class Board : Node2D
{
    public override void _Input(InputEvent @event)
    {
        if (!InteractionEnabled) return;

        // 鼠标
        if (@event is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
            {
                // 检查是否点在 UI 上
                var hovered = GetViewport().GuiGetHoveredControl();
                if (hovered != null) return;

                Vector2 localPos = GetLocalMousePosition();
                Vector2I gridPos = GridUtils.WorldToGrid(localPos);
                ProcessClick(gridPos);
            }
        }

        // 触摸
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                var hovered = GetViewport().GuiGetHoveredControl();
                if (hovered != null) return;

                Vector2I gridPos = GridUtils.WorldToGrid(touch.Position);
                ProcessClick(gridPos);
            }
        }
    }

    private void ProcessClick(Vector2I gridPos)
    {
        if (gridPos.X < 0 || gridPos.Y < 0)
        {
            StateMachine.ClearSelection();
            return;
        }

        int index = GridUtils.ToIndex(gridPos.Y, gridPos.X);
        var tile = TileManager.GetActiveTile(index);
        if (tile != null)
            StateMachine.OnTileClicked(tile);
        else
            StateMachine.ClearSelection();
    }
}
```

### 滑动检测

```csharp
private Vector2 _swipeStart;

public override void _Input(InputEvent @event)
{
    if (@event is InputEventMouseButton mouseBtn)
    {
        if (mouseBtn.Pressed)
        {
            _swipeStart = mouseBtn.Position;
        }
        else
        {
            var swipe = mouseBtn.Position - _swipeStart;
            if (swipe.Length() > 30.0f)
                HandleSwipe(_swipeStart, swipe);
            else
                HandleTap(mouseBtn.Position);
        }
    }
}

private void HandleSwipe(Vector2 start, Vector2 vector)
{
    Vector2I dir;
    if (Mathf.Abs(vector.X) > Mathf.Abs(vector.Y))
        dir = vector.X > 0 ? new Vector2I(1, 0) : new Vector2I(-1, 0);
    else
        dir = vector.Y > 0 ? new Vector2I(0, 1) : new Vector2I(0, -1);

    var from = GridUtils.WorldToGrid(start);
    TrySwap(from, from + dir);
}
```

### InputMap

在 Project Settings > InputMap 中定义动作名:
- `click` — 点击
- `swipe` — 滑动
- `pause` — 暂停

### 最佳实践

- 使用 InputMap 定义跨平台动作
- 同时支持鼠标和触摸 (使用 `is` 模式匹配检测事件类型)
- 设置滑动阈值区分点击和滑动 (建议 30px)
- 开发时启用 Project Settings > "Emulate Touch From Mouse"
- C# 中 `@event` 使用 `@` 前缀因为 `event` 是 C# 关键字

---

## 9. 场景管理

### 场景切换

```csharp
// 通过文件路径
GetTree().ChangeSceneToFile("res://scenes/game.tscn");

// 通过预加载的 PackedScene (推荐)
private static readonly PackedScene GameScene =
    GD.Load<PackedScene>("res://scenes/game.tscn");

public void StartGame()
{
    GetTree().ChangeSceneToPacked(GameScene);
}
```

### 实例化场景

```csharp
private static readonly PackedScene TileScene =
    GD.Load<PackedScene>("res://assets/scenes/tile.tscn");

public Tile CreateTile(Vector2 pos, int type)
{
    var tile = TileScene.Instantiate<Tile>();  // 泛型 Instantiate<T>
    tile.Position = pos;
    AddChild(tile);
    return tile;
}

// 自动清理特效
public async void SpawnEffect(Vector2 pos)
{
    var effect = EffectScene.Instantiate<Node2D>();
    effect.Position = pos;
    AddChild(effect);

    var animPlayer = effect.GetNode<AnimationPlayer>("AnimationPlayer");
    animPlayer.Play("explode");
    await ToSignal(animPlayer, AnimationPlayer.SignalName.AnimationFinished);
    effect.QueueFree();
}
```

### 自定义场景切换 (Autoload 方式)

```csharp
// SceneManager.cs (autoload)
public partial class SceneManager : Node
{
    public static SceneManager Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public void GotoScene(string path)
    {
        CallDeferred(MethodName.DeferredGotoScene, path);
    }

    private void DeferredGotoScene(string path)
    {
        var root = GetTree().Root;
        foreach (Node child in root.GetChildren())
            child.QueueFree();

        var scene = GD.Load<PackedScene>(path);
        root.AddChild(scene.Instantiate());
    }
}
```

### Godot 3 vs 4

| Godot 3 | Godot 4 C# |
|---------|------------|
| `change_scene("path")` | `GetTree().ChangeSceneToFile("path")` |
| `PackedScene.Instance()` | `PackedScene.Instantiate()` 或 `Instantiate<T>()` |
| `Node.Filename` | `Node.SceneFilePath` |
| `preload("...")` | `GD.Load<T>("...")` (无编译期常量，用 static readonly) |

---

## 10. Autoload 单例

Project Settings > Globals > Autoload，添加 `.cs` 脚本并命名。

### C# 单例模式

```csharp
using Godot;

namespace Match3Demo;

public partial class GameData : Node
{
    // C# 静态单例 (替代全局变量访问)
    public static GameData Instance { get; private set; }

    // 游戏状态 (使用属性)
    public int HighScore { get; set; }
    public int CurrentScore { get; set; }
    public int CurrentCombo { get; set; }
    public int MovesRemaining { get; set; } = 30;

    // 在 _EnterTree 中注册单例
    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        DetectPlatform();
    }

    private void DetectPlatform()
    {
        string osName = OS.GetName();
        // IsWeb check, etc.
    }

    public void ResetLevel()
    {
        CurrentScore = 0;
        CurrentCombo = 0;
        MovesRemaining = 30;

        EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, CurrentScore, 0);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ComboUpdated, CurrentCombo);
        EventBus.Instance.EmitSignal(EventBus.SignalName.MovesChanged, MovesRemaining);
    }

    public void AddScore(int points)
    {
        CurrentScore += points;
        if (CurrentScore > HighScore)
            HighScore = CurrentScore;
        EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, CurrentScore, points);
    }

    public void UseMove()
    {
        MovesRemaining -= 1;
        EventBus.Instance.EmitSignal(EventBus.SignalName.MovesChanged, MovesRemaining);
        if (MovesRemaining <= 0)
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameOver);
    }
}
```

### EventBus 示例

```csharp
using Godot;

namespace Match3Demo;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    [Signal] public delegate void BoardInitializedEventHandler();
    [Signal] public delegate void TileSelectedEventHandler(Node2D tile, Vector2I pos);
    [Signal] public delegate void TileDeselectedEventHandler();
    [Signal] public delegate void SwapRequestedEventHandler(Vector2I from, Vector2I to);
    [Signal] public delegate void SwapCompletedEventHandler(bool valid);
    [Signal] public delegate void SwapInvalidEventHandler();

    [Signal] public delegate void MatchesFoundEventHandler(Godot.Collections.Array matches);
    [Signal] public delegate void TilesClearedEventHandler(Godot.Collections.Array positions);
    [Signal] public delegate void SpecialTileSpawnedEventHandler(Vector2I pos, int type);
    [Signal] public delegate void CascadeTriggeredEventHandler(int depth);

    [Signal] public delegate void ScoreChangedEventHandler(int newScore, int delta);
    [Signal] public delegate void ComboUpdatedEventHandler(int combo);
    [Signal] public delegate void MovesChangedEventHandler(int remaining);
    [Signal] public delegate void TimeChangedEventHandler(float remaining);

    [Signal] public delegate void GameStateChangedEventHandler(int oldState, int newState);
    [Signal] public delegate void GamePausedEventHandler();
    [Signal] public delegate void GameResumedEventHandler();
    [Signal] public delegate void LevelCompleteEventHandler();
    [Signal] public delegate void GameOverEventHandler();

    [Signal] public delegate void PlayEffectEventHandler(string effectName, Vector2 pos);
    [Signal] public delegate void ScreenShakeEventHandler(float intensity, float duration);
    [Signal] public delegate void ShowFloatingTextEventHandler(string text, Vector2 pos, Color color);

    public override void _EnterTree()
    {
        Instance = this;
    }
}
```

### 访问 Autoload

```csharp
// 通过静态 Instance 访问 (推荐)
GameData.Instance.AddScore(100);
EventBus.Instance.EmitSignal(EventBus.SignalName.TileSelected, tile, pos);
AudioManager.Instance.PlaySfx(AudioManager.Sound.Match);

// 通过 root 访问 (备选)
GetNode<GameData>("/root/GameData").AddScore(100);

// 通过 GetTree().Root 访问
var gd = GetTree().Root.GetNode<GameData>("GameData");
```

### 最佳实践

- 每个 Autoload 类提供 `public static X Instance { get; private set; }` 静态属性
- 在 `_EnterTree()` 中赋值 `Instance = this;`
- 使用信号解耦 Autoload 之间的通信
- 保持 Autoload 数量可控 (3-5 个)
- 不要 free autoload (会导致崩溃)

---

## 11. 导出变量与 [Tool]

### 导出注解参考

```csharp
// 基础导出
[Export] public float Speed { get; set; } = 100.0f;
[Export] public Color Tint { get; set; } = Colors.Red;
[Export] public Vector2I TileSize { get; set; } = new(32, 32);
[Export] public LevelData LevelData { get; set; }
[Export] public PackedScene TileScene { get; set; }

// 范围限制
[Export(PropertyHint.Range, "0,10")]
public int Difficulty { get; set; } = 5;

[Export(PropertyHint.Range, "0.0,1.0,0.1")]
public float Volume { get; set; } = 0.8f;

// 枚举
[Export(PropertyHint.Enum, "Red,Green,Blue")]
public int CrystalType { get; set; }

// 类型安全的节点引用
[Export] public Match3Board Board { get; set; }

// 分组织
[ExportGroup("Board Settings")]
[Export] public int BoardWidth { get; set; } = 8;
[Export] public int BoardHeight { get; set; } = 8;

[ExportSubgroup("Animation")]
[Export] public float SwapDuration { get; set; } = 0.2f;

// 文件路径选择器
[Export(PropertyHint.File, "*.tscn")]
public string ScenePath { get; set; }

// 目录选择器
[Export(PropertyHint.Dir)]
public string AssetPath { get; set; }

// 多层
[Export(PropertyHint.MultilineText)]
public string Description { get; set; }
```

### [Tool] 脚本

```csharp
using Godot;

namespace Match3Demo;

[Tool]
public partial class BoardPreview : Node2D
{
    [Export]
    public Vector2I PreviewSize
    {
        get => _previewSize;
        set
        {
            _previewSize = value;
            if (Engine.IsEditorHint())
                QueueRedraw();
        }
    }
    private Vector2I _previewSize = new(8, 8);

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
            return;

        // 编辑器预览绘制
        for (int x = 0; x < PreviewSize.X; x++)
            for (int y = 0; y < PreviewSize.Y; y++)
                DrawRect(new Rect2(x * 64, y * 64, 64, 64), Colors.White, false);
    }
}
```

### 最佳实践

- `[Tool]` 放在类声明前
- 用 `Engine.IsEditorHint()` 守卫编辑器专用代码
- 使用 `[ExportGroup]` / `[ExportSubgroup]` 组织相关导出变量
- 使用 `/// <summary>` XML 文档注释作为编辑器提示
- C# 的 `[Export]` 必须用在属性 (property) 上，不能用在字段 (field) 上

---

## 12. Typed Collections 类型化集合

C# 使用 .NET 原生集合 + Godot.Collections 兼容集合。

### .NET 原生集合 (推荐用于内部逻辑)

```csharp
using System.Collections.Generic;

// List<T>
var scores = new List<int> { 100, 200, 300 };
var tiles = new List<Vector2I>();
var levels = new List<LevelData>();

// Dictionary<K, V>
var config = new Dictionary<string, int> { ["width"] = 8, ["height"] = 8 };
var lookup = new Dictionary<Vector2I, Node>();

// HashSet<T>
var visited = new HashSet<Vector2I>();

// Queue<T>, Stack<T>
var queue = new Queue<Vector2I>();
queue.Enqueue(pos);
var current = queue.Dequeue();
```

### Godot.Collections (需要序列化或传递给 Godot API 时)

```csharp
using Godot.Collections;

// 类型化数组
var positions = new Godot.Collections.Array<Vector2I>();
positions.Add(new Vector2I(3, 5));

// 类型化字典
var data = new Godot.Collections.Dictionary<int, float>();
data[0] = 0.5f;
float val = data.TryGetValue(0, out float v) ? v : 0.0f;

// Dictionary 用作信号参数
[Signal] public delegate void TilesClearedEventHandler(Godot.Collections.Array positions);

// 发射
var arr = new Godot.Collections.Array<Vector2I>();
EmitSignal(SignalName.TilesCleared, arr);
```

### 常用操作对照

```csharp
// List<T>
var arr = new List<int> { 1, 2, 3, 4, 5 };

arr.Count;               // 5 (不是 Size 或 Count for Array)
arr[0];                  // 1
arr[^1];                 // 5 (最后一个, C# 8+ 索引语法)
arr[0];                  // 1 (第一个)
arr[^1];                 // 5 (最后一个)

arr.Add(6);              // [1, 2, 3, 4, 5, 6]
arr.Insert(0, 0);        // [0, 1, 2, 3, 4, 5, 6]
arr.Remove(3);           // 移除第一个值为 3 的元素
arr.RemoveAt(2);         // 移除索引 2
arr.Clear();             // 清空

arr.Contains(3);         // 是否包含
arr.IndexOf(3);          // 返回索引, -1 表示未找到

// LINQ 函数式方法
using System.Linq;
arr.Select(x => x * 2).ToList();              // [2, 4, 6, 8, 10]
arr.Where(x => x > 2).ToList();               // [3, 4, 5]
arr.Aggregate(0, (acc, x) => acc + x);        // 15 (Sum() 更方便)

arr.Reverse();           // 原地反转
arr.Sort();              // 原地升序

arr.GetRange(1, 2);     // [2, 3] (起始 + count)
var copy = new List<int>(arr);  // 浅拷贝
arr.Count == 0;          // 是否为空

// Dictionary<K, V>
var dict = new Dictionary<Vector2I, Tile>();
dict[pos] = tile;                    // 设置
dict.TryGetValue(pos, out var t);     // 安全获取
dict.ContainsKey(pos);               // 是否包含键
dict.Keys;                           // 所有键
dict.Values;                         // 所有值
dict.Remove(pos);                    // 移除键
dict.Count;                          // 大小
```

### PackedArray (高性能)

```csharp
// C# 使用原生数组或 Span<T>
var byteData = new byte[64];            // PackedByteArray
var intData = new int[64];              // PackedInt32Array
var floatData = new float[64];          // PackedFloat32Array
var colorData = new Color[64];          // PackedColorArray

// 也可使用 Godot.Collections.Array<T>
var typedArray = new Godot.Collections.Array<int>();
```

### Match-3 典型模式

```csharp
// 1D 数组棋盘
var boardTiles = new CellData[64];  // byte[] for 8x8

// 位置查找字典
private readonly Dictionary<int, Tile> _activeTiles = new();

public void AddTile(int index, Tile tile)
{
    _activeTiles[index] = tile;
}

public Tile GetActiveTile(int index)
{
    _activeTiles.TryGetValue(index, out var tile);
    return tile;
}

public void RemoveTile(int index)
{
    if (_activeTiles.TryGetValue(index, out var tile))
    {
        tile.QueueFree();
        _activeTiles.Remove(index);
    }
}

// 方向常量
private static readonly Vector2I[] Directions = new[]
{
    new Vector2I(0, -1),  // 上
    new Vector2I(1, 0),   // 右
    new Vector2I(0, 1),   // 下
    new Vector2I(-1, 0),  // 左
};
```

---

## 13. Godot 3 → 4 迁移对照表

| 类别 | Godot 3 (GDScript) | Godot 4 (C#) |
|------|-------------------|--------------|
| **协程** | `yield()` | `await ToSignal()` / `await` |
| **信号连接** | `signal.connect(target, "method")` | `signal += Handler` (C# event) |
| **信号断开** | `signal.disconnect(callable)` | `signal -= Handler` |
| **信号定义** | `signal my_signal(arg)` | `[Signal] delegate void MySignalEventHandler(int arg);` |
| **信号发射** | `signal.emit(args)` | `EmitSignal(SignalName.SignalName, args)` |
| **导出** | `export var x = 5` | `[Export] public int X { get; set; } = 5;` |
| **导出范围** | `export(int, 0, 10) var x` | `[Export(PropertyHint.Range, "0,10")] public int X` |
| **Setter/Getter** | `setget _set_x, _get_x` | C# 原生 `get =>` / `set { }` |
| **实例化** | `PackedScene.instance()` | `scene.Instantiate()` / `Instantiate<T>()` |
| **场景切换** | `get_tree().change_scene("path")` | `GetTree().ChangeSceneToFile("path")` |
| **场景路径** | `Node.filename` | `Node.SceneFilePath` |
| **Tween** | `Tween` 节点, `interpolate_property()` | `Tween` RefCounted, `TweenProperty()` |
| **Tween 创建** | `Tween.new()` + `add_child()` | `CreateTween()` |
| **Tween 过渡** | `Tween.TRANS_BACK` | `Tween.TransitionType.Back` |
| **Tween 缓动** | `Tween.EASE_OUT` | `Tween.EaseType.Out` |
| **@onready** | `@onready var x = $Node` | `GetNode<T>("Node")` in `_Ready()` |
| **@tool** | `tool` | `[Tool]` attribute |
| **class_name** | `class_name MyClass` | `[GlobalClass] partial class MyClass : Node` |
| **主题属性** | `get_stylebox("normal")` | `GetThemeStylebox("normal")` |
| **边距** | `margin_left` 等 | `OffsetLeft` 等 (C# 属性名首字母大写) |
| **输入检测** | `event.is_action("name")` | `@event.IsActionPressed("name")` |
| **修饰键** | `event.control` | `@event.CtrlPressed` |
| **双击** | `event.doubleclick` | `@event.DoubleClick` |
| **键盘** | `KEY_SPACE` | `Key.Space` |
| **Array** | 无类型 | `List<T>` / `Godot.Collections.Array<T>` |
| **Dictionary** | 无类型 | `Dictionary<K,V>` / `Godot.Collections.Dictionary<K,V>` |
| **load()** | `load("res://...")` | `GD.Load<T>("res://...")` |
| **preload()** | `const X = preload("...")` | `static readonly X = GD.Load<T>("...")` |
| **随机数** | `randi() % n` | `GD.Randi() % n` / `GD.Randf()` |
| **File** | `File` | `FileAccess` |
| **JSON** | `JSON.parse(data)` | `Json.ParseString(data)` |
| **double delta** | `_process(delta: float)` | `_Process(double delta)` — 需要 `(float)delta` 转换 |
| **粒子** | `Particles2D` | `GpuParticles2D` |
| **2D 光照** | `Light2D` | `PointLight2D` |
| **shader** | `shader_type canvas_item;` | 相同 |
| **渲染器** | GLES2/GLES3 | Compatibility/Mobile/Forward+ |

---

## 14. C# vs GDScript 常见陷阱

### `_Ready()` 执行顺序

**C# 与 GDScript 相反**: C# 中 **父节点的 `_Ready()` 先于子节点执行**。这意味着在父节点 `_Ready()` 中访问子节点时，子节点的 `_Ready()` 可能尚未运行。

```csharp
// BAD — child._Ready() hasn't run yet
public override void _Ready()
{
    var child = GetNode<Board>("Board");  // child 存在，但 _Ready() 尚未执行
    child.ResetBoard();                    // 可能访问子节点的未初始化字段!
}

// GOOD — 用 CallDeferred 延迟执行
public override void _Ready()
{
    CallDeferred(MethodName.InitializeAfterChildrenReady);
}

private void InitializeAfterChildrenReady()
{
    var child = GetNode<Board>("Board");
    child.ResetBoard();  // 所有子节点 _Ready() 已完成
}

// GOOD — 用 await 等一帧
public override async void _Ready()
{
    await ToSignal(GetTree(), "process_frame");
    var child = GetNode<Board>("Board");
    child.ResetBoard();  // OK
}
```

### 文件名必须匹配类名

C# Godot 项目中，`.cs` 文件名**必须**与其中 `partial class` 的名称完全一致。如果类名为 `TileManager`，文件名必须是 `TileManager.cs`。

```csharp
// 文件: TileManager.cs  ← 必须匹配
public partial class TileManager : Node2D { }
```

### string 可空问题

C# 8+ 的 `string` 可为 null，但 Godot 的字符串通常不为 null。使用空字符串 `""` 作为空值，而非 `null`。

```csharp
// BAD
string name = null;  // 可能引发 NullReferenceException

// GOOD
string name = "";     // Godot 内部通常用空字符串
```

### [Signal] delegate 命名规则

`[Signal]` 委托的**名称**必须以此文件中的 `EventHandler` 结尾。

```csharp
// WRONG — 不能被 Godot 识别为信号
[Signal] public delegate void ScoreChanged(int newScore);

// CORRECT
[Signal] public delegate void ScoreChangedEventHandler(int newScore, int delta);
```

### async void 限制

`_Ready()` 可以是 `async void`，但有些场景不能：

```csharp
// OK — Godot 虚拟方法
public override async void _Ready() { }

// OK — fire-and-forget 事件处理
public async void OnButtonPressed() { }

// BETTER — 可等待的内部方法
private async System.Threading.Tasks.Task RunCascadeLoop() { }
```

### 资源释放

C# 和 Godot 各自管理内存，需要显式清理：

```csharp
public override void _ExitTree()
{
    // 清理信号订阅 (最重要！)
    EventBus.Instance.ScoreChanged -= OnScoreChanged;
    EventBus.Instance.GameOver -= OnGameOver;

    // 清理 C# 事件
    _timer.Timeout -= OnTimerTick;
}
```

### CallDeferred 的使用场景

```csharp
// 需要子节点 Ready 之后执行
CallDeferred(MethodName.SetupBoard);

// 需要节点从场景树移除后执行
CallDeferred(MethodName.CleanupAfterRemoval);

// 需要当前帧处理完毕
CallDeferred("nameof", arg1, arg2);  // 字符串版本
CallDeferred(MethodName.ProcessEvent, arg1);  // StringName 版本
```

### Godot 值类型 vs 引用类型

```csharp
// Vector2, Color, Vector2I 是 struct (值类型) — 不可为 null
Vector2 pos = Vector2.Zero;      // ✅
// Vector2 pos = null;           // ❌ 编译错误

// Node, Resource, PackedScene 是 class (引用类型) — 可为 null
Node node = null;                 // ✅
```

### 平台检测

```csharp
// 使用 OS.GetName() 而非 OS name 属性
string osName = OS.GetName();
bool isWeb = osName == "Web";

// 运行环境检测
if (OS.HasFeature("editor"))
    GD.Print("Running in editor");
```

---

## 15. C# Build 系统

### .csproj 配置

```xml
<Project Sdk="Godot.NET.Sdk/4.4.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
    <LangVersion>12</LangVersion>
  </PropertyGroup>
</Project>
```

### 常用命令

```bash
# 构建项目
dotnet build

# 构建并运行
dotnet build && godot --path .

# 清理
dotnet clean

# 添加 NuGet 包
dotnet add package PackageName

# 发布 (AOT/单文件)
dotnet publish -c Release
```

### NuGet 包管理

```bash
# 安装
dotnet add package Newtonsoft.Json

# 更新
dotnet list package --outdated
dotnet add package Newtonsoft.Json --version 13.0.3

# 移除
dotnet remove package Newtonsoft.Json
```

### 项目结构

```
project_root/
├── project.godot
├── match3-pets.sln
├── match3-pets.csproj
├── scripts/
│   ├── autoload/          # Autoload 单例
│   ├── core/              # 纯游戏逻辑 (无场景依赖)
│   ├── game/              # 场景附加的脚本
│   ├── ui/                # UI 控制脚本
│   ├── fx/                # 特效/粒子脚本
│   └── utils/             # 工具类/枚举/常量
└── tests/                 # 单元测试
```

### C# 编译问题排查

- **`SignalName` 找不到**: 委托名称是否正确 (必须 `EventHandler` 结尾)?
- **`EmitSignal` 编译错误**: 检查参数数量是否与 delegate 原型一致
- **`IsInstanceValid(obj)` 报错**: 使用 `GodotObject.IsInstanceValid(obj)` 完整形式
- **`@event` 名称冲突**: `@` 前缀仅在参数名与 C# 关键字冲突时使用
- **dll 加载错误**: 确保 `.csproj` 中 `<TargetFramework>` 匹配 GODOT 版本要求的 .NET 版本

---

## 16. 项目专用 C# 速查

### GridUtils — 坐标转换

```csharp
public static class GridUtils
{
    public static int CellSize { get; private set; }
    public static int CellStep { get; private set; }

    // Grid (世界坐标) ↔ 行列坐标
    public static Vector2 GridToWorld(int row, int col) { ... }
    public static Vector2I WorldToGrid(Vector2 worldPos) { ... }

    // 1D 索引 ↔ 2D 行列
    public static int ToIndex(int row, int col, int cols = 8)
    {
        return row * cols + col;
    }

    public static Vector2I ToRowCol(int index, int cols = 8)
    {
        return new Vector2I(index % cols, index / cols);
    }
}
```

### BoardData — 1D 数组棋盘

```csharp
public partial class BoardData
{
    public CellData[] Tiles;  // 1D 数组，Tiles[row * Cols + col]

    public CellData GetTile(int row, int col) => Tiles[row * Cols + col];
    public int GetIndex(int row, int col)     => row * Cols + col;
    public Vector2I RowCol(int index)         => new(index % Cols, index / Cols);
    public bool IsInBounds(int row, int col)  => row >= 0 && row < Rows && col >= 0 && col < Cols;
    public void Swap(int r1, int c1, int r2, int c2) { ... }

    public class CellData
    {
        public int CrystalType = -1;
        public int SpecialType = -1;
        public bool IsEmpty = true;
        public bool IsNormal() => !IsEmpty && SpecialType == -1;
        public bool IsSpecial() => !IsEmpty && SpecialType != -1;
    }
}
```

### 状态机 setter 模式

```csharp
public partial class GameStateMachine : Node
{
    [Signal] public delegate void StateChangedEventHandler(int previous, int current);

    private int _currentState;
    public int CurrentState
    {
        get => _currentState;
        set
        {
            if (value == _currentState)
                return;
            int prev = _currentState;
            ExitState(prev);
            _currentState = value;
            EnterState(value);
            EmitSignal(SignalName.StateChanged, prev, value);
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameStateChanged, prev, value);
        }
    }

    private void EnterState(int newState) { }
    private void ExitState(int fromState) { }
}
```

### 常用枚举

```csharp
public enum CrystalType
{
    RED = 0, BLUE = 1, GREEN = 2, YELLOW = 3, PURPLE = 4, EMPTY = -1
}

public enum GameState
{
    IDLE = 0, SELECTED = 1, SWAPPING = 2, SWAP_BACK = 3,
    CHECKING_MATCHES = 4, CLEARING = 5, FALLING = 6, SPAWNING = 7,
    CASCADE_CHECK = 8, CHECK_VALID = 9, RESHUFFLING = 10,
    PAUSED = 11, GAME_OVER = 12, RESETTING = 13
}
```

### BFS 洪水填充匹配检测

```csharp
private MatchGroup FloodFill(Vector2I start, int targetType)
{
    var queue = new Queue<Vector2I>();
    var visited = new HashSet<Vector2I>();
    queue.Enqueue(start);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!visited.Add(current))
            continue;

        foreach (var dir in Directions)
        {
            var neighbor = current + dir;
            if (IsInBounds(neighbor) && !visited.Contains(neighbor))
            {
                var tile = BoardData.GetTile(neighbor.Y, neighbor.X);
                if (!tile.IsEmpty && tile.CrystalType == targetType)
                    queue.Enqueue(neighbor);
            }
        }
    }

    return ClassifyShape(visited, targetType);
}
```

### 异步动画链 (标准模式)

```csharp
// 模式: 交换 → 检测 → 清除 → 下落 → 生成 → 循环
private async System.Threading.Tasks.Task ExecuteSwap()
{
    var tileA = TileManager.GetActiveTile(idxA);
    var tileB = TileManager.GetActiveTile(idxB);

    // 交换动画
    await AnimController.PlaySwapAsync(tileA, tileB, 0.15f);

    // 数据交换
    BoardData.Swap(r1, c1, r2, c2);
    TileManager.RefreshFromData(BoardData);

    if (!IsInsideTree()) return;

    var result = MatchDetector.DetectAll(BoardData);
    if (result.HasMatches())
    {
        GameData.Instance.UseMove();
        await RunCascadeLoop();
    }
    else
    {
        await ExecuteSwapBack();
    }
}
```

### 事件订阅与清理模式

```csharp
public partial class MyNode : Node
{
    public override void _Ready()
    {
        // 订阅
        EventBus.Instance.ScoreChanged += OnScoreChanged;
        EventBus.Instance.GameOver += OnGameOver;
    }

    public override void _ExitTree()
    {
        // 清理 (必须！防止内存泄漏)
        EventBus.Instance.ScoreChanged -= OnScoreChanged;
        EventBus.Instance.GameOver -= OnGameOver;
    }

    private void OnScoreChanged(int newScore, int delta) { }
    private void OnGameOver() { }
}
```

### 快捷键操作速查

| 操作 | C# 写法 |
|------|---------|
| 加载资源 | `GD.Load<PackedScene>("res://...")` |
| 实例化场景 | `scene.Instantiate<T>()` |
| 获取子节点 | `GetNode<T>("Path")` |
| 创建 Tween | `CreateTween()` |
| 等待信号 | `await ToSignal(node, "signal_name")` |
| 等待帧 | `await ToSignal(GetTree(), "process_frame")` |
| 等待计时器 | `await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout)` |
| 发射本节点信号 | `EmitSignal(SignalName.MySignal, args)` |
| 发射 Autoload 信号 | `EventBus.Instance.EmitSignal(EventBus.SignalName.MySignal, args)` |
| 订阅信号 | `EventBus.Instance.MySignal += Handler` |
| 取消订阅 | `EventBus.Instance.MySignal -= Handler` |
| 延迟调用 | `CallDeferred(MethodName.Method, args)` |
| 队列入队 | `tile.QueueFree()` |
| 检查有效 | `IsInstanceValid(node)` |
| 检查在场景树 | `IsInsideTree()` |
| 场景切换 | `GetTree().ChangeSceneToFile("res://...")` |
| 暂停 | `GetTree().Paused = true;` |
| 视口大小 | `GetViewportRect().Size` |
| 随机数 | `GD.Randi() % n`, `GD.Randf()` |
| 编辑器检测 | `Engine.IsEditorHint()` |
