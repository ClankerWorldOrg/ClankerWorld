namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IStorageOrdersWorld StorageOrdersWorld => new StorageOrdersAdapter(this);

    private sealed class StorageOrdersAdapter(WorldRuntime runtime) : IStorageOrdersWorld
    {
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId) => OrderSystem.CustodyOrderHouse(runtime.CustodyOrdersWorld, instruction, ownerId);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalStorageLots(string actor, string houseId) => runtime.PersonalStorageLots(actor, houseId);
        public string? StorageOrderItemKind(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.StorageOrderItemKind(order);
        public string StorageOrderSubject(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.StorageOrderSubject(order);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.StorageOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.StorageOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null) => OrderSystem.BindCustodyOrder(runtime.CustodyOrdersWorld, instruction, house, lotId);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null) => runtime.StorePersonalGoods(actor, lotId, maximumQuantity, requiredHouseId, requiredHouseholdId, requiredPosition);
        public string PersonalStorageReceipt(string move) => WorldRuntime.PersonalStorageReceipt(move);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
    }
}
