# Task 26: 抽卡 UI 层

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §6 — 流程示意图（抽卡 → 稀有度判定 → 展示动画） |
| ↖ 设计 | [design/gacha_system.md](../design/gacha_system.md) §7 — 多抽 UI（十连抽逐个展示、跳过按钮） |

## 状态
- [ ] 待执行

## 依赖
- Task 17 (ServiceInitializer — 服务定位器，注入 `GachaDrawService` / `IDataSource<GachaBanner>` / `ICurrencyService`)
- Task 25 (GachaDrawService — `PerformPull(bannerId)`, `PerformMultiPull(bannerId, count)`, `GetPullsUntilGuarantee(bannerId)`)
- Task 24 (GachaBanner, GachaRollResult, RewardType, PetRarity — 数据模型与枚举)
- Task 16 (EventBus — `GachaPullResult`, `GachaMultiPullResult`, `CurrencyChanged`, `ScreenShake` 信号)

## 产出文件
```
scripts/gacha/ui/GachaBannerUI.cs         [新增]
scripts/gacha/ui/PullAnimation.cs         [新增]
scripts/gacha/ui/RarityRevealEffect.cs    [新增]
```

## 实现要求

### GachaBannerUI.cs — 主抽卡界面

`scripts/gacha/ui/GachaBannerUI.cs`，命名空间 `Match3Demo`。主抽卡界面 Control，显示当前卡池信息、保底进度、消耗货币，并提供单抽 / 十连抽按钮。场景结构如下：

```
GachaBannerUI (Control)
├── BannerPanel (Panel)
│   ├── BannerNameLabel (Label)
│   ├── PityLabel (Label)
│   └── CostLabel (Label)
├── ButtonContainer (HBoxContainer)
│   ├── PullOnceBtn (Button)
│   └── PullMultiBtn (Button)
└── ResultContainer (Control) — 动画容器
```

```csharp
using Godot;
using System.Threading.Tasks;

namespace Match3Demo;

public partial class GachaBannerUI : Control
{
    [Export] public string DefaultBannerId { get; set; } = "standard_banner";
    
    private Label _bannerNameLabel;
    private Label _pullsUntilGuaranteeLabel;
    private Label _costLabel;
    private Button _pullOnceBtn;
    private Button _pullMultiBtn;
    private Control _resultContainer;
    
    private GachaDrawService _gachaService;
    private IDataSource<GachaBanner> _bannerDataSource;
    private ICurrencyService _currencyService;
    private string _activeBannerId;
    
    public override void _Ready()
    {
        _bannerNameLabel = GetNode<Label>("BannerPanel/BannerNameLabel");
        _pullsUntilGuaranteeLabel = GetNode<Label>("BannerPanel/PityLabel");
        _costLabel = GetNode<Label>("BannerPanel/CostLabel");
        _pullOnceBtn = GetNode<Button>("ButtonContainer/PullOnceBtn");
        _pullMultiBtn = GetNode<Button>("ButtonContainer/PullMultiBtn");
        _resultContainer = GetNode<Control>("ResultContainer");
        
        _gachaService = ServiceInitializer.Instance?.GetService<GachaDrawService>();
        _bannerDataSource = ServiceInitializer.Instance?.GetService<IDataSource<GachaBanner>>();
        _currencyService = ServiceInitializer.Instance?.GetService<ICurrencyService>();
        
        _pullOnceBtn.Pressed += OnPullOnce;
        _pullMultiBtn.Pressed += OnPullMulti;
        
        EventBus.Instance.GachaPullResult += OnPullResult;
        EventBus.Instance.GachaMultiPullResult += OnMultiPullResult;
        EventBus.Instance.CurrencyChanged += OnCurrencyChanged;
        
        LoadBanner(DefaultBannerId);
    }
    
    public override void _ExitTree()
    {
        EventBus.Instance.GachaPullResult -= OnPullResult;
        EventBus.Instance.GachaMultiPullResult -= OnMultiPullResult;
        EventBus.Instance.CurrencyChanged -= OnCurrencyChanged;
    }
    
    public void LoadBanner(string bannerId)
    {
        _activeBannerId = bannerId;
        var banner = _bannerDataSource?.Get(bannerId);
        if (banner == null) return;
        
        _bannerNameLabel.Text = banner.DisplayName;
        _costLabel.Text = $"Cost: {banner.CostPerPull}";
        UpdatePityDisplay();
    }
    
    private void UpdatePityDisplay()
    {
        int pullsLeft = _gachaService?.GetPullsUntilGuarantee(_activeBannerId) ?? -1;
        _pullsUntilGuaranteeLabel.Text = pullsLeft > 0
            ? $"Pulls until guaranteed SSR: {pullsLeft}"
            : "";
    }
    
    private void OnPullOnce()
    {
        try { _gachaService?.PerformPull(_activeBannerId); }
        catch (InvalidOperationException) { ShowInsufficientCurrency(); }
    }
    
    private void OnPullMulti()
    {
        try { _gachaService?.PerformMultiPull(_activeBannerId, 10); }
        catch (InvalidOperationException) { ShowInsufficientCurrency(); }
    }
    
    private async void ShowInsufficientCurrency()
    {
        _costLabel.Text = "Insufficient currency!";
        _costLabel.Modulate = Colors.Red;
        await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
        var banner = _bannerDataSource?.Get(_activeBannerId);
        _costLabel.Text = banner != null ? $"Cost: {banner.CostPerPull}" : "";
        _costLabel.Modulate = Colors.White;
    }
    
    private async void OnPullResult(string rewardId, int rarity, Godot.Collections.Dictionary pity)
    {
        var parsedRarity = (PetRarity)rarity;
        _pullOnceBtn.Disabled = true;
        _pullMultiBtn.Disabled = true;
        
        await ShowPullAnimation(rewardId, parsedRarity);
        
        _pullOnceBtn.Disabled = false;
        _pullMultiBtn.Disabled = false;
        UpdatePityDisplay();
    }
    
    private async void OnMultiPullResult(Godot.Collections.Array results)
    {
        _pullOnceBtn.Disabled = true;
        _pullMultiBtn.Disabled = true;
        
        foreach (Godot.Collections.Dictionary result in results)
        {
            string rewardId = (string)result["rewardId"];
            var rarity = (PetRarity)(int)result["rarity"];
            await ShowPullAnimation(rewardId, rarity);
            await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        }
        
        _pullOnceBtn.Disabled = false;
        _pullMultiBtn.Disabled = false;
        UpdatePityDisplay();
    }
    
    private async Task ShowPullAnimation(string rewardId, PetRarity rarity)
    {
        var anim = new PullAnimation();
        _resultContainer.AddChild(anim);
        await anim.Play(rewardId, rarity);
        anim.QueueFree();
    }
    
    private void OnCurrencyChanged(string currencyId, int newBalance, int delta)
    {
        UpdatePityDisplay();
    }
}
```

