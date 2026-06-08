namespace Match3Demo;

public interface IPetEventBus
{
    void EmitPetAcquired(string petDefId);
    void EmitPetLeveledUp(string petInstanceId, int newLevel);
    void EmitPetEvolved(string petInstanceId, string newPetDefId);
    void EmitActivePetChanged(string petInstanceId);
}
