# AI Context — Match-3 Cat Puzzle

> Full project context for AI-assisted development. Read this before making any changes.

---

## Quick Reference

- **Engine**: Godot 4.6 .NET (Mono edition required, NOT standard edition)
- **Language**: C# 12, .NET 8.0
- **Namespace**: `Match3Demo`
- **SDK**: `Godot.NET.Sdk/4.6.3`
- **Solution**: `Match3Demo.sln` / `Match3Demo.csproj`
- **Entry scene**: `res://assets/scenes/main.tscn`
- **Viewport**: 1080×1920 (portrait 9:16), resizable, `viewport` stretch + `expand` aspect
- **Build**: `dotnet build` → 0 errors, 0 warnings
- **Test**: 41 xUnit `[Fact]` tests in `Tests/` directory

---

## Scene Tree

```
Main (Node2D) [Main.cs]
├── Camera2D           ← at (0,0), screen shake target
├── Board [Board.cs]   ← 8×8 grid drawing, input handling
│   ├── BackgroundLayer
│   ├── TileLayer      ← TileManager (created in code) lives here
│   ├── EffectLayer
│   ├── InputHandler   ← DISABLED (ProcessModeEnum.Disabled), board handles input directly
│   ├── GameStateMachine [GameStateMachine.cs]
│   └── AnimationController [AnimationController.cs]
├── HUD (CanvasLayer) [HUD.cs]
│   ├── TopPanel → Score, Best, Combo, Moves labels
│   ├── PauseButton
│   └── FloatingTextLayer → FloatingTextSpawner
└── UILayer (CanvasLayer)        ← NEW: all overlays on CanvasLayer for screen-space rendering
	├── TitleScreen [TitleScreen.cs]
	├── PauseMenu [PauseMenu.cs]
	└── GameOverPanel [GameOverPanel.cs]
```

**CRITICAL**: UI overlays MUST be under a `CanvasLayer` node. Controls under `Node2D` render in world-space (affected by camera), not screen-space. The `UILayer` CanvasLayer was added specifically to fix this.

---

## Tile Scene

```
Tile (Node2D) [Tile.cs]
├── CatTexture (TextureRect)    ← displays SVG cat by type
├── SelectionBorder (ColorRect) ← gold, shown on select
├── SpecialIcon (ColorRect)     ← bomb/rainbow/cross indicator
├── GlowEffect (ColorRect)      ← shown on select
└── ClickArea (Area2D)          ← DISABLED (InputPickable=false), input via Board._Input()
```

---

## Architecture Layers

```
scripts/
├── autoload/    Global singletons (EventBus signals, GameData state)
├── core/        Pure logic — no Godot node dependency (static classes)
├── game/        Scene nodes — Board, StateMachine, Tile, TileManager, AnimationController
├── ui/          UI screens — HUD, Title, Pause, GameOver, FloatingText
├── fx/          Effects — particles, screen shake
└── utils/       Enums, constants, grid math
```

---

## Signal Bus (EventBus.cs — Autoload Singleton)

21 signals. Access via `EventBus.Instance.SignalName`:

| Signal | Params | Emitted By | Listened By |
|--------|--------|-----------|-------------|
| `BoardInitialized` | — | GameStateMachine | — |
| `TileSelected` | `Node2D tile, Vector2I pos` | GameStateMachine | — |
| `TileDeselected` | — | GameStateMachine | — |
| `SwapRequested` | `Vector2I from, to` | GameStateMachine | — |
| `SwapCompleted` | `bool valid` | GameStateMachine | — |
| `SwapInvalid` | — | GameStateMachine | — |
| `MatchesFound` | `Array matches` | GameStateMachine | — |
| `TilesCleared` | `Array positions` | GameStateMachine | — |
| `SpecialTileSpawned` | `Vector2I pos, int type` | GameStateMachine | — |
| `CascadeTriggered` | `int depth` | GameStateMachine | — |
| `ScoreChanged` | `int newScore, int delta` | GameData | HUD |
| `ComboUpdated` | `int combo` | GameData | HUD |
| `MovesChanged` | `int remaining` | GameData | HUD |
| `GameStateChanged` | `int oldState, int newState` | GameStateMachine | — |
| `GamePaused` | — | GameStateMachine | PauseMenu |
| `GameResumed` | — | GameStateMachine | PauseMenu |
| `GameOver` | — | GameData | Main, GameOverPanel |
| `ScreenShake` | `float intensity, duration` | — | ScreenShake |
| `ShowFloatingText` | `string text, Vector2 pos, Color` | HUD | FloatingTextSpawner |

