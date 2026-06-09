using Godot;
using System;

namespace Match3Demo;

public enum PetMood { Walking, Idle, Sitting, Sleeping }

public partial class PetActor : Node2D
{
	private string _displayName = "";
	private PetRarity _rarity;
	private PetNeeds? _needs;
	private Color _bodyColor = Colors.Gray;
	private Color _accentColor = Colors.White;
	private Color _earInner = new(1f, 0.7f, 0.75f);
	private Color _blushColor = new(1f, 0.5f, 0.5f, 0.25f);
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
	private bool _hasSprite;

	[Signal] public delegate void PetClickedEventHandler(InputEvent inputEvent);
	public PetInstance? PetInstance { get; set; }
	public PetDefinition? PetDef { get; set; }

	public override void _Ready()
	{
		ZIndex = 100;
		Scale = new Vector2(1.5f, 1.5f);
		_sprite = GetNodeOrNull<Sprite2D>("Sprite");
		_hasSprite = _sprite != null;
		PickMood();
	}

	public void Setup(PetInstance pet, PetDefinition def, bool showBars = true)
	{
		_displayName = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : def.DisplayName;
		_rarity = def.Rarity;
		_needs = pet.Needs;
		_showBars = showBars;
		PetInstance = pet;
		PetDef = def;

		(_bodyColor, _accentColor) = GetRarityColors(_rarity);

		if (_sprite != null && def.SpriteSheet != null)
		{
			_sprite.Texture = def.SpriteSheet;
			_sprite.Hframes = Math.Max(1, def.FrameCount);
			_sprite.Visible = true;
			_hasSprite = true;
		}
		else
		{
			if (_sprite != null) _sprite.Visible = false;
			_hasSprite = false;
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

		if (_hasSprite && _sprite != null && _sprite.Hframes > 1 && _mood == PetMood.Walking)
			_sprite.Frame = (int)(_animTime * 6f) % _sprite.Hframes;

		QueueRedraw();
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
		if (_hasSprite) return;
		float y = _bobY;
		var font = ThemeDB.FallbackFont;

		if (_mood == PetMood.Sleeping) DrawSleeping(y);
		else if (_mood == PetMood.Sitting) DrawSitting(y);
		else DrawStanding(y);

		if (font != null)
		{
			float ny = _mood == PetMood.Sleeping ? y - 20 : (_mood == PetMood.Sitting ? y - 32 : y - 38);
			DrawString(font, new Vector2(-50, ny), _displayName, HorizontalAlignment.Center, 100, 14, Colors.White);
		}

		if (_showBars && _needs != null)
		{
			float by = _mood == PetMood.Sleeping ? y - 32 : (_mood == PetMood.Sitting ? y - 44 : y - 50);
			float w = 56, h = 4, bx = -w / 2f;
			Bar(bx, by, w * _needs.Hunger / PetNeeds.MaxValue, h, new Color(1f, 0.3f, 0.1f));
			Bar(bx, by + 6, w * _needs.Happiness / PetNeeds.MaxValue, h, new Color(1f, 0.65f, 0.1f));
			Bar(bx, by + 12, w * _needs.Energy / PetNeeds.MaxValue, h, new Color(0.2f, 0.6f, 1f));
		}
	}

	private void DrawStanding(float y)
	{
		float r = 22f;
		DrawEllipse(new Vector2(0, y + r + 4), r * 1.1f, 5f, new Color(0, 0, 0, 0.1f));
		DrawCircle(new Vector2(0, y + 6), r - 2, _bodyColor);
		DrawCircle(new Vector2(0, y + 6), r - 2, _accentColor, false, 2f);
		DrawCircle(new Vector2(0, y - 8), r - 2, _bodyColor);
		DrawCircle(new Vector2(0, y - 8), r - 2, _accentColor, false, 2f);

		Triangle(-16, y - 22, -8, y - 30, -3, y - 20, _bodyColor);
		Triangle(16, y - 22, 8, y - 30, 3, y - 20, _bodyColor);
		Triangle(-13, y - 23, -9, y - 27, -5, y - 21, _earInner);
		Triangle(13, y - 23, 9, y - 27, 5, y - 21, _earInner);

		DrawCircle(new Vector2(-7, y - 10), 5.5f, Colors.White);
		DrawCircle(new Vector2(7, y - 10), 5.5f, Colors.White);
		DrawCircle(new Vector2(-6, y - 9), 3f, Colors.Black);
		DrawCircle(new Vector2(8, y - 9), 3f, Colors.Black);
		DrawCircle(new Vector2(-8, y - 12), 1.8f, Colors.White);
		DrawCircle(new Vector2(6, y - 12), 1.8f, Colors.White);

		DrawCircle(new Vector2(0, y - 3), 2f, new Color(1f, 0.5f, 0.55f));
		DrawLine(new Vector2(0, y - 1), new Vector2(-3, y + 2), new Color(0.3f, 0.3f, 0.3f), 1.5f);
		DrawLine(new Vector2(0, y - 1), new Vector2(3, y + 2), new Color(0.3f, 0.3f, 0.3f), 1.5f);

		DrawCircle(new Vector2(-12, y - 6), 3.5f, _blushColor);
		DrawCircle(new Vector2(12, y - 6), 3.5f, _blushColor);

		DrawLine(new Vector2(-5, y - 2), new Vector2(-18, y - 4), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1f);
		DrawLine(new Vector2(-5, y - 1), new Vector2(-18, y + 1), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1f);
		DrawLine(new Vector2(5, y - 2), new Vector2(18, y - 4), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1f);
		DrawLine(new Vector2(5, y - 1), new Vector2(18, y + 1), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1f);

		float lx = _mood == PetMood.Walking ? Mathf.Sin(_animTime * 10f) * 3f : 0;
		DrawLine(new Vector2(-6, y + 18), new Vector2(-6 + lx, y + 24), _bodyColor.Darkened(0.1f), 4f);
		DrawLine(new Vector2(6, y + 18), new Vector2(6 - lx, y + 24), _bodyColor.Darkened(0.1f), 4f);
	}

	private void DrawSitting(float y)
	{
		float r = 22f;
		DrawCircle(new Vector2(0, y + 8), r, _bodyColor);
		DrawCircle(new Vector2(0, y + 8), r, _accentColor, false, 2f);
		DrawCircle(new Vector2(0, y - 8), r - 2, _bodyColor);
		DrawCircle(new Vector2(0, y - 8), r - 2, _accentColor, false, 2f);

		Triangle(-16, y - 24, -8, y - 32, -3, y - 20, _bodyColor);
		Triangle(16, y - 24, 8, y - 32, 3, y - 20, _bodyColor);
		Triangle(-13, y - 25, -9, y - 29, -5, y - 21, _earInner);
		Triangle(13, y - 25, 9, y - 29, 5, y - 21, _earInner);

		DrawCircle(new Vector2(-7, y - 10), 6f, Colors.White);
		DrawCircle(new Vector2(7, y - 10), 6f, Colors.White);
		DrawCircle(new Vector2(-6, y - 9), 3.5f, Colors.Black);
		DrawCircle(new Vector2(8, y - 9), 3.5f, Colors.Black);
		DrawCircle(new Vector2(-8, y - 12), 2.2f, Colors.White);
		DrawCircle(new Vector2(6, y - 12), 2.2f, Colors.White);

		DrawArc(new Vector2(0, y - 4), 6, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 8, Colors.Black, 1.5f);
		DrawCircle(new Vector2(0, y - 5), 2f, new Color(1f, 0.5f, 0.55f));
		DrawCircle(new Vector2(-12, y - 6), 4f, _blushColor);
		DrawCircle(new Vector2(12, y - 6), 4f, _blushColor);
	}

	private void DrawSleeping(float y)
	{
		DrawEllipse(new Vector2(0, y + 10), 24, 14, _bodyColor);
		DrawEllipse(new Vector2(0, y + 10), 24, 14, _accentColor, false, 2f);
		DrawCircle(new Vector2(16, y + 2), 10, _bodyColor);
		DrawCircle(new Vector2(16, y + 2), 10, _accentColor, false, 1.5f);
		Triangle(20, y - 2, 15, y - 10, 13, y - 2, _bodyColor);
		DrawArc(new Vector2(14, y), 0, Mathf.Pi, Mathf.Pi * 0.1f, 5, Colors.Black, 2f);
		DrawArc(new Vector2(19, y), 0, Mathf.Pi, Mathf.Pi * 0.1f, 5, Colors.Black, 2f);
		DrawCircle(new Vector2(18, y + 4), 1.5f, new Color(1f, 0.5f, 0.55f));
		var f = ThemeDB.FallbackFont;
		if (f != null) DrawString(f, new Vector2(22, y - 16), "zZz", HorizontalAlignment.Left, 30, 12, new Color(0.5f, 0.5f, 0.7f, 0.6f));
	}

	private void Triangle(float x1, float y1, float x2, float y2, float x3, float y3, Color c)
	{
		DrawColoredPolygon(new[] { new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3) }, c);
	}

