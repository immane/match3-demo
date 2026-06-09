# Task 23: 宠物 UI 层

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) §9 — EventBus 信号消费示例、UI 组件架构 |
| ↖ 设计 | [design/pet_system.md](../design/pet_system.md) §1.3 — 表现层架构分层 (scripts/pets/ui/) |

## 状态
- [x] 已完成

## 依赖
- Task 17 (ServiceInitializer — 通过 `GetService<T>()` 获取 `IPetCollectionService`、`IPetDataSource`)
- Task 22 (IPetCollectionService — `GetAllOwnedPets`、`SetFavorite`、`SetNickname`、`TryEvolve`)
- Task 20 (PetDefinition, PetRarity — 数据定义和稀有度枚举)
- Task 21 (PetInstance — 运行时宠物实例)

## 产出文件
```
scripts/pets/ui/PetCollectionPanel.cs   [新增]
scripts/pets/ui/PetDetailPopup.cs       [新增]
scripts/pets/ui/PetLevelUpAnimation.cs  [新增]
```

## 实现要求

### PetCollectionPanel.cs — 宠物网格合集面板

`scripts/pets/ui/PetCollectionPanel.cs`，命名空间 `Match3Demo`。继承 `Control`，使用 `GridContainer` 布局展示宠物网格。通过 `ServiceInitializer` 获取 `IPetCollectionService` 和 `IPetDataSource` 服务，监听 EventBus 的宠物变更事件自动刷新。

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Match3Demo;

public partial class PetCollectionPanel : Control
{
    [Export] public PackedScene PetSlotScene { get; set; }
    private GridContainer _grid;
    private IPetCollectionService _petService;
    private IPetDataSource _petDataSource;

    public override void _Ready()
    {
        _grid = GetNode<GridContainer>("ScrollContainer/Grid");
        _petService = ServiceInitializer.Instance?.GetService<IPetCollectionService>();
        _petDataSource = ServiceInitializer.Instance?.GetService<IPetDataSource>();

        EventBus.Instance.PetAcquired += OnPetAcquired;
        EventBus.Instance.PetLeveledUp += OnPetLeveledUp;
        EventBus.Instance.PetEvolved += OnPetEvolved;

        RefreshGrid();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.PetAcquired -= OnPetAcquired;
        EventBus.Instance.PetLeveledUp -= OnPetLeveledUp;
        EventBus.Instance.PetEvolved -= OnPetEvolved;
    }

    private void OnPetAcquired(string petDefId) => RefreshGrid();
    private void OnPetLeveledUp(string petInstanceId, int newLevel) => RefreshGrid();
    private void OnPetEvolved(string oldPetInstanceId, string newPetDefId) => RefreshGrid();

    public void RefreshGrid()
    {
        foreach (var child in _grid.GetChildren())
            child.QueueFree();

        if (_petService == null) return;

        var ownedPets = _petService.GetAllOwnedPets();
        foreach (var pet in ownedPets)
        {
            if (PetSlotScene == null) continue;
            var slot = PetSlotScene.Instantiate<PetSlot>();
            var def = _petDataSource.GetPetDefinition(pet.PetDefId);
            if (def == null) continue;

            slot.SetPet(pet, def);
            slot.Pressed += () => ShowDetailPopup(pet, def);
            _grid.AddChild(slot);
        }
    }

    private void ShowDetailPopup(PetInstance pet, PetDefinition def)
    {
        var popup = new PetDetailPopup();
        popup.SetPetData(pet, def, _petService);
        popup.PopupCentered();
        AddChild(popup);
    }
}
```

关键点：
- 服务通过 `ServiceInitializer.Instance?.GetService<T>()` 获取（服务定位器模式）
- `_ExitTree()` 中取消所有 EventBus 订阅，防止内存泄漏
- `RefreshGrid()` 先清空再重建网格（`QueueFree` + `Instantiate` 模式）
- 点击 `PetSlot` 时弹出 `PetDetailPopup`
- `PetSlotScene` 为 `[Export] PackedScene`，通过 Inspector 指定 `PetSlot` 场景文件
- Goss 模型层级：`Control > ScrollContainer > GridContainer`

### PetSlot.cs — 宠物网格槽位

`scripts/pets/ui/PetCollectionPanel.cs` 同文件或独立文件 `scripts/pets/ui/PetSlot.cs`。继承 `Button`，显示单个宠物的图标、名称、等级和稀有度色边框。

```csharp
public partial class PetSlot : Button
{
    private TextureRect _icon;
    private Label _nameLabel;
    private Label _levelLabel;
    private ColorRect _rarityBorder;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _nameLabel = GetNode<Label>("NameLabel");
        _levelLabel = GetNode<Label>("LevelLabel");
        _rarityBorder = GetNode<ColorRect>("RarityBorder");
    }

    public void SetPet(PetInstance pet, PetDefinition def)
    {
        if (def.Icon != null) _icon.Texture = def.Icon;
        _nameLabel.Text = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : def.DisplayName;
        _levelLabel.Text = $"Lv.{pet.Level}";
        _rarityBorder.Color = GetRarityColor(def.Rarity);
    }

    private static Color GetRarityColor(PetRarity rarity) => rarity switch
    {
        PetRarity.Common => new Color(0.6f, 0.6f, 0.6f),
        PetRarity.Rare => new Color(0.2f, 0.5f, 1.0f),
        PetRarity.Epic => new Color(0.7f, 0.2f, 1.0f),
        PetRarity.Legendary => new Color(1.0f, 0.7f, 0.1f),
        _ => Colors.White
    };
}
```

关键点：
- `SetPet` 优先显示昵称，无昵称则显示 `def.DisplayName`
- 稀有度颜色通过 `GetRarityColor` switch 表达式映射（灰/蓝/紫/金）
- 场景结构需包含子节点：`Icon (TextureRect)`、`NameLabel (Label)`、`LevelLabel (Label)`、`RarityBorder (ColorRect)`
- 稀有度颜色值与 `PetLevelCalculator.RarityColor()` 保持一致

### PetDetailPopup.cs — 宠物详情弹窗

`scripts/pets/ui/PetDetailPopup.cs`，命名空间 `Match3Demo`。继承 `Popup`，显示宠物详细信息：图标、名称、稀有度、等级/XP 进度条、进化按钮、收藏按钮、昵称输入。

```csharp
using Godot;

