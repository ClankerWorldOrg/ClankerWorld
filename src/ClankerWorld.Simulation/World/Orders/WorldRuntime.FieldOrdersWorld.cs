namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IFieldOrdersWorld FieldOrdersWorld => new FieldOrdersAdapter(this);

    private sealed class FieldOrdersAdapter(WorldRuntime runtime) : IFieldOrdersWorld
    {
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public global::ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action) => WorldRuntime.FieldOrderKind(action);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.LandFertility Fertility => runtime.fertility;
        public global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household) => runtime.HouseholdLandHeldByOthers(household);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(global::ClankerWorld.Simulation.World.GridPoint origin) => FarmingSystem.NearbyFarmTiles(runtime.farmingWorld, origin);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.FarmFieldState> Fields => runtime.fields;
        public bool HasOtherAgentClaimedPlanting(global::ClankerWorld.Simulation.World.FarmFieldState field, string actor) => runtime.farmingSystem.HasOtherAgentClaimedPlanting(runtime.farmingWorld, field, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, global::ClankerWorld.Simulation.World.FarmFieldState field, string crop, bool requirePickupRoute = false) => FarmingSystem.PlantingStock(runtime.farmingWorld, actor, field, crop, requirePickupRoute);
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => runtime.farmingSystem.FarmWorkFor(runtime.farmingWorld, workerId);
        public global::ClankerWorld.Simulation.World.GridPoint? FieldOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.FieldOrderSite(instruction, person);
        public bool ContinueFarmWork(string workerId, out global::ClankerWorld.Simulation.World.FarmFieldState? completed) => runtime.farmingSystem.ContinueFarmWork(runtime.farmingWorld, workerId, out completed);
        public string FieldWorkOrderReceipt(string actor, long tick, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.FarmWorkKind kind, int cycle) => WorldRuntime.FieldWorkOrderReceipt(actor, tick, position, kind, cycle);
        public long WorldTick => runtime.WorldTick;
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field) => runtime.farmingSystem.CancelFarmWork(runtime.farmingWorld, field);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string FieldOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.FieldOrderBlockedReason(instruction);
        public void ApplyFieldCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, string? orderInstructionId = null) => runtime.farmingSystem.ApplyFieldCandidate(runtime.farmingWorld, actor, state, candidate, orderInstructionId);
        public string FarmCandidate(global::ClankerWorld.Simulation.World.FarmWorkKind kind, global::ClankerWorld.Simulation.World.GridPoint point, string? crop = null) => FarmingSystem.FarmCandidate(kind, point, crop);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool FarmableFreeTile(GridPoint point, LandFertility fertility) => runtime.farmingSystem.FarmableFreeTile(runtime.farmingWorld, point, fertility);
    }
}
