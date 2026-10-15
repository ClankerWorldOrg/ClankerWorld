using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IShelterOrdersWorld
{
    SocietyCheckpoint Society { get; }
    int ResourceInteractionRange { get; }
    DeclarativeWorldContentState Content { get; }
    WorldMap Map { get; }
    WorldKnowledgeState Knowledge { get; }
    IReadOnlyDictionary<string, ResourceState> Resources { get; }
    SurvivalState? Survival { get; }
    OrderStore Orders { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    bool HasHouseGuestInvitation(string actor, string houseId);
    bool IsTownHallStormRefuge(string actor, PlacedBuilding building);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    bool IsSwimming(string actor, GridPoint position);
    bool IsTownHall(PlacedBuilding building);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.PlacedBuilding> AccessibleHeatingBuildings(string actor);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true);
    ClankerWorld.Simulation.World.WeatherKind WeatherAt(GridPoint position);
    bool NaturalStormCover(GridPoint point);
    bool IsFireLit(PlacedBuilding building);
    ClankerWorld.Simulation.World.GridPoint? ReachableHallShelterPoint(string actor, GridPoint origin, PlacedBuilding hall);
    bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    ClankerWorld.Simulation.World.GridPoint? NearbyNaturalStormCover(string actor, GridPoint origin);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    bool CanGatherFromSource(string actor, string itemKind, MapResource source, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache);
    int FreeCarryCapacity(string actor);
    ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, MapResource source, bool useHarvestBonus = true);
    bool CanReturnWithHarvest(string actor, string itemKind, MapResource source, GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true);
    bool HasCarriedOwnItem(string actor, string kind);
    ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool RecordKnowledgeFact(string actor, GridPoint position);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0);
    ClankerWorld.Simulation.World.WorldRuntime.FireFuelEffect? TendFireAt(string actor, AgentState person, PlacedBuilding building);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string ShelterOrderReceipt(string instructionId, OrderTask order);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    void MarkCheckpointCurrent();
}
