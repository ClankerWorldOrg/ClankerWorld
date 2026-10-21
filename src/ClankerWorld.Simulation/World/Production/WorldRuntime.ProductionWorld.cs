using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IProductionWorld ProductionWorld => new ProductionWorldAdapter(this);
    private IRecipeWorld RecipeWorld => new ProductionWorldAdapter(this);
    private IProductionStartWorld ProductionStartWorld => new ProductionWorldAdapter(this);

    private sealed class ProductionWorldAdapter(WorldRuntime runtime) : IProductionWorld, IProductionStartWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public string NormalizeRequiredText(string? value, string name) => WorldRuntime.NormalizeRequiredText(value, name);
        public bool HasCarriedUnreservedQuantities(string actor, IReadOnlyList<ContentQuantity> quantities) => runtime.HasCarriedUnreservedQuantities(actor, quantities);
        public int DestinationRoom(string building) => runtime.DestinationRoom(building);
        public int SkilledRecipeWorkTicks(string actor, RecipeDefinition recipe, int ticks) => runtime.SkilledWorkTicks(actor, runtime.SkillForRecipe(recipe), ticks);
        public string ProductionJobId(long sequence) => WorldRuntime.ProductionJobId(sequence);
        public IReadOnlyList<string> ReserveProductionInputs(RecipeProjectActionPlan plan, string purpose, long expiry)
        {
            IReadOnlyList<string> ids = [];
            runtime.ApplyInventoryTransition(inventory => ReserveRecipeInputs(inventory, plan, purpose, expiry, out ids));
            return ids;
        }
        public string? ToolMakingRequestJobFor(string worker, RecipeDefinition recipe, string building) => runtime.ToolMakingRequestJobFor(worker, recipe, building);
        public void BindToolMakingJob(WorldProductionJob job) => runtime.BindToolMakingJob(job);
        public void MarkCurrentSchema() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string actor) => runtime.PendingInstructionFor(actor);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public string ProductionOwnerFor(PlacedBuilding? building, string actor) => runtime.ProductionOwnerFor(building, actor);
        public bool IsHouseholdBuilding(BuildingDefinition definition) => definition.Tags.Any(IsHouseholdBuildingTag);
        public IEnumerable<GoodsMatch> ProductionGoods(InventoryCheckpoint inventory, GoodsRequest request, bool carried) =>
            runtime.ProductionGoods(inventory, request, carried);
        public System.Collections.ObjectModel.ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target,
            ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.Reach(actor, target, purpose, range, load, swimming);
        public void ReleaseProductionInputs(IReadOnlyList<Identity> ids, string reason) => runtime.ApplyInventoryTransition(inventory =>
        {
            foreach (var id in ids)
                if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                    inventory = InventoryAuthority.ReleaseReservation(inventory, id, reason);
            return inventory;
        });
        public void CompleteRecipeInventory(WorldProductionJob job, RecipeDefinition recipe, ToolWorkPlan? knife, long tick) =>
            runtime.CompleteRecipeInventory(job, recipe, knife, tick);
        public void CreditProducedWork(string worker, RecipeDefinition recipe) =>
            runtime.CreditCompletedWork(worker, "crafting", runtime.SkillForRecipe(recipe));
        public void LearnProducedRecipe(WorldProductionJob job, RecipeDefinition recipe, long tick) => runtime.LearnProducedRecipe(job, recipe, tick);
        public void CreditProductionOrderJob(WorldProductionJob job, RecipeDefinition recipe) => runtime.CreditProductionOrderJob(job, recipe);
        public void AppendEvent(WorldEventPayload payload, GridPoint? position = null) => runtime.AppendEvent(payload, position);
    }
}
