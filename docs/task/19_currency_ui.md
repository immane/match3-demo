# Task 19: CurrencyDisplay UI 控件

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/currency_system.md](../design/currency_system.md) §8 — CurrencyDisplay 控件 + Tween 动画 |

## 状态
- [x] 已完成

## 依赖
- Task 16 (EventBus CurrencyChanged 信号)
- Task 17 (ServiceInitializer 提供 ICurrencyService)
- Task 18 (ICurrencyService 实现)

## 产出文件
```
scripts/currency/ui/CurrencyDisplay.cs  [新增]
```

## 实现要求

### CurrencyDisplay.cs

一个 `Control` 派生类，显示指定货币类型的图标与余额，通过 EventBus 自动实时更新，数值变化带 Tween 动画。

```csharp
using Godot;
using System.Threading.Tasks;

namespace Match3Demo;

public partial class CurrencyDisplay : Control
{
    [Export] public string CurrencyId { get; set; } = "soft_currency";
    [Export] public Texture2D CurrencyIcon { get; set; }

    private Label _amountLabel;
    private TextureRect _iconRect;
    private ICurrencyService _currencyService;
    private int _displayedAmount;

    public override void _Ready()
    {
        _amountLabel = GetNode<Label>("AmountLabel");
        _iconRect = GetNode<TextureRect>("Icon");

        if (CurrencyIcon != null)
            _iconRect.Texture = CurrencyIcon;

        _currencyService = ServiceInitializer.Instance?.GetService<ICurrencyService>();

        _displayedAmount = _currencyService?.GetBalance(CurrencyId) ?? 0;
        _amountLabel.Text = FormatAmount(_displayedAmount);

        EventBus.Instance.CurrencyChanged += OnCurrencyChanged;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.CurrencyChanged -= OnCurrencyChanged;
    }

    private void OnCurrencyChanged(string currencyId, int newBalance, int delta)
    {
        if (currencyId != CurrencyId) return;
        _ = AnimateUpdate(newBalance);
    }

    private async Task AnimateUpdate(int newBalance)
    {
        var tween = CreateTween();
        int oldAmount = _displayedAmount;
        float duration = Mathf.Min(0.5f, Mathf.Abs(newBalance - oldAmount) * 0.02f);

        tween.TweenMethod(
            Callable.From<int>((int value) => {
                _displayedAmount = value;
                _amountLabel.Text = FormatAmount(value);
            }),
            oldAmount, newBalance, duration
        );

        await ToSignal(tween, Tween.SignalName.Finished);
        _displayedAmount = newBalance;
        _amountLabel.Text = FormatAmount(newBalance);
    }

    private static string FormatAmount(int amount)
    {
        if (amount >= 1_000_000)
            return $"{(amount / 1_000_000f):F1}M";
        if (amount >= 1_000)
            return $"{(amount / 1_000f):F1}K";
        return amount.ToString("N0");
    }
}
```

### 场景结构（备注: 需在 Godot 编辑器中创建）

```
CurrencyDisplay (Control)
├── Icon (TextureRect)       — 货币图标，锚点左侧
├── AmountLabel (Label)      — 金额文本，锚点右侧
└── PlusAnimation (Label)    — +X 动画（可选）
```

## 验收标准
- CurrencyDisplay 正确从 ServiceInitializer 获取 ICurrencyService
- 初始显示当前余额
- EventBus.CurrencyChanged 触发时正确过滤 CurrencyId 并更新显示
- 数值变化有 Tween 动画过渡
- `_ExitTree()` 中正确取消事件订阅
- FormatAmount 正确处理 K/M 缩写
- `dotnet build` 0 errors 0 warnings

## 注意
- `AnimateUpdate` 使用 `async Task` + `await ToSignal()` 等待 Tween 完成
- Tween duration 根据差值动态计算，最小 0.5s 上限，大额变化更快
- `N0` 格式化保证千位分隔符显示（如 `1,234`）
- `PlusAnimation` 节点为可选，本次任务不强制实现其弹出动画逻辑
