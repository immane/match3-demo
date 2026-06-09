using Godot;
using System;

namespace Match3Demo;

public enum PetMood { Walking, Idle, Sitting, Sleeping }

public partial class PetActor : Node2D
{
	private string _displayName = "";
	private PetRarity _rarity = PetRarity.Common;
	private PetNeeds? _needs;
	private Color _bodyColor = Colors.Gray;
	private bool _showBars;
	private PetMood _mood = PetMood.Walking;
	private float _moodTimer;
	private float _sitTimer;
	private float _animTimer;
	private bool _facingRight = true;

	private Tween? _walkTween;
	private Vector2 _targetPos;
	private Vector2 _walkAreaMin;
	private Vector2 _walkAreaMax;
	private System.Random _rng = new();

	[Signal] public delegate void PetClickedEventHandler(InputEvent inputEvent);

	public PetInstance? PetInstance { get; set; }
	public PetDefinition? PetDef { get; set; }

	public override void _Ready()
	{
		ZIndex = 100;
		Scale = new Vector2(1.5f, 1.5f);
		_walkAreaMin = Position - new Vector2(100, 80);
		_walkAreaMax = Position + new Vector2(100, 80);
		_targetPos = Position;
		PickMood();
	}

	public void Setup(PetInstance pet, PetDefinition def, bool showBars = true)
	{
		_displayName = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : def.DisplayName;
		_rarity = def.Rarity;
		_needs = pet.Needs;
		_bodyColor = GetRarityColor(def.Rarity);
		_showBars = showBars;
		PetInstance = pet;
		PetDef = def;
		QueueRedraw();
	}

	public void SetWalkArea(Vector2 min, Vector2 max)
	{
		_walkAreaMin = min;
		_walkAreaMax = max;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_animTimer += dt;
		_moodTimer -= dt;

		if (_moodTimer <= 0)
			PickMood();

		switch (_mood)
		{
			case PetMood.Walking:
				if (_walkTween == null || !_walkTween.IsRunning())
					WalkToNewTarget();
				break;
			case PetMood.Sitting:
				_sitTimer -= dt;
				if (_sitTimer <= 0) PickMood();
				break;
			case PetMood.Sleeping:
				break;
		}

		QueueRedraw();
	}

	private void PickMood()
	{
		if (_needs != null && _needs.Energy < 30f)
		{
			_mood = PetMood.Sleeping;
			_moodTimer = _rng.Next(4, 8);
			_walkTween?.Kill();
			return;
		}

		double roll = _rng.NextDouble();
		if (roll < 0.5f)
		{
			_mood = PetMood.Walking;
			_moodTimer = _rng.Next(2, 5);
		}
		else if (roll < 0.8f)
		{
			_mood = PetMood.Sitting;
			_moodTimer = _rng.Next(2, 4);
			_walkTween?.Kill();
		}
		else
		{
			_mood = PetMood.Idle;
			_moodTimer = _rng.Next(1, 3);
			_walkTween?.Kill();
		}
	}

	private void WalkToNewTarget()
	{
		float x = (float)(_rng.NextDouble() * (_walkAreaMax.X - _walkAreaMin.X) + _walkAreaMin.X);
		float y = (float)(_rng.NextDouble() * (_walkAreaMax.Y - _walkAreaMin.Y) + _walkAreaMin.Y);
		_targetPos = new Vector2(x, y);

		_facingRight = _targetPos.X > Position.X;

		_walkTween?.Kill();
		_walkTween = CreateTween();
		float dist = Position.DistanceTo(_targetPos);
		float dur = Mathf.Clamp(dist / 80f, 0.6f, 3f);
		_walkTween.TweenProperty(this, "position", _targetPos, dur)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_mood = PetMood.Walking;
	}

