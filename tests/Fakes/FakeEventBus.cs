using System.Collections.Generic;
using Match3Demo;

namespace Match3Demo.Tests;

public class FakeEventBus : IPetEventBus, IGachaEventBus
{
    public List<string> EmittedSignals { get; } = new();
    public Dictionary<string, object?> LastSignalArgs { get; } = new();

    public void EmitSignal(string signalName, params object?[] args)
    {
        EmittedSignals.Add(signalName);
        if (args.Length > 0)
            LastSignalArgs[signalName] = args[0];
    }

    public bool HasSignal(string signalName) => EmittedSignals.Contains(signalName);

    public void EmitPetAcquired(string petDefId)
    {
        EmitSignal("PetAcquired", petDefId);
    }

    public void EmitPetLeveledUp(string petInstanceId, int newLevel)
    {
        EmitSignal("PetLeveledUp", petInstanceId, newLevel);
    }

    public void EmitPetEvolved(string petInstanceId, string newPetDefId)
    {
        EmitSignal("PetEvolved", petInstanceId, newPetDefId);
    }

    public void EmitActivePetChanged(string petInstanceId)
    {
        EmitSignal("ActivePetChanged", petInstanceId);
    }

    public void EmitGachaBeforePull(string bannerId)
    {
        EmitSignal("GachaBeforePull", bannerId);
    }

    public void EmitGachaPullResult(string rewardId, int rarity)
    {
        EmitSignal("GachaPullResult", rewardId, rarity);
    }

    public void EmitGachaMultiPullResult(System.Collections.Generic.List<GachaRollResult> results)
    {
        EmitSignal("GachaMultiPullResult", results);
    }
}
