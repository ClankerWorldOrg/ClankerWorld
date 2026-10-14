using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ProjectInputActionPlan PlanProjectInput(string actor, ContentQuantity input, string owner, GridPoint? personalSite)
    {
        var project = agents[actor].Project!;
        var carried = FindGoods(ProjectContributionRequest(actor, input.ResourceId)).First;
        var house = HouseForHousehold(owner);
        GridPoint? destination = owner == actor ? personalSite : house?.Position ?? SharedStoragePosition;
        var range = owner == actor || house is not null ? 0 : ResourceInteractionRange;
        ProjectInputStep step;
        GridPoint? target = destination;
        var targetRange = range;
        IReadOnlyList<GridPoint> returnRoute = [];
        if (carried is not null && owner != actor)
        {
            house = society.Checkpoint.GetAgent(actor).HouseholdId == owner ? house : null;
            destination = house?.Position ?? SharedStoragePosition;
            range = house is null ? ResourceInteractionRange : 0;
            target = destination;
            targetRange = range;
            var quantity = Math.Min(input.Amount, carried.Quantity);
            if (house is not null) quantity = Math.Min(quantity, DestinationRoom(house.InstanceId));
            step = new ProjectInputDelivery(carried.Lot.Id, quantity);
        }
        else if (destination is null) step = new ProjectInputWait(ProjectInputWaitReason.WorkSite);
        else if (WarehouseMaterialSource(actor, input.ResourceId, destination.Value, range) is { } warehouse)
        {
            var quantity = Math.Min(WarehouseLoadQuantity, Math.Min(input.Amount, warehouse.Source.Quantity));
            quantity = Math.Min(quantity, PickupCarryCapacity(actor, warehouse.Source.Lot, destination.Value, range));
            step = new ProjectInputPickup(warehouse.Warehouse, warehouse.Source.Lot.Id, quantity);
            target = warehouse.Warehouse.Position;
            targetRange = 0;
        }
        else if (MaterialSource(input.ResourceId, actor, destination, range, useHarvestBonus: false) is { } source)
        {
            var bonus = UseHarvestBonusForBuildingMaterial(actor, input.ResourceId, source, destination, range);
            var harvest = ProjectMaterialHarvest(actor, input.ResourceId, source, bonus);
            var tool = harvest?.ToolLotId is { } id ? society.Checkpoint.Inventory.GetLot(id) : null;
            PlannedEquipmentPickup? pickup = null;
            if (harvest is null && RequiredGatheringTool(input.ResourceId, source) is { } required &&
                FindReachableSharedGatheringTool(actor, required) is { } available &&
                SharedItem(available.ItemKind, actor) is { ContainerLotId: null } stock)
                pickup = new(stock.Id, stock.OwnerId, stock.ItemKind, HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock));
            step = new ProjectInputHarvest(source, harvest, tool?.OwnerId, tool?.ItemKind, pickup);
            if (harvest is not null && FreeCarryCapacity(actor) < checked(harvest.Quantity + harvest.TreeSeedQuantity))
                step = new ProjectInputWait(ProjectInputWaitReason.CarrySpace, source);
            else if (pickup is not null && (FreeCarryCapacity(actor) <= 0 || HasCarriedEquipmentAtLeast(actor, pickup.ItemKind)))
                step = new ProjectInputHarvest(source, null, null, null, null);
            target = pickup?.Position ?? source.Position;
            targetRange = pickup?.InteractionRange ?? ResourceInteractionRange;
            if (harvest is not null) returnRoute = HarvestReturnRoute(actor, source, destination.Value, ReachPurpose.ProjectInput,
                checked(harvest.Quantity + harvest.TreeSeedQuantity), range);
        }
        else step = new ProjectInputWait(ProjectInputWaitReason.Source);
        return new(actor, project.CandidateId, project.StartedTick, input, HouseholdFor(actor), owner, house?.InstanceId, destination, range, step,
            target is { } point ? Reach(actor, point, ReachPurpose.ProjectInput, targetRange, 0, SwimmingPolicy.WhenCapable) : [], returnRoute);
    }

    private ActionCheck CheckProjectInput(ProjectInputActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (agents[plan.Actor].Project is not { } project || project.CandidateId != plan.ProjectCandidateId ||
            project.StartedTick != plan.ProjectStartedTick || project.RequiresFreshChoice || project.Stage is "completed" or "cancelled" ||
            HouseholdFor(plan.Actor) != plan.ActorHouseholdId || HasAvailableQuantities([plan.Input], plan.OwnerId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project material need has changed");
        var house = HouseForHousehold(plan.OwnerId);
        if (plan.Step is ProjectInputDelivery && society.Checkpoint.GetAgent(plan.Actor).HouseholdId != plan.OwnerId) house = null;
        if (house?.InstanceId != plan.HouseId || plan.OwnerId != plan.Actor &&
            (house?.Position ?? SharedStoragePosition) != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project material destination has changed");
        GridPoint? target = plan.Destination;
        var range = plan.DestinationRange;
        switch (plan.Step)
        {
            case ProjectInputDelivery delivery:
                if (RecheckGoods(ProjectContributionRequest(plan.Actor, plan.Input.ResourceId), delivery.LotId) is not { } carried ||
                    carried.Quantity < delivery.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project delivery lot is unavailable");
                if (house is not null && DestinationRoom(house.InstanceId) < delivery.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected project storage has no room for this delivery");
                break;
            case ProjectInputPickup pickup:
                if (!worldSimulation.Buildings.Contains(pickup.Warehouse) || !MayCollectWarehouseStock(plan.Actor, pickup.Warehouse) ||
                    RecheckGoods(new(GoodsUse.Collect, plan.Actor, GoodsOwners.One(pickup.Warehouse.TownId!),
                        GoodsKinds.One(plan.Input.ResourceId), AtBuilding: pickup.Warehouse.InstanceId), pickup.LotId) is not { } match ||
                    match.Quantity < pickup.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project Warehouse lot is unavailable");
                if (PickupCarryCapacity(plan.Actor, match.Lot, plan.Destination!.Value, plan.DestinationRange) < pickup.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected project pickup");
                target = pickup.Warehouse.Position;
                range = 0;
                break;
            case ProjectInputHarvest { Harvest: { } harvest } gathering:
                var source = CheckPlannedProjectHarvest(plan.Actor, gathering.Source, harvest, gathering.ToolOwnerId, gathering.ToolKind);
                if (!source.Allowed) return source;
                var quantity = checked(harvest.Quantity + harvest.TreeSeedQuantity);
                if (FreeCarryCapacity(plan.Actor) < quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected project harvest");
                if (HarvestReturnRoute(plan.Actor, gathering.Source, plan.Destination!.Value, ReachPurpose.ProjectInput, quantity, plan.DestinationRange).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project harvest cannot return to its destination");
                target = gathering.Source.Position;
                range = ResourceInteractionRange;
                break;
            case ProjectInputHarvest { ToolPickup: { } tool } gathering:
                var selectedSource = CheckProjectMaterialSource(gathering.Source);
                if (!selectedSource.Allowed) return selectedSource;
                if (HasCarriedEquipmentAtLeast(plan.Actor, tool.ItemKind))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project tool is already carried");
                return CheckEquipmentPickup(plan.Actor, tool, purpose: ReachPurpose.ProjectInput);
            case ProjectInputHarvest:
            case ProjectInputWait:
                return new();
        }
        return target is not { } point || IsWithinInteractionRange(agents[plan.Actor].Position, point, range) ||
            Reach(plan.Actor, point, ReachPurpose.ProjectInput, range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project material step is out of reach");
    }

    private void ApplyProjectInput(ProjectInputActionPlan plan)
    {
        if (CheckProjectInput(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var state = agents[plan.Actor];
        var project = state.Project!;
        switch (plan.Step)
        {
            case ProjectInputDelivery delivery:
                SetProject(plan.Actor, project with { Stage = "delivering", Blocker = $"Taking {plan.Input.ResourceId.Replace('_', ' ')} to household storage" });
                if (!IsWithinInteractionRange(state.Position, plan.Destination!.Value, plan.DestinationRange))
                {
                    MoveToward(plan.Actor, agents[plan.Actor], plan.Destination.Value, "deliver", plan.DestinationRange);
                    return;
                }
                if (delivery.Quantity == 0)
                {
                    SetProject(plan.Actor, project with { Stage = "blocked", Blocker = "House storage is full; expand it before delivering more." });
                    return;
                }
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"project-delivery:{WorldTick}:{plan.Actor}", plan.Actor, plan.OwnerId, delivery.LotId,
                    delivery.Quantity, "project_contribution", plan.HouseId,
                    destinationGroundPosition: plan.HouseId is null ? new InventoryGroundPosition(plan.Destination.Value.X, plan.Destination.Value.Y) : null,
                    holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));
                AppendEvent("project_material_delivered", $"{plan.Actor}:{plan.Input.ResourceId}");
                return;
            case ProjectInputPickup pickup:
                SetProject(plan.Actor, project with { Stage = "gathering", Blocker = $"Collecting {plan.Input.ResourceId.Replace('_', ' ')} from the Town Warehouse" });
                if (state.Position != pickup.Warehouse.Position)
                {
                    MoveToward(plan.Actor, agents[plan.Actor], pickup.Warehouse.Position, "warehouse_materials", 0);
                    return;
                }
                if (pickup.Quantity == 0) return;
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"warehouse-pickup:{WorldTick}:{plan.Actor}", pickup.Warehouse.TownId!, plan.Actor,
                    pickup.LotId, pickup.Quantity, "town_resource_collected", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
                AppendEvent("town_resource_collected", $"{plan.Actor}:{pickup.LotId}:{pickup.Quantity}:{pickup.Warehouse.InstanceId}");
                return;
            case ProjectInputHarvest harvest:
                SetProject(plan.Actor, project with { Stage = "gathering", Blocker = $"Need {plan.Input.Amount} {plan.Input.ResourceId.Replace('_', ' ')}" });
                if (harvest.ToolPickup is { } tool) ApplyPlannedEquipmentPickup(plan.Actor, tool);
                else if (harvest.Harvest is { } selected) GatherPlannedProjectMaterial(plan.Actor, state, plan.Input.ResourceId, harvest.Source, selected);
                return;
            case ProjectInputWait { Reason: ProjectInputWaitReason.CarrySpace, Source: { } source }:
                SetProject(plan.Actor, project with { Stage = "gathering", Blocker = $"Need {plan.Input.Amount} {plan.Input.ResourceId.Replace('_', ' ')}" });
                if (!IsWithinInteractionRange(state.Position, source.Position, ResourceInteractionRange))
                {
                    MoveToward(plan.Actor, agents[plan.Actor], source.Position, "materials", ResourceInteractionRange);
                    return;
                }
                AppendEvent("carrying_full", plan.Actor);
                return;
            case ProjectInputWait waiting:
                SetProject(plan.Actor, project with
                {
                    Stage = "blocked",
                    Blocker = waiting.Reason == ProjectInputWaitReason.WorkSite
                    ? WaitingForWorkSiteBlocker : $"No available source of {plan.Input.ResourceId.Replace('_', ' ')}"
                });
                return;
        }
    }
}
