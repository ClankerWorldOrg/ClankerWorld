using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICollectionOrdersWorld
{
    WorldMap Map { get; }
    bool ReadyForBriefInteraction(string actor);
    int FreeCarryCapacity(string actor);
    ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot);
    string CollectionOrderGoodsName(OrderTask order);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> PersonalGoodsAwaitingCollection(string actor);
    bool IsEdibleFood(string kind);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647);
    string PersonalCollectionReceipt(string move);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    bool VesselFits(InventoryLot lot, int room);
}