---

## State Machine (14 States)

```
IDLE(0) → tap tile → SELECTED(1) → tap adjacent → SWAPPING(2)
													├─ no match → SWAP_BACK(3) → IDLE
													└─ match → CHECKING_MATCHES(4)
															   → CLEARING(5)
															   → FALLING(6)
															   → SPAWNING(7)
															   → CASCADE_CHECK(8)
															   → loop back or → CHECK_VALID(9)
																			   ├─ valid → IDLE
																			   └─ deadlock → RESHUFFLING(10) → IDLE

PAUSED(11)  ← any state → PAUSED → resume → previous state
GAME_OVER(12)  ← GameData.UseMove() when moves ≤ 0
RESETTING(13)  ← RestartGame()
```

**Input allowed states**: only `IDLE` (0) and `SELECTED` (1).

**init trick**: `Initialize()` sets `_currentState = -1` directly (bypasses setter), so the subsequent `CurrentState = IDLE` (0) always fires the `StateChanged` signal. This is needed because `CurrentState` defaults to 0, and the setter skips when `value == _currentState`.

---

## Data Flow

### Click → Swap → Cascade
```
User click
  → Board._Input() [get_local_mouse_position, GridUtils.WorldToGrid]
  → TileManager.GetActiveTile(index)
  → GameStateMachine.OnTileClicked(tile)
	→ IDLE: tile.Select(), state→SELECTED
	→ SELECTED: check adjacency (|dx|+|dy|==1)
	  → ExecuteSwap():
		1. AnimController.PlaySwapAsync(tileA, tileB)
		2. BoardData.Swap()
		3. TileManager.RefreshFromData()
		4. MatchDetector.DetectAll()
		→ match → RunCascadeLoop()
		→ no match → ExecuteSwapBack()
```

### Cascade Loop (per iteration)
```
1. MatchDetector.DetectAll()
2. ScoreCalculator.CalculateTotal() + ApplyCombo()
3. GameData.AddScore() + UpdateCombo()
4. AnimController.PlayClearAsync(positions) → shrink+fade tiles, release to pool
5. BoardData cells cleared
6. GravitySystem.ApplyGravity() → returns List<FallInfo>
7. AnimController.PlayFallingAsync(falls) → bounce tiles to new positions
8. SpawnSystem.FillEmpty() → returns List<SpawnInfo>
9. AnimController.PlaySpawnAsync(spawns) → drop new tiles from above
10. Repeat up to 20 times
11. ValidMoveChecker.HasAnyValidMove() → IDLE or Reshuffle
```

---

## Grid System (GridUtils.cs — Static Class)

Dynamic grid that auto-scales to viewport:

```csharp
GridUtils.Configure(int cols, int rows, Vector2 boardArea)
```

Properties after Configure:
- `CellSize` (40–120px, dynamically computed)
- `CellStep` = `CellSize + 4`
- `OffsetX, OffsetY` = centering margins
- `GridCols, GridRows` = always 8, 8

Key methods:
- `GridToWorld(row, col)` → `Vector2` (cell center)
- `WorldToGrid(Vector2)` → `Vector2I` (col, row), returns `(-1,-1)` if OOB
- `ToIndex(row, col)` → `int` (flat 1D index)
- `ToRowCol(index)` → `Vector2I` (col, row)

---

## C# Godot Conventions

