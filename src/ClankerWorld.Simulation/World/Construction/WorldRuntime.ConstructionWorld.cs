using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IConstructionPlanningWorld ConstructionWorld => new ConstructionWorldAdapter(this);
    private IConstructionPlacementWorld ConstructionPlacementWorld => new ConstructionWorldAdapter(this);
    private IExpansionAdmissionWorld ExpansionAdmissionWorld => new ConstructionWorldAdapter(this);
    private IExpansionCompletionWorld ExpansionCompletionWorld => new ConstructionWorldAdapter(this);
    private IExpansionPlanningWorld ExpansionPlanningWorld => new ConstructionWorldAdapter(this);

    private sealed class ConstructionWorldAdapter(WorldRuntime runtime) : IConstructionPlacementWorld, IExpansionAdmissionWorld, IExpansionCompletionWorld, IExpansionPlanningWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public long NextProductionJobSequence => runtime.productionSystem.State.NextSequence;
        public InventoryCheckpoint Inventory => runtime.society.Checkpoint.Inventory;
        public int StoredQuantity(string building) => runtime.StoredQuantity(building);
        public HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household) => runtime.HouseResidentCapacity(household);
        public int AnimalYardCapacity(PlacedBuilding building) => runtime.AnimalYardCapacity(building);
        public (GridPoint Position, BuildingFootprintRevision Footprint) ExpansionShapeForOrder(PlacedBuilding building, OwnerQueuedInstruction? instruction) =>
            runtime.ExpansionShapeForOrder(building, instruction);
        public ExpansionMaterialStep? PlanExpansionMaterialStep(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) =>
            runtime.PlanExpansionMaterialStep(actor, building, costs);
        public ActionCheck CheckExpansionMaterialStep(ExpansionActionPlan plan, PlacedBuilding building) => runtime.CheckExpansionMaterialStep(plan, building);
        public IEnumerable<InventoryLot> ExpansionMaterialLots(string actor, PlacedBuilding building) => runtime.ExpansionMaterialLots(actor, building);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure) =>
            runtime.CanFitExpansion(building, position, footprint, out failure);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string actor) => runtime.PendingInstructionFor(actor);
        public PlacedBuilding? ExpansionOrderBuilding(OwnerQueuedInstruction instruction) => runtime.ExpansionOrderBuilding(instruction);
        public bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure) => runtime.MayExpandBuilding(actor, building, out failure);
        public ExpansionActionPlan? PlanExpansion(string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction) => runtime.PlanExpansion(actor, building, instruction);
        public ActionCheck CheckExpansion(ExpansionActionPlan plan) => runtime.CheckExpansion(plan);
        public bool HasExpansionMaterials(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) => runtime.HasExpansionMaterials(actor, building, costs);
        public ExpansionMaterialInput[]? PlanExpansionInputs(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) => runtime.PlanExpansionInputs(actor, building, costs);
        public int SkilledBuildingWorkTicks(string actor, int ticks) => runtime.SkilledWorkTicks(actor, SkillKind.Building, ticks);
        public string ExpansionJobId(long sequence) => WorldRuntime.ExpansionJobId(sequence);
        public IReadOnlyList<string> ReserveExpansionInputs(IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string id, long expiry)
        {
            IReadOnlyList<string> ids = [];
            runtime.ApplyInventoryTransition(inventory => WorldRuntime.ReserveExpansionInputs(inventory, inputs, actor, id, expiry, out ids));
            return ids;
        }
        public void AdvanceExpansionSequence() => runtime.productionSystem.AdvanceExpansionSequence();
        public void BindStartedExpansionOrder(OwnerQueuedInstruction instruction, BuildingExpansionJob job) => runtime.BindStartedExpansionOrder(instruction, job);
        public bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure, string jobId) =>
            runtime.CanFitExpansion(building, position, footprint, out failure, jobId);
        public void CompleteExpansionInputs(IReadOnlyList<string> ids) => ResolveExpansionInputs(ids, completed: true);
        public void CancelExpansionInputs(IReadOnlyList<string> ids) => ResolveExpansionInputs(ids, completed: false);
        private void ResolveExpansionInputs(IReadOnlyList<string> ids, bool completed) => runtime.ApplyInventoryTransition(inventory =>
        {
            foreach (var id in ids)
                if (completed) inventory = InventoryAuthority.ConsumeReservation(inventory, id);
                else if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                    inventory = InventoryAuthority.ReleaseReservation(inventory, id, "expansion_cancelled");
            return inventory;
        });
        public void RecordExpandedWork(PlacedBuilding building, BuildingExpansionJob job)
        {
            if (runtime.towns.SingleOrDefault(town => town.Id == building.TownId) is { } town)
                runtime.SetTown(town with { BorderTiles = TownBorderRules.Expand(runtime.map, town, ExpansionTiles(job)) });
            runtime.CreditCompletedWork(job.WorkerId, "building");
        }
        public void CreditExpansionOrderJob(BuildingExpansionJob job) => runtime.CreditExpansionOrderJob(job);
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public string NormalizeRequiredText(string? value, string name) => WorldRuntime.NormalizeRequiredText(value, name);
        public bool CanPlaceBuilding(BuildingDefinition definition, GridPoint position, out string failure) => runtime.CanPlaceBuilding(definition, position, out failure);
        public void ConsumeConstructionInputs(IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner) =>
            runtime.ApplyInventoryTransition(inventory => WorldRuntime.ConsumeConstructionInputs(inventory, inputs, purpose, owner));
        public void AssignBuildingToTown(PlacedBuilding building, BuildingDefinition definition) => runtime.AssignBuildingToTown(building, definition);
        public void AppendEvent(string kind, string detail, GridPoint? position = null) => runtime.AppendEvent(kind, detail, position);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public string? TownForResident(string actor) => runtime.TownForResident(actor);
        public OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, AgentProject? project) => runtime.ConstructionInstructionForProject(actor, project);
        public string BuildingConstructionOwner(string actor, BuildingDefinition definition) => runtime.BuildingConstructionOwner(actor, definition);
        public string BuildInstanceId(string actor, BuildingDefinition definition) => runtime.BuildInstanceId(actor, definition);
        public bool IsHouseholdBuilding(BuildingDefinition definition) => definition.Tags.Any(IsHouseholdBuildingTag);
        public string? HouseholdBuildingKind(BuildingDefinition definition) => WorldRuntime.HouseholdBuildingKind(definition);
        public PlacedBuilding? HouseholdBuildingWithTag(string household, string tag) => runtime.HouseholdBuildingWithTag(household, tag);
        public PlacedBuilding? FarmhouseForHousehold(string household) => runtime.FarmhouseForHousehold(household);
        public bool HouseholdBuildingProjectInProgress(string household, string kind, string actor) => runtime.HouseholdBuildingProjectInProgress(household, kind, actor);
        public bool BuildingIdentityIsReserved(string instance, string? instruction) => runtime.BuildingIdentityIsReserved(instance, instruction);
        public TownLayoutContext CreateTownLayoutContext(string actor, GridPoint site, BuildingDefinition definition) => runtime.CreateTownLayoutContext(actor, site, definition);
        public IEnumerable<GoodsMatch> ConstructionGoods(InventoryCheckpoint inventory, GoodsRequest request) => runtime.ProductionGoods(inventory, request);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) =>
            runtime.Reach(actor, target, purpose, range, load, swimming);
    }
}
