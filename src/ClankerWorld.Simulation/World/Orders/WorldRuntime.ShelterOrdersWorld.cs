using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IShelterOrdersWorld ShelterOrdersWorld => new ShelterOrdersAdapter(this);

    private sealed class ShelterOrdersAdapter(WorldRuntime runtime) : IShelterOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public WorldMap Map => runtime.map;
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public IReadOnlyDictionary<string, ResourceState> Resources => runtime.resources;
        public SurvivalState? Survival => runtime.survivalState;
        public OrderStore Orders => runtime.orderStore;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public bool HasHouseGuestInvitation(string actor, string houseId) => runtime.HasHouseGuestInvitation(actor, houseId);
        public bool IsTownHallStormRefuge(string actor, PlacedBuilding building) => runtime.IsTownHallStormRefuge(actor, building);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public bool IsSwimming(string actor, GridPoint position) => runtime.IsSwimming(actor, position);
        public bool IsTownHall(PlacedBuilding building) => runtime.IsTownHall(building);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.PlacedBuilding> AccessibleHeatingBuildings(string actor) => runtime.AccessibleHeatingBuildings(actor);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true) => runtime.AccessibleShelters(actor, includeStormRefuge);
        public ClankerWorld.Simulation.World.WeatherKind WeatherAt(GridPoint position) => runtime.WeatherAt(position);
        public bool NaturalStormCover(GridPoint point) => runtime.NaturalStormCover(point);
        public bool IsFireLit(PlacedBuilding building) => runtime.IsFireLit(building);
        public ClankerWorld.Simulation.World.GridPoint? ReachableHallShelterPoint(string actor, GridPoint origin, PlacedBuilding hall) => runtime.ReachableHallShelterPoint(actor, origin, hall);
        public bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public ClankerWorld.Simulation.World.GridPoint? NearbyNaturalStormCover(string actor, GridPoint origin) => runtime.NearbyNaturalStormCover(actor, origin);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool CanGatherFromSource(string actor, string itemKind, MapResource source, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache) => runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public bool CanReturnWithHarvest(string actor, string itemKind, MapResource source, GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true) => runtime.CanReturnWithHarvest(actor, itemKind, source, destination, destinationRange, useHarvestBonus);
        public bool HasCarriedOwnItem(string actor, string kind) => runtime.HasCarriedOwnItem(actor, kind);
        public ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool RecordKnowledgeFact(string actor, GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public ClankerWorld.Simulation.World.WorldRuntime.FireFuelEffect? TendFireAt(string actor, AgentState person, PlacedBuilding building) => runtime.TendFireAt(actor, person, building);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ShelterOrderReceipt(string instructionId, OrderTask order) => WorldRuntime.ShelterOrderReceipt(instructionId, order);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