namespace Match3Demo;

public partial class PetDetailPopup : Popup
{
    private PetInstance _pet;
    private PetDefinition _def;
    private IPetCollectionService _petService;

    private TextureRect _icon;
    private Label _nameLabel;
    private Label _rarityLabel;
    private Label _levelLabel;
    private ProgressBar _xpBar;
    private Label _xpLabel;
    private Button _favoriteBtn;
    private Button _evolveBtn;
    private LineEdit _nicknameEdit;

    public void SetPetData(PetInstance pet, PetDefinition def, IPetCollectionService service)
    {
        _pet = pet;
        _def = def;
        _petService = service;
        RefreshDisplay();
    }

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("VBoxContainer/Icon");
        _nameLabel = GetNode<Label>("VBoxContainer/NameLabel");
        _rarityLabel = GetNode<Label>("VBoxContainer/RarityLabel");
        _levelLabel = GetNode<Label>("VBoxContainer/Info/LevelLabel");
        _xpBar = GetNode<ProgressBar>("VBoxContainer/Info/XPBar");
        _xpLabel = GetNode<Label>("VBoxContainer/Info/XPLabel");
        _favoriteBtn = GetNode<Button>("VBoxContainer/FavoriteBtn");
        _evolveBtn = GetNode<Button>("VBoxContainer/EvolveBtn");
        _nicknameEdit = GetNode<LineEdit>("VBoxContainer/NicknameEdit");

        _favoriteBtn.Pressed += ToggleFavorite;
        _evolveBtn.Pressed += OnEvolvePressed;
        _nicknameEdit.TextSubmitted += OnNicknameChanged;
    }

    private void RefreshDisplay()
    {
        if (_def == null || _pet == null) return;
        if (_def.Icon != null) _icon.Texture = _def.Icon;
        _nameLabel.Text = _def.DisplayName;
        _rarityLabel.Text = $"{_def.Rarity}";
        _levelLabel.Text = $"Lv.{_pet.Level} (Max Lv.{_def.MaxLevel})";
        float progress = (float)_pet.CurrentXP / _pet.NextLevelXP;
        _xpBar.Value = _pet.IsMaxLevel(_def) ? 1.0 : Mathf.Min(progress, 1.0);
        _xpLabel.Text = _pet.IsMaxLevel(_def) ? "MAX" : $"{_pet.CurrentXP}/{_pet.NextLevelXP}";
        _favoriteBtn.Text = _pet.IsFavorite ? "★ Unfavorite" : "☆ Favorite";
        _evolveBtn.Visible = _def.EvolutionChain?.Count > 0 && !_pet.IsMaxLevel(_def);
        _nicknameEdit.Text = _pet.Nickname ?? "";
    }

    private void ToggleFavorite()
    {
        _petService?.SetFavorite(_pet.Id, !_pet.IsFavorite);
        RefreshDisplay();
    }

    private void OnEvolvePressed()
    {
        if (_petService?.TryEvolve(_pet.Id) == true)
        {
            _def = ServiceInitializer.Instance.GetService<IPetDataSource>()
                ?.GetPetDefinition(_pet.PetDefId);
            ShowLevelUpAnimation();
            RefreshDisplay();
        }
    }

    private void OnNicknameChanged(string newName)
    {
        _petService?.SetNickname(_pet.Id, newName);
    }

    private void ShowLevelUpAnimation()
    {
        var anim = new PetLevelUpAnimation();
        anim.Play(_icon?.GlobalPosition ?? Vector2.Zero);
        AddChild(anim);
    }
}
```

关键点：
- 场景结构：`Popup > VBoxContainer > [Icon, NameLabel, RarityLabel, Info(HBoxContainer > LevelLabel + XPBar + XPLabel), FavoriteBtn, EvolveBtn, NicknameEdit]`
- XP 进度条满级时强制显示 1.0，并显示 "MAX" 文字
- 进化按钮仅在存在进化链 (`EvolutionChain?.Count > 0`) 且未满级时可见
- 收藏按钮文字根据当前状态切换 "★ Unfavorite" / "☆ Favorite"
- 进化成功后重新获取 `PetDefinition`（因为 `PetDefId` 已变更）、播放动画、刷新显示
- 昵称提交 (`TextSubmitted`) 后调用 `SetNickname`

### PetLevelUpAnimation.cs — 升级文字浮现动画

`scripts/pets/ui/PetLevelUpAnimation.cs`，命名空间 `Match3Demo`。继承 `Control`，在指定位置播放金色 "LEVEL UP!" 文字上浮淡出动画，播放完毕后自动清理。

```csharp
using Godot;

