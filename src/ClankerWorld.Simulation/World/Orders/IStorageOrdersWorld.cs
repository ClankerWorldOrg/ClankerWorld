namespace ClankerWorld.Simulation.World;

internal interface IStorageOrdersWorld
{
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId);
    string HouseholdFor(string actor);
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalStorageLots(string actor, string houseId);
    string? StorageOrderItemKind(global::ClankerWorld.Simulation.World.OrderTask order);
    string StorageOrderSubject(global::ClankerWorld.Simulation.World.OrderTask order);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null);
    global::ClankerWorld.Simulation.World.WorldRuntime.PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null);
    string PersonalStorageReceipt(string move);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
}
