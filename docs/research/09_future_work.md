# Match-3 游戏未来扩展架构研究文档

> **定位**: 本文档为 Match-3 Demo 向 iOS/Android 完整产品演进提供架构建议。所有内容为推荐模式与设计思路，不涉及具体实现。

---

## 目录

1. [项目目录结构总览](#1-项目目录结构总览)
2. [关卡系统架构](#2-关卡系统架构)
3. [障碍物类型体系](#3-障碍物类型体系)
4. [道具与强化系统](#4-道具与强化系统)
5. [变现模式架构（仅文档）](#5-变现模式架构仅文档)
6. [社交功能架构](#6-社交功能架构)
7. [每日挑战与活动系统](#7-每日挑战与活动系统)
8. [存档系统](#8-存档系统)
9. [本地化 / i18n 架构](#9-本地化--i18n-架构)
10. [数据分析钩子](#10-数据分析钩子)
11. [多平台统一架构](#11-多平台统一架构)
12. [音频系统架构](#12-音频系统架构)
13. [教程系统设计](#13-教程系统设计)
14. [Godot 4.x 专项](#14-godot-4x-专项)

---

## 1. 项目目录结构总览

以下是推荐的项目目录结构，遵循 Godot 官方最佳实践（按场景组织，snake_case 命名）：

```
match3-demo/
├── project.godot                  # 项目配置（含 autoload 声明）
├── export_presets.cfg             # 导出预设（Web / iOS / Android）
├── .gitignore
│
├── docs/                          # 设计文档（.gdignore 防止被 Godot 导入）
│   ├── .gdignore
│   ├── research/
│   └── design/
│
├── addons/                        # 第三方插件
│   ├── .gdignore
│   └── (future: firebase, admob, etc.)
│
├── assets/                        # 原始素材（非场景）
│   ├── gdscript/                  # autoload 单例脚本
│   │   ├── game_manager.gd
│   │   ├── save_manager.gd
│   │   ├── audio_manager.gd
│   │   ├── analytics_manager.gd
│   │   ├── localization_manager.gd
│   │   ├── event_bus.gd           # 全局信号总线
│   │   └── config_manager.gd      # 配置读取
│   │
│   ├── resources/                 # 自定义 Resource 类型定义
│   │   ├── level_data.gd          # 关卡数据 Resource
│   │   ├── obstacle_data.gd       # 障碍物配置 Resource
│   │   ├── booster_data.gd        # 道具配置 Resource
│   │   ├── level_goal_data.gd     # 关卡目标 Resource
│   │   └── daily_challenge_data.gd
│   │
│   ├── json/                      # JSON 数据文件
│   │   ├── levels/                # 关卡定义
│   │   │   ├── world_01.json
│   │   │   ├── world_02.json
│   │   │   └── ...
│   │   ├── obstacles/
│   │   ├── boosters/
│   │   ├── settings/
│   │   └── localization/          # 本地化文本
│   │       ├── zh_cn.json
│   │       ├── en_us.json
│   │       └── ja_jp.json
│   │
│   ├── sprites/                   # 2D 图片素材
│   │   ├── tiles/
│   │   ├── obstacles/
│   │   ├── ui/
│   │   ├── effects/
│   │   └── backgrounds/
│   │
│   ├── audio/                     # 音频素材
│   │   ├── music/
│   │   ├── sfx/
│   │   └── voice/
│   │
│   ├── fonts/                     # 字体文件
│   │   ├── default.tres
│   │   └── default_fallback.tres
│   │
│   └── shaders/                   # 自定义着色器
│       ├── tile_effects.gdshader
│       └── obstacle_effects.gdshader
│
├── scenes/                        # 场景文件（按功能模块组织）
│   ├── main.tscn                  # 主场景（入口）
│   ├── _debug/                    # 调试场景
│   │
│   ├── gameplay/                  # 核心玩法
│   │   ├── board/                 # 棋盘相关
│   │   │   ├── game_board.tscn
│   │   │   ├── tile.tscn
│   │   │   └── cell.tscn
│   │   ├── obstacles/             # 每种障碍物独立场景
│   │   │   ├── base_obstacle.tscn
│   │   │   ├── frozen_tile.tscn
│   │   │   ├── locked_tile.tscn
│   │   │   ├── chocolate.tscn
│   │   │   ├── stone_block.tscn
│   │   │   ├── portal_pair.tscn
│   │   │   └── conveyor.tscn
│   │   ├── powerups/              # 游戏内道具特效
│   │   │   ├── bomb.tscn
│   │   │   ├── rocket.tscn
│   │   │   ├── rainbow.tscn
│   │   │   └── hammer.tscn
│   │   ├── goals/                 # 关卡目标显示
│   │   │   └── goal_display.tscn
│   │   └── effects/               # 粒子特效
│   │       ├── match_explosion.tscn
│   │       └── combo_text.tscn
│   │
│   ├── ui/                        # 用户界面
│   │   ├── main_menu/
│   │   ├── level_select/          # 关卡选择（地图式）
│   │   ├── hud/                   # 游戏内 HUD
│   │   ├── boosters/              # 战前道具选择界面
│   │   ├── shop/                  # 商店界面
│   │   ├── inventory/             # 背包/库存
│   │   ├── leaderboard/           # 排行榜
│   │   ├── daily_challenge/       # 每日挑战入口
│   │   ├── events/                # 限时活动
│   │   ├── settings/              # 设置面板
│   │   ├── tutorial/              # 教程覆盖层
│   │   ├── popups/                # 通用弹窗
│   │   └── transitions/           # 场景切换过渡动画
│   │
│   └── templates/                 # 可复用模板（非直接使用场景）
│       ├── base_popup.tscn
│       ├── base_button.tscn
│       └── loading_screen.tscn
│
└── utils/                         # 开发工具/辅助场景
    ├── level_editor.tscn          # 关卡编辑器
    └── debug_console.tscn
```

### 目录设计原则

| 原则 | 说明 |
|------|------|
| **场景就近原则** | 每个场景所使用的脚本、资源尽量放在场景同目录或子目录，方便模块化迁移 |
| **Assets 集中管理** | 跨场景共用的原始素材（图片、音频、字体）集中在 `assets/` 下 |
| **Autoload 脚本独立** | autoload 脚本放在 `assets/gdscript/`，与具体场景解耦 |
| **JSON 数据驱动** | 关卡、配置、本地化文本全部 JSON 化，支持热更新 |
| **snake_case 命名** | 文件和文件夹统一使用 snake_case，避免跨平台大小写问题 |
| **PascalCase 命名节点** | 场景内的节点名使用 PascalCase，与 Godot 内置节点命名一致 |

---

## 2. 关卡系统架构

### 2.1 架构概览

```
┌─────────────────────────────────────────────────────┐
│                    LevelManager                       │
│  (Autoload - 关卡加载/验证/进度管理)                    │
└──────────┬──────────────────────────┬────────────────┘
           │                          │
    ┌──────▼──────┐            ┌──────▼──────┐
    │ LevelData   │            │ LevelProgress│
    │ (Resource)  │            │ (Resource)   │
    │ - 关卡元数据 │            │ - 星级/分数   │
    │ - 障碍物列表 │            │ - 解锁状态    │
    │ - 目标定义  │            │ - 尝试次数    │
    └──────┬──────┘            └──────────────┘
           │
    ┌──────▼──────┐
    │  JSON 文件   │
    │ levels/     │
    │ world_XX.json│
    └─────────────┘
```

### 2.2 JSON 关卡定义格式

```json
{
  "format_version": "1.0",
  "level_id": "world_01_level_005",
  "world": 1,
  "level": 5,
  "name_key": "level_name_w1_5",
  "energy_cost": 5,

  "board": {
    "width": 8,
    "height": 8,
    "cells": [
      {"row": 0, "col": 2, "type": "empty", "data": {}},
      {"row": 1, "col": 3, "type": "obstacle", "data": {"obstacle": "stone_block", "layer": 2}},
      {"row": 2, "col": 2, "type": "portal_entrance", "data": {"exit_row": 5, "exit_col": 6}},
      {"row": 5, "col": 6, "type": "portal_exit", "data": {"entrance_row": 2, "entrance_col": 2}}
    ]
  },

  "tile_pool": {
    "colors": ["red", "blue", "green", "yellow", "purple"],
    "color_count": 5,
    "drop_frequencies": {
      "red": 0.20,
      "blue": 0.20,
      "green": 0.20,
      "yellow": 0.25,
      "purple": 0.15
    }
  },

  "obstacles": [
    {
      "type": "frozen_tile",
      "row": 3,
      "col": 4,
      "layer": 1,
      "can_spread": false
    },
    {
      "type": "chocolate",
      "row": 2,
      "col": 2,
      "max_spread_count": 3
    },
    {
      "type": "conveyor_belt",
      "path": [
        {"row": 0, "col": 0},
        {"row": 0, "col": 1},
        {"row": 0, "col": 2}
      ],
      "direction": "right",
      "speed": 1
    }
  ],

  "goals": [
    {
      "type": "collect_color",
      "color": "red",
      "count": 20
    },
    {
      "type": "clear_obstacle",
      "obstacle": "stone_block",
      "count": 5
    },
    {
      "type": "reach_score",
      "score": 10000
    }
  ],

  "difficulty": {
    "moves_limit": 25,
    "target_scores": {
      "star_1": 5000,
      "star_2": 15000,
      "star_3": 30000
    },
    "difficulty_rating": 3.5,
    "estimated_attempts": 1.5,
    "dynamic_difficulty": {
      "enabled": true,
      "extra_moves_threshold": 2,
      "max_extra_moves": 5
    }
  },

  "special_rules": {
    "no_match_penalty": false,
    "cascade_multiplier": 1.5,
    "time_limit_seconds": 0,
    "pre_placed_powerups": []
  }
}
```

### 2.3 关卡进度与难度曲线

```
难度
 10 ┤                                          ╭────  ★ 技能关卡
    │                                       ╭──╯
  8 ┤                                   ╭──╯     ◆  Boss 关卡
    │                               ╭──╯
  6 ┤                           ╭──╯             ●  普通关卡
    │                     ╭─────╯
  4 ┤              ╭──────╯                     ▲  放松关卡（Fuu 效应）
    │        ╭─────╯
  2 ┤  ╭────╯                                   ▬  Wow 效应关卡
    │╭─╯
  0 ┼────┬────┬────┬────┬────┬────┬────┬────► 关卡序号
    0   50   100  150  200  250  300  350  400

难度曲线特点：
  - 前 1-10 关：教程阶段，难度极低（1-2）
  - 11-30 关：引入新机制，每次引入后紧跟 2-3 关低难度练习
  - 31-100 关：逐步提升，每 15 关一个难度小峰值（"Fuu 关卡"）
  - 101-300 关：难度波动增大，穿插 Wow 效应关卡缓解疲劳
  - 300+ 关：引入技能性关卡，考验综合能力
```

### 2.4 关卡验证规则

每个关卡发布前应通过以下验证：

| 验证项 | 说明 |
|--------|------|
| 可达性检查 | 确保所有目标在给定步数内可达 |
| 生成器校验 | 棋盘初始状态不会产生死局 |
| 颜色数量匹配 | 5色及以上才引入障碍物，4色以下仅基础关卡 |
| 步数余量 | 通关所需最小步数 ≤ 给定步数的 80% |
| 连锁可能 | 棋盘至少存在 3 种不同连锁路径 |

### 2.5 动态难度系统（DDD）

```
┌──────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  玩家行为数据 │────▶│ DifficultyEngine  │────▶│ 实时参数调整     │
│  - 失败次数   │     │                  │     │ - 额外步数       │
│  - 通过步数   │     │ 计算玩家技能评分  │     │ - 掉落概率微调   │
│  - 连击次数   │     │ 匹配最优难度配置  │     │ - 提示冷却时间   │
│  - 使用道具数 │     │                  │     │                 │
└──────────────┘     └──────────────────┘     └─────────────────┘

原则：
  - 难度只能降低，不能升高（防止挫败感）
  - 失败 2-3 次后触发（避免过早干预）
  - 动态调整不超过原始难度的 30%
  - A/B 测试框架支撑效果验证
```

---

## 3. 障碍物类型体系

### 3.1 基类设计模式

```
                    ┌─────────────────┐
                    │  BaseObstacle   │
                    │  (extends Node2D)│
                    ├─────────────────┤
                    │ + obstacle_type │
                    │ + layer_count   │
                    │ + can_spread    │
                    ├─────────────────┤
                    │ + on_match_nearby│
                    │ + on_turn_start  │
                    │ + on_turn_end   │
                    │ + on_destroy()  │
                    │ + can_be_matched│
                    └───────┬─────────┘
                            │
        ┌───────┬───────────┼───────────┬──────────┐
        │       │           │           │          │
   ┌────▼──┐ ┌──▼───┐ ┌────▼────┐ ┌───▼────┐ ┌───▼─────┐
   │Frozen │ │Locked│ │Chocolate│ │Stone   │ │Portal   │
   │Tile   │ │Tile  │ │         │ │Block   │ │         │
   └───────┘ └──────┘ └────────┘ └────────┘ └─────────┘
```

### 3.2 障碍物详细清单

| 障碍物 | 分类 | 行为描述 | JSON 参数 | 复杂度 |
|--------|------|----------|-----------|--------|
| **Frozen Tile (冰冻方块)** | 不可移动障碍 | 覆盖在普通方块上，匹配相邻方块可消除。多层冰冻需要多次相邻匹配 | `layer: 1-3` | 低 |
| **Locked Tile (锁定方块)** | 被封锁元素 | 方块被锁住无法移动。需与同色方块匹配后才能解锁移动 | `lock_level: 1-2`, `color: "red"` | 低 |
| **Chocolate (巧克力/蔓延障碍)** | 生长型障碍 | 每回合向相邻空格蔓延一格。相邻匹配可清除。快速蔓延产生压迫感 | `max_spread: 3`, `spread_rate: 1` | 中 |
| **Stone Block (石块)** | 不可破坏障碍 | 占据格子，在其上无法放置方块。只能通过特殊道具（如锤子）或相邻特殊消除 | `indestructible: bool` | 低 |
| **Iron Block (铁块)** | 不可破坏障碍 | 比石块更硬，任何道具都无法移除，属于永久的"地形障碍" | `indestructible: true` | 低 |
| **Portal (传送门)** | 特殊机关 | 方块到达入口时被传送到出口。可改变棋盘流向，创造连锁机会 | `entrance_row/col`, `exit_row/col`, `bidirectional: bool` | 中 |
| **Conveyor Belt (传送带)** | 移动机关 | 每回合将上方方块沿指定方向移动一格。可多条传送带组成路径 | `path: [{row,col}...]`, `speed: 1-2` | 高 |
| **Ice/Layer (冰层)** | 多层覆盖 | 棋盘单元格被冰层覆盖，需在该格匹配 1-N 次才能消除。可覆盖障碍物 | `layer: 1-5` | 低 |
| **Wall (墙壁)** | 分割障碍 | 位于单元格之间，阻挡方块交换。可被相邻匹配消除或永久存在 | `removable: bool`, `direction: "horizontal/vertical"` | 低 |
| **Bomb (定时炸弹)** | 威胁型障碍 | 倒计时结束爆炸则失败。必须匹配同色消除 | `timer: 5-15` | 中 |
| **Growing Vine (藤蔓)** | 生长型障碍 | 类似巧克力，但沿特定方向或依附于已有藤蔓生长 | `direction: "random/nearest"`, `growth_chance: 0.8` | 中 |
| **Question Mark (?)** | 隐藏型障碍 | 显示为问号，可能隐藏道具或障碍。相邻匹配后揭示 | `reveal_pool: ["booster", "obstacle"]` | 低 |
| **Dirty Tile (污染方块)** | 恶性的匹配 | 普通方块被污染，与之匹配的对方也会被污染。需匹配两次才能消除 | `can_spread: true` | 中 |
| **Wind Zone (风区)** | 环境效果 | 在特定行/列，匹配后所有方块向操作方向移动一格 | `direction: "horizontal/vertical"` | 中 |
| **Element Generator (生成器)** | 辅助机关 | 相邻匹配时产生特定颜色方块，自动收集 | `produces_color: "red"`, `cooldown: 0-3` | 中 |

### 3.3 障碍物组合策略

```
推荐引入节奏（每 20 关引入一个新障碍物）：

世界 1 (关卡 1-20):    冰冻方块, 石块 → 基础障碍
世界 2 (关卡 21-40):   锁定方块, 冰层 → 策略性匹配
世界 3 (关卡 41-60):   巧克力, 问号 → 时间压力
世界 4 (关卡 61-80):   传送门, 墙壁 → 空间策略
世界 5 (关卡 81-100):  传送带, 风区 → 动态棋盘
世界 6 (关卡 101-120): 定时炸弹, 藤蔓 → 复合威胁
世界 7+ (关卡 121+):   高级组合, 生成器 → 大师挑战

障碍物组合不超过 3 种/关（前50关不超过 2 种）
```

---

## 4. 道具与强化系统

### 4.1 道具分类架构

```
┌──────────────────────────────────────────────────┐
│                  道具系统                          │
│                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌───────────┐ │
│  │ 战前道具     │  │ 局内道具     │  │ 永久强化   │ │
│  │ (Pre-game)  │  │ (In-game)   │  │ (Permanent)│ │
│  ├─────────────┤  ├─────────────┤  ├───────────┤ │
│  │ • 额外步数   │  │ • 炸弹       │  │ • 起始步数+│ │
│  │ • 锤子      │  │ • 火箭       │  │ • 得分倍率 │ │
│  │ • 彩虹道具   │  │ • 彩虹球     │  │ • 颜色-1   │ │
│  │ • 双色移除   │  │ • 交换       │  │            │ │
│  └──────┬──────┘  └──────┬──────┘  └─────┬──────┘ │
│         │                │                │        │
│    ┌────▼────┐      ┌────▼────┐     ┌─────▼─────┐ │
│    │ Booster │      │Match -> │     │  Player   │ │
│    │ Select  │      │Power-up │     │  Level    │ │
│    │  UI     │      │  Spawn  │     │  System   │ │
│    └─────────┘      └─────────┘     └───────────┘ │
└──────────────────────────────────────────────────┘
```

### 4.2 局内道具产生规则

```
匹配模式 → 产生道具：

3 个普通匹配 ──────────────────────────► 无道具（仅消除）
4 个直线匹配 ──────────────────────────► 火箭（消除整行/列）
L 型或 T 型 5 个匹配 ───────────────────► 炸弹（消除 3x3 区域）
5 个直线匹配 ──────────────────────────► 彩虹球（消除一种颜色）
道具 + 道具 组合 ──────────────────────► 超级道具（扩大范围）

道具链：
  火箭 × 火箭   → 十字消除
  炸弹 × 炸弹   → 5x5 爆炸
  彩虹 × 彩虹   → 全屏消除
  彩虹 × 火箭   → 所有该色变火箭
  彩虹 × 炸弹   → 所有该色变炸弹
```

### 4.3 库存/背包系统

```json
{
  "player_id": "xxx",
  "inventory": {
    "boosters": [
      {"type": "extra_moves",    "id": "booster_moves",    "count": 3,  "max_capacity": 99},
      {"type": "hammer",         "id": "booster_hammer",   "count": 2,  "max_capacity": 99},
      {"type": "rainbow_start",  "id": "booster_rainbow",  "count": 1,  "max_capacity": 10},
      {"type": "color_remover",  "id": "booster_colorrm",  "count": 0,  "max_capacity": 10}
    ],
    "in_game_powerups": [
      {"type": "extra_time",     "id": "pwr_time",    "count": 5,  "max_capacity": 5},
      {"type": "shuffle",        "id": "pwr_shuffle",  "count": 3,  "max_capacity": 5},
      {"type": "undo",           "id": "pwr_undo",     "count": 2,  "max_capacity": 3}
    ]
  },
  "permanent_upgrades": {
    "starting_moves_bonus": 0,
    "score_multiplier": 1.0,
    "color_reduction": 0
  }
}
```

### 4.4 道具获取途径设计

| 途径 | 道具类型 | 数量设计 |
|------|----------|----------|
| 每日登录奖励 | 战前道具 | 第 1 天: 锤子×1, 第 3 天: 额外步数×1, 第 7 天: 彩虹×1 |
| 关卡通关奖励 | 混合 | 首次通关: 随机道具×1, 三星通关: 额外道具×1 |
| 活动奖励 | 局内道具 | 完成活动目标阶梯奖励 |
| 商店购买 (金币) | 战前道具 | 固定价格，限购 |
| 商店购买 (付费) | 混合 | 礼包形式，限定品类 |
| 看广告获取 | 局内道具 | 失败后看广告获得 3 额外步数，或转盘抽奖 |
| 社交赠送 | 战前道具 | 好友每天可互赠，免费但限量 |

---

## 5. 变现模式架构（仅文档）

> **免责声明**: 本文档仅从架构角度记录移动端游戏常见的变现模式设计思路。是否在项目中实现这些功能由团队决策，且需遵守各平台政策（Apple App Store / Google Play）及当地法律法规。

### 5.1 变现系统架构

```
┌──────────────────────────────────────────────────────┐
│                   MonetizationManager                 │
│                   (Autoload)                          │
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │ IAP      │  │ Ads      │  │ Currency │           │
│  │ Manager  │  │ Manager  │  │ Manager  │           │
│  ├──────────┤  ├──────────┤  ├──────────┤           │
│  │ • 商品列表│  │ • 激励广告│  │ • 金币   │           │
│  │ • 购买验证│  │ • 插屏广告│  │ • 钻石   │           │
│  │ • 恢复购买│  │ • Banner │  │ • 体力   │           │
│  │ • 订阅   │  │ • 频率控制│  │ • 交易记录│           │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘           │
│       │             │             │                  │
│  ┌────▼─────────────▼─────────────▼─────┐           │
│  │         平台插件层 (Platform Plugins) │           │
│  │  iOS: StoreKit 2                     │           │
│  │  Android: Google Play Billing 6      │           │
│  │  Ads: AdMob / Unity Ads / AppLovin   │           │
│  └──────────────────────────────────────┘           │
└──────────────────────────────────────────────────────┘
```

### 5.2 货币体系设计（推荐双货币制）

| 货币 | 获取方式 | 用途 | 存储上限 |
|------|----------|------|----------|
| **金币 (Coin)** | 通关奖励、每日任务、看广告 | 购买基础道具、续命 | 无上限 |
| **钻石 (Gem)** | 付费购买、成就奖励、极少免费获取 | 购买高级道具、解锁特殊关卡、体力恢复加速 | 无上限 |
| **体力 (Energy)** | 随时间恢复、看广告、付费购买 | 进入关卡消耗 | 5 格上限（免费） |

### 5.3 激励广告接入点

```
游戏流程中的广告点位设计（不打断核心玩法）：

┌─────────┐    失败弹窗    ┌──────────────┐
│ 关卡失败 ├──────────────►│ "看广告 +3步"  │ (激励视频)
└─────────┘               └──────────────┘

┌─────────┐    每日奖励    ┌──────────────┐
│ 每日登录 ├──────────────►│ "看广告 x2"   │ (激励视频)
└─────────┘               └──────────────┘

┌─────────┐    道具不足    ┌──────────────┐
│ 使用道具 ├──────────────►│ "看广告获得"   │ (激励视频)
└─────────┘               └──────────────┘

┌─────────┐    体力不足    ┌──────────────┐
│ 开始关卡 ├──────────────►│ "看广告恢复"   │ (激励视频)
└─────────┘               └──────────────┘

┌─────────┐    奖励结算    ┌──────────────┐
│ 通关结算 ├──────────────►│ "看广告 x2金币"│ (激励视频)
└─────────┘               └──────────────┘
```

### 5.4 IAP 商品结构（推荐分层）

| 层级 | 商品示例 | 价格区间 | 目标用户 |
|------|----------|----------|----------|
| **入门包** | 新手礼包（钻石+道具+金币） | $1.99-$2.99 | 首日付费转化 |
| **基础包** | 钻石袋（小/中/大） | $0.99-$9.99 | 日常付费 |
| **进阶包** | 通关助力包（步数+道具） | $4.99-$14.99 | 卡关用户 |
| **限定包** | 节日限定皮肤/道具 | $2.99-$19.99 | 收藏型用户 |
| **订阅** | 月卡（每日钻石+N次免广告） | $4.99/月 | 核心用户 |
| **通行证** | 赛季战令（阶梯奖励） | $9.99/赛季 | 活跃用户 |

### 5.5 避税与合规备忘

- 价格以美元为基准，各区域使用 App Store / Play Store 自动换算层级
- 消耗型 IAP 可多次购买，非消耗型需提供"恢复购买"按钮
- 订阅需明示自动续费条款，提供取消渠道
- 激励广告需在儿童模式或 GDPR 地区提供关闭选项
- 虚拟货币不可兑换法币，不可转让

---

## 6. 社交功能架构

### 6.1 架构概览

```
┌────────────────────────────────────────────────┐
│              SocialManager (Autoload)           │
├────────────────────────────────────────────────┤
│                                                │
│  ┌──────────┐  ┌───────────┐  ┌─────────────┐ │
│  │ 排行榜    │  │ 好友系统   │  │ 赠送系统     │ │
│  │Leaderboard│  │ Friends   │  │ Gifting     │ │
│  ├──────────┤  ├───────────┤  ├─────────────┤ │
│  │ • 好友榜  │  │ • 添加好友 │  │ • 赠送体力   │ │
│  │ • 全球榜  │  │ • 好友列表 │  │ • 赠送道具   │ │
│  │ • 周榜   │  │ • 挑战邀请 │  │ • 请求帮助   │ │
│  │ • 活动榜  │  │ • 在线状态 │  │ • 收件箱     │ │
│  └────┬─────┘  └─────┬─────┘  └──────┬──────┘ │
│       │              │               │         │
│  ┌────▼──────────────▼───────────────▼──────┐  │
│  │           后端服务抽象层                    │  │
│  │  (可切换: Firebase / PlayFab / 自建 API)   │  │
│  └──────────────────────────────────────────┘  │
└────────────────────────────────────────────────┘
```

### 6.2 排行榜设计

```
排行榜维度设计：
  ┌──────────────────┬────────────┬──────────┐
  │ 维度              │ 刷新频率    │ 规模      │
  ├──────────────────┼────────────┼──────────┤
  │ 好友总分榜        │ 实时        │ 好友圈    │
  │ 好友本周榜        │ 每日        │ 好友圈    │
  │ 全球本周榜        │ 每小时      │ Top 100  │
  │ 关卡最高分榜      │ 实时        │ Top 50   │
  │ 活动限时榜        │ 实时        │ 全员      │
  │ 联赛段位榜        │ 赛季        │ 分组 30人 │
  └──────────────────┴────────────┴──────────┘

数据结构（后端）：
  leaderboard_entry {
    id: uuid
    leaderboard_id: string   # "global_weekly" / "level_005"
    player_id: string
    display_name: string
    score: int
    rank: int
    timestamp: datetime
    metadata: json            # 头像、等级等附属信息
  }
```

### 6.3 好友挑战系统

```
挑战流程：
  玩家A 完成关卡 ──► 生成挑战令牌 ──► 分享给好友B
                                       │
                                       ▼
                                  好友B 接受挑战
                                       │
                             ┌─────────▼─────────┐
                             │  相同关卡配置下     │
                             │  比拼得分/步数      │
                             └─────────┬─────────┘
                                       │
                               ┌───────▼───────┐
                               │ 胜利方获得奖励  │
                               │ 挑战结果通知    │
                               └───────────────┘

挑战数据：
  {
    "challenge_id": "uuid",
    "from_player": "A_id",
    "to_player": "B_id",
    "level_id": "world_03_level_012",
    "from_score": 15200,
    "from_stars": 3,
    "status": "pending/accepted/completed/expired",
    "expires_at": "ISO8601",
    "reward": {"type": "coin", "amount": 100}
  }
```

### 6.4 赠送系统

| 赠送类型 | 每日限额 | 接收上限 | 过期时间 |
|----------|----------|----------|----------|
| 体力赠送 | 5 次 | 20 个/天 | 24 小时 |
| 随机道具 | 3 次 | 10 个/天 | 7 天 |
| 关卡求助（请求体力） | 不限 | N/A | 24 小时 |
| 活动特殊礼物 | 1 次 | 无上限 | 活动结束 |

---

## 7. 每日挑战与活动系统

### 7.1 系统架构

```
┌────────────────────────────────────────────────────┐
│                EventManager (Autoload)              │
├────────────────────────────────────────────────────┤
│                                                    │
│  ┌─────────────────┐  ┌─────────────────────────┐ │
│  │ DailyChallenge   │  │ TimedEvent              │ │
│  │ Manager          │  │ Manager                 │ │
│  ├─────────────────┤  ├─────────────────────────┤ │
│  │ • 每日关卡生成    │  │ • 限时活动管理           │ │
│  │ • 每日任务        │  │ • 活动进度追踪           │ │
│  │ • 连胜奖励        │  │ • 排行榜积分             │ │
│  │ • 月历进度        │  │ • 奖励发放               │ │
│  └────────┬────────┘  └───────────┬─────────────┘ │
│           │                       │                │
│  ┌────────▼───────────────────────▼─────────────┐  │
│  │             EventConfig (JSON)                │  │
│  │  events/daily_challenges.json                │  │
│  │  events/seasonal_events.json                 │  │
│  │  events/special_events.json                  │  │
│  └──────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────┘
```

### 7.2 每日挑战设计

```json
{
  "daily_challenge": {
    "date": "2026-06-08",
    "seed": 1234567,
    "level_config": {
      "board_width": 8,
      "board_height": 8,
      "colors": 5,
      "moves_limit": 20,
      "goals": [
        {"type": "reach_score", "score": 20000}
      ],
      "special_rule": "no_boosters_allowed"
    },
    "rewards": {
      "participation": {"type": "coin", "amount": 50},
      "completion":    {"type": "coin", "amount": 200},
      "top_10_percent": {"type": "gem", "amount": 5}
    },
    "leaderboard_id": "daily_2026_06_08"
  }
}
```

### 7.3 每日任务系统

```
任务类型清单：
┌───────────────────────┬────────────┬──────────────┐
│ 任务                   │ 可完成次数  │ 奖励          │
├───────────────────────┼────────────┼──────────────┤
│ 完成 3 个关卡          │ 1次/天     │ 金币 ×100     │
│ 达到 5 次连击          │ 1次/天     │ 金币 ×50      │
│ 使用 2 次道具          │ 1次/天     │ 锤子 ×1       │
│ 三星通关任意关卡        │ 1次/天     │ 钻石 ×2       │
│ 观看 1 次广告          │ 1次/天     │ 金币 ×30      │
│ 赠送好友体力            │ 1次/天     │ 体力 ×1       │
├───────────────────────┼────────────┼──────────────┤
│ 完成全部 6 个每日任务    │ 额外奖励    │ 钻石 ×5       │
└───────────────────────┴────────────┴──────────────┘

月历系统：
  连续登录
  第 1 天:  金币 ×50
  第 2 天:  锤子 ×1
  第 3 天:  额外步数 ×1
  第 4 天:  金币 ×100
  第 5 天:  体力 ×1
  第 6 天:  钻石 ×3
  第 7 天:  彩虹道具 ×1  (7天周期循环)
  ...
  第 28 天: 钻石 ×10 (月度大奖)
```

### 7.4 限时活动类型

| 活动类型 | 持续时间 | 核心机制 | 奖励类型 |
|----------|----------|----------|----------|
| **周末锦标赛** | 周五-周日 | 积分累积赛道 | 阶梯奖励 + 排行榜 |
| **节日主题活动** | 7-14天 | 收集活动代币兑换 | 限定道具/皮肤 |
| **新关卡冲刺** | 3-5天 | 新发布关卡三星挑战 | 额外金币 + 体力 |
| **连击大师** | 48小时 | 最高连击数挑战 | 独家头像框 |
| **联盟赛** | 30天赛季 | 分组对战，段位晋升 | 赛季宝箱 |
| **单人冒险** | 永久/循环 | 独立剧情线，特殊规则关卡 | 冒险代币 → 永久强化 |

### 7.5 活动配置 JSON 结构

```json
{
  "event_id": "summer_festival_2026",
  "event_type": "seasonal_collection",
  "display_name_key": "event_summer_2026_name",
  "description_key": "event_summer_2026_desc",
  "start_time": "2026-07-01T00:00:00Z",
  "end_time": "2026-07-14T23:59:59Z",
  "requirements": {
    "min_player_level": 3,
    "unlocked_world": 1
  },
  "mechanics": {
    "collection_items": ["starfish", "shell", "pearl"],
    "drop_rates": {"starfish": 0.6, "shell": 0.3, "pearl": 0.1},
    "drop_on_level_complete": true
  },
  "reward_tiers": [
    {"items_collected": 10,  "rewards": [{"type": "coin", "amount": 200}]},
    {"items_collected": 30,  "rewards": [{"type": "booster", "id": "hammer", "amount": 2}]},
    {"items_collected": 50,  "rewards": [{"type": "gem", "amount": 15}]},
    {"items_collected": 100, "rewards": [{"type": "exclusive_skin", "id": "summer_board_2026"}]}
  ],
  "leaderboard": {
    "enabled": true,
    "ranking_by": "items_collected",
    "top_rewards": [
      {"rank": "1",      "rewards": [{"type": "gem", "amount": 100}]},
      {"rank": "2-10",   "rewards": [{"type": "gem", "amount": 50}]},
      {"rank": "11-100", "rewards": [{"type": "coin", "amount": 5000}]}
    ]
  }
}
```

---

## 8. 存档系统

### 8.1 分层存储架构

```
┌─────────────────────────────────────────────────┐
│                  数据存储层                        │
├─────────────────────────────────────────────────┤
│                                                 │
│  Layer 1: 本地存档 (user://)                      │
│  ┌───────────────────────────────────────────┐  │
│  │ save_data.json        # 玩家进度           │  │
│  │ settings.cfg          # ConfigFile 格式     │  │
│  │ analytics_cache.json  # 离线分析缓存        │  │
│  └───────────────────────────────────────────┘  │
│                                                 │
│  Layer 2: 平台云存档 (可选)                       │
│  ┌───────────────────────────────────────────┐  │
│  │ iOS: iCloud / Game Center                  │  │
│  │ Android: Google Play Saved Games           │  │
│  │ Web: LocalStorage / IndexedDB              │  │
│  └───────────────────────────────────────────┘  │
│                                                 │
│  Layer 3: 远程服务端存档 (社交功能必须)            │
│  ┌───────────────────────────────────────────┐  │
│  │ REST API / Firebase Realtime DB            │  │
│  │ 玩家档案、排行榜数据、活动进度              │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

### 8.2 本地存档数据结构

```json
{
  "save_version": 2,
  "player_id": "uuid-generated-on-first-launch",
  "created_at": "ISO8601",
  "updated_at": "ISO8601",
  "play_time_seconds": 36000,

  "game_progress": {
    "current_world": 3,
    "current_level": 12,
    "total_stars": 87,
    "levels_completed": 42,
    "levels": {
      "world_01_level_001": {"stars": 3, "high_score": 15000, "completed": true, "attempts": 1},
      "world_01_level_002": {"stars": 2, "high_score": 12000, "completed": true, "attempts": 3},
      "world_01_level_003": {"stars": 0, "high_score": 0,     "completed": false, "attempts": 0}
    }
  },

  "currencies": {
    "coin": 1250,
    "gem": 45,
    "energy": 4,
    "energy_last_refill_at": "ISO8601"
  },

  "inventory": {
    "boosters": [
      {"type": "hammer", "count": 3},
      {"type": "extra_moves", "count": 1}
    ],
    "in_game_powerups": [
      {"type": "shuffle", "count": 2}
    ]
  },

  "permanent_upgrades": {
    "starting_moves_bonus": 1,
    "score_multiplier": 1.1
  },

  "events": {
    "current_season": "season_03",
    "completed_events": ["summer_festival_2026"],
    "active_event_progress": {
      "spring_bloom_2026": {"items_collected": 23}
    }
  },

  "daily": {
    "last_login_date": "2026-06-08",
    "login_streak": 5,
    "daily_tasks_completed": ["task_01", "task_03"],
    "daily_challenge_attempted": true,
    "daily_challenge_score": 18000
  },

  "statistics": {
    "total_matches_made": 12000,
    "total_combos": 450,
    "longest_combo": 12,
    "boosters_used": 89,
    "total_play_time_seconds": 36000
  },

  "settings": {
    "music_volume": 0.8,
    "sfx_volume": 1.0,
    "language": "zh_cn",
    "notifications_enabled": true,
    "vibration_enabled": true,
    "battery_saver_mode": false
  },

  "tutorial_flags": {
    "completed_basic_tutorial": true,
    "completed_powerup_tutorial": false,
    "completed_obstacle_tutorial": false,
    "seen_feature_X": false
  }
}
```

### 8.3 SaveManager 接口模式

```
SaveManager (Autoload) 对外接口：

  save_game() -> Error
  load_game() -> Error
  get_progress() -> Dictionary
  update_level_result(level_id, stars, score)
  get_currency(type) -> int
  add_currency(type, amount, source)
  spend_currency(type, amount, reason) -> bool
  has_save() -> bool
  delete_save()
  export_save() -> String          # 导出 JSON（用于迁移/客服）
  import_save(json_string) -> Error # 导入 JSON

存储策略：
  - 使用 FileAccess + JSON.stringify 序列化
  - 每次写入前备份上一份存档（save_data.json.bak）
  - 写入流程：临时文件 → 重命名（原子操作避免损坏）
  - 可选加密：使用 AES-256-CBC 加密敏感数据（防止修改）
  - 存档迁移：save_version 字段驱动迁移逻辑
```

### 8.4 云存档同步策略

```
冲突解决：
  优先使用 "最后修改时间" 策略（Last-Write-Wins）
  关键操作（如付费购买）使用服务端权威数据

同步时机：
  - 应用启动时拉取
  - 关卡通关后推送
  - 离开应用时推送（后台任务）
  - 手动同步按钮

离线处理：
  - 本地始终保留完整存档
  - 在线时自动合并（服务端维护操作日志）
  - 冲突时提示用户选择
```

---

## 9. 本地化 / i18n 架构

### 9.1 架构设计

```
┌──────────────────────────────────────────────┐
│          LocalizationManager (Autoload)        │
├──────────────────────────────────────────────┤
│                                              │
│  ┌────────────────┐  ┌────────────────────┐  │
│  │ tr(key, args)  │  │ 语言切换/检测       │  │
│  │ 获取本地化文本   │  │ auto_detect_locale │  │
│  └───────┬────────┘  └─────────┬──────────┘  │
│          │                     │              │
│  ┌───────▼─────────────────────▼──────────┐  │
│  │           JSON 翻译文件                  │  │
│  │                                         │  │
│  │  i18n/zh_cn.json  (简体中文)            │  │
│  │  i18n/en_us.json  (English)            │  │
│  │  i18n/ja_jp.json  (日本語)             │  │
│  │  i18n/ko_kr.json  (한국어)             │  │
│  │  i18n/de_de.json  (Deutsch)            │  │
│  │  ...                                    │  │
│  └─────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

### 9.2 翻译 Key 命名规范

```
命名模式: category_section_specific

示例:
  # UI 通用
  ui_button_ok              → "确定"
  ui_button_cancel          → "取消"
  ui_button_play            → "开始游戏"
  ui_button_shop            → "商店"
  ui_button_settings        → "设置"

  # 关卡
  level_name_w1_5           → "甜蜜草原 1-5"
  level_goal_collect        → "收集 {count} 个 {color} 方块"
  level_goal_score          → "达到 {score} 分"
  level_result_win          → "恭喜通关!"
  level_result_stars_{n}    → "{n} 星通关"

  # 道具
  booster_name_hammer       → "神锤"
  booster_desc_hammer       → "消除任意一个方块或障碍物"
  booster_name_extramoves   → "额外步数"
  booster_desc_extramoves   → "获得 5 步额外移动机会"

  # 商店
  shop_pack_starter_name    → "新手礼包"
  shop_pack_starter_desc    → "含 100 钻石 + 3 个道具"

  # 教程
  tutorial_step_1           → "交换相邻方块，匹配 3 个同色方块即可消除"
  tutorial_step_2           → "匹配 4 个以上可产生特殊道具"

  # 错误/提示
  error_no_internet         → "网络连接失败，请稍后重试"
  error_purchase_failed     → "购买失败，请检查支付设置"
  tip_daily_reset           → "每日挑战已刷新！"
```

### 9.3 本地化数据加载体

```
加载策略：
  1. 启动时仅加载当前语言文件（避免内存浪费）
  2. 语言切换时异步加载新文件
  3. 翻译文件支持分模块拆分（如 ui.json, levels.json, boosters.json）
  4. 缺失翻译 fallback: zh_cn → en_us → key 原文

JSON 格式：
  {
    "locale": "zh_cn",
    "locale_name": "简体中文",
    "version": "1.2.0",
    "strings": {
      "ui_button_ok": "确定",
      "level_goal_collect": "收集 {count} 个 {color} 方块"
    }
  }

占位符格式：
  使用 {变量名} 风格，兼容 Godot 的 String.format()
  示例: "收集 {count} 个 {color} 方块" → tr("level_goal_collect", {"count": 5, "color": "红色"})

RTL 语言支持（阿拉伯语等）：
  - 使用 Godot 内置的 TextServer RTL 支持
  - UI 布局使用容器自适应（Container 的 mirror 属性）
```

---

## 10. 数据分析钩子

### 10.1 架构设计

```
┌──────────────────────────────────────────────────┐
│            AnalyticsManager (Autoload)            │
├──────────────────────────────────────────────────┤
│                                                  │
│   game_event(event_type, parameters)              │
│         │                                        │
│   ┌─────▼──────┐     ┌──────────────┐           │
│   │ 事件队列    │────▶│ 本地缓存      │           │
│   │ (内存)     │     │ JSON 文件     │           │
│   └────────────┘     └──────┬───────┘           │
│                             │                    │
│   ┌─────────────────────────▼────────────────┐  │
│   │           Analytics Adapter               │  │
│   │  (可插拔后端: Firebase / GameAnalytics /  │  │
│   │   Unity Analytics / 自建 HTTP API)        │  │
│   └──────────────────┬───────────────────────┘  │
│                      │                           │
│   ┌──────────────────▼───────────────────────┐  │
│   │ 发送策略:                                 │  │
│   │  - 批量发送 (每 30 秒或累积 50 条)         │  │
│   │  - 失败重试 (指数退避，最多 3 次)          │  │
│   │  - 离线缓存 (最多 1000 条)                │  │
│   │  - 压缩发送 (gzip)                       │  │
│   └──────────────────────────────────────────┘  │
└──────────────────────────────────────────────────┘
```

### 10.2 事件追踪清单

```
核心游戏事件：
  ┌──────────────────────────┬─────────────────────────────────┐
  │ 事件名                     │ 参数                             │
  ├──────────────────────────┼─────────────────────────────────┤
  │ level_start               │ level_id, world, move_limit     │
  │ level_complete            │ level_id, stars, score, moves_used, time_spent, boosters_used │
  │ level_fail                │ level_id, fail_reason, moves_used, progress_pct │
  │ level_restart             │ level_id, attempt_number         │
  │ match_made                │ match_size(3/4/5/L/T), colors   │
  │ powerup_created           │ powerup_type, method(match/combine) │
  │ powerup_used              │ powerup_type, location          │
  │ powerup_combined          │ powerup_a, powerup_b, result    │
  │ booster_used              │ booster_id, source(inventory/purchase/ad) │
  │ combo_achieved            │ combo_count                     │
  │ obstacle_destroyed        │ obstacle_type, method           │
  │ hint_used                 │ level_id                        │
  └──────────────────────────┴─────────────────────────────────┘

经济系统事件：
  ┌──────────────────────────┬─────────────────────────────────┐
  │ currency_earned           │ currency_type, amount, source   │
  │ currency_spent            │ currency_type, amount, reason   │
  │ purchase_started          │ product_id, price, currency     │
  │ purchase_completed        │ product_id, transaction_id      │
  │ purchase_failed           │ product_id, error_code          │
  │ reward_ad_watched         │ placement, reward_type          │
  │ reward_ad_skipped         │ placement                       │
  │ shop_opened               │ source_screen                   │
  │ shop_item_clicked         │ product_id                      │
  └──────────────────────────┴─────────────────────────────────┘

社交事件：
  ┌──────────────────────────┬─────────────────────────────────┐
  │ leaderboard_viewed        │ leaderboard_type                │
  │ challenge_sent             │ level_id, to_player_id         │
  │ challenge_accepted         │ challenge_id                   │
  │ gift_sent                 │ gift_type, to_player_id         │
  │ friend_added              │ method(search/invite/link)      │
  └──────────────────────────┴─────────────────────────────────┘

功能/进度事件：
  ┌──────────────────────────┬─────────────────────────────────┐
  │ tutorial_step_completed   │ step_id, step_name              │
  │ tutorial_skipped          │ step_id                         │
  │ feature_unlocked          │ feature_name, level             │
  │ settings_changed          │ setting_key, old_value, new_value │
  │ language_changed          │ old_locale, new_locale          │
  │ daily_challenge_played    │ score, rank_pct                 │
  │ event_participated        │ event_id                        │
  │ session_start             │ platform, version, device_info  │
  │ session_end               │ duration_seconds                │
  │ screen_view               │ screen_name                     │
  └──────────────────────────┴─────────────────────────────────┘

漏斗事件（关键留存指标）：
  ┌──────────────────────────┬─────────────────────────────────┐
  │ first_launch              │ install_source, country         │
  │ registration_completed    │ method                          │
  │ ftue_completed            │ total_steps, time_spent         │
  │ first_purchase            │ product_id, days_since_install  │
  │ day1_retention            │ (由后端计算)                      │
  │ day7_retention            │ (由后端计算)                      │
  │ day30_retention           │ (由后端计算)                      │
  └──────────────────────────┴─────────────────────────────────┘
```

### 10.3 无痕集成策略

```
代码中使用方式（示例）：

  # 在关卡通关处
  Analytics.game_event("level_complete", {
    "level_id": level_data.level_id,
    "stars": result.stars,
    "score": result.score,
    "moves_used": result.moves_used,
    "time_spent": result.time_elapsed,
    "boosters_used": result.boosters_used
  })

AnalyticsManager 特性：
  - 编译宏控制: #if ANALYTICS_ENABLED
  - 采样率配置: 可设置 1%-100% 采样
  - 用户隐私: 遵守 GDPR/CCPA，提供 opt-out 开关
  - PII 过滤: 自动脱敏（不发送用户名、设备ID 等）
  - 开发/生产环境分离: 开发环境事件不发送到生产后端
```

---

## 11. 多平台统一架构

### 11.1 平台抽象层设计

```
                    ┌─────────────────┐
                    │   游戏逻辑层      │
                    │  (平台无关代码)    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  平台抽象接口     │
                    │ PlatformInterface│
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
  ┌─────▼──────┐    ┌───────▼───────┐    ┌───────▼──────┐
  │ Web        │    │ iOS           │    │ Android      │
  │ Platform   │    │ Platform      │    │ Platform     │
  ├────────────┤    ├───────────────┤    ├──────────────┤
  │ JavaScript │    │ Swift/ObjC    │    │ Kotlin/Java  │
  │ Bridge     │    │ Bridge        │    │ Bridge       │
  │ + WebRTC   │    │ + StoreKit    │    │ + Play Billing│
  └─────┬──────┘    └───────┬───────┘    └───────┬──────┘
        │                   │                    │
        └───────────────────┼────────────────────┘
                            │
          ┌─────────────────▼───────────────────┐
          │ 功能开关 + 导出配置                    │
          │                                     │
          │  is_mobile()    → 显示触控UI        │
          │  is_web()       → 显示键盘提示       │
          │  has_iap()      → 显示商店按钮       │
          │  has_ads()      → 加载广告SDK        │
          │  has_cloud()    → 启用云存档         │
          └─────────────────────────────────────┘
```

### 11.2 平台差异化处理

| 功能 | Web | iOS | Android | 实现策略 |
|------|-----|-----|---------|----------|
| **存档** | LocalStorage / IndexedDB | iCloud + Keychain | Google Play Saved Games | 统一 SaveManager 接口 |
| **支付** | Stripe / 禁用 | StoreKit 2 | Google Play Billing 6 | IAPManager 适配器 |
| **广告** | 禁用 / 独立方案 | AdMob via iOS plugin | AdMob via Android plugin | AdManager 适配器 |
| **推送通知** | 浏览器通知 API | APNs | FCM | NotificationManager 抽象 |
| **社交登录** | OAuth (弹窗) | Sign in with Apple | Google Sign-In | AuthManager 抽象 |
| **云存档** | Firebase (Web SDK) | Firebase (iOS SDK) | Firebase (Android SDK) | 统一 REST API |
| **输入** | 鼠标 + 触控 | 触控 | 触控 | Godot Input 自动处理 |
| **屏幕适配** | 任意比例 | 竖屏优先 | 竖屏优先 | 响应式 UI 容器 |
| **性能** | 中 | 高 | 中-高 | 特效分级控制 |

### 11.3 编译宏与导出配置

```gdscript
# platform_config.gd (Autoload)
extends Node

enum Platform { WEB, IOS, ANDROID, DESKTOP }

static func get_platform() -> int:
    if OS.has_feature("web"):
        return Platform.WEB
    elif OS.has_feature("ios"):
        return Platform.IOS
    elif OS.has_feature("android"):
        return Platform.ANDROID
    else:
        return Platform.DESKTOP

static func is_mobile() -> bool:
    return OS.has_feature("ios") or OS.has_feature("android")

static func is_web() -> bool:
    return OS.has_feature("web")

static func has_iap() -> bool:
    return is_mobile()  # Web 禁用 IAP

static func has_ads() -> bool:
    return is_mobile()  # 仅移动端显示广告

# 功能开关示例
static func enabled_features() -> Dictionary:
    return {
        "iap": has_iap(),
        "ads": has_ads(),
        "cloud_save": true,
        "leaderboards": true,
        "daily_challenges": true,
        "social_features": true,   # Web 版限制部分社交功能
        "push_notifications": is_mobile()
    }
```

### 11.4 Godot 插件桥接模式

```
Godot GDScript  ←──→  原生代码

使用 GDExtension / 自定义 Module：
  - 平台相关功能封装为 GDExtension 插件
  - 每个平台提供相同接口，不同实现

iOS 示例流程：
  GameScene.gd
    → IAPManager.gd (Autoload)
      → IAPInterface.gdextension (C++/Swift binding)
        → StoreKitManager.swift (原生实现)

Android 示例流程：
  GameScene.gd
    → IAPManager.gd (Autoload)
      → IAPInterface.gdextension
        → BillingManager.kt (原生实现)

Web：
  GameScene.gd
    → IAPManager.gd
      → JavaScriptBridge 直接调用 JS API
```

---

## 12. 音频系统架构

### 12.1 架构设计

```
┌──────────────────────────────────────────────────┐
│              AudioManager (Autoload)              │
├──────────────────────────────────────────────────┤
│                                                  │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐ │
│  │ Music      │  │ SFX        │  │ Voice       │ │
│  │ Player     │  │ Pool       │  │ Player      │ │
│  ├────────────┤  ├────────────┤  ├────────────┤ │
│  │ • 循环播放  │  │ • 对象池复用│  │ • 一次播放   │ │
│  │ • 淡入淡出  │  │ • 优先级队列│  │ • 语言绑定   │ │
│  │ • 播放列表  │  │ • 最多同时N │  │ • 字幕同步   │ │
│  │ • 动态切换  │  │ • 随机音高  │  │             │ │
│  └─────┬──────┘  └─────┬──────┘  └──────┬──────┘ │
│        │               │               │         │
│  ┌─────▼───────────────▼───────────────▼──────┐  │
│  │         Godot AudioServer                    │  │
│  │   Bus Layout:                                │  │
│  │   Master                                      │  │
│  │   ├── Music    (effects: reverb, EQ)         │  │
│  │   ├── SFX      (effects: compressor)         │  │
│  │   ├── Voice    (effects: none)               │  │
│  │   └── UI       (effects: none)               │  │
│  └──────────────────────────────────────────────┘  │
│                                                  │
│  ┌──────────────────────────────────────────────┐ │
│  │             AudioData (Resource)              │ │
│  │  - sound_id: "match_3"                        │ │
│  │  - audio_stream: .mp3/.ogg                    │ │
│  │  - category: "sfx"                            │ │
│  │  - volume_db: -3.0                            │ │
│  │  - pitch_variation: 0.1                       │ │
│  │  - max_instances: 3                           │ │
│  │  - priority: 1 (越高越优先)                    │ │
│  └──────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

### 12.2 音效清单规划

```
音乐分类：
  主菜单主题
  游戏内 BGM (按世界/主题变化)
  关卡通关音乐
  关卡失败音乐
  商店/特殊界面 BGM

音效分类：
  # 方块相关
  tile_select          → 方块选中
  tile_swap            → 方块交换
  tile_drop            → 方块下落
  tile_match_3         → 3 连消除
  tile_match_4         → 4 连消除（火箭）
  tile_match_5         → 5 连消除（彩虹球）
  tile_match_L         → L/T 型消除（炸弹）
  cascade_1            → 连续消除第 1 次
  cascade_2            → 连续消除第 2 次
  cascade_3+           → 连续消除第 3 次以上（递增音高）

  # 道具相关
  powerup_create       → 道具产生
  powerup_activate     → 道具激活
  booster_hammer       → 锤子使用
  booster_rainbow      → 彩虹使用
  combo_super          → 超级组合道具

  # UI 音效
  ui_button_click      → 按钮点击
  ui_popup_open        → 弹窗打开
  ui_popup_close       → 弹窗关闭
  ui_coin_earn         → 获得金币
  ui_gem_earn          → 获得钻石
  ui_star_earn         → 获得星星
  ui_level_up          → 升级
  ui_purchase_success  → 购买成功

  # 障碍物相关
  obstacle_frozen_break → 冰冻破裂
  obstacle_chocolate_spread → 巧克力蔓延
  obstacle_lock_release → 锁解除
  obstacle_portal_use   → 传送门使用

  # 环境
  win_fanfare          → 通关胜利
  lose_jingle          → 关卡失败
  countdown_tick       → 计时器滴答（最后 5 秒）
  bomb_tick            → 炸弹倒计时
  bomb_explode         → 炸弹爆炸
```

### 12.3 音频优化策略

```
内存管理：
  - 音乐: 流式加载 (AudioStreamMP3 / AudioStreamOggVorbis)
  - 短音效: 预加载到内存 (AudioStreamWAV)
  - SFX 对象池: 预创建 10 个 AudioStreamPlayer，循环复用

声音优先级：
  P0: 关键 UI 反馈、道具使用
  P1: 消除音效、连续消除
  P2: 背景音效、环境音
  P3: 次要 UI 音效

当同时播放数超过上限时，按优先级裁剪低优先级声音。

平台适配：
  - Web: 需用户首次交互后才允许播放（浏览器的 AudioContext 策略）
  - iOS: 静音开关由系统控制，游戏内音量独立
  - Android: 音频焦点管理（电话/通知时自动降低音量）

音量设置独立存储：
  音乐音量: 0-100 (Settings → Audio Bus "Music" 音量 dB)
  音效音量: 0-100 (Settings → Audio Bus "SFX" 音量 dB)
  语音音量: 0-100 (Settings → Audio Bus "Voice" 音量 dB)
```

---

## 13. 教程系统设计

### 13.1 教程架构

```
┌──────────────────────────────────────────────────┐
│            TutorialManager (Autoload)              │
├──────────────────────────────────────────────────┤
│                                                  │
│  ┌────────────────────────────────────────────┐  │
│  │          TutorialSequence (Resource)        │  │
│  │                                            │  │
│  │  sequence_id: "basic_match_tutorial"       │  │
│  │  steps: [                                  │  │
│  │    {                                       │  │
│  │      "id": "step_1",                       │  │
│  │      "type": "highlight",  # 高亮区域       │  │
│  │      "target_node": "Board/Tile_3_2",      │  │
│  │      "text_key": "tutorial_swap_hint",     │  │
│  │      "required_action": "swap_tiles",      │  │
│  │      "allowed_area": [3,2, 3,3],           │  │
│  │      "arrow_direction": "left_to_right",   │  │
│  │      "dim_background": true,               │  │
│  │      "block_input_outside": true           │  │
│  │    },                                      │  │
│  │    {                                       │  │
│  │      "id": "step_2",                       │  │
│  │      "type": "dialog",    # 对话框         │  │
│  │      "text_key": "tutorial_match_3_desc",  │  │
│  │      "dismiss": "auto",   # 自动/手动       │  │
│  │      "duration": 2.0                        │  │
│  │    }                                       │  │
│  │  ]                                         │  │
│  └────────────────────────────────────────────┘  │
│                                                  │
│  触发方式:                                        │
│  ┌────────────────────────────────────────────┐  │
│  │  • 首次进入 (first_launch)                  │  │
│  │  • 功能解锁 (feature_unlocked)              │  │
│  │  • 新机制首次出现 (new_mechanic_encountered) │  │
│  │  • 用户主动触发 (help_button)               │  │
│  │  • 卡关检测 (stuck_detection)              │  │
│  └────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────┘
```

### 13.2 分阶段教程设计

```
阶段 1: 核心玩法教程（首次启动，约 2 分钟）
  ┌─────────────────────────────────────────────┐
  │ Step 1: "点击并交换相邻方块"                  │
  │ Step 2: "匹配 3 个同色方块即可消除"            │
  │ Step 3: "消除越多，得分越高"                   │
  │ Step 4: "完成目标即可通关"                    │
  │ Step 5: 自由操作，完成第一个简单关卡             │
  └─────────────────────────────────────────────┘

阶段 2: 道具教程（第 3-5 关触发）
  ┌─────────────────────────────────────────────┐
  │ Step 1: "匹配 4 个方块产生火箭"               │
  │ Step 2: "火箭可消除整行或整列"                │
  │ Step 3: "匹配 L/T 型产生炸弹"                 │
  │ Step 4: "两个道具组合产生更强效果"             │
  └─────────────────────────────────────────────┘

阶段 3: 障碍物教程（每个新障碍物首次出现时）
  ┌─────────────────────────────────────────────┐
  │ Step 1: "这是一个冰冻方块，需要匹配相邻位置才能消除" │
  │ Step 2: 引导完成 1-2 次相邻匹配              │
  └─────────────────────────────────────────────┘

阶段 4: 商店/道具使用教程（关卡失败 1 次后触发）
  ┌─────────────────────────────────────────────┐
  │ Step 1: "你可以使用战前道具获得帮助"           │
  │ Step 2: 引导打开道具选择界面                  │
  └─────────────────────────────────────────────┘

阶段 5: 社交/活动教程（第 10 关后触发）
  ┌─────────────────────────────────────────────┐
  │ Step 1: "每日挑战可获得额外奖励"               │
  │ Step 2: "添加好友一起比拼高分"                 │
  └─────────────────────────────────────────────┘
```

### 13.3 教程设计原则

| 原则 | 说明 |
|------|------|
| **渐进式揭示** | 每次只介绍一个概念，不信息轰炸 |
| **做中学** | 不让玩家阅读长文本，引导实际操作 |
| **可跳过** | 所有教程提供"跳过"按钮（不影响游戏进度） |
| **非阻塞** | 教程不要完全阻断游戏流程，保持交互感 |
| **条件触发** | 教程在需要时出现，而非一次性全部灌输 |
| **可重放** | 帮助菜单中可重新观看任何教程 |
| **上下文敏感** | 对话文本和图示针对当前局面定制 |
| **弱引导** | 使用视觉高亮、箭头、遮罩代替强制步骤 |

### 13.4 卡关智能提示

```
卡关检测规则：
  - 5 秒内无操作 → 提示可能的交换
  - 15 秒内无有效操作 → 高亮提示
  - 30 秒 → 询问是否需要提示

提示分级：
  Level 1 (轻度): 高亮一个可能的匹配
  Level 2 (中度): 显示箭头引导交换方向
  Level 3 (强烈): 直接闪烁可产生道具的匹配
```

---

## 14. Godot 4.x 专项

### 14.1 推荐 Addons 清单

| Addon | 用途 | 引入时机 |
|-------|------|----------|
| **Beehave** | 行为树系统，用于 AI 障碍物（Boss、移动角色） | 中期 |
| **Scene Manager** | 场景切换管理、过渡动画 | 早期 |
| **Dialogic** | 对话系统，用于教程、剧情对话 | 中期 |
| **Phantom Camera** | 相机控制（抖动、跟随） | 早期 |
| **Godot Firebase** | Firebase 集成（认证、数据库、云存储） | 中期 |
| **Gut** | 单元测试框架 | 早期 |
| **Todo Manager** | 内嵌 TODO 追踪 | 早期 |
| **CSV to JSON Converter** | 配表工具 | 早期 |
| **Shaker** | 震动/屏幕抖动效果 | 早期 |

### 14.2 自定义 Resource 类设计

```gdscript
# level_data.gd
class_name LevelData extends Resource
@export var level_id: String
@export var world: int
@export var level: int
@export var board_width: int = 8
@export var board_height: int = 8
@export var moves_limit: int = 20
@export var colors: Array[int] = [0, 1, 2, 3]
@export var obstacles: Array[ObstacleData] = []
@export var goals: Array[LevelGoalData] = []
@export var star_scores: Array[int] = [1000, 3000, 6000]
@export var difficulty_rating: float = 1.0

static func from_json(json_string: String) -> LevelData:
    # 工厂方法：从 JSON 创建 Resource
    pass


# obstacle_data.gd
class_name ObstacleData extends Resource
enum ObstacleType { FROZEN, LOCKED, CHOCOLATE, STONE, IRON, PORTAL, CONVEYOR }
@export var type: ObstacleType
@export var row: int
@export var col: int
@export var layer: int = 1
@export var extra_data: Dictionary = {}


# booster_data.gd
class_name BoosterData extends Resource
enum BoosterType { HAMMER, EXTRA_MOVES, RAINBOW, COLOR_REMOVER, SHUFFLE, UNDO }
@export var type: BoosterType
@export var display_name: String
@export var description: String
@export var icon: Texture2D
@export var max_capacity: int = 99
@export var coin_cost: int = 200
@export var gem_cost: int = 5
```

### 14.3 模块化场景设计模式

```
原则: "组合优于继承"

场景组织模式:
  ┌─────────────────────────────────────────────┐
  │  GameBoard (核心场景)                         │
  │  ├── BoardGrid (棋盘逻辑)                     │
  │  │   ├── Tile (可复用场景 × N)                │
  │  │   ├── Obstacle (按类型实例化子场景)          │
  │  │   └── CellHighlight (高亮覆盖层)           │
  │  ├── BoardInput (输入处理)                    │
  │  ├── BoardAnimator (动画控制)                 │
  │  ├── GoalTracker (目标追踪显示)               │
  │  └── EffectsLayer (粒子/特效层)               │
  └─────────────────────────────────────────────┘

每个功能模块 = 独立场景 + 独立脚本
  - 不依赖 get_parent() 调用
  - 使用信号与父级通信
  - 可单独运行调试（F6 播放当前场景）

信号设计原则:
  # 使用全局 EventBus (Autoload) 用于跨模块通信
  EventBus.level_completed.emit(result_data)
  EventBus.currency_changed.emit("coin", new_amount)
  EventBus.tile_swapped.emit(from_pos, to_pos)

  # 使用局部信号用于父子通信
  signal match_found(cells: Array)
  signal cascades_complete()
```

### 14.4 性能优化策略（Godot 4.x 特有）

```
渲染优化：
  - 使用 TileMapLayer 替代大量独立 Sprite2D 节点
  - 粒子使用 GPUParticles2D（而非 CPUParticles2D）
  - 动画使用 AnimationPlayer + 关键帧，避免逐帧 _process() 计算
  - 大量重复元素使用 MultiMeshInstance2D

内存优化：
  - 关卡数据使用 Resource 引用而非每次 JSON 解析
  - 纹理使用 Atlas 合图（减少 Draw Call）
  - 音频使用 AudioStreamPlayer 对象池（复用而非创建/销毁）
  - UI 使用 ObjectPool 模式复用列表项

电池优化：
  - 移动端在无交互时降低 _process() 频率
  - 提供"省电模式"：降低粒子数量、关闭背景动画
  - 使用 Engine.time_scale 控制游戏速度

Godot 4.x 新特性利用：
  - 类型化数组: Array[Tile] 替代 Array
  - lambda 函数: 用于回调简化
  - @export 增强: @export_storage, @export_multiline
  - TileMap 新图层系统: 分离逻辑层/渲染层/碰撞层
  - 新 Tween 系统: create_tween().tween_property() 替代旧 Tween 节点
```

### 14.5 CI/CD 与测试

```
测试策略：
  ┌────────────────┬──────────┬──────────────────────┐
  │ 测试类型        │ 工具      │ 覆盖范围              │
  ├────────────────┼──────────┼──────────────────────┤
  │ 单元测试        │ GUT      │ 数据类、管理器逻辑     │
  │ 集成测试        │ GUT      │ 棋盘逻辑、匹配算法     │
  │ 性能测试        │ Godot Profiler │ 渲染帧率、内存   │
  │ 关卡验证测试    │ 自定义    │ 每个关卡的可通过性     │
  └────────────────┴──────────┴──────────────────────┘

CI 流水线建议:
  1. Lint (gdformat / gdlint)
  2. Type Check (GDScript 类型检查)
  3. Unit Tests (GUT headless mode)
  4. Export (Web / iOS / Android artifact)
  5. Level Validator (批量验证所有关卡 JSON)

GitHub Actions / GitLab CI 示例触发:
  push → lint + test
  PR to main → lint + test + export check
  tag v* → full CI + deploy to stores
```

### 14.6 热更新与配置下发

```
远程配置架构:
  ┌────────────┐     HTTP(S)      ┌──────────────────┐
  │ 客户端      │ ◄──────────────► │ 远程配置服务       │
  │ ConfigMgr  │                  │ (Firebase Remote  │
  │            │                  │  Config / 自建)   │
  └──────┬─────┘                  └──────────────────┘
         │
  ┌──────▼──────────────────────────────┐
  │ 支持热更新的内容:                      │
  │  • 关卡 JSON 文件（新增/修改）         │
  │  • 活动配置                           │
  │  • 道具/商品定价                       │
  │  • 难度参数                           │
  │  • 功能开关                           │
  │  • 本地化文本更新                      │
  │                                      │
  │ 不支持热更新:                          │
  │  • 核心代码逻辑                        │
  │  • 资源文件(.png, .ogg, .tscn)       │
  │  • 需要应用商店审核的部分               │
  └──────────────────────────────────────┘

更新策略:
  1. 启动时比对远程配置版本号
  2. 有新版本时后台下载 JSON 到 user:// 目录
  3. 下次启动生效（避免游戏中切换导致状态不一致）
  4. 保留本地 fallback（assets/json/ 中的内置版本）
```

---

## 附录 A: 核心设计模式速查

| 模式 | 应用场景 | Godot 实现方式 |
|------|----------|---------------|
| **Singleton** | 全局管理器 | Autoload 单例 |
| **Observer** | 事件驱动通信 | Signal + EventBus |
| **Strategy** | 障碍物行为、解析规则 | 基类 + 虚函数 / Resource 策略 |
| **Command** | 撤销/重做 (Undo) | Command 对象 + 栈 |
| **Object Pool** | 方块、音效、粒子 | 预分配数组 + 复用 |
| **State Machine** | 游戏状态、教程流程 | enum + match 或 State 节点 |
| **Factory** | 关卡从 JSON 创建 | LevelData.from_json() |
| **Adapter** | 平台差异（IAP/Ad/Cloud） | 接口类 + 平台具体实现 |
| **Component** | 节点分离关注点 | 独立子节点 + 信号通信 |
| **Mediator** | UI 面板协调 | UIManager 控制面板显隐 |

## 附录 B: 关键技术决策记录

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 数据格式 | JSON | 可读性好，跨平台兼容，支持热更新，Godot 原生支持 |
| 存档格式 | JSON + 可选加密 | 调试友好，迁移方便；上线后可加加密层 |
| 关卡定义 | 外部 JSON 文件 | 不需要代码修改即可新增关卡，方便关卡策划并行工作 |
| 信号通信 | EventBus (Autoload) | 解耦模块，避免深层节点引用 |
| UI 框架 | 自建 Control 节点体系 | Godot 的 Control 系统足够强大，不需要第三方 UI 框架 |
| 后端方案 | 抽象接口 + 可替换实现 | 早期可无后端，中期接 Firebase，后期可换自建 |
| 动画系统 | Tween + AnimationPlayer | Godot 4 新 Tween 系统强大灵活 |
| 测试框架 | GUT | Godot 生态最成熟的测试框架 |

---

> 最后更新: 2026-06-08
> 文档性质: 架构研究 / 推荐建议（非实现规范）
