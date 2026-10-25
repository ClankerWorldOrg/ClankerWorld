namespace ClankerWorld.Simulation.World;

internal interface IFieldOrdersWorld
{
    string HouseholdFor(string actor);
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    global::ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.LandFertility Fertility { get; }
    global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(global::ClankerWorld.Simulation.World.GridPoint origin);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.FarmFieldState> Fields { get; }
    bool HasOtherAgentClaimedPlanting(global::ClankerWorld.Simulation.World.FarmFieldState field, string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, global::ClankerWorld.Simulation.World.FarmFieldState field, string crop, bool requirePickupRoute = false);
    global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    global::ClankerWorld.Simulation.World.GridPoint? FieldOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool ContinueFarmWork(string workerId, out global::ClankerWorld.Simulation.World.FarmFieldState? completed);
    string FieldWorkOrderReceipt(string actor, long tick, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.FarmWorkKind kind, int cycle);
    long WorldTick { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string FieldOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ApplyFieldCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, string? orderInstructionId = null);
    string FarmCandidate(global::ClankerWorld.Simulation.World.FarmWorkKind kind, global::ClankerWorld.Simulation.World.GridPoint point, string? crop = null);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool FarmableFreeTile(GridPoint point, LandFertility fertility);
}
