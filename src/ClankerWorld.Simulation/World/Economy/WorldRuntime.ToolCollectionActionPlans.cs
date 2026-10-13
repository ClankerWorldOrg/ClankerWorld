using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ToolGatheringPlan? PendingSmithHarvest(string actor) => HouseholdFor(actor) is { } household &&
        BlacksmithForHousehold(household) is { } smith && BlacksmithHarvestToPrepare(household, smith.InstanceId, actor) is { } missing
        ? ProjectMaterialHarvest(actor, missing.ItemKind, missing.Source) : null;

    private ToolCollectionActionPlan? PlanToolCollectionCandidate(string actor, string candidate) =>
        ToolCollectionChoice.Decode(candidate) is { } choice ? PlanToolCollection(actor, choice.ItemKind, pendingHarvest: PendingSmithHarvest(actor)) : null;

    private ToolCollectionActionPlan? PlanToolCollection(string actor, string itemKind,
        InventoryLot? selectedStock = null, ToolGatheringPlan? pendingHarvest = null)
    {
        var choice = new ToolCollectionChoice(itemKind);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), SmithDeliveryPolicy).Allowed ||
            ToolProgressionRules.Find(itemKind) is not { } definition || !MayCollectToolFamily(actor, definition.Family) ||
            (selectedStock ?? SharedItem(itemKind, actor)) is not { ContainerLotId: null } stock)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var pendingFamily = pendingHarvest?.ToolLotId is { } requiredTool
            ? ToolProgressionRules.Find(inventory.GetLot(requiredTool).ItemKind)?.Family : null;
        var minimumFree = pendingHarvest is null ? 0 : checked((definition.Family == pendingFamily
            ? definition.GatherQuantity : pendingHarvest.Quantity) + pendingHarvest.TreeSeedQuantity);
        PlannedCargoStorage? futureStorage = null;
        var missing = minimumFree - (FreeCarryCapacity(actor) - 1);
        if (missing > 0)
        {
            if (definition.Family != pendingFamily || HouseholdFor(actor) is not { } household) return null;
            var cargo = SpareCargoForFood(actor, missing);
            if (cargo.Count == 0 || SpareCargoDestination(actor, agents[actor], cargo.Sum(move => move.PhysicalQuantity)) is not { } storage)
                return null;
            futureStorage = new(actor, household, CaptureCargoMoves(cargo), storage.Position, storage.Range, storage.StorageBuildingId,
                Reach(actor, storage.Position, ReachPurpose.FoodCargoStorage, storage.Range, 0, SwimmingPolicy.WhenCapable));
        }
        var target = HouseholdStockPosition(stock);
        var range = HouseholdStockInteractionRange(stock);
        return new(actor, choice, HouseholdFor(actor), new(stock.Id, stock.OwnerId, stock.ItemKind, target, range),
            minimumFree, futureStorage, Reach(actor, target, ReachPurpose.MaterialGathering, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckToolCollectionPlan(ToolCollectionActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || ToolProgressionRules.Find(plan.Choice.ItemKind) is not { } definition ||
            !MayCollectToolFamily(plan.Actor, definition.Family))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household can no longer use this tool family");
        if (HasCarriedEquipmentAtLeast(plan.Actor, plan.Choice.ItemKind))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry a tool at least this good");
        var pickup = CheckGatheringToolPickup(plan.Actor, plan.Pickup);
        if (!pickup.Allowed) return pickup;
        if (FreeCarryCapacity(plan.Actor) - 1 + (plan.FutureCargoStorage?.Cargo.Sum(move => move.PhysicalQuantity) ?? 0) < plan.MinimumFreeAfterPickup)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected tool would leave no room for the planned harvest");
        return plan.FutureCargoStorage is { } cargo ? CheckCargoStorage(cargo) : new();
    }

    private ActionCheck CheckGatheringToolPickup(string actor, PlannedEquipmentPickup pickup)
    {
        if (FreeCarryCapacity(actor) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected tool");
        if (RecheckGoods(SharedCollectionRequest(actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { Lot: var selected } ||
            selected.ContainerLotId is not null || selected.CarrierId is not null ||
            HouseholdStockPosition(selected) != pickup.Position || HouseholdStockInteractionRange(selected) != pickup.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected shared tool is no longer available");
        return IsWithinInteractionRange(agents[actor].Position, pickup.Position, pickup.InteractionRange) ||
            Reach(actor, pickup.Position, ReachPurpose.MaterialGathering, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected tool is out of reach now");
    }

    private void ApplyToolCollectionPlan(ToolCollectionActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckToolCollectionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyGatheringToolPickup(plan.Actor, plan.Pickup);
    }

    private void ApplyGatheringToolPickup(string actor, PlannedEquipmentPickup pickup) => ApplyPlannedEquipmentPickup(actor, pickup);
}
