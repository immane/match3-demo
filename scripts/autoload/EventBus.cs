using Godot;

namespace Match3Demo;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    [Signal] public delegate void BoardInitializedEventHandler();
    [Signal] public delegate void TileSelectedEventHandler(Node2D tile, Vector2I pos);
    [Signal] public delegate void TileDeselectedEventHandler();
    [Signal] public delegate void SwapRequestedEventHandler(Vector2I from, Vector2I to);
    [Signal] public delegate void SwapCompletedEventHandler(bool valid);
    [Signal] public delegate void SwapInvalidEventHandler();

    [Signal] public delegate void MatchesFoundEventHandler(Godot.Collections.Array matches);
    [Signal] public delegate void TilesClearedEventHandler(Godot.Collections.Array positions);
    [Signal] public delegate void SpecialTileSpawnedEventHandler(Vector2I pos, int type);
    [Signal] public delegate void CascadeTriggeredEventHandler(int depth);

    [Signal] public delegate void ScoreChangedEventHandler(int newScore, int delta);
    [Signal] public delegate void ComboUpdatedEventHandler(int combo);
	[Signal] public delegate void MovesChangedEventHandler(int remaining);
	[Signal] public delegate void TimeChangedEventHandler(float remaining);

    [Signal] public delegate void GameStateChangedEventHandler(int oldState, int newState);
    [Signal] public delegate void GamePausedEventHandler();
    [Signal] public delegate void GameResumedEventHandler();
    [Signal] public delegate void LevelCompleteEventHandler();
    [Signal] public delegate void GameOverEventHandler();

    [Signal] public delegate void PlayEffectEventHandler(string effectName, Vector2 pos);
    [Signal] public delegate void ScreenShakeEventHandler(float intensity, float duration);

    [Signal] public delegate void ShowFloatingTextEventHandler(string text, Vector2 pos, Color color);

	// Pet system signals
	[Signal] public delegate void PetAcquiredEventHandler(string petDefId);
	[Signal] public delegate void PetLeveledUpEventHandler(string petInstanceId, int newLevel);
	[Signal] public delegate void PetEvolvedEventHandler(string oldPetInstanceId, string newPetDefId);
	[Signal] public delegate void ActivePetChangedEventHandler(string petInstanceId);
	[Signal] public delegate void PetFedEventHandler(string petInstanceId, string foodId);

	// Gacha system signals
	[Signal] public delegate void GachaPullResultEventHandler(string rewardId, int rarity, Godot.Collections.Dictionary pityState);
	[Signal] public delegate void GachaPityMilestoneEventHandler(int pullsTowardGuarantee);
	[Signal] public delegate void GachaMultiPullResultEventHandler(Godot.Collections.Array results);
	[Signal] public delegate void GachaBeforePullEventHandler(string bannerId);

	// Currency system signals
	[Signal] public delegate void CurrencyChangedEventHandler(string currencyId, int newBalance, int delta);

    public override void _EnterTree()
    {
        Instance = this;
    }
}