	public override void _Draw()
	{
		var font = ThemeDB.FallbackFont;
		float bob = 0, legBob = 0;
		bool isSleeping = _mood == PetMood.Sleeping;
		bool isSitting = _mood == PetMood.Sitting;
		bool isWalking = _mood == PetMood.Walking;

		if (isWalking) { bob = Mathf.Sin(_animTimer * 6f) * 4f; legBob = Mathf.Sin(_animTimer * 12f) * 5f; }
		else if (_mood == PetMood.Idle) bob = Mathf.Sin(_animTimer * 3f) * 2f;

		float y0 = bob;

		if (isSleeping)
		{
			DrawSleepingCat(y0, font);
		}
		else if (isSitting)
		{
			DrawSittingCat(y0, legBob);
		}
		else
		{
			DrawStandingCat(y0, legBob, isWalking);
		}

		// Name tag
		if (font != null)
		{
			float nameY = isSleeping ? y0 - 22 : (isSitting ? y0 - 36 : y0 - 44);
			DrawString(font, new Vector2(-60, nameY), _displayName, HorizontalAlignment.Center, 120, 16, Colors.White);
		}

		// Needs bars
		if (_showBars && _needs != null)
		{
			float barY = isSleeping ? y0 - 38 : (isSitting ? y0 - 50 : y0 - 58);
			float barW = 60, barH = 5;
			float bx = -barW / 2f;
			DrawBar(bx, barY, barW * (_needs.Hunger / PetNeeds.MaxValue), barH, new Color(1f, 0.35f, 0.1f));
			DrawBar(bx, barY + 7, barW * (_needs.Happiness / PetNeeds.MaxValue), barH, new Color(1f, 0.7f, 0.1f));
			DrawBar(bx, barY + 14, barW * (_needs.Energy / PetNeeds.MaxValue), barH, new Color(0.2f, 0.65f, 1f));
		}
	}

