namespace Match3Demo;

public interface IGachaEventBus
{
    void EmitGachaBeforePull(string bannerId);
    void EmitGachaPullResult(string rewardId, int rarity);
    void EmitGachaMultiPullResult(System.Collections.Generic.List<GachaRollResult> results);
}
