namespace ClankerWorld.Simulation.World;

public sealed record AnimalSupplySelection(string StockId, string YardId, string? AnimalId, string? Action)
{
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalSupplyId(StockId, YardId, AnimalId, Action);
}

public sealed record AnimalSupplyActionPlan(string ActorId, AnimalSupplySelection Choice, string OwnerId,
    string ItemKind, int Quantity, GridPoint Pickup, int PickupRange, bool Carried, string HouseholdId,
    GridPoint Destination, IReadOnlyList<PlannedStockMember>? Family, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "collect supplies for the animal yard";
}
