using Godot;
using System;

namespace Match3Demo;

public partial class PetActor : Node2D
{
	private string _petDefId = "";
	private string _nickname = "";
	private string _displayName = "";
	private PetRarity _rarity = PetRarity.Common;
	private int _level = 1;
	private Color _bodyColor = Colors.Gray;
	private Color _textColor = Colors.White;

	private Tween? _walkTween;
	private float _walkTimer;
	private float _bobOffset;
	private Vector2 _targetPos;
	private Vector2 _walkAreaMin;
	private Vector2 _walkAreaMax;
	private System.Random _rng = new();

	[Signal] public delegate void PetClickedEventHandler(InputEvent inputEvent);

	public override void _Ready()
	{
		ZIndex = 100;
		_walkAreaMin = new Vector2(-300, 200);
		_walkAreaMax = new Vector2(300, 500);
		_targetPos = Position;
		PickNewTarget();

		// Enable input
		var area = new Area2D();
		area.Name = "ClickArea";
		var shape = new CollisionShape2D();
		shape.Shape = new CircleShape2D { Radius = 35f };
		area.AddChild(shape);
		area.InputEvent += (Node _, InputEvent evt, long _) => EmitSignal(SignalName.PetClicked, evt);
		AddChild(area);
	}

	public void Setup(string petDefId, string nickname, PetRarity rarity, int level)
	{
		_petDefId = petDefId;
		_nickname = nickname;
		_rarity = rarity;
		_level = level;
		_displayName = !string.IsNullOrEmpty(nickname) ? nickname : petDefId;
		_bodyColor = GetRarityBodyColor(rarity);
		_textColor = GetRarityTextColor(rarity);
		QueueRedraw();
	}

	public void SetWalkArea(Vector2 min, Vector2 max)
	{
		_walkAreaMin = min;
		_walkAreaMax = max;
		PickNewTarget();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_walkTimer -= dt;

		if (_walkTimer <= 0)
			WalkToTarget();

		_bobOffset = Mathf.Sin((float)Time.GetTicksMsec() / 1000f * 2f + GetInstanceId() % 100) * 3f;
		QueueRedraw();
	}

	private void WalkToTarget()
	{
		PickNewTarget();

		_walkTween?.Kill();
		_walkTween = CreateTween();
		float distance = Position.DistanceTo(_targetPos);
		float duration = Mathf.Clamp(distance / 80f, 0.5f, 3f);

		_walkTween.TweenProperty(this, "position", _targetPos, duration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_walkTween.Finished += () => _walkTimer = _rng.Next(1, 4);
	}

	private void PickNewTarget()
	{
		float x = (float)(_rng.NextDouble() * (_walkAreaMax.X - _walkAreaMin.X) + _walkAreaMin.X);
		float y = (float)(_rng.NextDouble() * (_walkAreaMax.Y - _walkAreaMin.Y) + _walkAreaMin.Y);
		_targetPos = new Vector2(x, y);
	}

	public override void _Draw()
	{
		float r = 28f;
		// Body shadow
		DrawCircle(new Vector2(0, _bobOffset + 4f), r, new Color(0, 0, 0, 0.2f));
		// Body
		DrawCircle(new Vector2(0, _bobOffset), r, _bodyColor);
		// Outline
		DrawArc(new Vector2(0, _bobOffset), r, 0, Mathf.Pi * 2, 32, Colors.White, 2f, true);
		// Ears
		var earDark = _bodyColor.Darkened(0.2f);
		DrawColoredPolygon(new Vector2[] { new(-22, _bobOffset - 18), new(-12, _bobOffset - 32), new(-4, _bobOffset - 18) }, earDark);
		DrawColoredPolygon(new Vector2[] { new(4, _bobOffset - 18), new(12, _bobOffset - 32), new(22, _bobOffset - 18) }, earDark);
		// Eyes
		DrawCircle(new Vector2(-9, _bobOffset - 6), 5f, Colors.White);
		DrawCircle(new Vector2(9, _bobOffset - 6), 5f, Colors.White);
		DrawCircle(new Vector2(-9, _bobOffset - 6), 2.5f, Colors.Black);
		DrawCircle(new Vector2(9, _bobOffset - 6), 2.5f, Colors.Black);
		// Mouth
		DrawArc(new Vector2(0, _bobOffset + 4), 7f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 8, Colors.Black, 2f);
		// Tiny nose
		DrawCircle(new Vector2(0, _bobOffset - 2), 2.5f, new Color(1f, 0.5f, 0.5f));
		// Name tag
		var font = ThemeDB.FallbackFont;
		if (font != null)
		{
			float nameY = _bobOffset - 42;
			DrawString(font, new Vector2(-40, nameY), _displayName, HorizontalAlignment.Center, 80, 14, _textColor);
			DrawString(font, new Vector2(-30, nameY + 16), $"Lv.{_level}", HorizontalAlignment.Center, 60, 12, _textColor);
		}
	}

	private static Color GetRarityBodyColor(PetRarity rarity)
	{
		return rarity switch
		{
			PetRarity.Common => new Color(0.6f, 0.6f, 0.6f),
			PetRarity.Rare => new Color(0.25f, 0.5f, 0.95f),
			PetRarity.Epic => new Color(0.7f, 0.25f, 0.9f),
			PetRarity.Legendary => new Color(1.0f, 0.65f, 0.05f),
			_ => Colors.Gray
		};
	}

	private static Color GetRarityTextColor(PetRarity rarity)
	{
		return rarity switch
		{
			PetRarity.Common => new Color(0.85f, 0.85f, 0.85f),
			PetRarity.Rare => new Color(0.4f, 0.7f, 1.0f),
			PetRarity.Epic => new Color(0.9f, 0.5f, 1.0f),
			PetRarity.Legendary => new Color(1.0f, 0.8f, 0.2f),
			_ => Colors.White
		};
	}
}
