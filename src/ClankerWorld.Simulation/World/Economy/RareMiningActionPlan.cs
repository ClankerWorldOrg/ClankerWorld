using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record RareMiningChoice(string ItemKind)
{
    public const string Prefix = "gather_rare_material:";
    public string Encode() => Prefix + ItemKind;
}

public sealed record RareMiningActionPlan(string ActorId, RareMiningChoice Choice, string? HouseholdId,
    MapResource Source, ToolGatheringPlan Harvest, string ToolOwnerId, string ToolKind,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => $"mine {Choice.ItemKind.Replace('_', ' ')}";
}
