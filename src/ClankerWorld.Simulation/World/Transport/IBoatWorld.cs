using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal interface IBoatWorld
{
    TransportSystem Transport { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyList<AnimalState> Animals { get; }
    IReadOnlyList<FarmFieldState> Fields { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    InventoryCheckpoint Inventory { get; }
    AgentStore PhysicalAgents { get; }
    long WorldTick { get; }
    int CivicDay { get; }
    WorldMap Map { get; }
    ActorCapabilityPolicy BoatTripCapabilities { get; }
    ActorCapabilityPolicy TransportCapabilities { get; }
    bool IsAdult(string actor);
    AgentConversation? ConversationFor(string actor);
    InventoryLot? AttachedHandcart(string actor);
    bool HasGuardianPlacementTask(string actor);
    IReadOnlyList<string> MovingCareGroup(string actor);
    bool IsActiveTownProjectDelivery(string lot);
    PortGeometry PortGeometryFor(PlacedBuilding port);
    HashSet<GridPoint> PortObstacles(string? exceptProjectId = null, string? exceptPortId = null, bool protectDocks = false);
    PlacedBuilding? Port(string id);
    bool PortIsLegal(PlacedBuilding port);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles);
    ActionCheck CheckActionPlan(ActionPlan plan);
    void SetBoatOrderTravelStatus(BoatTripRequest request, string status, string? reason);
    void CreditBoatOrderArrival(BoatTripRequest request);
    IEnumerable<GridPoint> FreePortDocks(PlacedBuilding port, string? exceptBoatId = null);
    void AppendEvent(string kind, string detail, GridPoint? position = null);
    ActorCapabilityState? CapabilitiesFor(string actor);
    void MoveToward(string actor, AgentState state, GridPoint destination, string reason);
    IReadOnlyList<GridPoint> ReachBoatGeography(BoatState boat, PlacedBuilding destination);
    void MoveGroundCargo(IReadOnlyList<string> remaining, GridPoint position, bool preserveCustody);
    GridPoint? FreePortDock(PlacedBuilding port, string? exceptBoatId = null);
    void SetTownProject(string townId, TownProject project);
    GroundOccupancy Ground { get; }
    IReadOnlyList<BuildingDefinition> Definitions { get; }
}
