using Godot;
using System;
using System.Threading.Tasks;

namespace Match3Demo;

public partial class GachaBannerUI : Control
{
	[Export] public string DefaultBannerId { get; set; } = "standard_banner";

	private Label _bannerNameLabel = null!;
	private Label _pullsUntilGuaranteeLabel = null!;
	private Label _costLabel = null!;
	private Button _pullOnceBtn = null!;
	private Button _pullMultiBtn = null!;
	private Control _resultContainer = null!;

	private GachaDrawService? _gachaService;
	private IDataSource<GachaBanner>? _bannerDataSource;
	private ICurrencyService? _currencyService;
	private string _activeBannerId = "";

	public override void _Ready()
	{
		if (!HasNode("BannerPanel"))
		{
			Size = GetViewport().GetVisibleRect().Size;
			MouseFilter = MouseFilterEnum.Stop;

			var bg = new ColorRect();
			bg.Color = new Color(0.1f, 0.1f, 0.2f, 1.0f);
			bg.Size = Size;
			AddChild(bg);

			var bannerPanel = new Panel();
			bannerPanel.Name = "BannerPanel";
			bannerPanel.Position = new Vector2(40, 100);
			bannerPanel.Size = new Vector2(Size.X - 80, 150);
			AddChild(bannerPanel);

			var bannerVBox = new VBoxContainer();
			bannerVBox.Position = new Vector2(20, 20);
			bannerVBox.Size = new Vector2(bannerPanel.Size.X - 40, 100);
			bannerPanel.AddChild(bannerVBox);

			_bannerNameLabel = new Label();
			_bannerNameLabel.Name = "BannerNameLabel";
			_bannerNameLabel.AddThemeFontSizeOverride("font_size", 32);
			_bannerNameLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
			bannerVBox.AddChild(_bannerNameLabel);

			_pullsUntilGuaranteeLabel = new Label();
			_pullsUntilGuaranteeLabel.Name = "PityLabel";
			_pullsUntilGuaranteeLabel.AddThemeFontSizeOverride("font_size", 20);
			bannerVBox.AddChild(_pullsUntilGuaranteeLabel);

			_costLabel = new Label();
			_costLabel.Name = "CostLabel";
			_costLabel.AddThemeFontSizeOverride("font_size", 22);
			bannerVBox.AddChild(_costLabel);

			var btnContainer = new HBoxContainer();
			btnContainer.Name = "ButtonContainer";
			btnContainer.Position = new Vector2(40, 280);
			AddChild(btnContainer);

			_pullOnceBtn = new Button();
			_pullOnceBtn.Name = "PullOnceBtn";
			_pullOnceBtn.Text = "Pull x1";
			_pullOnceBtn.CustomMinimumSize = new Vector2(180, 60);
			_pullOnceBtn.AddThemeFontSizeOverride("font_size", 24);
			btnContainer.AddChild(_pullOnceBtn);

			_pullMultiBtn = new Button();
			_pullMultiBtn.Name = "PullMultiBtn";
			_pullMultiBtn.Text = "Pull x10";
			_pullMultiBtn.CustomMinimumSize = new Vector2(180, 60);
			_pullMultiBtn.AddThemeFontSizeOverride("font_size", 24);
			btnContainer.AddChild(_pullMultiBtn);

			_resultContainer = new Control();
			_resultContainer.Name = "ResultContainer";
			_resultContainer.Size = Size;
			_resultContainer.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(_resultContainer);

			var closeBtn = new Button();
			closeBtn.Text = "X Close";
			closeBtn.AddThemeFontSizeOverride("font_size", 28);
			closeBtn.Position = new Vector2(Size.X - 120, 20);
			closeBtn.Size = new Vector2(100, 50);
			closeBtn.Pressed += () => {
				var m = GetTree().GetFirstNodeInGroup("main") as Main;
				m?.HideGachaUI();
				var ts = GetTree().GetFirstNodeInGroup("title_screen") as Control;
				ts?.Show();
			};
			AddChild(closeBtn);
		}
		else
		{
			_bannerNameLabel = GetNode<Label>("BannerPanel/BannerNameLabel");
			_pullsUntilGuaranteeLabel = GetNode<Label>("BannerPanel/PityLabel");
			_costLabel = GetNode<Label>("BannerPanel/CostLabel");
			_pullOnceBtn = GetNode<Button>("ButtonContainer/PullOnceBtn");
			_pullMultiBtn = GetNode<Button>("ButtonContainer/PullMultiBtn");
			_resultContainer = GetNode<Control>("ResultContainer");
		}

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
		var banner = (_bannerDataSource as GachaBannerDataSource)?.GetOrCreateDefault(bannerId) ?? _bannerDataSource?.Get(bannerId);
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
		try
		{
			_gachaService?.PerformPull(_activeBannerId);
		}
		catch (InvalidOperationException)
		{
			ShowInsufficientCurrency();
		}
	}

	private void OnPullMulti()
	{
		try
		{
			_gachaService?.PerformMultiPull(_activeBannerId, 10);
		}
		catch (InvalidOperationException)
		{
			ShowInsufficientCurrency();
		}
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

		card.Scale = Vector2.Zero;
		var tween = CreateTween();
		tween.TweenProperty(card, "scale", new Vector2(1.1f, 1.1f), 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(card, "scale", Vector2.One, 0.15f);

		if (rarity >= PetRarity.Epic)
		{
			var glow = new RarityRevealEffect();
			AddChild(glow);
			glow.Play(card.GlobalPosition + card.Size / 2, rarity);
		}

		await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
	}

	private static Color GetRarityColor(PetRarity rarity)
	{
		return rarity switch
		{
			PetRarity.Common => new Color(0.5f, 0.5f, 0.5f),
			PetRarity.Rare => new Color(0.2f, 0.4f, 1.0f),
			PetRarity.Epic => new Color(0.6f, 0.2f, 1.0f),
			PetRarity.Legendary => new Color(1.0f, 0.65f, 0.0f),
			_ => Colors.White
		};
	}
}
