namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IExpansionOrdersWorld ExpansionOrdersWorld => new ExpansionOrdersAdapter(this);

    private sealed class ExpansionOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IExpansionOrdersWorld
    {
        public string ExpansionOwner(global::ClankerWorld.Simulation.World.PlacedBuilding building) => WorldRuntime.ExpansionOwner(building);
        public bool ExpansionJobMatchesOrder(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.ExpansionJobMatchesOrder(job, instruction);
        public global::System.Collections.Generic.IEnumerable<(global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(global::ClankerWorld.Simulation.World.PlacedBuilding building) => Runtime.ExpansionShapes(building);
        public bool CanFitExpansion(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true) => Runtime.CanFitExpansion(building, position, footprint, out failure, ownJobId, requireLandRights);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? ExpansionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ExpansionOrderBuilding(Runtime.ExpansionOrdersWorld, instruction);
        public bool MayExpandBuilding(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, out string failure) => Runtime.MayExpandBuilding(actor, building, out failure);
        public (global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint) ExpansionShapeForOrder(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => OrderSystem.ExpansionShapeForOrder(Runtime.ExpansionOrdersWorld, building, instruction);
        public bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null) => Runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool CanAcquireExpansionMaterials(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> costs) => Runtime.CanAcquireExpansionMaterials(actor, building, costs);
        public global::ClankerWorld.Simulation.World.BuildingExpansionJob? BoundExpansionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.BoundExpansionJob(Runtime.ExpansionOrdersWorld, instruction);
        public bool ExpansionOrderInputsAvailable(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.PlacedBuilding building) => OrderSystem.ExpansionOrderInputsAvailable(Runtime.ExpansionOrdersWorld, job, building);
        public global::ClankerWorld.Simulation.World.OrderSystem.ExpansionOrderPlan? ExpansionPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExpansionPlanFor(Runtime.ExpansionOrdersWorld, instruction, person);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public void ApplyBuildingExpansionCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string buildingId, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? orderInstruction = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyBuildingExpansionCandidate(actor, state, buildingId, orderInstruction, originalPlan);
        public global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(global::ClankerWorld.Simulation.World.PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelExpansionForOrder(Runtime.ExpansionOrdersWorld, instruction);
        public string ExpansionOrderReceipt(string jobId) => WorldRuntime.ExpansionOrderReceipt(jobId);
        public void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => Runtime.RunInventoryCommand(InventorySystem.ExtendOrderReservations(reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<Identity> reservations) => Runtime.RunInventoryCommand(InventorySystem.HoldOrderReservations(reservations));
        public void ReleaseExpansionOrderInputs(BuildingExpansionJob job) => Runtime.RunInventoryCommand(InventorySystem.ReleaseExpansionOrderInputs(job));
        public void ResumeExpansion(string job, long deadline) => Runtime.constructionSystem.Buildings.ResumeExpansion(job, deadline);
        public void PauseExpansion(string job, long tick) => Runtime.constructionSystem.Buildings.PauseExpansion(job, tick);
        public void CancelExpansions(IReadOnlySet<string> jobs, string reason) => Runtime.constructionSystem.Buildings.CancelPausedExpansions(jobs, reason);
    }
}
