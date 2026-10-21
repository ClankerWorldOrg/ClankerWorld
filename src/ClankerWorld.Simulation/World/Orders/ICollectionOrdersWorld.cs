namespace ClankerWorld.Simulation.World;

internal interface ICollectionOrdersWorld
{
    bool ReadyForBriefInteraction(string actor);
    int FreeCarryCapacity(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CollectionOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.GoodsUse use = (global::ClankerWorld.Simulation.World.GoodsUse)4);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    string CollectionOrderGoodsName(global::ClankerWorld.Simulation.World.OrderTask order);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalGoodsAwaitingCollection(string actor);
    bool IsEdibleFood(string kind);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647);
    string PersonalCollectionReceipt(string move);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room);
}