	private void DrawStandingCat(float y0, float legBob, bool walking)
	{
		float tailSway = walking ? Mathf.Sin(_animTimer * 8f) * 6f : 0;
		float earBob = walking ? Mathf.Abs(Mathf.Sin(_animTimer * 12f)) * 2f : 0;

		// Shadow
		DrawEllipse(new Vector2(0, y0 + 30), 22, 6, new Color(0, 0, 0, 0.12f));

		// Tail (behind body)
		DrawThickCurve(new Vector2(10, y0 + 18), new Vector2(24 + tailSway, y0 + 8), new Vector2(20, y0 - 12 + tailSway), _bodyColor.Darkened(0.05f), 5f);
		// Tail tip
		DrawCircle(new Vector2(22, y0 - 10 + tailSway), 5f, _bodyColor.Darkened(0.05f));

		// Legs
		float lx = walking ? legBob : 0;
		DrawLine(new Vector2(-8, y0 + 22), new Vector2(-8 + lx, y0 + 32), _bodyColor.Darkened(0.08f), 6f);
		DrawLine(new Vector2(8, y0 + 22), new Vector2(8 - lx, y0 + 32), _bodyColor.Darkened(0.08f), 6f);
		// Paws
		DrawCircle(new Vector2(-8 + lx, y0 + 33), 4.5f, _bodyColor.Darkened(0.05f));
		DrawCircle(new Vector2(8 - lx, y0 + 33), 4.5f, _bodyColor.Darkened(0.05f));

		// Body (small oval)
		DrawEllipse(new Vector2(0, y0 + 12), 18, 16, _bodyColor);
		// Outline body
		DrawEllipseOutline(new Vector2(0, y0 + 12), 18, 16, 24, Colors.White, 2f);

		// Front paws (tiny circles on top of body)
		DrawCircle(new Vector2(-6, y0 + 6), 4f, _bodyColor.Darkened(0.05f));
		DrawCircle(new Vector2(6, y0 + 6), 4f, _bodyColor.Darkened(0.05f));

		// Head (big circle, center-right or center-left above body)
		DrawCircle(new Vector2(0, y0 - 4), 20, _bodyColor);
		DrawCircle(new Vector2(0, y0 - 4), 20, Colors.White, false, 2.5f);

		// Ears
		DrawTriangle(new Vector2(-18, y0 - 16), new Vector2(-8, y0 - 28 + earBob), new Vector2(-4, y0 - 16), _bodyColor);
		DrawTriangle(new Vector2(18, y0 - 16), new Vector2(8, y0 - 28 + earBob), new Vector2(4, y0 - 16), _bodyColor);
		// Inner ears
		DrawTriangle(new Vector2(-15, y0 - 17), new Vector2(-9, y0 - 24 + earBob), new Vector2(-6, y0 - 17), new Color(1f, 0.75f, 0.8f));
		DrawTriangle(new Vector2(15, y0 - 17), new Vector2(9, y0 - 24 + earBob), new Vector2(6, y0 - 17), new Color(1f, 0.75f, 0.8f));

		// Face
		// Big sparkling eyes
		DrawCircle(new Vector2(-8, y0 - 6), 7, Colors.White);
		DrawCircle(new Vector2(8, y0 - 6), 7, Colors.White);
		DrawCircle(new Vector2(-7, y0 - 5), 3.5f, new Color(0.1f, 0.1f, 0.15f));
		DrawCircle(new Vector2(9, y0 - 5), 3.5f, new Color(0.1f, 0.1f, 0.15f));
		// Eye highlights (cute shine)
		DrawCircle(new Vector2(-9.5f, y0 - 8.5f), 2.2f, Colors.White);
		DrawCircle(new Vector2(-6.5f, y0 - 5.5f), 1.2f, Colors.White);
		DrawCircle(new Vector2(6.5f, y0 - 8.5f), 2.2f, Colors.White);
		DrawCircle(new Vector2(9.5f, y0 - 5.5f), 1.2f, Colors.White);

		// Tiny pink nose
		DrawTriangle(new Vector2(-3, y0 + 4), new Vector2(3, y0 + 4), new Vector2(0, y0 + 2), new Color(1f, 0.55f, 0.6f));

		// Mouth
		DrawArc(new Vector2(-4, y0 + 5), 4, Mathf.Pi * 0.1f, Mathf.Pi * 0.5f, 8, new Color(0.3f, 0.3f, 0.3f), 1.5f);
		DrawArc(new Vector2(4, y0 + 5), 4, Mathf.Pi * 0.5f, Mathf.Pi * 0.9f, 8, new Color(0.3f, 0.3f, 0.3f), 1.5f);

		// Whiskers
		DrawLine(new Vector2(-6, y0 + 3), new Vector2(-20, y0 - 2), new Color(0.5f, 0.5f, 0.5f, 0.6f), 1.5f);
		DrawLine(new Vector2(-5, y0 + 4), new Vector2(-20, y0 + 5), new Color(0.5f, 0.5f, 0.5f, 0.6f), 1.5f);
		DrawLine(new Vector2(6, y0 + 3), new Vector2(20, y0 - 2), new Color(0.5f, 0.5f, 0.5f, 0.6f), 1.5f);
		DrawLine(new Vector2(5, y0 + 4), new Vector2(20, y0 + 5), new Color(0.5f, 0.5f, 0.5f, 0.6f), 1.5f);

		// Blush
		DrawCircle(new Vector2(-14, y0 - 1), 4.5f, new Color(1f, 0.6f, 0.6f, 0.25f));
		DrawCircle(new Vector2(14, y0 - 1), 4.5f, new Color(1f, 0.6f, 0.6f, 0.25f));
	}

