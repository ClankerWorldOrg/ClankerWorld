namespace ClankerWorld.Simulation.World;

internal interface ICustodyOrdersWorld
{
    global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    OrderStore Orders { get; }
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnableBorrowedGoods(string actor);
    bool ReadyForBriefInteraction(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId);
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null);
    global::ClankerWorld.Simulation.World.WorldRuntime.BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null);
    string BorrowedReturnReceipt(string move);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    void MarkCheckpointCurrent();
}
