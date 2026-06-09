using Godot;
using System;

namespace Match3Demo;

public enum PetMood { Walking, Idle, Sitting, Sleeping }

public partial class PetActor : Node2D
{
	private string _displayName = "";
	private PetNeeds? _needs;
	private bool _showBars;
	private PetMood _mood = PetMood.Idle;
	private float _moodTimer;
	private float _animTime;
	private float _bobY;

	private Tween? _walkTween;
	private Vector2 _targetPos;
	private Vector2 _areaMin;
	private Vector2 _areaMax;
	private System.Random _rng = new();
	private Sprite2D? _sprite;
	private bool _hasSheet;
	private float _sheetTimer;

	private const int SP_COLS = 3;
	private const int SP_ROWS = 3;
	private static readonly int[] SP_X = { 0, 341, 682 };
	private static readonly int[] SP_Y = { 0, 341, 682 };
	private static readonly int[] SP_W = { 341, 341, 342 };
	private static readonly int[] SP_H = { 341, 341, 342 };

	private static readonly int[][] MOOD_CELLS = {
		new[] { 1, 2 },      // Walking: happy, excited
		new[] { 0, 8 },      // Idle: neutral, blink
		new[] { 4 },         // Sitting
		new[] { 7 },         // Sleeping
	};

	private static readonly System.Collections.Generic.Dictionary<string, string> SHEET_PATH_MAP = new()
	{
		["cat_sleepy_01"] = "res://assets/textures/pets/cat_1_sheet.png",
		["cat_playful_02"] = "res://assets/textures/pets/cat_2_sheet.png",
		["dog_common_01"] = "res://assets/textures/pets/dog_1_sheet.png",
		["dog_happy_01"] = "res://assets/textures/pets/dog_1_sheet.png",
		["bunny_rare_01"] = "res://assets/textures/pets/bunny_1_sheet.png",
		["duck_common_01"] = "res://assets/textures/pets/duck_1_sheet.png",
	};

	[Signal] public delegate void PetClickedEventHandler(InputEvent inputEvent);
	public PetInstance? PetInstance { get; set; }
	public PetDefinition? PetDef { get; set; }

	public override void _Ready()
	{
		ZIndex = 100;
		Scale = new Vector2(1.0f, 1.0f);
		_sprite ??= GetNodeOrNull<Sprite2D>("Sprite");
		PickMood();
	}

	public void Setup(PetInstance pet, PetDefinition def, bool showBars = true)
	{
		_displayName = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : def.DisplayName;
		_needs = pet.Needs;
		_showBars = showBars;
		PetInstance = pet;
		PetDef = def;
		_hasSheet = false;

		_sprite ??= GetNodeOrNull<Sprite2D>("Sprite");

		if (_sprite != null)
		{
			var sheet = def.SpriteSheet ?? LoadSheetForPet(def.Id, def.SpriteSheetPath);
			GD.Print($"[PetActor] Setup pet={def.Id} sprite={_sprite != null} sheet={sheet != null} sprite_node={_sprite}");
			if (sheet != null)
			{
				_sprite.Texture = sheet;
				_sprite.RegionEnabled = true;
				_sprite.RegionRect = GetCellRect(0);
				_sprite.Visible = true;
				_hasSheet = true;
			}
			else
			{
				_sprite.Texture = null;
				_sprite.RegionEnabled = false;
				_sprite.Visible = false;
			}
		}
		QueueRedraw();
	}

	public void SetWalkArea(Vector2 min, Vector2 max) { _areaMin = min; _areaMax = max; }

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_animTime += dt;
		_moodTimer -= dt;
		if (_moodTimer <= 0) PickMood();

		switch (_mood)
		{
			case PetMood.Walking:
				if (_walkTween == null || !_walkTween.IsRunning()) WalkTo();
				_bobY = Mathf.Sin(_animTime * 7f) * 3f;
				break;
			case PetMood.Idle:
				_bobY = Mathf.Sin(_animTime * 3f) * 1.5f;
				break;
			default:
				_bobY = 0;
				break;
		}

		if (_hasSheet && _sprite != null)
		{
			int cell = GetSheetCell(dt);
			_sprite.RegionRect = GetCellRect(cell);
			if (_mood == PetMood.Walking)
				_sprite.FlipH = _targetPos.X < Position.X;
		}