关键点：
- 从 `ServiceInitializer.Instance` 获取 `GachaDrawService`、`IDataSource<GachaBanner>`、`ICurrencyService`
- `_Ready()` 加载默认卡池、绑定按钮事件与 EventBus 信号
- `_ExitTree()` 取消所有 EventBus 订阅，防止泄漏
- `PerformPull` / `PerformMultiPull` 抛出 `InvalidOperationException` 时显示红色货币不足提示（2 秒后恢复）
- 抽卡期间禁用按钮（`Disabled = true`）
- 十连抽逐个展示结果，每个间隔 0.3 秒
- `PullAnimation` 即时创建 → 播放 → 释放（`QueueFree`）

### PullAnimation.cs — 抽卡结果展示动画

`scripts/gacha/ui/PullAnimation.cs`，命名空间 `Match3Demo`。展示单次抽卡结果的 Control，包含缩放入场动画、稀有度颜色卡片、稀有度辉光效果。

```csharp
using Godot;
using System.Threading.Tasks;

namespace Match3Demo;

public partial class PullAnimation : Control
{
    public async Task Play(string rewardId, PetRarity rarity)
    {
        var card = new ColorRect();
        card.Size = new Vector2(200, 280);
        card.Position = new Vector2(Size.X / 2 - 100, Size.Y / 2 - 140);
        card.Color = GetRarityColor(rarity);
        AddChild(card);
        
        var label = new Label();
        label.Text = $"{rarity}\n{rewardId}";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Size = card.Size;
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 18);
        card.AddChild(label);
        
        // Reveal animation: scale from 0 + glow pulse
        card.Scale = Vector2.Zero;
        var tween = CreateTween();
        tween.TweenProperty(card, "scale", new Vector2(1.1f, 1.1f), 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "scale", Vector2.One, 0.15f);
        
        // Rarity glow effect
        if (rarity >= PetRarity.Epic)
        {
            var glow = new RarityRevealEffect();
            AddChild(glow);
            glow.Play(card.GlobalPosition + card.Size / 2, rarity);
        }
        
        // Show for 1.5 seconds
        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
    }
    
    private static Color GetRarityColor(PetRarity rarity) => rarity switch
    {
        PetRarity.Common => new Color(0.5f, 0.5f, 0.5f),
        PetRarity.Rare => new Color(0.2f, 0.4f, 1.0f),
        PetRarity.Epic => new Color(0.6f, 0.2f, 1.0f),
        PetRarity.Legendary => new Color(1.0f, 0.65f, 0.0f),
        _ => Colors.White
    };
}
```

关键点：
- 卡片尺寸 200×280，居中显示
- 初始 Scale = Vector2.Zero → 1.1（overshoot）→ 1.0，使用 Back/Out 缓动产生弹性缩放效果
- `Epic` 及以上稀有度触发 `RarityRevealEffect` 辉光效果
- 卡片停留展示 1.5 秒
- `GetRarityColor` 为静态方法，按稀有度返回对应颜色：灰 / 蓝 / 紫 / 金

### RarityRevealEffect.cs — 稀有度揭示特效

