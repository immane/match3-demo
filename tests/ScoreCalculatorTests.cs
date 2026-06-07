using Godot;
using Xunit;

namespace Match3Demo.Tests;

public class ScoreCalculatorTests
{
    [Fact]
    public void TestLine3Score()
    {
        var group = new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.H_LINE,
            MatchLength = 3,
            Positions = new()
            {
                new Vector2I(0, 0),
                new Vector2I(1, 0),
                new Vector2I(2, 0),
            },
        };
        Assert.Equal(30, ScoreCalculator.CalculateGroupScore(group));
    }

    [Fact]
    public void TestLine4Score()
    {
        var group = new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.V_LINE,
            MatchLength = 4,
        };
        Assert.Equal(60, ScoreCalculator.CalculateGroupScore(group));
    }

    [Fact]
    public void TestLine5Score()
    {
        var group = new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.H_LINE,
            MatchLength = 5,
        };
        Assert.Equal(100, ScoreCalculator.CalculateGroupScore(group));
    }

    [Fact]
    public void TestLShapeScore()
    {
        var group = new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.L_SHAPE,
            MatchLength = 5,
        };
        Assert.Equal(180, ScoreCalculator.CalculateGroupScore(group));
    }

    [Fact]
    public void TestTShapeScore()
    {
        var group = new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.T_SHAPE,
            MatchLength = 5,
        };
        Assert.Equal(180, ScoreCalculator.CalculateGroupScore(group));
    }

    [Fact]
    public void TestCrossScore()
    {
        var group = new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.CROSS,
            MatchLength = 5,
        };
        Assert.Equal(230, ScoreCalculator.CalculateGroupScore(group));
    }

    [Fact]
    public void TestCalculateTotalSingleGroup()
    {
        var result = new MatchResult();
        result.Groups.Add(new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.H_LINE,
            MatchLength = 3,
        });
        result.Groups.Add(new MatchResult.MatchGroup
        {
            Shape = (int)MatchShape.H_LINE,
            MatchLength = 4,
        });
        Assert.Equal(90, ScoreCalculator.CalculateTotal(result));
    }

    [Fact]
    public void TestCalculateTotalNoGroups()
    {
        var result = new MatchResult();
        Assert.Equal(0, ScoreCalculator.CalculateTotal(result));
    }

    [Fact]
    public void TestApplyComboDepth0()
    {
        Assert.Equal(100, ScoreCalculator.ApplyCombo(100, 0));
    }

    [Fact]
    public void TestApplyComboDepth1()
    {
        Assert.Equal(100, ScoreCalculator.ApplyCombo(100, 1));
    }

    [Fact]
    public void TestApplyComboDepth2()
    {
        Assert.Equal(200, ScoreCalculator.ApplyCombo(100, 2));
    }

    [Fact]
    public void TestApplyComboDepth3()
    {
        Assert.Equal(300, ScoreCalculator.ApplyCombo(100, 3));
    }
}
