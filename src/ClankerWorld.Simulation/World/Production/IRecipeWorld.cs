using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IRecipeWorld
{
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    ActorCapabilityState? CapabilitiesFor(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    string HouseholdFor(string actor);
    string ProductionOwnerFor(PlacedBuilding? building, string actor);
    bool IsHouseholdBuilding(BuildingDefinition definition);
    IEnumerable<GoodsMatch> ProductionGoods(InventoryCheckpoint inventory, GoodsRequest request, bool carried);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
}
