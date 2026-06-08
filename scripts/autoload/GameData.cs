using Godot;

namespace Match3Demo;

public partial class GameData : Node
{
    public static GameData Instance { get; private set; }

    public int HighScore { get; set; } = 0;
    public int CurrentScore { get; set; } = 0;
    public int CurrentCombo { get; set; } = 0;
    public int BestCombo { get; set; } = 0;
    public int MovesRemaining { get; set; } = 30;
    public float TimeRemaining { get; set; } = 30f;

    public bool MusicEnabled { get; set; } = true;
    public bool SfxEnabled { get; set; } = true;
    public int ParticleQuality { get; set; } = 1;

    public bool IsMobile { get; set; } = false;
    public bool IsWeb { get; set; } = false;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        DetectPlatform();
    }

    private void DetectPlatform()
    {
        string osName = OS.GetName();
        IsWeb = osName == "Web";
        if (IsWeb)
        {
        }
    }

    public void ResetLevel()
    {
        CurrentScore = 0;
        CurrentCombo = 0;
        MovesRemaining = 30;
        TimeRemaining = 30f;

        EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, CurrentScore, 0);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ComboUpdated, CurrentCombo);
        EventBus.Instance.EmitSignal(EventBus.SignalName.MovesChanged, MovesRemaining);
        EventBus.Instance.EmitSignal(EventBus.SignalName.TimeChanged, TimeRemaining);
    }

    public void AddScore(int points)
    {
        CurrentScore += points;
        if (CurrentScore > HighScore)
            HighScore = CurrentScore;
        EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, CurrentScore, points);
    }

    public void UseMove()
    {
        MovesRemaining -= 1;
        EventBus.Instance.EmitSignal(EventBus.SignalName.MovesChanged, MovesRemaining);
        if (MovesRemaining <= 0)
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameOver);
    }

    public void UpdateCombo(int combo)
    {
        CurrentCombo = combo;
        if (combo > BestCombo)
            BestCombo = combo;
        EventBus.Instance.EmitSignal(EventBus.SignalName.ComboUpdated, combo);
    }
}
