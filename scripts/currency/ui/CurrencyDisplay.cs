using Godot;

namespace Match3Demo;

public partial class CurrencyDisplay : Control
{
    [Export] public string CurrencyId { get; set; } = "soft_currency";
    [Export] public Texture2D? CurrencyIcon { get; set; }

    private Label _amountLabel = null!;
    private TextureRect _iconRect = null!;
    private ICurrencyService? _currencyService;
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
        AnimateUpdate(newBalance);
    }

    private async void AnimateUpdate(int newBalance)
    {
        var tween = CreateTween();
        int oldAmount = _displayedAmount;
        float duration = Mathf.Min(0.5f, Mathf.Abs(newBalance - oldAmount) * 0.02f);

        tween.TweenMethod(
            Callable.From<int>((int value) =>
            {
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
