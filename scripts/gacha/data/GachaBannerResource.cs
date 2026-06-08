using Godot;
using System.Linq;

namespace Match3Demo;

[GlobalClass]
public partial class GachaBannerResource : Resource
{
    [Export] public string BannerId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int CostPerPull { get; set; } = 50;
    [Export] public int SoftPityStart { get; set; } = 70;
    [Export] public int HardPity { get; set; } = 90;
    [Export] public double SoftPityRateIncrease { get; set; } = 0.06;
    [Export] public string RateUpRewardId { get; set; } = "";
    [Export] public double RateUpChanceOnSSR { get; set; } = 0.5;
    [Export] public Godot.Collections.Array<GachaPoolEntryResource> Pool { get; set; } = new();

    public GachaBannerResource() { }

    public GachaBanner ToBanner()
    {
        return new GachaBanner
        {
            Id = BannerId,
            DisplayName = DisplayName,
            Pool = Pool.Select(p => p.ToEntry()).ToList(),
            CostPerPull = CostPerPull,
            SoftPityStart = SoftPityStart,
            HardPityGuarantee = HardPity,
            SoftPityRateIncrease = SoftPityRateIncrease,
            RateUpRewardId = RateUpRewardId,
            RateUpChanceOnSSR = RateUpChanceOnSSR
        };
    }
}
