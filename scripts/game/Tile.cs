using Godot;

namespace Match3Demo;

public partial class Tile : Node2D
{
    [Signal] public delegate void TileClickedEventHandler(Node2D tile, Vector2I boardPosition);

    private static readonly Color[] CrystalColors = new Color[]
    {
        new("ff4466"),
        new("4488ff"),
        new("44dd66"),
        new("ffcc33"),
        new("cc44ff"),
    };

    public BoardData.CellData TileData { get; set; }
    public Vector2I BoardPosition { get; set; } = new(-1, -1);
    public int VisualState { get; set; }

    private ColorRect _crystalRect;
    private ColorRect _selectionBorder;
    private ColorRect _specialIcon;
    private ColorRect _glowEffect;
    private Area2D _clickArea;
    private ShaderMaterial _shaderMaterial;

    public override void _Ready()
    {
        _crystalRect = GetNode<ColorRect>("CrystalRect");
        _selectionBorder = GetNode<ColorRect>("SelectionBorder");
        _specialIcon = GetNode<ColorRect>("SpecialIcon");
        _glowEffect = GetNode<ColorRect>("GlowEffect");
        _clickArea = GetNode<Area2D>("ClickArea");
        _shaderMaterial = (ShaderMaterial)_crystalRect.Material;

        _shaderMaterial = (ShaderMaterial)_shaderMaterial.Duplicate();
        _crystalRect.Material = _shaderMaterial;

        _clickArea.InputPickable = false;
    }

    public void Setup(BoardData.CellData data)
    {
        TileData = data;
        if (data != null)
        {
            BoardPosition = new Vector2I(data.Col, data.Row);
        }
        Scale = Vector2.One;
        UpdateAppearance();
        VisualState = 0;
        _selectionBorder.Hide();
        _specialIcon.Hide();
        _glowEffect.Hide();
    }

    private void UpdateAppearance()
    {
        if (TileData == null || TileData.IsEmpty)
        {
            Hide();
            return;
        }

        Show();
        Color color = TileData.CrystalType >= 0 && TileData.CrystalType < CrystalColors.Length
            ? CrystalColors[TileData.CrystalType]
            : new Color(1, 1, 1);
        _shaderMaterial.SetShaderParameter("crystal_color", color);
        _shaderMaterial.SetShaderParameter("is_special", TileData.IsSpecial());
        _shaderMaterial.SetShaderParameter("special_type", TileData.SpecialType);

        if (TileData.IsSpecial())
        {
            SetSpecialIcon(TileData.SpecialType);
        }
    }

    private void SetSpecialIcon(int special)
    {
        _specialIcon.Show();
        switch (special)
        {
            case 0:
                _specialIcon.Color = new Color(1.0f, 0.4f, 0.2f);
                break;
            case 1:
                _specialIcon.Color = new Color(1, 1, 1);
                break;
            case 2:
                _specialIcon.Color = new Color(0.2f, 0.8f, 1.0f);
                break;
            default:
                _specialIcon.Hide();
                break;
        }
    }

    public void Select()
    {
        VisualState = 1;
        Scale = new Vector2(1.15f, 1.15f);
        _selectionBorder.Show();
        _glowEffect.Show();
        _shaderMaterial.SetShaderParameter("is_selected", true);
    }

    public void Deselect()
    {
        VisualState = 0;
        Scale = Vector2.One;
        _selectionBorder.Hide();
        _glowEffect.Hide();
        _shaderMaterial.SetShaderParameter("is_selected", false);
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_shaderMaterial))
        {
            _shaderMaterial.SetShaderParameter("time_sec", Time.GetTicksMsec() / 1000.0f);
        }
    }
}
