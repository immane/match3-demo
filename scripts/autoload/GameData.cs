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

	public Godot.Collections.Dictionary<string, int> CurrencyBalances { get; set; } = new();
	public Godot.Collections.Array<Godot.Collections.Dictionary> OwnedPetsRaw { get; set; } = new();
	public string ActivePetId { get; set; } = "";
	public Godot.Collections.Dictionary<string, Godot.Collections.Dictionary> GachaPityStates { get; set; } = new();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        DetectPlatform();
        AddCurrency("soft_currency", 500);
        CallDeferred(MethodName.AddDefaultPet);
    }

    private void AddDefaultPet()
    {
        if (ServiceInitializer.Instance == null) return;
        var petService = ServiceInitializer.Instance.GetService<IPetCollectionService>();
        if (petService != null && petService.GetAllOwnedPets().Count == 0)
            petService.AddPet("cat_sleepy_01");
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

        AddCurrency("soft_currency", 500);
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

	public void AddCurrency(string currencyId, int amount)
	{
		if (!CurrencyBalances.ContainsKey(currencyId))
			CurrencyBalances[currencyId] = 0;
		CurrencyBalances[currencyId] += amount;
		EventBus.Instance.EmitSignal(EventBus.SignalName.CurrencyChanged, currencyId, CurrencyBalances[currencyId], amount);
	}

	public bool SpendCurrency(string currencyId, int amount)
	{
		int current = CurrencyBalances.ContainsKey(currencyId) ? CurrencyBalances[currencyId] : 0;
		if (current < amount)
			return false;
		CurrencyBalances[currencyId] = current - amount;
		EventBus.Instance.EmitSignal(EventBus.SignalName.CurrencyChanged, currencyId, CurrencyBalances[currencyId], -amount);
		return true;
	}

	public int GetCurrencyBalance(string currencyId)
	{
		return CurrencyBalances.ContainsKey(currencyId) ? CurrencyBalances[currencyId] : 0;
	}
}
