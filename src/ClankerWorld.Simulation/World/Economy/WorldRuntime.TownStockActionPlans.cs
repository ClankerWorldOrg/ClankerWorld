using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<TownStockRequest, TownStockActionPlan> WarehouseStockDefinition => TownStockActions.Warehouse;
    private static ActionDefinition<TownStockRequest, TownStockActionPlan> SalvageStockDefinition => TownStockActions.Salvage;

    private static class TownStockActions
    {
        internal static readonly ActionDefinition<TownStockRequest, TownStockActionPlan> Warehouse = Create(
            TownStockRequest.TryDecodeWarehouse,
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true));
        internal static readonly ActionDefinition<TownStockRequest, TownStockActionPlan> Salvage = Create(
            TownStockRequest.TryDecodeSalvage, TownSystem.SalvageCapabilities);

        private static ActionDefinition<TownStockRequest, TownStockActionPlan> Create(
            ChoiceDecoder<TownStockRequest> decode, ActorCapabilityPolicy policy) => new(
            decode, static choice => choice.Encode(), policy,
            static (world, actor, choice, _) => choice.Kind == TownStockActionKind.StoreSurplus
                ? world.PlanWarehouseSurplus(actor)
                : world.townSystem.PlanTownSalvage(world.civicWorld, actor, choice.CandidateId),
            static (world, plan, capabilities) => world.CheckTownStockPlan(plan, capabilities),
            static (world, plan, _, _) =>
            {
                world.ApplyCheckedTownStockPlan(plan);
                return true;
            });
    }

    private TownStockActionPlan? PlanWarehouseSurplus(string actor)
    {
        if (WarehouseForResident(actor) is not { } warehouse) return null;
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        var surplus = PersonalWarehouseLots(actor)
            .Where(lot => InventoryHoldRules.Allows(index, lot, InventoryHoldRules.DeliveryFor(actor, warehouse.InstanceId)))
            .Select(lot => PersonalWarehouseSurplus(actor, lot.ItemKind, lot.Id)).FirstOrDefault(item => item is not null);
        if (surplus is not { } selected) return null;
        var quantity = Math.Min(DestinationRoom(warehouse.InstanceId), Math.Min(WarehouseLoadQuantity, selected.Quantity));
        return new(actor, new(TownStockActionKind.StoreSurplus, warehouse.TownId!, selected.Lot.Id), selected.Lot.ItemKind,
            quantity, warehouse.Position, 0, warehouse.InstanceId,
            Reach(actor, warehouse.Position, ReachPurpose.TownWarehouse, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckTownStockPlan(TownStockActionPlan plan, ActorCapabilityPolicy policy)
    {
        if (plan.Choice.Kind == TownStockActionKind.Salvage) return townSystem.CheckTownSalvagePlan(civicWorld, plan, policy);
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        if (TownForResident(plan.Actor) != plan.Choice.TownId ||
            WarehousesForTown(plan.Choice.TownId).SingleOrDefault(building => building.InstanceId == plan.WarehouseId) is not { } warehouse ||
            warehouse.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Warehouse is no longer available");
        if (plan.Quantity <= 0 || DestinationRoom(warehouse.InstanceId) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Warehouse no longer has room for that load");
        if (PersonalWarehouseSurplus(plan.Actor, plan.ItemKind, plan.Choice.StockId) is not { } surplus ||
            surplus.Quantity < plan.Quantity || !InventoryHoldRules.Allows(InventoryIndex.For(society.Checkpoint.Inventory), surplus.Lot,
                InventoryHoldRules.DeliveryFor(plan.Actor, plan.WarehouseId)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected spare goods are no longer available");
        return Reach(plan.Actor, plan.Destination, ReachPurpose.TownWarehouse, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "it's out of reach now");
    }

    private void ApplyTownStockPlan(TownStockActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal)
        {
            RefuseChoice(plan, refusal);
            return;
        }
        ApplyCheckedTownStockPlan(plan);
    }

    private void ApplyCheckedTownStockPlan(TownStockActionPlan plan)
    {
        if (plan.Choice.Kind == TownStockActionKind.Salvage)
        {
            TownSystem.ApplyTownSalvagePlan(civicWorld, plan);
            return;
        }
        var state = agents[plan.Actor];
        if (state.Position != plan.Destination)
        {
            MoveToward(plan.Actor, state, plan.Destination, "town_warehouse", plan.InteractionRange);
            return;
        }
        var lot = society.Checkpoint.Inventory.GetLot(plan.Choice.StockId);
        var availableBefore = PersonalWarehouseAvailableQuantity(plan.Actor, plan.ItemKind);
        var operation = WarehouseStockId(WorldTick, plan.Actor);
        ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
            operation, plan.Actor, plan.Choice.TownId, plan.Choice.StockId,
            plan.Quantity, "town_resources_stored", plan.WarehouseId,
            holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.WarehouseId)));
        RecordNonLandGoodsCompletion(plan.Actor, lot, plan.Choice.TownId, plan.Quantity, plan.WarehouseId,
            operation, "public_service_goods", availableBefore);
        AppendEvent(new TownResourcesStoredEventPayload(plan.Actor, plan.Choice.StockId, plan.Quantity, plan.WarehouseId!));
    }
}
