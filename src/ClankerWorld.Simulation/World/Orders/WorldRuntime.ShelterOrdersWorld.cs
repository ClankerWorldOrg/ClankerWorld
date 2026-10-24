namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IShelterOrdersWorld ShelterOrdersWorld => new ShelterOrdersAdapter(this);

    private sealed class ShelterOrdersAdapter(WorldRuntime runtime) : IShelterOrdersWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool HasHouseGuestInvitation(string actor, string houseId) => runtime.HasHouseGuestInvitation(actor, houseId);
        public bool IsTownHallStormRefuge(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.IsTownHallStormRefuge(actor, building);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool IsSwimming(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.IsSwimming(actor, position);
        public bool IsTownHall(global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.IsTownHall(building);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleHeatingBuildings(string actor) => runtime.AccessibleHeatingBuildings(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true) => runtime.AccessibleShelters(actor, includeStormRefuge);
        public global::ClankerWorld.Simulation.World.WeatherKind WeatherAt(global::ClankerWorld.Simulation.World.GridPoint position) => runtime.WeatherAt(position);
        public bool NaturalStormCover(global::ClankerWorld.Simulation.World.GridPoint point) => runtime.NaturalStormCover(point);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? BoundShelterBuilding(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => OrderSystem.BoundShelterBuilding(runtime.ShelterOrdersWorld, actor, order);
        public bool IsFireLit(global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.IsFireLit(building);
        public bool ShelterBuildingCovers(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint point) => OrderSystem.ShelterBuildingCovers(runtime.ShelterOrdersWorld, actor, building, point);
        public bool ShelterBuildingKnown(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building) => OrderSystem.ShelterBuildingKnown(runtime.ShelterOrdersWorld, actor, person, building);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.World.GridPoint? ReachableHallShelterPoint(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.PlacedBuilding hall) => runtime.ReachableHallShelterPoint(actor, origin, hall);
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.World.GridPoint? NearbyNaturalStormCover(string actor, global::ClankerWorld.Simulation.World.GridPoint origin) => runtime.NearbyNaturalStormCover(actor, origin);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources => runtime.resources;
        public bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache) => runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public bool CanReturnWithHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true) => runtime.CanReturnWithHarvest(actor, itemKind, source, destination, destinationRange, useHarvestBonus);
        public bool HasCarriedOwnItem(string actor, string kind) => runtime.HasCarriedOwnItem(actor, kind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.MapResource? KnownOrderFirewoodSource(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null) => OrderSystem.KnownOrderFirewoodSource(runtime.ShelterOrdersWorld, actor, person, returnTo);
        public global::ClankerWorld.Simulation.World.SurvivalState? SurvivalState => runtime.survivalState;
        public global::ClankerWorld.Simulation.World.OwnerShelterBinding? SelectShelterBinding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.SelectShelterBinding(runtime.ShelterOrdersWorld, instruction, person);
        public bool CanInspectShelterTarget(global::ClankerWorld.Simulation.World.OrderTask order, string actor, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.CanInspectShelterTarget(runtime.ShelterOrdersWorld, order, actor, person);
        public bool ShelterBindingIsAvailable(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => OrderSystem.ShelterBindingIsAvailable(runtime.ShelterOrdersWorld, actor, order);
        public bool CanObtainFirewood(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null) => OrderSystem.CanObtainFirewood(runtime.ShelterOrdersWorld, actor, person, returnTo);
        public OrderStore Orders => runtime.orderStore;
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ShelterOrderBlockedReason(instruction, person);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FireFuelEffect? TendFireAt(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.TendFireAt(actor, person, building);
        public long WorldTick => runtime.WorldTick;
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ShelterOrderReceipt(string instructionId, global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.ShelterOrderReceipt(instructionId, order);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
