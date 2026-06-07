using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public partial class ParticleController : Node2D
{
    private static readonly Dictionary<int, Color> CrystalParticleColors = new()
    {
        { 0, new Color("ff4466") },
        { 1, new Color("4488ff") },
        { 2, new Color("44dd66") },
        { 3, new Color("ffcc33") },
        { 4, new Color("cc44ff") },
    };

    public void SpawnMatchParticles(Vector2 worldPos, int crystalType)
    {
        var particles = new GpuParticles2D();
        particles.Position = worldPos;
        particles.OneShot = true;
        particles.Explosiveness = 1.0f;
        particles.Lifetime = 0.6f;

        int quality = GameData.Instance.ParticleQuality;
        particles.Amount = quality <= 0 ? 8 : 20;

        var mat = new ParticleProcessMaterial();
        mat.Gravity = new Vector3(0, 100, 0);
        mat.InitialVelocityMin = 40f;
        mat.InitialVelocityMax = 120f;
        mat.AngleMin = -180f;
        mat.AngleMax = 180f;
        mat.ScaleMin = 0.5f;
        mat.ScaleMax = 1.5f;
        mat.Color = CrystalParticleColors.GetValueOrDefault(crystalType, new Color(1, 1, 1));
        particles.ProcessMaterial = mat;

        particles.Emitting = true;
        AddChild(particles);

        GetTree().CreateTimer(1.5f).Timeout += particles.QueueFree;
    }

    public void SpawnComboParticles(Vector2 worldPos, int comboLevel)
    {
        var particles = new GpuParticles2D();
        particles.Position = worldPos;
        particles.OneShot = true;
        particles.Explosiveness = 1.0f;
        particles.Lifetime = 0.8f;
        particles.Amount = 12 + comboLevel * 4;

        var mat = new ParticleProcessMaterial();
        mat.Gravity = new Vector3(0, 50, 0);
        mat.InitialVelocityMin = 60f;
        mat.InitialVelocityMax = 180f;
        mat.AngleMin = -180f;
        mat.AngleMax = 180f;
        mat.ScaleMin = 0.3f;
        mat.ScaleMax = 2.0f;

        float r = 1.0f;
        float g = 1.0f - comboLevel * 0.1f;
        float b = 0.3f;
        mat.Color = new Color(r, g, b);
        particles.ProcessMaterial = mat;

        particles.Emitting = true;
        AddChild(particles);

        GetTree().CreateTimer(1.5f).Timeout += particles.QueueFree;
    }

    public void SpawnSpecialActivation(Vector2 pos, int specialType)
    {
        var particles = new GpuParticles2D();
        particles.Position = pos;
        particles.OneShot = true;
        particles.Explosiveness = 1.0f;
        particles.Lifetime = 1.0f;
        particles.Amount = 30;

        var mat = new ParticleProcessMaterial();
        mat.Gravity = new Vector3(0, 20, 0);
        mat.InitialVelocityMin = 80f;
        mat.InitialVelocityMax = 200f;
        mat.AngleMin = -180f;
        mat.AngleMax = 180f;

        mat.Color = specialType switch
        {
            0 => new Color(1.0f, 0.4f, 0.2f),
            1 => new Color(1, 1, 1),
            2 => new Color(0.2f, 0.8f, 1.0f),
            _ => new Color(1, 1, 1),
        };

        particles.ProcessMaterial = mat;
        particles.Emitting = true;
        AddChild(particles);

        GetTree().CreateTimer(2.0f).Timeout += particles.QueueFree;
    }
}