	private void DrawEllipse(Vector2 c, float rx, float ry, Color color, bool filled = true, float outline = 0)
	{
		if (rx <= 0 || ry <= 0) return;
		DrawSetTransform(c, 0, new Vector2(rx, ry));
		if (filled) DrawCircle(Vector2.Zero, 1f, color);
		if (outline > 0) DrawArc(Vector2.Zero, 1f, 0, Mathf.Pi * 2, 32, color, outline, true);
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	private void Bar(float x, float y, float w, float h, Color c)
	{
		DrawRect(new Rect2(x - 1, y - 1, w + 2, h + 2), new Color(0, 0, 0, 0.5f));
		DrawRect(new Rect2(x, y, w, h), c);
	}

	public static (Color Body, Color Accent) GetRarityColors(PetRarity r) => r switch
	{
		PetRarity.Common => (new Color(0.55f, 0.55f, 0.55f), Colors.White),
		PetRarity.Rare => (new Color(0.15f, 0.45f, 0.9f), Colors.White),
		PetRarity.Epic => (new Color(0.65f, 0.2f, 0.85f), Colors.White),
		PetRarity.Legendary => (new Color(1f, 0.55f, 0.02f), new Color(1f, 0.9f, 0.5f)),
		_ => (Colors.Gray, Colors.White)
	};
}
