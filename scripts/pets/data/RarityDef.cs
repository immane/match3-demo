using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class RarityDef : Resource
{
    [Export] public PetRarity Rarity { get; set; } = PetRarity.Common;
    [Export] public float StatMultiplier { get; set; } = 1.0f;
    [Export] public Color DisplayColor { get; set; } = Colors.White;
    [Export] public double GachaWeight { get; set; } = 1.0;

    public RarityDef() { }
}
