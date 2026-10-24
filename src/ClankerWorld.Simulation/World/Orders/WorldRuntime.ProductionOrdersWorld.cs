using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IProductionOrdersWorld ProductionOrdersWorld => new ProductionOrdersAdapter(this);

    private sealed class ProductionOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IProductionOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.PlacedBuilding? ProductionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => OrderSystem.ProductionOrderBuilding(Runtime.ProductionOrdersWorld, instruction, recipe);
        public bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null) => Runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool CanResumeHeldInputs(string actor, string buildingId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Identity> reservationIds) => CommunitySystem.CanResumeHeldInputs(Runtime.CommunityWorkWorld, actor, buildingId, reservationIds);
        public bool TryResumeKnife(string actor, string jobId, out string? knifeId) => CommunitySystem.TryResumeKnife(Runtime.CommunityWorkWorld, actor, jobId, out knifeId);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.CancelProductionForOrder(instruction);
        public string ProductionOrderReceipt(string jobId) => WorldRuntime.ProductionOrderReceipt(jobId);
        public void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => Runtime.RunInventoryCommand(InventorySystem.OrdersExtendOrderReservations(reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<Identity> reservations) => Runtime.RunInventoryCommand(InventorySystem.OrdersHoldOrderReservations(reservations));
        public void ReleaseProductionOrderInputs(WorldProductionJob job) => Runtime.RunInventoryCommand(InventorySystem.ReleaseProductionOrderInputs(job));
        public void ResumeProductionJob(string job, long deadline, string? knife) => Runtime.productionSystem.ResumeJob(job, deadline, knife);
        public void PauseProductionJob(string job, long tick) => Runtime.productionSystem.PauseJob(job, tick);
        public void CancelProductionJobs(IReadOnlySet<string> jobs) => Runtime.productionSystem.CancelJobs(jobs);
    }
}
