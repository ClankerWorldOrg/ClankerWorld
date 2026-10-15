using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IFieldOrdersWorld FieldOrdersWorld => new FieldOrdersAdapter(this);

    private sealed class FieldOrdersAdapter(WorldRuntime runtime) : IFieldOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public LandFertility Fertility => runtime.fertility;
        public WorldMap Map => runtime.map;
        public IReadOnlyList<FarmFieldState> Fields => runtime.fields;
        public long WorldTick => runtime.WorldTick;
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action) => WorldRuntime.FieldOrderKind(action);
        public System.Collections.Generic.HashSet<ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household) => runtime.HouseholdLandHeldByOthers(household);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(GridPoint origin) => runtime.NearbyFarmTiles(origin);
        public bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public bool HasOtherAgentClaimedPlanting(FarmFieldState field, string actor) => runtime.HasOtherAgentClaimedPlanting(field, actor);
        public ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, FarmFieldState field, string crop, bool requirePickupRoute = false) => runtime.PlantingStock(actor, field, crop, requirePickupRoute);
        public ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => runtime.FarmWorkFor(workerId);
        public bool ContinueFarmWork(string workerId, out FarmFieldState? completed) => runtime.ContinueFarmWork(workerId, out completed);
        public string FieldWorkOrderReceipt(string actor, long tick, GridPoint position, FarmWorkKind kind, int cycle) => WorldRuntime.FieldWorkOrderReceipt(actor, tick, position, kind, cycle);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void CancelFarmWork(FarmFieldState field) => runtime.CancelFarmWork(field);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void ApplyFieldCandidate(string actor, AgentState state, string candidate, string? orderInstructionId = null) => runtime.ApplyFieldCandidate(actor, state, candidate, orderInstructionId);
        public string FarmCandidate(FarmWorkKind kind, GridPoint point, string? crop = null) => WorldRuntime.FarmCandidate(kind, point, crop);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool FarmableFreeTile(GridPoint point, LandFertility fertility) => runtime.farmingSystem.FarmableFreeTile(runtime.farmingWorld, point, fertility);
    }
}
