namespace ClankerWorld.Simulation.World;

internal interface IShelterOrdersWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasHouseGuestInvitation(string actor, string houseId);
    bool IsTownHallStormRefuge(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    bool IsSwimming(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    bool IsTownHall(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleHeatingBuildings(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true);
    global::ClankerWorld.Simulation.World.WeatherKind WeatherAt(global::ClankerWorld.Simulation.World.GridPoint position);
    bool NaturalStormCover(global::ClankerWorld.Simulation.World.GridPoint point);
    global::ClankerWorld.Simulation.World.PlacedBuilding? BoundShelterBuilding(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool IsFireLit(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool ShelterBuildingCovers(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint point);
    bool ShelterBuildingKnown(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::ClankerWorld.Simulation.World.GridPoint? ReachableHallShelterPoint(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.PlacedBuilding hall);
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.World.GridPoint? NearbyNaturalStormCover(string actor, global::ClankerWorld.Simulation.World.GridPoint origin);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources { get; }
    bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache);
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true);
    bool CanReturnWithHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true);
    bool HasCarriedOwnItem(string actor, string kind);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    global::ClankerWorld.Simulation.World.MapResource? KnownOrderFirewoodSource(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null);
    global::ClankerWorld.Simulation.World.SurvivalState? SurvivalState { get; }
    global::ClankerWorld.Simulation.World.OwnerShelterBinding? SelectShelterBinding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanInspectShelterTarget(global::ClankerWorld.Simulation.World.OrderTask order, string actor, global::ClankerWorld.Simulation.World.AgentState person);
    bool ShelterBindingIsAvailable(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool CanObtainFirewood(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null);
    OrderStore Orders { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    global::ClankerWorld.Simulation.World.WorldRuntime.FireFuelEffect? TendFireAt(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    long WorldTick { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string ShelterOrderReceipt(string instructionId, global::ClankerWorld.Simulation.World.OrderTask order);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    void MarkCheckpointCurrent();
}
