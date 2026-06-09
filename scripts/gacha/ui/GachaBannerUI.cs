using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Match3Demo;

public partial class GachaBannerUI : Control
{
	[Export] public string DefaultBannerId { get; set; } = "standard_banner";

	private Label _bannerName = null!;
	private ProgressBar _pityBar = null!;
	private Label _pityText = null!;
	private Label _coinLabel = null!;
	private Button _pullOnceBtn = null!;
	private Button _pullMultiBtn = null!;
	private Control _resultContainer = null!;

	private GachaDrawService? _gachaService;
	private IDataSource<GachaBanner>? _bannerDs;
	private string _bid = "";

	public override void _Ready()
	{
		_bannerName = GetNode<Label>("MainArea/BannerCard/BannerVBox/BannerName");
		_pityBar = GetNode<ProgressBar>("MainArea/BannerCard/BannerVBox/PityRow/PityBar");
		_pityText = GetNode<Label>("MainArea/BannerCard/BannerVBox/PityRow/PityText");
		_coinLabel = GetNode<Label>("HeaderPanel/HeaderBox/CoinBadge/CoinLabel");
		_pullOnceBtn = GetNode<Button>("ButtonArea/PullOnceBtn");
		_pullMultiBtn = GetNode<Button>("ButtonArea/PullMultiBtn");
		_resultContainer = GetNode<Control>("ResultContainer");

		// Style
		StyleButton(_pullOnceBtn, new Color(0.15f, 0.4f, 0.85f));
		StyleButton(_pullMultiBtn, new Color(0.75f, 0.25f, 0.85f));
		var pityFill = new StyleBoxFlat { BgColor = new Color(1f, 0.6f, 0.05f) };
		_pityBar.AddThemeStyleboxOverride("fill", pityFill);
		_pityBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.15f) });

		var headerPanel = GetNode<PanelContainer>("HeaderPanel");
		var headerStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.16f, 0.95f), CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
		headerPanel.AddThemeStyleboxOverride("panel", headerStyle);

		_pullOnceBtn.Pressed += () => Pull(1);
		_pullMultiBtn.Pressed += () => Pull(10);
		GetNode<Button>("HeaderPanel/HeaderBox/CloseButton").Pressed += () =>
			(GetNode("/root/Main") as Main)?.HideGachaUI();

		_gachaService = ServiceInitializer.Instance?.GetService<GachaDrawService>();
		_bannerDs = ServiceInitializer.Instance?.GetService<IDataSource<GachaBanner>>();

		if (_gachaService == null)
		{
			var s = new GodotFileStorage();
			var c = new CurrencyService(s);
			var r = new GachaRollService();
			var b = new GachaBannerDataSource();
			var d = new ResourcePetDataSource();
			var p = new PetCollectionService(d, EventBus.Instance, s);
			var t = new GachaPityTracker(s);
			_gachaService = new GachaDrawService(c, r, b, p, EventBus.Instance, t);
			_bannerDs = b;
		}

		EventBus.Instance.GachaPullResult += OnResult;
		EventBus.Instance.GachaMultiPullResult += OnMultiResult;
		EventBus.Instance.CurrencyChanged += OnCoinChanged;

		Refresh();
	}

	public override void _ExitTree()
	{
		EventBus.Instance.GachaPullResult -= OnResult;
		EventBus.Instance.GachaMultiPullResult -= OnMultiResult;
		EventBus.Instance.CurrencyChanged -= OnCoinChanged;
	}

	public void Refresh()
	{
		var banner = (_bannerDs as GachaBannerDataSource)?.GetOrCreateDefault(DefaultBannerId) ?? _bannerDs?.Get(DefaultBannerId);
		if (banner == null) return;
		_bid = banner.Id;
		_bannerName.Text = banner.DisplayName;
		UpdateCoin();
		UpdatePity();
	}

	private void UpdateCoin()
	{
		var c = ServiceInitializer.Instance?.GetService<ICurrencyService>();
		_coinLabel.Text = $"💰 {(c != null ? c.GetBalance("soft_currency") : 0)}";
	}

	private void UpdatePity()
	{
		var b = _bannerDs?.Get(_bid);
		if (b == null) return;
		int left = _gachaService?.GetPullsUntilGuarantee(_bid) ?? 0;
		float prog = 1f - (float)left / b.HardPityGuarantee;
		_pityBar.Value = prog * b.HardPityGuarantee;
		_pityText.Text = left > 0 ? $"SSR in {left} pulls" : "SSR guaranteed next!";
		if (left <= 10) _pityText.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0));
	}

	private void Pull(int count)
	{
		try
		{
			if (count == 1) _gachaService?.PerformPull(_bid);
			else _gachaService?.PerformMultiPull(_bid, count);
		}
		catch (InvalidOperationException) { FlashCoin(); }
		catch (Exception e) { GD.PrintErr($"[Gacha] {e.Message}"); }
	}

	private async void FlashCoin()
	{
		_coinLabel.AddThemeColorOverride("font_color", Colors.Red);
		await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
		UpdateCoin();
	}

	private async void OnResult(string rewardId, int rarity, Godot.Collections.Dictionary pity)
	{
		SetButtons(false);
		await ShowCard(rewardId, (PetRarity)rarity, false);
		SetButtons(true);
		UpdatePity();
		UpdateCoin();
	}

	private async void OnMultiResult(Godot.Collections.Array results)
	{
		SetButtons(false);
		foreach (Godot.Collections.Dictionary r in results)
		{
			var id = (string)r["rewardId"];
			var rarity = (PetRarity)(int)r["rarity"];
			bool skip = results.IndexOf(r) < results.Count - 1;
			await ShowCard(id, rarity, skip);
		}
		SetButtons(true);
		UpdatePity();
		UpdateCoin();
	}

	private void SetButtons(bool enabled)
	{
		_pullOnceBtn.Disabled = !enabled;
		_pullMultiBtn.Disabled = !enabled;
	}

	private async Task ShowCard(string rewardId, PetRarity rarity, bool fast)
	{
		var colors = new Dictionary<PetRarity, Color>
		{
			[PetRarity.Common] = new(0.4f, 0.4f, 0.4f),
			[PetRarity.Rare] = new(0.15f, 0.38f, 0.9f),
			[PetRarity.Epic] = new(0.65f, 0.15f, 0.9f),
			[PetRarity.Legendary] = new(1f, 0.5f, 0.02f),
		};

		var bg = new ColorRect();
		bg.Color = new Color(0, 0, 0, 0.7f);
		bg.Size = Size;
		_resultContainer.AddChild(bg);

		var card = new PanelContainer();
		card.Size = new Vector2(260, 340);
		card.Position = new Vector2((Size.X - 260) / 2, (Size.Y - 340) / 2 - 40);
		card.PivotOffset = new Vector2(130, 170);
		card.Scale = Vector2.Zero;
		card.Modulate = Colors.Transparent;
		_resultContainer.AddChild(card);

		var glow = new ColorRect();
		glow.Color = colors[rarity];
		glow.Size = new Vector2(268, 348);
		glow.Position = new Vector2(-4, -4);
		card.AddChild(glow);

		var inner = new ColorRect();
		inner.Color = new Color(0.12f, 0.12f, 0.18f);
		inner.Size = new Vector2(260, 340);
		card.AddChild(inner);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddThemeConstantOverride("separation", 8);
		card.AddChild(vbox);

		var stars = rarity switch
		{
			PetRarity.Legendary => "★★★★★",
			PetRarity.Epic => "★★★★",
			PetRarity.Rare => "★★★",
			_ => "★★"
		};
		var starLabel = new Label();
		starLabel.Text = stars;
		starLabel.HorizontalAlignment = HorizontalAlignment.Center;
		starLabel.AddThemeFontSizeOverride("font_size", 28);
		starLabel.AddThemeColorOverride("font_color", colors[rarity]);
		vbox.AddChild(starLabel);

		var nameLabel = new Label();
		nameLabel.Text = rewardId.Replace("_", " ");
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(nameLabel);

		var rarityLabel = new Label();
		rarityLabel.Text = rarity.ToString().ToUpper();
		rarityLabel.HorizontalAlignment = HorizontalAlignment.Center;
		rarityLabel.AddThemeFontSizeOverride("font_size", 16);
		rarityLabel.AddThemeColorOverride("font_color", colors[rarity]);
		vbox.AddChild(rarityLabel);

		// Animate
		var t = CreateTween();
		t.SetParallel(true);
		t.TweenProperty(bg, "modulate:a", 1f, 0.2f).From(0);
		t.TweenProperty(card, "modulate:a", 1f, 0.3f);
		t.TweenProperty(card, "scale", new Vector2(1.05f, 1.05f), 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(card, "position:y", card.Position.Y - 20, 0.35f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		await ToSignal(t, Tween.SignalName.Finished);

		if (!fast)
		{
			var settle = CreateTween();
			settle.TweenProperty(card, "scale", Vector2.One, 0.15f);
			await ToSignal(settle, Tween.SignalName.Finished);
			await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
		}
		else
		{
			await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
		}

		t = CreateTween();
		t.SetParallel(true);
		t.TweenProperty(bg, "modulate:a", 0f, 0.2f);
		t.TweenProperty(card, "modulate", Colors.Transparent, 0.15f);
		await ToSignal(t, Tween.SignalName.Finished);
		bg.QueueFree();
		card.QueueFree();
	}

	private void OnCoinChanged(string id, int balance, int delta) => UpdateCoin();

	private static void StyleButton(Button btn, Color bg)
	{
		var normal = new StyleBoxFlat { BgColor = bg, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
		var hover = new StyleBoxFlat { BgColor = bg.Lightened(0.15f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
		var pressed = new StyleBoxFlat { BgColor = bg.Darkened(0.15f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", pressed);
		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.AddThemeColorOverride("font_hover_color", Colors.White);
		var disabled = new StyleBoxFlat { BgColor = new Color(0.3f, 0.3f, 0.3f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
		btn.AddThemeStyleboxOverride("disabled", disabled);
	}
}
