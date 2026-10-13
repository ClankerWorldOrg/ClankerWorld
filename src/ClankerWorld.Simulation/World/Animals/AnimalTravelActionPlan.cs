namespace ClankerWorld.Simulation.World;

public enum AnimalTravelKind { Mount, Dismount, LeadHome }

public sealed record AnimalTravelChoice(AnimalTravelKind Kind, string AnimalId)
{
    public string Action => Kind switch { AnimalTravelKind.Mount => "mount", AnimalTravelKind.Dismount => "dismount", _ => "lead_home" };
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalActionId(Action, AnimalId, null);
}

public sealed record PlannedAnimalDrop(string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, IReadOnlyList<PlannedStockMember>? Family);

public sealed record PlannedAnimalDismount(string Actor, string AnimalId, GridPoint Position,
    int BaseCapacity, int CarriedQuantity, IReadOnlyList<PlannedAnimalDrop> Cargo);

public sealed record AnimalTravelActionPlan(string ActorId, AnimalTravelChoice Choice, GridPoint Position,
    string? HouseholdId, string? YardId, GridPoint? Destination, bool ReleaseLead,
    string? SaddleStockId, string? SaddleReservationId, PlannedAnimalDismount? Dismount, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    { AnimalTravelKind.Mount => "mount the horse", AnimalTravelKind.Dismount => "dismount the horse", _ => "lead the animal home" };
}
