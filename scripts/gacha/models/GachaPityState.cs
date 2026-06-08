namespace Match3Demo;

public readonly record struct GachaPityState(
    int TotalPulls,
    int PullsSinceLastSSR,
    int PullsSinceLastEpic,
    bool GuaranteedRateUpNext
);
