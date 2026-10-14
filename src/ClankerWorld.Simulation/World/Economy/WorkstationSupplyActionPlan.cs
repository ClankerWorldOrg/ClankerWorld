namespace ClankerWorld.Simulation.World;

public sealed record WorkstationSupplyChoice(string ItemKind)
{
    public const string Prefix = "supply_workstation:";
    public string Encode() => IdentityRules.PrefixedReference(Prefix, ItemKind);
    public static WorkstationSupplyChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) && value.Length > Prefix.Length
        ? new(value[Prefix.Length..]) : null;
}

public enum WorkstationSupplyKind { Carried, Pickup, Gather }

public sealed record WorkstationSupplyActionPlan(string ActorId, WorkstationSupplyChoice Choice,
    WorkstationSupplyKind Kind, string HouseholdId, string BuildingId, string DefinitionId,
    GridPoint Destination, string? StockId, string? StockOwnerId, string? StockKind, int Quantity,
    int PhysicalQuantity, GridPoint Target, int InteractionRange, IReadOnlyList<PlannedStockMember>? Family,
    MapResource? Source, ToolGatheringPlan? Harvest, string? ToolKind, string? ToolOwnerId,
    IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> ReturnRoute) : ActionPlan(ActorId, Choice.Encode())
{
    public string ItemKind => Choice.ItemKind;
    public override string Activity => "bring supplies to the household workstation";
}
