using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class PetDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public PetType Type { get; set; } = PetType.Cat;
    [Export] public PetRarity Rarity { get; set; } = PetRarity.Common;
    [Export] public int BaseLevel { get; set; } = 1;
    [Export] public int MaxLevel { get; set; } = 50;
    [Export] public Texture2D? Icon { get; set; }
    [Export] public Texture2D? SpriteSheet { get; set; }
    [Export] public int FrameCount { get; set; } = 4;
    [Export] public string Description { get; set; } = "";
    [Export] public Godot.Collections.Array<PetAbilityDef> Abilities { get; set; } = new();
    [Export] public Godot.Collections.Array<EvolutionStep> EvolutionChain { get; set; } = new();

    public PetDefinition() { }
}
