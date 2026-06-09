using Godot;

namespace Match3Demo;

[GlobalClass]
public partial class PetAbilityDef : Resource
{
    [Export] public string AbilityId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public int AbilityType { get; set; }
    [Export] public int TriggerCondition { get; set; }
    [Export] public int EffectValue { get; set; }

    public PetAbilityDef() { }
}
