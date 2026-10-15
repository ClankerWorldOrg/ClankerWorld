using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICustodyOrdersWorld
{
    OrderStore Orders { get; }
    ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    void CancelFarmWork(FarmFieldState field);
    ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> ReturnableBorrowedGoods(string actor);
    bool ReadyForBriefInteraction(string actor);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    bool VesselFits(InventoryLot lot, int room);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    ClankerWorld.Simulation.World.WorldRuntime.BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null);
    string BorrowedReturnReceipt(string move);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    void MarkCheckpointCurrent();
}
