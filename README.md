# 🐱 Match-3 Cat Puzzle

> A crystal-themed match-3 puzzle game with kawaii cat tiles, built with Godot 4.6 .NET + C#

<p align="center">
  <img src="https://img.shields.io/badge/Godot-4.6-blue?logo=godot-engine" alt="Godot 4.6">
  <img src="https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/language-C%23-green?logo=csharp" alt="C#">
  <img src="https://img.shields.io/badge/license-MIT-orange" alt="MIT">
</p>

---

## ✨ Features

- **8×8 grid** with 5 colorful cat types (Red, Blue, Green, Yellow, Purple)
- **Click-to-swap** input — tap one tile, then an adjacent one to swap
- **Match-3 detection** with horizontal, vertical, L-shape, T-shape, and Cross patterns
- **Cascading combos** — cleared tiles cause gravity, new tiles spawn, triggering chain reactions
- **Special tiles** — Bomb (4-match), Rainbow (5-match), Cross (L/T shape)
- **Scoring system** with combo multipliers
- **Animated everything** — swap sliding, clear shrinking, fall bouncing, spawn dropping
- **Dynamic board scaling** — automatically fits any window size
- **Pause / Resume** with pause menu
- **Game-over panel** with score display and retry
- **Object-pooled tiles** for performance

---

## 🎮 How to Play

1. Click **PLAY** on the title screen
2. **Click a cat** to select it (it will grow & glow)
3. **Click an adjacent cat** (up/down/left/right) to swap
4. If the swap makes 3+ in a row → they clear, score up!
5. Tiles fall down, new cats appear → cascade!
6. **30 moves** total. Get the highest score!

---

## 🏗 Architecture

```
scripts/
├── autoload/          # Global singletons
│   ├── EventBus.cs    # Signal bus (19 signals)
│   └── GameData.cs    # Score, moves, settings
├── core/              # Pure logic (no engine dependency)
│   ├── BoardData.cs   # 8×8 grid + CellData
│   ├── MatchDetector.cs   # Horizontal/vertical/flood-fill
│   ├── MatchResult.cs     # Match groups + special spawns
│   ├── GravitySystem.cs   # Column-based gravity
│   ├── SpawnSystem.cs     # Random tile filling
│   ├── ScoreCalculator.cs # Score + combo math
│   └── ValidMoveChecker.cs # Deadlock detection
├── game/              # Scene nodes
│   ├── Board.cs           # Grid rendering + input
│   ├── GameStateMachine.cs # 14-state FSM
│   ├── Tile.cs            # Cat texture display
│   ├── TileManager.cs     # Object pool
│   ├── AnimationController.cs # Tween animations
│   ├── Main.cs            # Root scene controller
│   └── InputHandler.cs    # (deprecated, board handles input)
├── ui/                # UI screens
│   ├── TitleScreen.cs
│   ├── HUD.cs             # Score, moves, combo
│   ├── PauseMenu.cs
│   ├── GameOverPanel.cs
│   └── FloatingTextSpawner.cs
├── fx/                # Visual effects
│   ├── ParticleController.cs
│   └── ScreenShake.cs
└── utils/
    ├── Enums.cs       # GameState, SpecialType, MatchShape...
    ├── Constants.cs   # Grid size, animation durations
    └── GridUtils.cs   # Coordinate conversion + dynamic layout
```

---

## 🧪 Tests

10 xUnit test files covering core logic:

| File | Tests |
|------|-------|
| `Tests/BoardDataTests.cs` | Index conversion, swap, bounds, clear |
| `Tests/BoardGenerationTests.cs` | No-match generation, all-cells-filled |
| `Tests/MatchDetectorTests.cs` | Horizontal/vertical/l-shape/no-match |
| `Tests/GravitySystemTests.cs` | Single/multiple/no falls, empty column |
| `Tests/ScoreCalculatorTests.cs` | Line scores, combos |
| `Tests/SpecialTilesTests.cs` | Bomb, rainbow, cross spawns |
| `Tests/ValidMoveCheckerTests.cs` | Valid move detection |
| `Tests/EdgeCasesTests.cs` | Cascade protection, empty board |
| `Tests/ReshuffleTests.cs` | Reshuffle logic |
| `Tests/SwapClearCascadeTests.cs` | Full swap→clear→gravity→spawn cycle |

```bash
dotnet build   # compile (0 errors, 0 warnings)
dotnet test    # run tests (requires .NET 8 runtime)
```

---

## 🚀 Getting Started

### Prerequisites
- **Godot 4.6 .NET edition** ([download](https://godotengine.org/download/))
- **.NET 8.0 SDK** (included with Godot .NET, or install separately)

### Open the project
1. Clone this repo
2. Open **Godot .NET** editor
3. Click **Import** → select the `project.godot` file
4. Wait for C# compilation (first time takes ~30s)
5. Press **F5** to run

### Run tests
```bash
dotnet build
dotnet test
```

---

## 🎨 Asset Credits

Cat SVGs are custom-designed vector graphics located in `assets/textures/cats/`.

---

## 📄 License

MIT — feel free to use, modify, and share!
