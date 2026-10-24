using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IBoatWorld BoatWorld => new TransportWorldAdapter(this);

    private sealed class TransportWorldAdapter(WorldRuntime runtime) : IBoatWorld
    {
        public TransportSystem Transport => runtime.transportSystem;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<AnimalState> Animals => runtime.animalWorld.Animals;
        public IReadOnlyList<FarmFieldState> Fields => runtime.fields;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public InventoryCheckpoint Inventory => runtime.society.Checkpoint.Inventory;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public long WorldTick => runtime.WorldTick;
        public int CivicDay => runtime.CivicDay;
        public WorldMap Map => runtime.map;
        public ActorCapabilityPolicy BoatTripCapabilities => WorldRuntime.BoatTripCapabilities;
        public ActorCapabilityPolicy TransportCapabilities => WorldRuntime.TransportCapabilities;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public AgentConversation? ConversationFor(string actor) => runtime.ConversationFor(actor);
        public InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(runtime.HandcartWorld, actor);
        public bool HasGuardianPlacementTask(string actor) => FamilySystem.HasGuardianPlacementTask(runtime.FamilyGuardianWorld, actor);
        public IReadOnlyList<string> MovingCareGroup(string actor) => runtime.MovingCareGroup(actor);
        public bool IsActiveTownProjectDelivery(string lot) => runtime.IsActiveTownProjectDelivery(lot);
        public PortGeometry PortGeometryFor(PlacedBuilding port) => runtime.PortGeometryFor(port);
        public HashSet<GridPoint> PortObstacles(string? exceptProjectId = null, string? exceptPortId = null, bool protectDocks = false) => runtime.PortObstacles(exceptProjectId, exceptPortId, protectDocks);
        public PlacedBuilding? Port(string id) => runtime.Port(id);
        public bool PortIsLegal(PlacedBuilding port) => runtime.PortIsLegal(port);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public ActionCheck CheckActionPlan(ActionPlan plan) => runtime.CheckActionPlan(plan);
        public void SetBoatOrderTravelStatus(BoatTripRequest request, OrderStatus status, string? reason) => runtime.SetBoatOrderTravelStatus(request, status, reason);
        public void CreditBoatOrderArrival(BoatTripRequest request) => runtime.CreditBoatOrderArrival(request);
        public IEnumerable<GridPoint> FreePortDocks(PlacedBuilding port, string? exceptBoatId = null) => TransportSystem.FreePortDocks(runtime.BoatWorld, port, exceptBoatId);
        public void AppendEvent(WorldEventPayload payload, GridPoint? position = null) => runtime.AppendEvent(payload, position);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public void MoveToward(string actor, AgentState state, GridPoint destination, string reason) => runtime.MoveToward(actor, state, destination, reason);
        public IReadOnlyList<GridPoint> ReachBoatGeography(BoatState boat, PlacedBuilding destination) => runtime.ReachBoatGeography(boat, destination);
        public void MoveGroundCargo(IReadOnlyList<string> remaining, GridPoint position, bool preserveCustody) => runtime.RunInventoryCommand(InventorySystem.MoveGroundCargo(remaining, position, preserveCustody));
        public GridPoint? FreePortDock(PlacedBuilding port, string? exceptBoatId = null) => TransportSystem.FreePortDock(runtime.BoatWorld, port, exceptBoatId);
        public void SetTownProject(string townId, TownProject project) => runtime.SetTownProject(townId, project);
        public GroundOccupancy Ground => runtime.Ground;
        public IReadOnlyList<BuildingDefinition> Definitions => runtime.worldContent.Buildings;
    }
}
