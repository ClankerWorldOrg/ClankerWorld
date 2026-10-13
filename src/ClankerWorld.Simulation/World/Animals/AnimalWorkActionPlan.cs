namespace ClankerWorld.Simulation.World;

public enum AnimalWorkKind { Care, Tame, Saddle }

public sealed record AnimalWorkSelection(AnimalWorkKind Kind, string AnimalId)
{
    public string Action => Kind switch { AnimalWorkKind.Care => "care", AnimalWorkKind.Tame => "tame", _ => "saddle" };
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalActionId(Action, AnimalId, null);
}

public sealed record PlannedAnimalInput(string StockId, string OwnerId, string ItemKind, int Quantity,
    string RootId, bool Carried, GridPoint? Position);

public sealed record AnimalWorkActionPlan(string ActorId, AnimalWorkSelection Choice, string? AnimalHouseholdId,
    string? AnimalYardId, GridPoint Position, IReadOnlyList<PlannedAnimalInput> Inputs,
    AnimalSupplyActionPlan? SupplyPreparation, string? DismountAnimalId, bool ApproachOnly,
    string? ReceivingHouseholdId, string? ReceivingYardId, GridPoint? ReceivingYardPosition,
    AnimalTamingWork? InitialTamingWork, int WorkTicks, int CareTicks, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    { AnimalWorkKind.Care => "care for the animal", AnimalWorkKind.Tame => "tame the animal", _ => "saddle the horse" };
}
