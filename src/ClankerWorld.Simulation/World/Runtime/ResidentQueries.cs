using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public interface IResidentWorld : IWorldEventWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, InhabitantState> Inhabitants { get; }
    GridPoint SettlementStoragePosition { get; }
    GridPoint BuildingPosition(string id);
    int FootDistance(GridPoint origin, GridPoint destination);
    int WarmthChange(InhabitantState person);
}

/// <summary>Shared resident rules with current physical state supplied per query.</summary>
public static class ResidentQueries
{
    public static bool AdultResident(IResidentWorld world, string actor) => world.Inhabitants.ContainsKey(actor) &&
        world.Society.GetInhabitant(actor).AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder;

    public static bool ChildResident(IResidentWorld world, string actor) => world.Inhabitants.ContainsKey(actor) &&
        world.Society.GetInhabitant(actor).AgeBand is SocietyAgeBand.Child or SocietyAgeBand.Adolescent;

    public static string HouseholdFor(IResidentWorld world, string actor) => world.Society.GetInhabitant(actor).HouseholdId ?? actor;

    public static bool IsWithinInteractionRange(IResidentWorld world, GridPoint origin, GridPoint destination, int range) =>
        world.FootDistance(origin, destination) <= range;

    public static GridPoint HouseholdStockPosition(IResidentWorld world, InventoryLot lot) =>
        lot.CarrierId is { } carrier ? world.Inhabitants[carrier].Position :
        lot.GroundPosition is { } ground ? new(ground.X, ground.Y) :
        lot.StorageBuildingId is { } buildingId ? world.BuildingPosition(buildingId) : world.SettlementStoragePosition;

    public static bool NeedsUrgentFood(InhabitantState person, int urgentFullness) => person.HungerBasisPoints < urgentFullness;

    public static bool NeedsUrgentWarmth(IResidentWorld world, InhabitantState person, int urgentWarmth) =>
        person.Survival is { } condition && condition.WarmthBasisPoints < urgentWarmth && world.WarmthChange(person) < 0;
}
