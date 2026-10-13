using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private SmithGatherActionPlan? PlanSmithGather(string actor, string itemKind,
        MapResource? selectedSource = null, PlacedBuilding? selectedSmith = null)
    {
        var choice = new SmithGatherChoice(itemKind);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), SmithDeliveryPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            (selectedSmith ?? BlacksmithForHousehold(household)) is not { } smith)
            return null;
        var source = selectedSource ?? (itemKind == "iron_ore" ? MaterialSource(itemKind, actor, smith.Position) :
            BlacksmithInputToGather(household, smith.InstanceId, actor, itemKind)?.Source);
        if (source is null) return null;
        var harvest = ProjectMaterialHarvest(actor, itemKind, source);
        PlannedCargoStorage? preparation = null;
        PlannedGatheringTool? pickup = null;
        if (harvest is { } load)
        {
            var missing = checked(load.Quantity + load.TreeSeedQuantity) - FreeCarryCapacity(actor);
            if (missing > 0)
            {
                var cargo = SpareCargoForFood(actor, missing, load.ToolLotId);
                if (cargo.Count == 0 || SpareCargoDestination(actor, agents[actor], cargo.Sum(move => move.PhysicalQuantity)) is not { } storage)
                    return null;
                preparation = new(actor, household, CaptureCargoMoves(cargo), storage.Position, storage.Range, storage.StorageBuildingId,
                    Reach(actor, storage.Position, ReachPurpose.FoodCargoStorage, storage.Range, 0, SwimmingPolicy.WhenCapable));
            }
        }
        else
        {
            if (itemKind == "iron_ore" || RequiredGatheringTool(itemKind, source) is not { } required ||
                FindReachableSharedGatheringTool(actor, required) is not { } available ||
                SharedItem(available.ItemKind, actor) is not { ContainerLotId: null } tool)
                return null;
            pickup = new(tool.Id, tool.OwnerId, tool.ItemKind, HouseholdStockPosition(tool), HouseholdStockInteractionRange(tool));
        }
        var inventory = society.Checkpoint.Inventory;
        return new(actor, choice, household, smith.InstanceId, smith.Position, source, harvest,
            harvest?.ToolLotId is { } toolId ? inventory.GetLot(toolId).OwnerId : null,
            harvest?.ToolLotId is { } selectedTool ? inventory.GetLot(selectedTool).ItemKind : null,
            pickup, preparation, Reach(actor, source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            harvest is null ? Array.AsReadOnly<GridPoint>([]) : HarvestReturnRoute(actor, source, smith.Position,
                ReachPurpose.MaterialGathering, checked(harvest.Quantity + harvest.TreeSeedQuantity)));
    }

    private ActionCheck CheckSmithGatherPlan(SmithGatherActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || BlacksmithForHousehold(plan.HouseholdId) is not { } smith ||
            smith.InstanceId != plan.SmithId || smith.Position != plan.Destination || CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their Blacksmith or delivery work has changed");
        var kind = plan.Choice.ItemKind;
        var missing = BlacksmithInputTarget(plan.SmithId, kind) - SmithStoredQuantity(plan.HouseholdId, plan.SmithId, kind) -
            (kind == "iron_ore" ? 0 : SmithIncomingQuantity(plan.SmithId, kind));
        if (missing <= 0 || kind == "iron_ore" && PersonalSmithOre(plan.Actor) is not null ||
            BlacksmithHasDeliverableInput(plan.HouseholdId, smith, plan.Actor, kind))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the Blacksmith no longer needs this gathering trip");
        if (DestinationRoom(plan.SmithId) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Blacksmith has no room for materials");
        if (!map.Resources.Contains(plan.Source) || resources.GetValueOrDefault(plan.Source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(plan.Source.Id), 1).IsValid)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected material source is no longer available");
        if (plan.Harvest is { ToolLotId: { } toolId } &&
            (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId!), GoodsKinds.One(plan.ToolKind!)), toolId) is not { Lot: var tool } ||
                !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gathering tool is no longer available");
        if (plan.CargoPreparation is { } cargo)
        {
            if (FreeCarryCapacity(plan.Actor) + cargo.Cargo.Sum(move => move.PhysicalQuantity) < plan.PhysicalQuantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected cargo cannot make room for this harvest");
            var storage = CheckCargoStorage(cargo);
            if (!storage.Allowed) return storage;
        }
        else if (plan.ToolPickup is { } pickup)
        {
            if (FreeCarryCapacity(plan.Actor) <= 0 || HasCarriedEquipmentAtLeast(plan.Actor, pickup.ItemKind))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected tool is no longer needed or cannot be carried");
            var sharedTool = CheckGatheringToolPickup(plan.Actor, pickup);
            if (!sharedTool.Allowed) return sharedTool;
        }
        else if (FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected harvest");
        if (plan.Harvest is not null ? HarvestReturnRoute(plan.Actor, plan.Source, plan.Destination,
                ReachPurpose.MaterialGathering, plan.PhysicalQuantity).Count == 0 :
            !CanReturnWithHarvest(plan.Actor, kind, plan.Source, plan.Destination))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected harvest cannot return to the Blacksmith now");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected material source is out of reach now");
    }

    private void ApplySmithGatherPlan(SmithGatherActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckSmithGatherPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (plan.CargoPreparation is { } cargo) { ApplyCargoStorage(cargo); return; }
        var state = agents[plan.Actor];
        if (plan.ToolPickup is { } pickup)
        {
            ApplyGatheringToolPickup(plan.Actor, pickup);
            return;
        }
        GatherPlannedProjectMaterial(plan.Actor, state, plan.Choice.ItemKind, plan.Source, plan.Harvest!);
    }
}
