namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IFieldOrdersWorld FieldOrdersWorld => new FieldOrdersAdapter(this);

    private sealed class FieldOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IFieldOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action) => WorldRuntime.FieldOrderKind(action);
        public global::ClankerWorld.Simulation.World.LandFertility Fertility => Runtime.fertility;
        public global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household) => Runtime.HouseholdLandHeldByOthers(household);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(global::ClankerWorld.Simulation.World.GridPoint origin) => Runtime.NearbyFarmTiles(origin);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.FarmFieldState> Fields => Runtime.fields;
        public bool HasOtherAgentClaimedPlanting(global::ClankerWorld.Simulation.World.FarmFieldState field, string actor) => Runtime.HasOtherAgentClaimedPlanting(field, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, global::ClankerWorld.Simulation.World.FarmFieldState field, string crop, bool requirePickupRoute = false) => Runtime.PlantingStock(actor, field, crop, requirePickupRoute);
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => Runtime.FarmWorkFor(workerId);
        public global::ClankerWorld.Simulation.World.GridPoint? FieldOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.FieldOrderSite(instruction, person);
        public bool ContinueFarmWork(string workerId, out global::ClankerWorld.Simulation.World.FarmFieldState? completed) => Runtime.ContinueFarmWork(workerId, out completed);
        public string FieldWorkOrderReceipt(string actor, long tick, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.FarmWorkKind kind, int cycle) => WorldRuntime.FieldWorkOrderReceipt(actor, tick, position, kind, cycle);
        public void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field) => Runtime.CancelFarmWork(field);
        public void ApplyFieldCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, string? orderInstructionId = null) => Runtime.ApplyFieldCandidate(actor, state, candidate, orderInstructionId);
        public string FarmCandidate(global::ClankerWorld.Simulation.World.FarmWorkKind kind, global::ClankerWorld.Simulation.World.GridPoint point, string? crop = null) => WorldRuntime.FarmCandidate(kind, point, crop);
        public bool FarmableFreeTile(GridPoint point, LandFertility fertility) => Runtime.farmingSystem.FarmableFreeTile(Runtime.farmingWorld, point, fertility);
    }
}
