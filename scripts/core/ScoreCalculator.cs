using System.Collections.Generic;

namespace Match3Demo;

public class ScoreCalculator
{
    public static int CalculateGroupScore(MatchResult.MatchGroup group)
    {
        switch (group.Shape)
        {
            case 0:
            case 1:
                return LineScore(group.MatchLength);
            case 2:
                return 180;
            case 3:
                return 180;
            case 4:
                return 230;
        }
        return 0;
    }

    private static int LineScore(int length)
    {
        switch (length)
        {
            case 3: return 30;
            case 4: return 60;
            case 5: return 100;
        }
        return 100 + (length - 5) * 50;
    }

    public static int CalculateTotal(MatchResult matchResult)
    {
        int total = 0;
        foreach (var group in matchResult.Groups)
        {
            total += CalculateGroupScore(group);
        }
        return total;
    }

    public static int ApplyCombo(int baseScore, int comboDepth)
    {
        if (comboDepth <= 1)
            return baseScore;
        return baseScore * comboDepth;
    }
}
