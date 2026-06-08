using Godot;

namespace Match3Demo;

public partial class PetSlot : Button
{
	private TextureRect _icon = null!;
	private Label _nameLabel = null!;
	private Label _levelLabel = null!;
	private ColorRect _rarityBorder = null!;
	private bool _built;

	public override void _Ready()
	{
		BuildUI();
	}

	private void BuildUI()
	{
		if (_built) return;
		_built = true;
		if (!HasNode("Icon"))
		{
			// Self-create UI when no .tscn present
			_rarityBorder = new ColorRect();
			_rarityBorder.Name = "RarityBorder";
			_rarityBorder.Size = CustomMinimumSize;
			_rarityBorder.Color = new Color(0.3f, 0.3f, 0.3f);
			AddChild(_rarityBorder);

			var vbox = new VBoxContainer();
			vbox.Alignment = BoxContainer.AlignmentMode.Center;
			vbox.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(vbox);

			_icon = new TextureRect();
			_icon.Name = "Icon";
			_icon.CustomMinimumSize = new Vector2(60, 60);
			_icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
			vbox.AddChild(_icon);

			_nameLabel = new Label();
			_nameLabel.Name = "NameLabel";
			_nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_nameLabel.AddThemeFontSizeOverride("font_size", 16);
			vbox.AddChild(_nameLabel);

			_levelLabel = new Label();
			_levelLabel.Name = "LevelLabel";
			_levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_levelLabel.AddThemeFontSizeOverride("font_size", 14);
			vbox.AddChild(_levelLabel);
		}
		else
		{
			_icon = GetNode<TextureRect>("Icon");
			_nameLabel = GetNode<Label>("NameLabel");
			_levelLabel = GetNode<Label>("LevelLabel");
			_rarityBorder = GetNode<ColorRect>("RarityBorder");
		}
	}

	public void SetPet(PetInstance pet, PetDefinition def)
	{
		BuildUI();
		_nameLabel.Text = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : def.DisplayName;
		_levelLabel.Text = $"Lv.{pet.Level}";
		var color = GetRarityColor(def.Rarity);
		_rarityBorder.Color = color;
		// Make border size match the slot
		_rarityBorder.Size = Size;
	}

	private static Color GetRarityColor(PetRarity rarity)
	{
		return rarity switch
		{
			PetRarity.Common => new Color(0.6f, 0.6f, 0.6f),
			PetRarity.Rare => new Color(0.2f, 0.5f, 1.0f),
			PetRarity.Epic => new Color(0.7f, 0.2f, 1.0f),
			PetRarity.Legendary => new Color(1.0f, 0.7f, 0.1f),
			_ => Colors.White
		};
	}
}
