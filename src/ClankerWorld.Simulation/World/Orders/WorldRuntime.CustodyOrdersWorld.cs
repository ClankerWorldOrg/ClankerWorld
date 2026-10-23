namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICustodyOrdersWorld CustodyOrdersWorld => new CustodyOrdersAdapter(this);

    private sealed class CustodyOrdersAdapter(WorldRuntime runtime) : ICustodyOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => runtime.farmingSystem.FarmWorkFor(runtime.farmingWorld, workerId);
        public void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field) => runtime.farmingSystem.CancelFarmWork(runtime.farmingWorld, field);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public OrderStore Orders => runtime.orderStore;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnableBorrowedGoods(string actor) => runtime.ReturnableBorrowedGoods(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ReturnOrderGoods(instruction);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId) => runtime.CustodyOrderHouse(instruction, ownerId);
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ReturnOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ReturnOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null) => runtime.BindCustodyOrder(instruction, house, lotId);
        public global::ClankerWorld.Simulation.World.WorldRuntime.BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null) => runtime.ReturnBorrowedGoods(actor, lotId, maximumQuantity, requiredHouseId, requiredHouseholdId, requiredPosition);
        public string BorrowedReturnReceipt(string move) => WorldRuntime.BorrowedReturnReceipt(move);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