`scripts/gacha/ui/RarityRevealEffect.cs`，命名空间 `Match3Demo`。Epic / Legendary 抽卡结果的特效 Control，包含扩散光晕与 Legendary 专属粒子线 + 屏幕震动。

```csharp
using Godot;

namespace Match3Demo;

public partial class RarityRevealEffect : Control
{
    public async void Play(Vector2 centerPosition, PetRarity rarity)
    {
        Color glowColor = rarity switch
        {
            PetRarity.Epic => new Color(0.6f, 0.2f, 1.0f, 0.3f),
            PetRarity.Legendary => new Color(1.0f, 0.65f, 0.0f, 0.4f),
            _ => Colors.Transparent
        };
        
        // Expanding glow circle
        var glowRect = new ColorRect();
        glowRect.Color = glowColor;
        glowRect.Size = new Vector2(20, 20);
        glowRect.PivotOffset = new Vector2(10, 10);
        glowRect.Position = centerPosition - new Vector2(10, 10);
        AddChild(glowRect);
        
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(glowRect, "size", new Vector2(400, 400), 0.8f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(glowRect, "position", centerPosition - new Vector2(200, 200), 0.8f);
        tween.TweenProperty(glowRect, "modulate:a", 0.0f, 0.8f);
        
        // Star particle lines (Legendary only)
        if (rarity == PetRarity.Legendary)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.Pi / 4;
                var line = new ColorRect();
                line.Color = new Color(1f, 0.8f, 0.2f, 0.8f);
                line.Size = new Vector2(100, 2);
                line.PivotOffset = new Vector2(0, 1);
                line.Position = centerPosition;
                line.Rotation = angle;
                AddChild(line);
                
                var lineTween = CreateTween();
                lineTween.TweenProperty(line, "size:x", 0f, 0.5f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }
        }
        
        // Screen shake for legendary
        if (rarity == PetRarity.Legendary)
        {
            EventBus.Instance.EmitSignal(
                EventBus.SignalName.ScreenShake, 6.0f, 0.3f);
        }
        
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        QueueFree();
    }
}
```

关键点：
- 扩散光晕：20×20 → 400×400，同时 alpha 从 0.3/0.4 → 0.0，使用 Cubic/Out 缓动
- Legendary 专属：8 条金色粒子线，从中心向外放射，每条约 100px 长，使用 Quad/In 缓动快速消失
- Legendary 专属：触发 `ScreenShake` 信号（强度 6.0，持续时间 0.3 秒）
- `Play` 为 `async void`，不阻塞调用方（特效独立播放）
- 特效结束后 `QueueFree()` 自清理

## 验收标准
- [ ] `GachaBannerUI` 正确从 `ServiceInitializer.Instance` 获取 `GachaDrawService`，空值安全（`?.`）
- [ ] 单抽按钮点击调用 `PerformPull(bannerId)`，十连抽按钮点击调用 `PerformMultiPull(bannerId, 10)`
- [ ] 货币不足时 `InvalidOperationException` 被捕获，显示 "Insufficient currency!" 红色提示（2 秒后恢复原文本与颜色）
- [ ] 抽卡期间两个按钮均 `Disabled = true`，动画完成后恢复
- [ ] `PullAnimation` 根据 `PetRarity` 显示不同颜色卡片（Common 灰 / Rare 蓝 / Epic 紫 / Legendary 金）
- [ ] `PullAnimation` 入场动画：Scale 从 0 弹跳到 1（Back/Out 缓动，overshoot 到 1.1）
- [ ] `Epic` 和 `Legendary` 稀有度触发 `RarityRevealEffect` 辉光特效
- [ ] `Legendary` 稀有度额外触发 8 条粒子线 + `ScreenShake` 信号
- [ ] 十连抽逐个展示结果，每个间隔 0.3 秒
- [ ] 所有 UI 在 `_ExitTree()` 中取消 EventBus 订阅（`GachaPullResult`, `GachaMultiPullResult`, `CurrencyChanged`）
- [ ] `RarityRevealEffect` 在动画结束后调用 `QueueFree()` 自清理
- [ ] `PullAnimation` 在 `GachaBannerUI.ShowPullAnimation()` 中实例化、播放、`QueueFree()`
- [ ] `dotnet build` 编译通过（0 错误，0 警告）
- [ ] 命名空间统一为 `Match3Demo`

## 注意
- 目录 `scripts/gacha/ui/` 为新增，需确保父目录 `scripts/gacha/` 已存在（由 Task 24 创建）
- 本 Task 依赖 Task 25（GachaDrawService）和 Task 16（EventBus 信号），确保这些 Task 已完成后再执行
- `GachaBannerUI` 通过 `.tscn` 场景文件绑定节点路径（`GetNode<...>(...)`），场景需手工在 Godot 编辑器中搭建
- `RarityRevealEffect` 使用 `ColorRect` 模拟粒子，非 Godot `GPUParticles2D`，保证纯代码可编译
- `ShowInsufficientCurrency` 的 `InvalidOperationException` 捕获约定来自 Task 25 的服务层定义
