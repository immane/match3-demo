using Godot;

namespace Match3Demo;

public partial class FloatingTextSpawner : Control
{
	public override void _Ready()
	{
		EventBus.Instance.ShowFloatingText += SpawnFloatingText;
	}

	private async void SpawnFloatingText(string text, Vector2 worldPos, Color color)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 28);
		label.AddThemeColorOverride("font_color", color);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.Position = worldPos;
		AddChild(label);

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position:y", worldPos.Y - 80.0f, 0.8f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "modulate:a", 0.0, 0.8);
		tween.TweenProperty(label, "scale", new Vector2(1.3f, 1.3f), 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		await ToSignal(tween, Tween.SignalName.Finished);
		label.QueueFree();
	}
}
