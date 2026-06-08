using Godot;

namespace Match3Demo;

public partial class PetDetailPopup : Popup
{
    private PetInstance? _pet;
    private PetDefinition? _def;
    private IPetCollectionService? _petService;

    private TextureRect _icon = null!;
    private Label _nameLabel = null!;
    private Label _rarityLabel = null!;
    private Label _levelLabel = null!;
    private ProgressBar _xpBar = null!;
    private Label _xpLabel = null!;
    private Button _favoriteBtn = null!;
    private Button _evolveBtn = null!;
    private LineEdit _nicknameEdit = null!;

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

        float progress = _pet.NextLevelXP > 0 ? (float)_pet.CurrentXP / _pet.NextLevelXP : 1.0f;
        _xpBar.Value = _pet.IsMaxLevel(_def) ? 1.0 : Mathf.Min(progress, 1.0);
        _xpLabel.Text = _pet.IsMaxLevel(_def) ? "MAX" : $"{_pet.CurrentXP}/{_pet.NextLevelXP}";

        _favoriteBtn.Text = _pet.IsFavorite ? "\u2605 Unfavorite" : "\u2606 Favorite";
        _evolveBtn.Visible = _def.EvolutionChain?.Count > 0 && !_pet.IsMaxLevel(_def);
        _nicknameEdit.Text = _pet.Nickname ?? "";
    }

    private void ToggleFavorite()
    {
        if (_pet != null && _petService != null)
        {
            _petService.SetFavorite(_pet.Id, !_pet.IsFavorite);
            RefreshDisplay();
        }
    }

    private void OnEvolvePressed()
    {
        if (_pet != null && _petService?.TryEvolve(_pet.Id) == true)
        {
            var ds = ServiceInitializer.Instance?.GetService<IPetDataSource>();
            _def = ds?.GetPetDefinition(_pet.PetDefId);
            ShowLevelUpAnimation();
            RefreshDisplay();
        }
    }

    private void OnNicknameChanged(string newName)
    {
        if (_pet != null)
            _petService?.SetNickname(_pet.Id, newName);
    }

    private void ShowLevelUpAnimation()
    {
        var anim = new PetLevelUpAnimation();
        anim.Play(_icon?.GlobalPosition ?? Vector2.Zero);
        AddChild(anim);
    }
}
