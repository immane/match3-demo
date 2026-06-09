using Godot;

namespace Match3Demo;

public partial class RarityRevealEffect : Control
{
    public async void Play(Vector2 centerPosition, PetRarity rarity)
    {
        Color glowColor = rarity switch
        {
            PetRarity.Epic => new Color(0.6f, 0.2f, 1.0f, 0.3f),
            PetRarity.Legendary => new Color(1.0f, 0.65f, 0.0f, 0.4f),
            _ => Colors.Transparent
        };

        var glowRect = new ColorRect();
        glowRect.Color = glowColor;
        glowRect.Size = new Vector2(20, 20);
        glowRect.PivotOffset = new Vector2(10, 10);
        glowRect.Position = centerPosition - new Vector2(10, 10);
        AddChild(glowRect);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(glowRect, "size", new Vector2(400, 400), 0.8f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(glowRect, "position", centerPosition - new Vector2(200, 200), 0.8f);
        tween.TweenProperty(glowRect, "modulate:a", 0.0f, 0.8f);

        if (rarity == PetRarity.Legendary)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.Pi / 4;
                var line = new ColorRect();
                line.Color = new Color(1f, 0.8f, 0.2f, 0.8f);
                line.Size = new Vector2(100, 2);
                line.PivotOffset = new Vector2(0, 1);
                line.Position = centerPosition;
                line.Rotation = angle;
                AddChild(line);

                var lineTween = CreateTween();
                lineTween.TweenProperty(line, "size:x", 0f, 0.5f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenShake, 6.0f, 0.3f);
        }

        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        QueueFree();
    }
}
