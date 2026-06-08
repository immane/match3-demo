using System;

namespace Match3Demo;

public class PetNeeds
{
	public float Hunger { get; set; } = 80f;
	public float Happiness { get; set; } = 80f;
	public float Energy { get; set; } = 80f;
	public long LastTickMs { get; set; }

	public const float MaxValue = 100f;
	public const float MinValue = 0f;
	public const float DecayPerSecond = 0.5f;     // points lost per second
	public const float StarvingThreshold = 20f;
	public const float WarningThreshold = 40f;

	public void Tick(double deltaSeconds)
	{
		float decay = DecayPerSecond * (float)deltaSeconds;
		Hunger = Math.Max(MinValue, Hunger - decay);
		Happiness = Math.Max(MinValue, Happiness - decay * 0.7f);
		Energy = Math.Max(MinValue, Energy - decay * 0.5f);
	}

	public void Feed(float hungerRestore, float happinessRestore)
	{
		Hunger = Math.Min(MaxValue, Hunger + hungerRestore);
		Happiness = Math.Min(MaxValue, Happiness + happinessRestore);
	}

	public void Play(float happinessRestore, float energyCost)
	{
		Happiness = Math.Min(MaxValue, Happiness + happinessRestore);
		Energy = Math.Max(MinValue, Energy - energyCost);
	}

	public bool IsStarving => Hunger < StarvingThreshold;
	public bool NeedsAttention => Hunger < WarningThreshold || Happiness < WarningThreshold;

	public PetNeedsSaveData ToSaveData()
	{
		return new PetNeedsSaveData
		{
			Hunger = Hunger,
			Happiness = Happiness,
			Energy = Energy,
			LastTickMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
		};
	}

	public void FromSaveData(PetNeedsSaveData data, double elapsedSeconds)
	{
		Hunger = Math.Max(MinValue, data.Hunger - DecayPerSecond * (float)elapsedSeconds);
		Happiness = Math.Max(MinValue, data.Happiness - DecayPerSecond * 0.7f * (float)elapsedSeconds);
		Energy = Math.Max(MinValue, data.Energy - DecayPerSecond * 0.5f * (float)elapsedSeconds);
	}
}

public class PetNeedsSaveData
{
	public float Hunger { get; set; } = 80f;
	public float Happiness { get; set; } = 80f;
	public float Energy { get; set; } = 80f;
	public long LastTickMs { get; set; }
}
