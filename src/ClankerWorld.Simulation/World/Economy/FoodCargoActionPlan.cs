namespace ClankerWorld.Simulation.World;

public sealed record PlannedCargoMove(string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, IReadOnlyList<PlannedStockMember>? Family,
    string? OrchardReservationId, int OrchardReservationQuantity, int ReleaseQuantity);

public sealed record PlannedCargoStorage(string Actor, string HouseholdId,
    IReadOnlyList<PlannedCargoMove> Cargo, GridPoint Destination, int InteractionRange,
    string? StorageBuildingId, IReadOnlyList<GridPoint> Route);

public sealed record FoodCargoActionPlan(string ActorId, string HouseholdId,
    IReadOnlyList<PlannedCargoMove> Cargo, GridPoint Destination, int InteractionRange,
    string? StorageBuildingId, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, FoodCargoChoice.Candidate)
{
    public override string Activity => "set down supplies to make room for food";
    public PlannedCargoStorage Storage => new(Actor, HouseholdId, Cargo, Destination,
        InteractionRange, StorageBuildingId, Route);
}

public readonly record struct FoodCargoChoice
{
    public const string Candidate = "make_room_for_food";
    public static string Encode() => Candidate;
    public static bool TryDecode(string candidate, out FoodCargoChoice choice)
    {
        choice = new();
        return candidate == Candidate;
    }
}
