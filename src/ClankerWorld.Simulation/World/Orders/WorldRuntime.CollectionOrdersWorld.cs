using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICollectionOrdersWorld CollectionOrdersWorld => new CollectionOrdersAdapter(this);

    private sealed class CollectionOrdersAdapter(WorldRuntime runtime) : ICollectionOrdersWorld
    {
        public WorldMap Map => runtime.map;
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public string CollectionOrderGoodsName(OrderTask order) => WorldRuntime.CollectionOrderGoodsName(order);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => runtime.RecoverablePersonalGoods(actor, kind);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> PersonalGoodsAwaitingCollection(string actor) => runtime.PersonalGoodsAwaitingCollection(actor);
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public string PersonalCollectionReceipt(string move) => WorldRuntime.PersonalCollectionReceipt(move);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
    }
}
