using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public interface IAgentWorld : IWorldEventWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    GridPoint SharedStoragePosition { get; }
    GridPoint BuildingPosition(string id);
    int FootDistance(GridPoint origin, GridPoint destination);
    int WarmthChange(AgentState person);
}

/// <summary>Shared agent rules with current physical state supplied per query.</summary>
public static class AgentQueries
{
    public static bool IsAdult(IAgentWorld world, string actor) => world.Agents.ContainsKey(actor) &&
        LifeStageRules.IsAdult(world.Society.GetAgent(actor).AgeBand);

    public static bool IsChild(IAgentWorld world, string actor) => world.Agents.ContainsKey(actor) &&
        LifeStageRules.IsChild(world.Society.GetAgent(actor).AgeBand);

    public static string HouseholdFor(IAgentWorld world, string actor) => world.Society.GetAgent(actor).HouseholdId ?? actor;

    public static bool IsWithinInteractionRange(IAgentWorld world, GridPoint origin, GridPoint destination, int range) =>
        world.FootDistance(origin, destination) <= range;

    public static GridPoint HouseholdStockPosition(IAgentWorld world, InventoryLot lot) =>
        lot.CarrierId is { } carrier ? world.Agents[carrier].Position :
        lot.GroundPosition is { } ground ? new(ground.X, ground.Y) :
        lot.StorageBuildingId is { } buildingId ? world.BuildingPosition(buildingId) : world.SharedStoragePosition;

    public static bool NeedsUrgentFood(AgentState person, int urgentFullness) => person.FullnessBasisPoints < urgentFullness;

    public static bool NeedsUrgentWarmth(IAgentWorld world, AgentState person, int urgentWarmth) =>
        person.Survival is { } condition && condition.WarmthBasisPoints < urgentWarmth && world.WarmthChange(person) < 0;
}
