using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    private static ActorCapabilityPolicy FieldPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private static ActionCheck CheckFieldContext(IFarmingWorld world, string actor, FieldChoice choice)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(actor), choice.Encode(), FieldPolicy);
        if (!capability.Allowed) return capability;
        if (world.HouseholdFor(actor) is not { } household || world.FarmhouseForHousehold(household) is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household no longer has a Farmhouse");
        if (world.IsConversationBusy(actor) ||
            world.Agents[actor].Project is { Stage: not ("completed" or "cancelled"), RequiresFreshChoice: false })
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their other work comes first");
        return new();
    }

    internal FieldActionPlan? PlanFieldAction(IFarmingWorld world, string actor, FieldChoice choice,
        InventoryLot? selectedSeed = null)
    {
        if (!CheckFieldContext(world, actor, choice).Allowed) return null;
        var household = world.HouseholdFor(actor)!;
        var farmhouse = world.FarmhouseForHousehold(household)!;
        var field = fields.SingleOrDefault(item => item.Position == choice.Position);
        var inventory = world.Society.Inventory;
        var hoe = choice.Kind is FarmWorkKind.Till or FarmWorkKind.Tend
            ? ToolProgressionRules.PlanWork(inventory, actor, ToolFamily.Hoe) : null;
        var sickle = choice.Kind == FarmWorkKind.Harvest
            ? ToolProgressionRules.PlanWork(inventory, actor, ToolFamily.Sickle) : null;
        PlannedFieldSeed? seed = null;
        if (choice.Kind == FarmWorkKind.Plant)
        {
            if (field is null || choice.Crop is null || !FarmFieldRules.IsCrop(choice.Crop) ||
                (selectedSeed ?? PlantingStock(world, actor, field, choice.Crop)) is not { } stock) return null;
            var claim = field.ReplantingReservationId is { } id && inventory.Reservations.Any(item =>
                item.Id == id && item.LotId == stock.Id && item.State == InventoryReservationState.Reserved) ? id : null;
            seed = new(stock.Id, stock.OwnerId, stock.ItemKind, stock.OwnerId == actor ? world.Agents[actor].Position :
                world.HouseholdStockPosition(stock), stock.OwnerId == actor ? 0 : WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(stock), claim);
        }
        PlannedFieldTool? Capture(ToolWorkPlan? work) => work is null ? null :
            new(work, inventory.GetLot(work.ToolLotId).OwnerId, inventory.GetLot(work.ToolLotId).ItemKind);
        return new(actor, choice, household, farmhouse.InstanceId, field, seed, Capture(hoe), Capture(sickle),
            world.SkilledWorkTicks(actor, SkillKind.Farming, FarmFieldRules.WorkTicks(choice.Kind)),
            world.Reach(actor, choice.Position, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain));
    }

    internal ActionCheck CheckFieldPlan(IFarmingWorld world, FieldActionPlan plan)
    {
        var context = CheckFieldContext(world, plan.Actor, plan.Choice);
        if (!context.Allowed) return context;
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || world.FarmhouseForHousehold(plan.HouseholdId)?.InstanceId != plan.FarmhouseId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or Farmhouse has changed");
        if (FarmWorkFor(world, plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already have field work under way");
        var field = fields.SingleOrDefault(item => item.Position == plan.Choice.Position);
        if (plan.Choice.Kind == FarmWorkKind.Till)
        {
            if (field is not null || !FarmableFreeTile(world, plan.Choice.Position) ||
                world.HouseholdLandHeldByOthers(plan.HouseholdId).Contains(plan.Choice.Position))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected land is no longer free for a field");
            if (fields.Count(item => item.HouseholdId == plan.HouseholdId) >= WantedFields(world, plan.HouseholdId))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the household already has enough fields");
        }
        else if (field is null || field.HouseholdId != plan.HouseholdId || field.Work is not null ||
            plan.Field is null || field.Cycle != plan.Field.Cycle || field.Crop != plan.Field.Crop || field.LastWorkedTick != plan.Field.LastWorkedTick ||
            plan.Choice.Kind == FarmWorkKind.Plant && (field.Stage is not (FarmFieldStage.Prepared or FarmFieldStage.Harvested) ||
                !FarmFieldRules.IsCrop(plan.Choice.Crop) || !FarmNeedsFood(world, plan.HouseholdId) || HasOtherAgentClaimedPlanting(world, field, plan.Actor)) ||
            plan.Choice.Kind == FarmWorkKind.Tend && (field.Stage != FarmFieldStage.Growing || field.Tended) ||
            plan.Choice.Kind == FarmWorkKind.Harvest && field.Stage != FarmFieldStage.Ready)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected field no longer needs this work");
        foreach (var tool in new[] { plan.Hoe, plan.Sickle }.OfType<PlannedFieldTool>())
            if (world.RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(tool.OwnerId), GoodsKinds.One(tool.ItemKind)),
                    tool.Work.ToolLotId) is not { Lot: var lot } || !ToolProgressionRules.IsTopLevelCarriedLot(lot, plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected field tool is no longer available");
        if ((plan.Choice.Kind is FarmWorkKind.Till or FarmWorkKind.Tend) && plan.Hoe is null)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "they need a usable carried hoe");
        if (plan.Seed is { } seed)
        {
            var request = new GoodsRequest(GoodsUse.FieldPlanting, plan.Actor, GoodsOwners.One(seed.OwnerId), GoodsKinds.One(seed.ItemKind),
                ReservationId: seed.ReplantingReservationId);
            if (world.RecheckGoods(request, seed.StockId) is not { } match || match.Quantity < 1 ||
                seed.OwnerId != plan.Actor && (world.HouseholdStockPosition(match.Lot) != seed.Position ||
                    WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(match.Lot) != seed.InteractionRange ||
                    world.PickupCarryCapacity(plan.Actor, match.Lot, plan.Choice.Position, 1) < 1) ||
                seed.ReplantingReservationId is { } id && !world.Society.Inventory.Reservations.Any(item =>
                    item.Id == id && item.LotId == seed.StockId && item.State == InventoryReservationState.Reserved))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected planting item is no longer available");
            if (seed.OwnerId != plan.Actor && world.Reach(plan.Actor, seed.Position, ReachPurpose.FieldSeedPickup, 0, 0,
                SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count == 0)
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected planting item is out of reach now");
        }
        return world.Reach(plan.Actor, plan.Choice.Position, ReachPurpose.FieldWork, 0, 0,
            SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0 ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected field is out of reach now");
    }

    internal void ApplyFieldPlan(IFarmingWorld world, FieldActionPlan plan)
    {
        var state = world.Agents[plan.Actor];
        if (plan.Seed is { } seed && seed.OwnerId != plan.Actor)
        {
            if (!world.IsWithinInteractionRange(state.Position, seed.Position, seed.InteractionRange))
            {
                world.MoveToward(plan.Actor, state, seed.Position, "planting_stock", seed.InteractionRange);
                return;
            }
            world.ApplyInventoryTransition(inventory =>
            {
                if (seed.ReplantingReservationId is { } id)
                    inventory = InventoryAuthority.ReleaseReservation(inventory, id, "planting_stock_collected");
                return InventoryAuthority.Transfer(inventory, $"field-seed-pickup:{world.WorldTick}:{plan.Actor}", plan.HouseholdId,
                    plan.Actor, seed.StockId, 1, "field_seed_picked_up", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor));
            });
            if (plan.Field is { ReplantingReservationId: { } claim } field && !ActiveFarmReservation(world, claim))
                SetFarmField(world, fields.Single(item => item.Position == field.Position) with { ReplantingReservationId = null });
            return;
        }
        if (state.Position != plan.Choice.Position)
        {
            world.MoveToward(plan.Actor, state, plan.Choice.Position, plan.Choice.Kind == FarmWorkKind.Plant ? "field_planting" : "field_work");
            return;
        }
        StartFieldWorkCore(world, plan.Actor, plan.Choice.Position, plan.Choice.Kind, plan.Choice.Crop,
            plan.Seed?.StockId, plannedWork: plan);
    }
}
