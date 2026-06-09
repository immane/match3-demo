using System.Collections.Generic;

namespace Match3Demo;

public class GachaPitySaveData
{
    public Dictionary<string, GachaPityStateDto> BannerPityStates { get; set; } = new();
}

public class GachaPityStateDto
{
    public int TotalPulls { get; set; }
    public int PullsSinceLastSSR { get; set; }
    public int PullsSinceLastEpic { get; set; }
    public bool GuaranteedRateUpNext { get; set; }
}