namespace Match3Demo;

public partial class PetLevelUpAnimation : Control
{
    public async void Play(Vector2 position)
    {
        var label = new Label();
        label.Text = "LEVEL UP!";
        label.AddThemeColorOverride("font_color", Colors.Gold);
        label.AddThemeFontSizeOverride("font_size", 32);
        label.Position = position;
        AddChild(label);

        var tween = CreateTween();
        tween.TweenProperty(label, "position:y", position.Y - 80, 1.0f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 1.0f);

        await ToSignal(tween, Tween.SignalName.Finished);
        QueueFree();
    }
}
```

关键点：
- 使用 `CreateTween()` 并行执行上浮（y - 80）和淡出（alpha → 0）动画
- 上浮使用 `Sine.Out` 缓动曲线，持续 1.0 秒
- `async void Play()` 异步等待动画完成后 `QueueFree()` 释放自身
- 不绑定场景树节点，纯代码动态创建

### 场景结构说明

**PetCollectionPanel 场景** (`pet_collection_panel.tscn`)：
```
Control (PetCollectionPanel)
└── ScrollContainer
    └── GridContainer (_grid)
        └── [动态 Instantiate PetSlot]
```

**PetSlot 场景** (`pet_slot.tscn`)：
```
Button (PetSlot)
├── ColorRect (RarityBorder)
├── TextureRect (Icon)
├── Label (NameLabel)
└── Label (LevelLabel)
```

**PetDetailPopup 场景** (`pet_detail_popup.tscn`)：
```
Popup (PetDetailPopup)
└── VBoxContainer
    ├── TextureRect (Icon)
    ├── Label (NameLabel)
    ├── Label (RarityLabel)
    ├── HBoxContainer (Info)
    │   ├── Label (LevelLabel)
    │   ├── ProgressBar (XPBar)
    │   └── Label (XPLabel)
    ├── Button (FavoriteBtn)
    ├── Button (EvolveBtn)
    └── LineEdit (NicknameEdit)
```

**PetLevelUpAnimation**：纯代码生成，不需 .tscn 场景文件。

## 验收标准
- [ ] `PetCollectionPanel` 通过 `ServiceInitializer.Instance.GetService<T>()` 正确获取 `IPetCollectionService` 和 `IPetDataSource`
- [ ] `PetCollectionPanel._ExitTree()` 中取消所有 EventBus 订阅（`PetAcquired`、`PetLeveledUp`、`PetEvolved`）
- [ ] `PetCollectionPanel.RefreshGrid()` 正确清空并重建宠物网格
- [ ] `PetSlot.SetPet()` 优先显示昵称，无昵称时显示 `def.DisplayName`
- [ ] `PetSlot.GetRarityColor()` 根据 `PetRarity` 返回对应颜色（灰/蓝/紫/金）
- [ ] `PetDetailPopup` 显示 XP 进度条（`ProgressBar.Value` 正确反映 `CurrentXP / NextLevelXP` 比例）
- [ ] `PetDetailPopup` 满级时 XP 进度条 = 1.0，文字显示 "MAX"
- [ ] `PetDetailPopup` 进化按钮仅在进化链存在且未满级时可见
- [ ] `PetDetailPopup` 进化成功后刷新 `PetDefinition`（因 `PetDefId` 已变更）+ 播放升级动画
- [ ] `PetLevelUpAnimation.Play()` 在指定位置播放 1 秒文字上浮淡出动画，完成后 `QueueFree()`
- [ ] `PetLevelUpAnimation` 使用 `Sine.Out` 缓动 + `Parallel` 并行动画
- [ ] 所有 3 个 UI 文件可通过 `dotnet build` 编译（0 错误，0 警告）
- [ ] 命名空间统一为 `Match3Demo`
- [ ] 场景结构注释与 .tscn 文件节点路径一致

## 注意
- `PetSlot` 可放在 `PetCollectionPanel.cs` 同文件，或独立为 `scripts/pets/ui/PetSlot.cs`
- `GetRarityColor()` 颜色值与 `PetLevelCalculator.RarityColor()` 静态方法保持一致
- `PetDetailPopup` 进化按钮按下后调用 `TryEvolve`（Task 22 中定义），而非直接调用旧版 `EvolvePet`
- `PetDetailPopup._def` 在进化成功后需重新获取，因为进化后的 `PetDefinition` 可能不同
- `PetLevelUpAnimation` 为纯动态 UI，不依赖 .tscn 场景文件
