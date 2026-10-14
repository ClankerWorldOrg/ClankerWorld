namespace ClankerWorld.Simulation.World;

public sealed record FoodCargoMove(string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, IReadOnlyList<PlannedStockMember>? Family,
    string? OrchardReservationId, int OrchardReservationQuantity, int ReleaseQuantity);

public sealed record FoodCargoActionPlan(string ActorId, string HouseholdId,
    IReadOnlyList<FoodCargoMove> Cargo, GridPoint Destination, int InteractionRange,
    string? StorageBuildingId, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, "make_room_for_food")
{
    public override string Activity => "set down supplies to make room for food";
}
