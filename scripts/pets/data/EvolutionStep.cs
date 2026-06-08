using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class EvolutionStep : Resource
{
    [Export] public string EvolvesToDefId { get; set; } = "";
    [Export] public int RequiredLevel { get; set; } = 10;
    [Export] public int RequiredDuplicates { get; set; } = 3;
    [Export] public string RequiredItemId { get; set; } = "";

    public EvolutionStep() { }
}
