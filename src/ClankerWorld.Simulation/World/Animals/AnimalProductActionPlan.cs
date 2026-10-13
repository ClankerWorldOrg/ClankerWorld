using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public enum AnimalProductKind { Milk, Product, Hide }

public sealed record AnimalProductChoice(AnimalProductKind Kind, string? AnimalId = null)
{
    public string Encode() => Kind == AnimalProductKind.Milk ? "drink_milk" :
        WorldRuntime.AnimalSharedRules.AnimalActionId(Kind == AnimalProductKind.Product ? "collect" : "collect_hide", AnimalId!, null);
}

public sealed record AnimalProductActionPlan(string AgentId, AnimalProductChoice Choice, string StockId,
    string OwnerId, string ItemKind, string RootId, int Quantity, GridPoint Position,
    string? ReservationId, PlannedAnimalDismount? Dismount, ReadOnlyCollection<GridPoint> Route)
    : ActionPlan(AgentId, Choice.Encode())
{
    public string? DismountAnimalId => Dismount?.AnimalId;
    public override string Activity => Choice.Kind switch
    {
        AnimalProductKind.Milk => "drink the milk",
        AnimalProductKind.Product => "collect the animal product",
        _ => "collect the hide",
    };
}
