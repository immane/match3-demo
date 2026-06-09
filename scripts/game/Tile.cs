using Godot;

namespace Match3Demo;

public partial class Tile : Node2D
{
	[Signal] public delegate void TileClickedEventHandler(Node2D tile, Vector2I boardPosition);

	private static readonly string[] CatTextures = new[]
	{
		"res://assets/textures/cats/cat_red.svg",
		"res://assets/textures/cats/cat_blue.svg",
		"res://assets/textures/cats/cat_green.svg",
		"res://assets/textures/cats/cat_yellow.svg",
		"res://assets/textures/cats/cat_purple.svg",
	};

	public BoardData.CellData TileData { get; set; }
	public Vector2I BoardPosition { get; set; } = new(-1, -1);
	public int VisualState { get; set; }

	private TextureRect _catTexture;
	private ColorRect _selectionBorder;
	private ColorRect _specialIcon;
	private ColorRect _glowEffect;
	private Area2D _clickArea;

	public override void _Ready()
	{
		_catTexture = GetNode<TextureRect>("CatTexture");
		_selectionBorder = GetNode<ColorRect>("SelectionBorder");
		_specialIcon = GetNode<ColorRect>("SpecialIcon");
		_glowEffect = GetNode<ColorRect>("GlowEffect");
		_clickArea = GetNode<Area2D>("ClickArea");

		_clickArea.InputPickable = false;
	}

	public void Setup(BoardData.CellData data)
	{
		TileData = data;
		if (data != null)
			BoardPosition = new Vector2I(data.Col, data.Row);
		Scale = Vector2.One;

		// Size the cat texture to match the current cell size
		int s = GridUtils.CellSize;
		_catTexture.OffsetLeft = -s / 2;
		_catTexture.OffsetTop = -s / 2;
		_catTexture.OffsetRight = s / 2;
		_catTexture.OffsetBottom = s / 2;
		_selectionBorder.OffsetLeft = -(s + 4) / 2;
		_selectionBorder.OffsetTop = -(s + 4) / 2;
		_selectionBorder.OffsetRight = (s + 4) / 2;
		_selectionBorder.OffsetBottom = (s + 4) / 2;
		_glowEffect.OffsetLeft = -(s + 8) / 2;
		_glowEffect.OffsetTop = -(s + 8) / 2;
		_glowEffect.OffsetRight = (s + 8) / 2;
		_glowEffect.OffsetBottom = (s + 8) / 2;

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
		_catTexture.Visible = true;
		int type = TileData.CrystalType;
		if (type >= 0 && type < CatTextures.Length)
			_catTexture.Texture = GD.Load<Texture2D>(CatTextures[type]);
		else
			_catTexture.Texture = GD.Load<Texture2D>(CatTextures[0]);

		if (TileData.IsSpecial())
			SetSpecialIcon(TileData.SpecialType);
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
	}

	public void Deselect()
	{
		VisualState = 0;
		Scale = Vector2.One;
		_selectionBorder.Hide();
		_glowEffect.Hide();
	}
}
