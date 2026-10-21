namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICollectionOrdersWorld CollectionOrdersWorld => new CollectionOrdersAdapter(this);

    private sealed class CollectionOrdersAdapter(WorldRuntime runtime) : ICollectionOrdersWorld
    {
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CollectionOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.GoodsUse use = (global::ClankerWorld.Simulation.World.GoodsUse)4) => runtime.CollectionOrderGoods(instruction, use);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public string CollectionOrderGoodsName(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.CollectionOrderGoodsName(order);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => runtime.RecoverablePersonalGoods(actor, kind);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalGoodsAwaitingCollection(string actor) => runtime.PersonalGoodsAwaitingCollection(actor);
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CollectionOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CollectionOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public string PersonalCollectionReceipt(string move) => WorldRuntime.PersonalCollectionReceipt(move);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room) => runtime.VesselFits(lot, room);
    }
}
