using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record SmithGatherChoice(string ItemKind)
{
    public const string InputPrefix = "gather_smith_input:";
    public static SmithGatherChoice? Decode(string candidate) => candidate == "gather_smith_ore" ? new("iron_ore")
        : candidate.StartsWith(InputPrefix, StringComparison.Ordinal) ? new(candidate[InputPrefix.Length..]) : null;
    public string Encode() => ItemKind == "iron_ore" ? "gather_smith_ore" : InputPrefix + ItemKind;
}

public sealed record SmithGatherActionPlan(string ActorId, SmithGatherChoice Choice, string HouseholdId,
    string SmithId, GridPoint Destination, MapResource Source, ToolGatheringPlan? Harvest,
    string? ToolOwnerId, string? ToolKind, PlannedEquipmentPickup? ToolPickup,
    PlannedCargoStorage? CargoPreparation, IReadOnlyList<GridPoint> Route,
    IReadOnlyList<GridPoint> ReturnRoute) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => $"gather {Choice.ItemKind.Replace('_', ' ')} for the Blacksmith";
    public int PhysicalQuantity => Harvest is null ? 0 : checked(Harvest.Quantity + Harvest.TreeSeedQuantity);
}
