using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record ToolRepairChoice(string ToolId)
{
    public const string Prefix = "repair_tool:";
    public string Encode() => IdentityRules.PrefixedReference(Prefix, ToolId);
}

public sealed record PlannedRepairMaterial(string StockId, string ItemKind, int Quantity);

public sealed record PlannedRepairInput(string ItemKind, int NeededQuantity, string? StockId,
    string? OwnerId, GridPoint Target, int InteractionRange, MapResource? Source,
    ToolGatheringPlan? Harvest, string? ToolOwnerId, string? ToolKind)
{
    public int CarryUnits => Harvest is { } harvest ? checked(harvest.Quantity + harvest.TreeSeedQuantity) : NeededQuantity;
}

public sealed record ToolRepairActionPlan(string ActorId, ToolRepairChoice Choice, string ToolOwnerId,
    string ToolKind, string HouseholdId, string SmithId, GridPoint Destination,
    IReadOnlyList<PlannedRepairMaterial> Materials, IReadOnlyList<PlannedRepairInput> Inputs,
    PlannedCargoStorage? CargoPreparation, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => $"repair the worn {ToolKind.Replace('_', ' ')}";
}
