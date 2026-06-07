using Godot;

namespace Match3Demo;

public partial class ScreenShake : Node
{
    [Export] public Camera2D Camera { get; set; }

    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private Vector2 _originalCameraPos;

    public override void _Ready()
    {
        EventBus.Instance.ScreenShake += OnScreenShake;
        if (Camera != null)
            _originalCameraPos = Camera.Position;
    }

    private void OnScreenShake(float intensity, float duration)
    {
        _shakeIntensity = intensity;
        _shakeDuration = duration;
        _shakeTimer = duration;
        if (Camera != null && _shakeTimer == duration)
            _originalCameraPos = Camera.Position;
    }

    public override void _Process(double delta)
    {
        if (_shakeTimer <= 0.0f || Camera == null)
            return;

        _shakeTimer -= (float)delta;

        if (_shakeTimer <= 0.0f)
        {
            Camera.Position = _originalCameraPos;
            return;
        }

        float progress = 1.0f - (_shakeTimer / _shakeDuration);
        float currentIntensity = _shakeIntensity * (1.0f - progress);

        float offsetX = (float)GD.RandRange(-currentIntensity, currentIntensity);
        float offsetY = (float)GD.RandRange(-currentIntensity, currentIntensity);
        Camera.Position = _originalCameraPos + new Vector2(offsetX, offsetY);
    }
}
