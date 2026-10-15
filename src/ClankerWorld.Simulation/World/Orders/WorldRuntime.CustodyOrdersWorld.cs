using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICustodyOrdersWorld CustodyOrdersWorld => new CustodyOrdersAdapter(this);

    private sealed class CustodyOrdersAdapter(WorldRuntime runtime) : ICustodyOrdersWorld
    {
        public OrderStore Orders => runtime.orderStore;
        public ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => runtime.FarmWorkFor(workerId);
        public void CancelFarmWork(FarmFieldState field) => runtime.CancelFarmWork(field);
        public ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> ReturnableBorrowedGoods(string actor) => runtime.ReturnableBorrowedGoods(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public ClankerWorld.Simulation.World.WorldRuntime.BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null) => runtime.ReturnBorrowedGoods(actor, lotId, maximumQuantity, requiredHouseId, requiredHouseholdId, requiredPosition);
        public string BorrowedReturnReceipt(string move) => WorldRuntime.BorrowedReturnReceipt(move);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