		QueueRedraw();
	}

	private const int SP_INSET = 3;
	private const int SP_BOTTOM_CROP = 37;

	private static Rect2 GetCellRect(int index)
	{
		int col = index % SP_COLS;
		int row = index / SP_COLS;
		return new Rect2(
			SP_X[col] + SP_INSET,
			SP_Y[row] + SP_INSET,
			SP_W[col] - SP_INSET * 2,
			SP_H[row] - SP_INSET * 2 - SP_BOTTOM_CROP);
	}

	private int GetSheetCell(float dt)
	{
		switch (_mood)
		{
			case PetMood.Walking:
				_sheetTimer += dt;
				return ((int)(_sheetTimer * 6f) % 2) == 0 ? MOOD_CELLS[0][0] : MOOD_CELLS[0][1];
			case PetMood.Idle:
				return Mathf.Sin(_animTime * 0.7f) > 0.75f ? MOOD_CELLS[1][1] : MOOD_CELLS[1][0];
			default:
				return MOOD_CELLS[(int)_mood][0];
		}
	}

	private static Texture2D? LoadSheetForPet(string petId, string pathFromDef)
	{
		if (SHEET_PATH_MAP.TryGetValue(petId, out var path))
			return LoadTexture(path);
		if (!string.IsNullOrEmpty(pathFromDef))
			return LoadTexture(pathFromDef);
		return null;
	}

	private static Texture2D? LoadTexture(string path)
	{
		if (!ResourceLoader.Exists(path))
		{
			GD.PrintErr($"[PetActor] Resource not found: {path}");
			return null;
		}
		var tex = GD.Load<Texture2D>(path);
		GD.Print($"[PetActor] Loaded texture: {path} -> {(tex != null ? "OK" : "FAIL")}");
		return tex;
	}

	private void PickMood()
	{
		if (_needs != null && _needs.Energy < 25f) { _mood = PetMood.Sleeping; _moodTimer = _rng.Next(4, 8); _walkTween?.Kill(); return; }
		double r = _rng.NextDouble();
		if (r < 0.45f) { _mood = PetMood.Walking; _moodTimer = _rng.Next(2, 4); }
		else if (r < 0.75f) { _mood = PetMood.Sitting; _moodTimer = _rng.Next(2, 5); _walkTween?.Kill(); }
		else { _mood = PetMood.Idle; _moodTimer = _rng.Next(1, 3); _walkTween?.Kill(); }
	}

	private void WalkTo()
	{
		float x = (float)(_rng.NextDouble() * (_areaMax.X - _areaMin.X) + _areaMin.X);
		float y = (float)(_rng.NextDouble() * (_areaMax.Y - _areaMin.Y) + _areaMin.Y);
		_targetPos = new Vector2(x, y);
		float dist = Position.DistanceTo(_targetPos);
		_walkTween?.Kill();
		_walkTween = CreateTween();
		_walkTween.TweenProperty(this, "position", _targetPos, Mathf.Clamp(dist / 80f, 0.5f, 2.5f))
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_mood = PetMood.Walking;
	}

	public override void _Draw()
	{
		if (!_hasSheet) return;
		DrawHUD();
	}

	private void DrawHUD()
	{
		float baseY = 185f;
		var font = ThemeDB.FallbackFont;
		if (font != null)
		{
			DrawString(font, new Vector2(-50, baseY), _displayName, HorizontalAlignment.Center, 100, 14, Colors.White);
		}
		if (_showBars && _needs != null)
		{
			float by = baseY + 16;
			float avg = (_needs.Hunger + _needs.Happiness + _needs.Energy) / (PetNeeds.MaxValue * 3f);
			float w = 64, h = 5, bx = -w / 2f;
			DrawRect(new Rect2(bx - 1, by - 1, w + 2, h + 2), new Color(0, 0, 0, 0.5f));
			Color barColor = avg > 0.6f ? new Color(0.2f, 0.75f, 0.35f)
				: avg > 0.3f ? new Color(1f, 0.7f, 0.1f)
				: new Color(1f, 0.3f, 0.1f);
			DrawRect(new Rect2(bx, by, w * avg, h), barColor);
		}
	}
}
