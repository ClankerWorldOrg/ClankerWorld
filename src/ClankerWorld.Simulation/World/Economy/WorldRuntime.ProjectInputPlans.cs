using ClankerWorld.Simulation.Content;

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

    private ActionCheck CheckProjectInput(ProjectInputActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ProjectInputCapabilities);
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

    private void ApplyCheckedProjectInput(ProjectInputActionPlan plan)
    {
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
                RunInventoryCommand(InventorySystem.DeliverProjectInput(InventoryEconomyWorld, plan, delivery));
                AppendEvent(new ProjectMaterialDeliveredEventPayload(plan.Actor, plan.Input.ResourceId));
                return;
            case ProjectInputPickup pickup:
                SetProject(plan.Actor, project with { Stage = "gathering", Blocker = $"Collecting {plan.Input.ResourceId.Replace('_', ' ')} from the Town Warehouse" });
                if (state.Position != pickup.Warehouse.Position)
                {
                    MoveToward(plan.Actor, agents[plan.Actor], pickup.Warehouse.Position, "warehouse_materials", 0);
                    return;
                }
                if (pickup.Quantity == 0) return;
                RunInventoryCommand(InventorySystem.CollectWarehouseProjectInput(InventoryEconomyWorld, plan, pickup));
                AppendEvent(new TownResourceCollectedEventPayload(plan.Actor, pickup.LotId, pickup.Quantity, pickup.Warehouse.InstanceId));
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
                AppendEvent(new CarryingFullEventPayload(plan.Actor));
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
    private static GoodsRequest ProjectContributionRequest(string actor, string kind) =>
        new(GoodsUse.ProjectMaterialContribution, actor, GoodsOwners.One(actor), GoodsKinds.One(kind));

    private ProjectAssistanceActionPlan? PlanProjectAssistance(string actor,
        (string Requester, ContentQuantity Input, string OwnerId) request)
    {
        if (!CanReceiveProjectAssistance(request)) return null;
        var project = agents[request.Requester].Project!;
        var house = HouseForHousehold(request.OwnerId);
        var destination = house?.Position ?? agents[request.Requester].Position;
        var range = house is not null && HouseholdFor(actor) == request.OwnerId ? 0 : ResourceInteractionRange;
        var carried = FindGoods(ProjectContributionRequest(actor, request.Input.ResourceId)).First;
        var quantity = carried is null ? 0 : Math.Min(request.Input.Amount,
            Math.Min(carried.Quantity, house is null ? FreeCarryCapacity(request.Requester) : DestinationRoom(house.InstanceId)));
        var source = carried is null ? MaterialSource(request.Input.ResourceId, actor, destination, range) : null;
        var harvest = source is null ? null : ProjectMaterialHarvest(actor, request.Input.ResourceId, source);
        if (carried is null && harvest is null || carried is not null && quantity == 0) return null;
        var tool = harvest?.ToolLotId is { } toolId ? society.Checkpoint.Inventory.GetLot(toolId) : null;
        return new(actor, new(request.Input.ResourceId), request.Requester, project.CandidateId, project.StartedTick,
            request.Input, request.OwnerId, house is null ? request.Requester : request.OwnerId, house?.InstanceId,
            destination, range, carried?.Lot.Id, quantity, source, harvest, tool?.OwnerId, tool?.ItemKind,
            Reach(actor, source?.Position ?? destination, ReachPurpose.ProjectAssistance,
                source is null ? range : ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            source is null ? [] : HarvestReturnRoute(actor, source, destination, ReachPurpose.ProjectAssistance,
                checked(harvest!.Quantity + harvest.TreeSeedQuantity), range));
    }

    private ActionCheck CheckProjectAssistance(ProjectAssistanceActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ProjectMaterialCapabilities);
        if (!capability.Allowed) return capability;
        var house = HouseForHousehold(plan.OwnerId);
        if (!agents.TryGetValue(plan.RequesterId, out var requester) || requester.Project is not { } project ||
            project.CandidateId != plan.ProjectCandidateId || project.StartedTick != plan.ProjectStartedTick ||
            !ProjectRequests(plan.Actor).Any(request => request.Requester == plan.RequesterId && request.OwnerId == plan.OwnerId && request.Input == plan.RequestedInput) ||
            house?.InstanceId != plan.HouseId || (house?.Position ?? requester.Position) != plan.Destination ||
            (house is null ? plan.RequesterId : plan.OwnerId) != plan.RecipientId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected requesting project or recipient has changed");
        if (plan.LotId is { } lotId)
        {
            if (RecheckGoods(ProjectContributionRequest(plan.Actor, plan.Choice.ItemKind), lotId) is not { } match || match.Quantity < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project contribution lot is unavailable");
            if ((house is null ? FreeCarryCapacity(plan.RequesterId) : DestinationRoom(house.InstanceId)) < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected project recipient has no room for this contribution");
        }
        else
        {
            if (plan.Source is null || plan.Harvest is null)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project assistance harvest is unavailable");
            if (!CanReceiveProjectAssistance((plan.RequesterId, plan.RequestedInput, plan.OwnerId)) ||
                FindGoods(ProjectContributionRequest(plan.Actor, plan.Choice.ItemKind)).Matches.Count > 0)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project gathering step is no longer needed");
            var harvest = plan.Harvest!;
            var source = CheckPlannedProjectHarvest(plan.Actor, plan.Source!, harvest, plan.ToolOwnerId, plan.ToolKind);
            if (!source.Allowed) return source;
            if (FreeCarryCapacity(plan.Actor) < checked(harvest.Quantity + harvest.TreeSeedQuantity))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected project assistance harvest");
            if (HarvestReturnRoute(plan.Actor, plan.Source!, plan.Destination, ReachPurpose.ProjectAssistance,
                    checked(harvest.Quantity + harvest.TreeSeedQuantity), plan.Range).Count == 0)
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected assistance materials cannot return to the recipient");
        }
        var target = plan.Source?.Position ?? plan.Destination;
        var range = plan.Source is null ? plan.Range : ResourceInteractionRange;
        return IsWithinInteractionRange(agents[plan.Actor].Position, target, range) ||
            Reach(plan.Actor, target, ReachPurpose.ProjectAssistance, range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project assistance destination is out of reach");
    }
    private ActionCheck CheckProjectMaterialSource(MapResource source, int? treeSeedQuantity = null)
    {
        if (!map.Resources.Contains(source) || resources.GetValueOrDefault(source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(source.Id), 1).IsValid ||
            treeSeedQuantity is { } seeds && seeds != (TreeGrowthRules.IsWoodTree(source.TreeKind) && worldSystems.Ecology.GetResource(source.Id).Quantity == 1
                ? TreeGrowthRules.TreeSeedsPerFelledTree : 0))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project material source or harvest has changed");
        return new();
    }

    private ActionCheck CheckPlannedProjectHarvest(string actor, MapResource source, ToolGatheringPlan harvest,
        string? toolOwner, string? toolKind)
    {
        var selectedSource = CheckProjectMaterialSource(source, harvest.TreeSeedQuantity);
        if (!selectedSource.Allowed) return selectedSource;
        if (harvest.ToolLotId is { } toolId &&
            (RecheckGoods(new(GoodsUse.CarriedInputs, actor, GoodsOwners.One(toolOwner!), GoodsKinds.One(toolKind!)), toolId) is not { Lot: var tool } ||
                !ToolProgressionRules.IsTopLevelCarriedLot(tool, actor)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project material tool is unavailable");
        return new();
    }
    private static ActorCapabilityPolicy RareMiningPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private ActionCheck CheckRareMiningContext(string actor, RareMiningChoice choice, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), policy ?? RareMiningPolicy);
        if (!capability.Allowed) return capability;
        if (agents[actor].Project is { Stage: not ("completed" or "cancelled"), RequiresFreshChoice: false })
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their ongoing work comes first");
        return RareMaterialStockQuantity(actor, choice.ItemKind) < RareMaterialStockTarget ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "there is already enough of this material");
    }

    private RareMiningActionPlan? PlanRareMining(string actor, string itemKind)
    {
        var choice = new RareMiningChoice(itemKind);
        if (itemKind is not ("gold_ore" or "diamond") ||
            !CheckRareMiningContext(actor, choice).Allowed ||
            ToolProgressionRules.BestUsableTool(society.Checkpoint.Inventory, actor, ToolFamily.Pickaxe) is not { } pick ||
            ToolProgressionRules.Find(pick.ItemKind)!.Tier < 3 ||
            MaterialSource(itemKind, actor) is not { } source || ProjectMaterialHarvest(actor, itemKind, source) is not { ToolLotId: { } toolId } harvest)
            return null;
        var tool = society.Checkpoint.Inventory.GetLot(toolId);
        return new(actor, choice, HouseholdFor(actor), source, harvest, tool.OwnerId, tool.ItemKind,
            Reach(actor, source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckRareMiningPlan(RareMiningActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var context = CheckRareMiningContext(plan.Actor, plan.Choice, policy);
        if (!context.Allowed) return context;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household has changed");
        if (!map.Resources.Contains(plan.Source) || resources.GetValueOrDefault(plan.Source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(plan.Source.Id), 1).IsValid)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected deposit is no longer available");
        if (plan.Harvest.ToolLotId is not { } toolId ||
            RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId), GoodsKinds.One(plan.ToolKind)), toolId) is not { Lot: var tool } ||
            !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected mining pick is no longer available");
        if (FreeCarryCapacity(plan.Actor) < checked(plan.Harvest.Quantity + plan.Harvest.TreeSeedQuantity))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected mining load");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected deposit is out of reach now");
    }

    private void ApplyCheckedRareMiningPlan(RareMiningActionPlan plan)
    {
        GatherPlannedProjectMaterial(plan.Actor, agents[plan.Actor], plan.Choice.ItemKind, plan.Source, plan.Harvest);
    }
}