### ✅ DO
- `partial class X : Node2D` for scene-attached scripts
- `[Signal] public delegate void XEventHandler(...)` for signals  
- `EmitSignal(SignalName.X, args)` to emit
- `EventBus.Instance.X += Handler` to subscribe to autoload signals
- `GetNode<T>("Path")` in `_Ready()` for child access (runs BEFORE children's _Ready)
- `GD.Load<PackedScene>("res://...")` or `GD.Load<Texture2D>("res://...")`
- `CreateTween()` for animations, `await ToSignal(tween, Tween.SignalName.Finished)`
- PascalCase for all C# signal/event names (C# naming convention)
- `Godot.Collections.Array` / `Godot.Collections.Dictionary` for Godot-compatible collections
- `System.Collections.Generic.List<T>` for internal C# lists
- `using Godot;` at top of files that use Godot types

### ❌ DON'T
- Don't use `sm.Call("method_name")` string invocation — use typed method calls
- Don't put Controls directly under Node2D — use CanvasLayer
- Don't use GDScript snake_case signal names in `Connect()` — use C# PascalCase
- Don't use `preload("res://...)` — use `GD.Load<T>("res://...")` 
- Don't store plain C# objects in `Godot.Collections.Array` — they can't be wrapped as Variant
- Don't use `partial class` without Godot base for data-only classes — just `public class`
- Don't forget `using Godot;` on files using Godot types (Node, Vector2, Color, etc.)

### File name = class name
Godot C# requires the `.cs` filename to match the class name exactly. `GameStateMachine.cs` contains `GameStateMachine` class. `Board.cs` contains `Board` class. Mismatch causes "Cannot instantiate C# script" error.

---

## Tile Textures

5 cat SVGs in `assets/textures/cats/`:
- `cat_red.svg` → `#ff4466`
- `cat_blue.svg` → `#4488ff`
- `cat_green.svg` → `#44dd66`
- `cat_yellow.svg` → `#ffcc33`
- `cat_purple.svg` → `#cc44ff`

Loaded by `Tile.cs` via `GD.Load<Texture2D>("res://assets/textures/cats/cat_xxx.svg")`.

Godot auto-imports SVGs as `CompressedTexture2D`. Clear `.godot/imported/` to force reimport after SVG changes.

---

## Board Layout (Dynamic)

`Board.RecalculateLayout()` called on `_Ready()` and on `GetTree().Root.SizeChanged`:
1. Gets viewport size
2. Calls `GridUtils.Configure(8, 8, viewportSize)`
3. Positions Board node so grid center is at world origin (0,0)
4. Tile positions updated via `TileManager.RefreshPositions()`
5. Redraws checkerboard background

---

## Pause System

- `GameStateMachine.TogglePause()` → saves current state to `StateBeforePause`, sets `PAUSED`, calls `GetTree().Paused = true`
- `PauseMenu.cs` has `ProcessMode = ProcessModeEnum.WhenPaused` so its buttons work during pause
- On resume: `GetTree().Paused = false`, restores `StateBeforePause`

---

## Common Pitfalls

1. **Godot caches SVGs** — after editing cat SVGs, delete `.godot/imported/` and `.godot/` to force reimport
2. **Signal name mismatch** — C# signals are PascalCase (`GameStarted`), not snake_case (`game_started`)
3. **Control under Node2D** — Controls need CanvasLayer parent for correct screen-space rendering
4. **Tween with no Tweeners** — `AnimationController` checks for empty lists/null tiles before creating tweens
5. **Tabs vs spaces** — Godot scripts use tabs (4-space width). Mixed indentation = parse error.
6. **Standard vs .NET Godot** — C# project MUST use Godot Mono/.NET edition
7. **_Ready() order in C#** — Parent's `_Ready()` runs BEFORE children's (opposite of GDScript). Use `CallDeferred` if you need children ready first.

---

## Test Infrastructure

- **Framework**: xUnit 2.9.2
- **Location**: `Tests/` directory (namespace `Match3Demo.Tests`)
- **Run**: `dotnet build && dotnet test`
- **Note**: `dotnet test` needs .NET 8.0 runtime. The test runner crashes on .NET 10 without it.
- **Godot test run**: Open in Godot .NET editor → build → run scene (integration test via gameplay)
- **Coverage**: 41 `[Fact]` tests covering board data, match detection, gravity, spawning, scoring, specials, cascades

---

## Key Files Index

| File | Purpose |
|------|---------|
| `scripts/game/Board.cs` | Grid rendering, input, layout |
| `scripts/game/GameStateMachine.cs` | 14-state FSM, swap/cascade logic |
| `scripts/game/AnimationController.cs` | Tween animation (swap/clear/fall/spawn) |
| `scripts/game/Tile.cs` | Cat texture display, selection |
| `scripts/game/TileManager.cs` | Object pool, refresh from data |
| `scripts/game/Main.cs` | Root scene, UI transitions |
| `scripts/core/BoardData.cs` | 8×8 grid data, CellData, swap |
| `scripts/core/MatchDetector.cs` | Match detection (2-pass algorithm) |
| `scripts/core/GravitySystem.cs` | Column-based gravity + FallInfo |
| `scripts/core/SpawnSystem.cs` | Random fill + SpawnInfo |
| `scripts/core/ScoreCalculator.cs` | Score math, combo multipliers |
| `scripts/core/ValidMoveChecker.cs` | Deadlock detection |
| `scripts/utils/GridUtils.cs` | Coordinate conversion, dynamic sizing |
| `scripts/utils/Enums.cs` | GameState, CrystalType, SpecialType, MatchShape |
| `scripts/autoload/EventBus.cs` | 21 signals, Instance singleton |
| `scripts/autoload/GameData.cs` | Score, moves, settings singleton |
