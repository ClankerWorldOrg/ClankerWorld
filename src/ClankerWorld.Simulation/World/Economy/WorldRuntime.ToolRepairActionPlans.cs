using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<InventoryLot> CarriedRepairTools(string actor, string household) =>
        FindGoods(new(GoodsUse.CarriedInputs, actor, new([actor, household]),
            new(ToolProgressionRules.All.Select(tool => tool.ItemKind).ToArray()))).Matches.Select(match => match.Lot)
            .Where(tool => ToolProgressionRules.IsTopLevelCarriedLot(tool, actor) && tool.ConditionBasisPoints is > 0 and < 10_000)
            .OrderBy(tool => tool.Id, StringComparer.Ordinal);

    private ToolRepairActionPlan? PlanToolRepair(string actor, string toolId, InventoryLot? selectedTool = null)
    {
        var choice = new ToolRepairChoice(toolId);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), SmithDeliveryPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || this.HouseholdBuildingWithTag(household, "blacksmith") is not { } smith ||
            (selectedTool ?? CarriedRepairTools(actor, household).SingleOrDefault(tool => tool.Id == toolId)) is not { } target)
            return null;
        var needs = ToolProgressionRules.RepairMaterials(target.ItemKind);
        if (needs.Count == 0) return null;
        var inventory = society.Checkpoint.Inventory;
        var materials = new List<PlannedRepairMaterial>();
        var inputs = new List<PlannedRepairInput>();
        foreach (var need in needs)
        {
            var carried = SpendingGoods(actor, GoodsUse.Spend, [actor], need.ResourceId)
                .FirstOrDefault(match => ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor) && match.Quantity >= need.Amount)?.Lot;
            if (carried is not null) { materials.Add(new(carried.Id, need.ResourceId, need.Amount)); continue; }
            if (SharedPreparationItem(need.ResourceId, actor, smith.Position) is { } stock)
            {
                if (AvailableLotQuantity(stock) < need.Amount) return null;
                inputs.Add(new(need.ResourceId, need.Amount, stock.Id, stock.OwnerId, HouseholdStockPosition(stock),
                    HouseholdStockInteractionRange(stock), null, null, null, null));
            }
            else
            {
                if (MaterialSource(need.ResourceId, actor, smith.Position) is not { } source ||
                    ProjectMaterialHarvest(actor, need.ResourceId, source) is not { } harvest) return null;
                inputs.Add(new(need.ResourceId, need.Amount, null, null, source.Position, ResourceInteractionRange, source, harvest,
                    harvest.ToolLotId is { } harvestTool ? inventory.GetLot(harvestTool).OwnerId : null,
                    harvest.ToolLotId is { } selectedHarvestTool ? inventory.GetLot(selectedHarvestTool).ItemKind : null));
            }
        }
        PlannedCargoStorage? storage = null;
        var required = inputs.Sum(input => input.CarryUnits);
        if (required > FreeCarryCapacity(actor))
        {
            var retained = materials.ToDictionary(material => material.StockId, material => material.Quantity, StringComparer.Ordinal);
            var protectedTools = inputs.Where(input => input.Harvest?.ToolLotId is not null).Select(input => input.Harvest!.ToolLotId!).ToArray();
            var cargo = OrdinarySpareCargo(actor, required - FreeCarryCapacity(actor), toolId, protectedTools, retained);
            if (cargo.Sum(move => move.PhysicalQuantity) < required - FreeCarryCapacity(actor) ||
                SpareCargoDestination(actor, agents[actor], cargo.Sum(move => move.PhysicalQuantity)) is not { } destination)
                return null;
            storage = new(actor, household, CaptureCargoMoves(cargo), destination.Position, destination.Range, destination.StorageBuildingId,
                Reach(actor, destination.Position, ReachPurpose.FoodCargoStorage, destination.Range, 0, SwimmingPolicy.WhenCapable));
        }
        else
        {
            var steps = inputs.Select(input => RepairCollectionStep(input)).ToArray();
            if (PlanInputCollection(actor, steps, smith.Position) is not { } ordered) return null;
            inputs = ordered.Select(step => inputs.Single(input => input.ItemKind == step.Kind)).ToList();
        }
        return new(actor, choice, target.OwnerId, target.ItemKind, household, smith.InstanceId, smith.Position,
            materials.AsReadOnly(), inputs.AsReadOnly(), storage,
            Reach(actor, smith.Position, ReachPurpose.ToolRepair, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private InputCollectionStep RepairCollectionStep(PlannedRepairInput input) => new(input.ItemKind, input.Target,
        input.InteractionRange, input.CarryUnits, input.StockId is { } stock ? society.Checkpoint.Inventory.GetLot(stock) : null,
        input.Source, input.Harvest?.ToolLotId);

    private ActionCheck CheckToolRepairPlan(ToolRepairActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || this.HouseholdBuildingWithTag(plan.HouseholdId, "blacksmith") is not { } smith ||
            smith.InstanceId != plan.SmithId || smith.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household Blacksmith has changed");
        if (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId), GoodsKinds.One(plan.ToolKind)),
                plan.Choice.ToolId) is not { Lot: var target } || !ToolProgressionRules.IsTopLevelCarriedLot(target, plan.Actor) ||
            target.ConditionBasisPoints is not (> 0 and < 10_000))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected worn tool is no longer available for repair");
        foreach (var material in plan.Materials)
            if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(material.ItemKind)),
                    material.StockId) is not { } match || match.Quantity < material.Quantity ||
                !ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material is no longer available");
        foreach (var input in plan.Inputs)
        {
            if (HasCarriedMaterial(plan.Actor, input.ItemKind, input.NeededQuantity))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry this repair input");
            if (input.StockId is { } stockId)
            {
                if (RecheckGoods(new(GoodsUse.ReachableHoldings, plan.Actor, GoodsOwners.One(input.OwnerId!), GoodsKinds.One(input.ItemKind)),
                        stockId) is not { } match || match.Quantity < input.NeededQuantity || !MayCollectGoodsAt(plan.Actor, match.Root) ||
                    match.Lot.ContainerLotId is not null || match.Lot.CarrierId is not null ||
                    HouseholdStockPosition(match.Lot) != input.Target || HouseholdStockInteractionRange(match.Lot) != input.InteractionRange)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair supplies are no longer available");
            }
            else
            {
                if (input.Source is not { } source || input.Harvest is not { } harvest || !map.Resources.Contains(source) ||
                    resources.GetValueOrDefault(source.Id) != ResourceState.Available ||
                    !EcologyRules.Harvest(worldSystems.Ecology.GetResource(source.Id), 1).IsValid)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material source is no longer available");
                if (harvest.ToolLotId is { } harvestTool &&
                    (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(input.ToolOwnerId!), GoodsKinds.One(input.ToolKind!)),
                        harvestTool) is not { Lot: var tool } || !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor)))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair gathering tool is no longer available");
                if (HarvestReturnRoute(plan.Actor, source, plan.Destination, ReachPurpose.ToolRepair, input.CarryUnits).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected repair harvest cannot return to the Blacksmith");
            }
        }
        var required = plan.Inputs.Sum(input => input.CarryUnits);
        if (plan.CargoPreparation is { } cargo)
        {
            if (FreeCarryCapacity(plan.Actor) + cargo.Cargo.Sum(move => move.PhysicalQuantity) < required)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected cargo cannot make room for repair inputs");
            var storage = CheckCargoStorage(cargo);
            if (!storage.Allowed) return storage;
        }
        else if (required > FreeCarryCapacity(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected repair inputs");
        else if (!RecheckInputCollection(plan.Actor, plan.Inputs.Select(RepairCollectionStep).ToArray(), plan.Destination))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected repair input trip is no longer possible");
        return agents[plan.Actor].Position == plan.Destination || Reach(plan.Actor, plan.Destination, ReachPurpose.ToolRepair,
            0, 0, SwimmingPolicy.WhenCapable).Count > 0 ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the Blacksmith is out of reach now");
    }

    private void ApplyToolRepairPlan(ToolRepairActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedToolRepairPlan(plan);
    }

    private void ApplyCheckedToolRepairPlan(ToolRepairActionPlan plan)
    {
        if (plan.CargoPreparation is { } cargo) { ApplyCargoStorage(cargo); return; }
        var state = agents[plan.Actor];
        if (plan.Inputs.Count > 0)
        {
            var input = plan.Inputs[0];
            if (input.StockId is { } stock)
                ApplyPlannedEquipmentPickup(plan.Actor, new(stock, input.OwnerId!, input.ItemKind, input.Target, input.InteractionRange));
            else GatherPlannedProjectMaterial(plan.Actor, state, input.ItemKind, input.Source!, input.Harvest!);
            return;
        }
        if (state.Position != plan.Destination) { MoveToward(plan.Actor, state, plan.Destination, "repair_tool", 0); return; }
        CompletePlannedToolRepair(plan.Actor, state, society.Checkpoint.Inventory.GetLot(plan.Choice.ToolId),
            plan.SmithId, plan.Materials);
    }
}
