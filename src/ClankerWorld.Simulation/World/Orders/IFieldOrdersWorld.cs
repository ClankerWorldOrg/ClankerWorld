using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFieldOrdersWorld
{
    SocietyCheckpoint Society { get; }
    LandFertility Fertility { get; }
    WorldMap Map { get; }
    IReadOnlyList<FarmFieldState> Fields { get; }
    long WorldTick { get; }
    string HouseholdFor(string actor);
    bool IsAdult(string actor);
    ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action);
    System.Collections.Generic.HashSet<ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(GridPoint origin);
    bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    bool HasOtherAgentClaimedPlanting(FarmFieldState field, string actor);
    ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, FarmFieldState field, string crop, bool requirePickupRoute = false);
    ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    bool ContinueFarmWork(string workerId, out FarmFieldState? completed);
    string FieldWorkOrderReceipt(string actor, long tick, GridPoint position, FarmWorkKind kind, int cycle);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    void CancelFarmWork(FarmFieldState field);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void ApplyFieldCandidate(string actor, AgentState state, string candidate, string? orderInstructionId = null);
    string FarmCandidate(FarmWorkKind kind, GridPoint point, string? crop = null);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    bool FarmableFreeTile(GridPoint point, LandFertility fertility);
}
