namespace ClankerWorld.Simulation.World;

public enum AnimalTripStep { Release, Work, Deliver }

public sealed record AnimalTripChoice(string ActorId)
{
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalSupplyTripId(ActorId);
}

public sealed record AnimalTripActionPlan(string ActorId, AnimalSupplyTrip Trip, string OwnerId,
    string ItemKind, int Quantity, IReadOnlyList<PlannedStockMember>? Family, AnimalTripStep Step,
    string? HouseholdId, GridPoint? Destination, AnimalWorkActionPlan? Work, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, new AnimalTripChoice(ActorId).Encode())
{
    public override string Activity => Step == AnimalTripStep.Work ? "finish the animal work" :
        Step == AnimalTripStep.Release ? "end the partial animal supply trip" : "deliver the animal supplies";
}
