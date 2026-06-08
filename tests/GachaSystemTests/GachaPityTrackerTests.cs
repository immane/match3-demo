using Xunit;
using Match3Demo;

namespace Match3Demo.Tests;

public class GachaPityTrackerTests
{
    [Fact]
    public void GetPityState_NewBanner_ReturnsDefault()
    {
        var tracker = new GachaPityTracker(new InMemoryStorage());
        var state = tracker.GetPityState("banner1");
        Assert.Equal(0, state.TotalPulls);
        Assert.Equal(0, state.PullsSinceLastSSR);
        Assert.False(state.GuaranteedRateUpNext);
    }

    [Fact]
    public void UpdatePityState_ThenRetrieve_MatchesUpdated()
    {
        var tracker = new GachaPityTracker(new InMemoryStorage());
        var newState = new GachaPityState(50, 10, 3, true);
        tracker.UpdatePityState("banner1", newState);
        var loaded = tracker.GetPityState("banner1");
        Assert.Equal(50, loaded.TotalPulls);
        Assert.Equal(10, loaded.PullsSinceLastSSR);
        Assert.True(loaded.GuaranteedRateUpNext);
    }

    [Fact]
    public void ResetPityState_ClearsBanner()
    {
        var tracker = new GachaPityTracker(new InMemoryStorage());
        tracker.UpdatePityState("b1", new GachaPityState(100, 50, 20, true));
        tracker.ResetPityState("b1");
        var state = tracker.GetPityState("b1");
        Assert.Equal(0, state.TotalPulls);
        Assert.Equal(0, state.PullsSinceLastSSR);
        Assert.False(state.GuaranteedRateUpNext);
    }

    [Fact]
    public void DifferentBanners_HaveIndependentState()
    {
        var tracker = new GachaPityTracker(new InMemoryStorage());
        tracker.UpdatePityState("b1", new GachaPityState(10, 3, 1, false));
        tracker.UpdatePityState("b2", new GachaPityState(50, 20, 5, true));

        var s1 = tracker.GetPityState("b1");
        var s2 = tracker.GetPityState("b2");

        Assert.Equal(10, s1.TotalPulls);
        Assert.Equal(50, s2.TotalPulls);
        Assert.False(s1.GuaranteedRateUpNext);
        Assert.True(s2.GuaranteedRateUpNext);
    }
}
