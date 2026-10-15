using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IConstructionPlanningWorld
{
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    ActorCapabilityState? CapabilitiesFor(string actor);
    string HouseholdFor(string actor);
    string? TownForResident(string actor);
    OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, AgentProject? project);
    string BuildingConstructionOwner(string actor, BuildingDefinition definition);
    string BuildInstanceId(string actor, BuildingDefinition definition);
    bool IsHouseholdBuilding(BuildingDefinition definition);
    string? HouseholdBuildingKind(BuildingDefinition definition);
    PlacedBuilding? HouseholdBuildingWithTag(string household, string tag);
    PlacedBuilding? FarmhouseForHousehold(string household);
    bool HouseholdBuildingProjectInProgress(string household, string kind, string actor);
    bool BuildingIdentityIsReserved(string instance, string? instruction);
    TownLayoutContext CreateTownLayoutContext(string actor, GridPoint site, BuildingDefinition definition);
    IEnumerable<GoodsMatch> ConstructionGoods(InventoryCheckpoint inventory, GoodsRequest request);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
}