	private void DrawSittingCat(float y0, float legBob)
	{
		// Shadow
		DrawEllipse(new Vector2(0, y0 + 32), 24, 8, new Color(0, 0, 0, 0.12f));

		// Body (rounder when sitting)
		DrawCircle(new Vector2(0, y0 + 14), 20, _bodyColor);
		DrawCircle(new Vector2(0, y0 + 14), 20, Colors.White, false, 2.5f);

		// Tail wrapping around from side
		DrawThickCurve(new Vector2(18, y0 + 18), new Vector2(26, y0 + 12), new Vector2(28 + legBob, y0), _bodyColor.Darkened(0.05f), 5f);

		// Paws in front
		DrawCircle(new Vector2(-8, y0 + 10), 5f, _bodyColor.Darkened(0.05f));
		DrawCircle(new Vector2(8, y0 + 10), 5f, _bodyColor.Darkened(0.05f));

		// Head
		DrawCircle(new Vector2(0, y0 - 6), 20, _bodyColor);
		DrawCircle(new Vector2(0, y0 - 6), 20, Colors.White, false, 2.5f);

		// Ears
		DrawTriangle(new Vector2(-18, y0 - 18), new Vector2(-8, y0 - 30), new Vector2(-4, y0 - 18), _bodyColor);
		DrawTriangle(new Vector2(18, y0 - 18), new Vector2(8, y0 - 30), new Vector2(4, y0 - 18), _bodyColor);
		DrawTriangle(new Vector2(-15, y0 - 19), new Vector2(-9, y0 - 26), new Vector2(-6, y0 - 19), new Color(1f, 0.75f, 0.8f));
		DrawTriangle(new Vector2(15, y0 - 19), new Vector2(9, y0 - 26), new Vector2(6, y0 - 19), new Color(1f, 0.75f, 0.8f));

		// Big eyes
		DrawCircle(new Vector2(-8, y0 - 8), 7.5f, Colors.White);
		DrawCircle(new Vector2(8, y0 - 8), 7.5f, Colors.White);
		DrawCircle(new Vector2(-7, y0 - 7), 4f, new Color(0.1f, 0.1f, 0.15f));
		DrawCircle(new Vector2(9, y0 - 7), 4f, new Color(0.1f, 0.1f, 0.15f));
		DrawCircle(new Vector2(-9.5f, y0 - 10.5f), 2.5f, Colors.White);
		DrawCircle(new Vector2(-6, y0 - 7), 1.3f, Colors.White);
		DrawCircle(new Vector2(6.5f, y0 - 10.5f), 2.5f, Colors.White);
		DrawCircle(new Vector2(10, y0 - 7), 1.3f, Colors.White);

		// Happy mouth (wider)
		DrawArc(new Vector2(-4, y0 + 4), 5, Mathf.Pi * 0.15f, Mathf.Pi * 0.55f, 8, new Color(0.2f, 0.2f, 0.2f), 1.5f);
		DrawArc(new Vector2(4, y0 + 4), 5, Mathf.Pi * 0.45f, Mathf.Pi * 0.85f, 8, new Color(0.2f, 0.2f, 0.2f), 1.5f);

		// Nose
		DrawTriangle(new Vector2(-2.5f, y0 + 2), new Vector2(2.5f, y0 + 2), new Vector2(0, y0), new Color(1f, 0.55f, 0.6f));

		// Blush
		DrawCircle(new Vector2(-14, y0 - 3), 5f, new Color(1f, 0.5f, 0.5f, 0.3f));
		DrawCircle(new Vector2(14, y0 - 3), 5f, new Color(1f, 0.5f, 0.5f, 0.3f));

		// Whiskers
		DrawLine(new Vector2(-6, y0 + 2), new Vector2(-22, y0), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1.5f);
		DrawLine(new Vector2(-5, y0 + 3), new Vector2(-22, y0 + 6), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1.5f);
		DrawLine(new Vector2(6, y0 + 2), new Vector2(22, y0), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1.5f);
		DrawLine(new Vector2(5, y0 + 3), new Vector2(22, y0 + 6), new Color(0.5f, 0.5f, 0.5f, 0.5f), 1.5f);
	}

