using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public partial class MatchResult
{
    public List<MatchGroup> Groups = new();
    public byte[] MatchedFlags = new byte[0];
    public List<SpecialSpawn> SpecialSpawns = new();
    public int TotalMatched = 0;

    public bool HasMatches()
    {
        return TotalMatched > 0;
    }

    public List<Vector2I> GetAllPositions()
    {
        var all = new List<Vector2I>();
        foreach (var group in Groups)
            all.AddRange(group.Positions);
        return all;
    }

    public class MatchGroup
    {
        public int Shape = 0;
        public List<Vector2I> Positions = new();
        public Vector2I Pivot = new(-1, -1);
        public int MatchLength = 0;
        public int CrystalType = -1;

        public int Size()
        {
            return Positions.Count;
        }
    }

    public class SpecialSpawn
    {
        public Vector2I Position = new();
        public int SpecialType = -1;
        public int CrystalType = -1;

        public override string ToString()
        {
            return $"SpecialSpawn(type={SpecialType}, pos={Position})";
        }
    }
}
