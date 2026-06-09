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
	private string _activeBannerId = "";

	public override void _Ready()
	{
		_bannerNameLabel = GetNode<Label>("BannerPanel/BannerVBox/BannerNameLabel");
		_pullsUntilGuaranteeLabel = GetNode<Label>("BannerPanel/BannerVBox/PityLabel");
		_costLabel = GetNode<Label>("BannerPanel/BannerVBox/CostLabel");
		_pullOnceBtn = GetNode<Button>("ButtonContainer/PullOnceBtn");
		_pullMultiBtn = GetNode<Button>("ButtonContainer/PullMultiBtn");
		_resultContainer = GetNode<Control>("ResultContainer");

		_pullOnceBtn.Pressed += OnPullOnce;
		_pullMultiBtn.Pressed += OnPullMulti;
		GetNode<Button>("CloseButton").Pressed += () =>
		{
			var m = GetNode("/root/Main") as Main;
			m?.HideGachaUI();
		};

		_gachaService = ServiceInitializer.Instance?.GetService<GachaDrawService>();
		_bannerDataSource = ServiceInitializer.Instance?.GetService<IDataSource<GachaBanner>>();

		// Fallback: create services directly if DI failed
		if (_gachaService == null)
		{
			GD.Print("[GachaBannerUI] ServiceInitializer failed, creating fallback services");
			var storage = new GodotFileStorage();
			var currency = new CurrencyService(storage);
			var roller = new GachaRollService();
			var banners = new GachaBannerDataSource();
			var petDs = new ResourcePetDataSource();
			var pets = new PetCollectionService(petDs, EventBus.Instance, storage);
			var pity = new GachaPityTracker(storage);
			_gachaService = new GachaDrawService(currency, roller, banners, pets, EventBus.Instance, pity);
			_bannerDataSource = banners;
		}

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
		_costLabel.Text = $"Cost: {banner.CostPerPull} coins";
		UpdatePity();
	}

	private void UpdatePity()
	{
		int left = _gachaService?.GetPullsUntilGuarantee(_activeBannerId) ?? -1;
		_pullsUntilGuaranteeLabel.Text = left > 0 ? $"Pulls until SSR: {left}" : "";
	}

	private void OnPullOnce()
	{
		try { _gachaService?.PerformPull(_activeBannerId); }
		catch (InvalidOperationException) { ShowError("Not enough coins!"); }
		catch (Exception e) { ShowError(e.Message); }
	}

	private void OnPullMulti()
	{
		try { _gachaService?.PerformMultiPull(_activeBannerId, 10); }
		catch (InvalidOperationException) { ShowError("Not enough coins!"); }
		catch (Exception e) { ShowError(e.Message); }
	}

	private async void ShowError(string msg)
	{
		_costLabel.Text = msg;
		_costLabel.AddThemeColorOverride("font_color", Colors.Red);
		await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
		LoadBanner(_activeBannerId);
	}

	private async void OnPullResult(string rewardId, int rarity, Godot.Collections.Dictionary pity)
	{
		_pullOnceBtn.Disabled = true;
		_pullMultiBtn.Disabled = true;
		await PlayAnim(rewardId, (PetRarity)rarity);
		_pullOnceBtn.Disabled = false;
		_pullMultiBtn.Disabled = false;
		UpdatePity();
	}

	private async void OnMultiPullResult(Godot.Collections.Array results)
	{
		_pullOnceBtn.Disabled = true;
		_pullMultiBtn.Disabled = true;
		foreach (Godot.Collections.Dictionary r in results)
		{
			await PlayAnim((string)r["rewardId"], (PetRarity)(int)r["rarity"]);
			await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
		}
		_pullOnceBtn.Disabled = false;
		_pullMultiBtn.Disabled = false;
		UpdatePity();
	}

	private async Task PlayAnim(string rewardId, PetRarity rarity)
	{
		var card = new ColorRect();
		card.Color = rarity switch
		{
			PetRarity.Common => new Color(0.4f, 0.4f, 0.4f),
			PetRarity.Rare => new Color(0.15f, 0.35f, 0.9f),
			PetRarity.Epic => new Color(0.6f, 0.15f, 0.9f),
			PetRarity.Legendary => new Color(1f, 0.55f, 0.02f),
			_ => Colors.Gray
		};
		card.Size = new Vector2(240, 320);
		card.Position = new Vector2((Size.X - 240) / 2, (Size.Y - 320) / 2);
		card.Scale = Vector2.Zero;
		_resultContainer.AddChild(card);

		var label = new Label();
		label.Text = $"{rarity}\n{rewardId}";
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.Size = card.Size;
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeFontSizeOverride("font_size", 22);
		card.AddChild(label);

		var t = CreateTween();
		t.TweenProperty(card, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		await ToSignal(t, Tween.SignalName.Finished);
		await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
		card.QueueFree();
	}

	private void OnCurrencyChanged(string currencyId, int newBalance, int delta) => UpdatePity();
}
