using Godot;

namespace Match3Demo;

public partial class PetLevelUpAnimation : Control
{
    public async void Play(Vector2 position)
    {
        var label = new Label();
        label.Text = "LEVEL UP!";
        label.AddThemeColorOverride("font_color", Colors.Gold);
        label.AddThemeFontSizeOverride("font_size", 32);
        label.Position = position;
        AddChild(label);

        var tween = CreateTween();
        tween.TweenProperty(label, "position:y", position.Y - 80, 1.0f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 1.0f);

        await ToSignal(tween, Tween.SignalName.Finished);
        QueueFree();
    }
}
