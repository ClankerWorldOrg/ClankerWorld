using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IStorageOrdersWorld
{
    bool ReadyForBriefInteraction(string actor);
    ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(OwnerQueuedInstruction instruction, string? ownerId);
    string HouseholdFor(string actor);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> PersonalStorageLots(string actor, string houseId);
    string? StorageOrderItemKind(OrderTask order);
    string StorageOrderSubject(OrderTask order);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(OwnerQueuedInstruction instruction, PlacedBuilding house, string? lotId = null);
    ClankerWorld.Simulation.World.WorldRuntime.PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null);
    string PersonalStorageReceipt(string move);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
}
