using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IStorageOrdersWorld StorageOrdersWorld => new StorageOrdersAdapter(this);

    private sealed class StorageOrdersAdapter(WorldRuntime runtime) : IStorageOrdersWorld
    {
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(OwnerQueuedInstruction instruction, string? ownerId) => runtime.CustodyOrderHouse(instruction, ownerId);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> PersonalStorageLots(string actor, string houseId) => runtime.PersonalStorageLots(actor, houseId);
        public string? StorageOrderItemKind(OrderTask order) => WorldRuntime.StorageOrderItemKind(order);
        public string StorageOrderSubject(OrderTask order) => WorldRuntime.StorageOrderSubject(order);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(OwnerQueuedInstruction instruction, PlacedBuilding house, string? lotId = null) => runtime.BindCustodyOrder(instruction, house, lotId);
        public ClankerWorld.Simulation.World.WorldRuntime.PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null) => runtime.StorePersonalGoods(actor, lotId, maximumQuantity, requiredHouseId, requiredHouseholdId, requiredPosition);
        public string PersonalStorageReceipt(string move) => WorldRuntime.PersonalStorageReceipt(move);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
    }
}