	private void DrawSleepingCat(float y0, Font? font)
	{
		// Sleeping on side - oval body
		DrawEllipse(new Vector2(0, y0 + 10), 28, 16, _bodyColor);
		DrawEllipseOutline(new Vector2(0, y0 + 10), 28, 16, 20, Colors.White, 2f);

		// Head tucked in
		DrawCircle(new Vector2(18, y0 + 2), 12, _bodyColor);
		DrawCircle(new Vector2(18, y0 + 2), 12, Colors.White, false, 2f);

		// Tiny ears
		DrawTriangle(new Vector2(22, y0 - 4), new Vector2(16, y0 - 12), new Vector2(14, y0 - 4), _bodyColor);
		DrawTriangle(new Vector2(21, y0 - 5), new Vector2(17, y0 - 10), new Vector2(15, y0 - 5), new Color(1f, 0.75f, 0.8f));

		// Closed happy eyes
		DrawArc(new Vector2(16, y0), 0, Mathf.Pi, Mathf.Pi * 0.1f, 6, new Color(0.2f, 0.2f, 0.2f), 2f);
		DrawArc(new Vector2(21, y0), 0, Mathf.Pi, Mathf.Pi * 0.1f, 6, new Color(0.2f, 0.2f, 0.2f), 2f);

		// Tiny nose
		DrawCircle(new Vector2(21, y0 + 4), 2f, new Color(1f, 0.55f, 0.6f));

		// ZZZ
		if (font != null)
			DrawString(font, new Vector2(24, y0 - 20), "zZz", HorizontalAlignment.Left, 30, 14, new Color(0.6f, 0.6f, 0.8f, 0.7f));

		// Tail curl
		DrawThickCurve(new Vector2(-24, y0 + 12), new Vector2(-32, y0 + 6), new Vector2(-24, y0 - 2), _bodyColor.Darkened(0.03f), 4f);
	}

	private void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color)
	{
		DrawColoredPolygon(new[] { p1, p2, p3 }, color);
	}

	private void DrawThickCurve(Vector2 p1, Vector2 ctrl, Vector2 p2, Color color, float width)
	{
		int segments = 12;
		var pts = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			float u = 1f - t;
			pts[i] = u * u * p1 + 2f * u * t * ctrl + t * t * p2;
		}
		for (int i = 0; i < segments; i++)
			DrawLine(pts[i], pts[i + 1], color, (int)width);
		var tipPts = new Vector2[segments + 1];
		float tipOffset = width * 0.6f;
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			Vector2 dir = 2f * ((1f - t) * (ctrl - p1) + t * (p2 - ctrl));
			Vector2 perp = new Vector2(-dir.Y, dir.X).Normalized() * tipOffset;
			tipPts[i] = pts[i] + perp;
		}
		for (int i = 0; i < segments; i++)
			DrawLine(tipPts[i], tipPts[i + 1], color.Lightened(0.1f), (int)(width * 0.5f));
	}

	private void DrawEllipseOutline(Vector2 center, float rx, float ry, int segs, Color color, float width)
	{
		var pts = new Vector2[segs + 1];
		for (int i = 0; i <= segs; i++)
		{
			float a = i * Mathf.Pi * 2f / segs;
			pts[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		for (int i = 0; i < segs; i++)
			DrawLine(pts[i], pts[i + 1], color, width);
	}

	private void DrawBar(float x, float y, float w, float h, Color color)
	{
		DrawRect(new Rect2(x - 1, y - 1, w + 2, h + 2), new Color(0, 0, 0, 0.6f));
		DrawRect(new Rect2(x, y, w, h), color);
	}

	private void DrawEllipse(Vector2 center, float rx, float ry, Color color)
	{
		if (rx <= 0 || ry <= 0) return;
		DrawSetTransform(center, 0, new Vector2(rx, ry));
		DrawCircle(Vector2.Zero, 1f, color);
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	public static Color GetRarityColor(PetRarity rarity) => rarity switch
	{
		PetRarity.Common => new Color(0.6f, 0.6f, 0.6f),
		PetRarity.Rare => new Color(0.15f, 0.45f, 0.95f),
		PetRarity.Epic => new Color(0.7f, 0.2f, 0.9f),
		PetRarity.Legendary => new Color(1f, 0.6f, 0.02f),
		_ => Colors.Gray
	};
}
