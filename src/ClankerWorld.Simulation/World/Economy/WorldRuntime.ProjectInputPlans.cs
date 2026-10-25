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

    private static ActorCapabilityPolicy ProjectInputCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static class ProjectInputs
    {
        internal static readonly ProjectInputActionDefinition Definition = new(
            static (world, actor, input, owner, site) => world.PlanProjectInput(actor, input, owner, site));
    }

    private sealed record ProjectInputActionDefinition(
        Func<WorldRuntime, string, ContentQuantity, string, GridPoint?, ProjectInputActionPlan> Planner) : ActionDefinition
    {
        public override Type PlanType => typeof(ProjectInputActionPlan);
        public override bool Matches(string candidate) => true;
        public override ActionPlan? PlanCandidate(WorldRuntime world, string actor, string candidate) => null;

        public ProjectInputActionPlan Plan(WorldRuntime world, string actor, ContentQuantity input, string owner, GridPoint? site) =>
            Planner(world, actor, input, owner, site);

        public override ActionCheck Check(WorldRuntime world, ActionPlan plan) =>
            world.CheckProjectInput((ProjectInputActionPlan)plan, ProjectInputCapabilities);

        public override bool Apply(WorldRuntime world, ActionContext context, ActionPhase phase)
        {
            if (phase != ActionPhase.ProjectInput || context.Plan is not ProjectInputActionPlan plan) return false;
            if (Check(world, plan).Refusal is { } refusal) world.RefuseChoice(plan, refusal);
            else world.ApplyCheckedProjectInput(plan);
            return true;
        }
    }

    private static ActorCapabilityPolicy ProjectMaterialCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static class ProjectMaterialActions
    {
        internal static readonly ActionDefinition<BuildingMaterialChoice, BuildingMaterialActionPlan> Building = new(
            DecodeReferenceChoice(BuildingMaterialChoice.Decode), static choice => choice.Encode(), ProjectMaterialCapabilities,
            static (world, actor, choice, _) => world.HouseholdFor(actor) is { } household && world.PlanBuildingMaterial(actor, household) is { } plan &&
                plan.Choice.ItemKind == choice.ItemKind ? plan : null,
            static (world, plan, policy) => world.CheckBuildingMaterial(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedBuildingMaterial(plan); return true; });

        internal static readonly ActionDefinition<ProjectAssistanceChoice, ProjectAssistanceActionPlan> Assistance = new(
            DecodeReferenceChoice(ProjectAssistanceChoice.Decode), static choice => choice.Encode(), ProjectMaterialCapabilities,
            static (world, actor, choice, _) => world.PlanRequestedAssistance(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckProjectAssistance(plan, policy),
            static (world, plan, action, _) =>
            {
                world.ApplyCheckedProjectAssistance(plan, action.State, plan.Choice.ItemKind);
                return true;
            });
    }

    private ProjectAssistanceActionPlan? PlanRequestedAssistance(string actor, string itemKind)
    {
        foreach (var candidate in ProjectRequests(actor).Where(request => request.Input.ResourceId == itemKind))
            if (PlanProjectAssistance(actor, candidate) is { } plan && CheckActionPlan(plan).Allowed) return plan;
        return null;
    }

    private static ActorCapabilityPolicy ProjectGoalCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ConstructionProjectActionPlan? PlanConstructionStart(string actor, ConstructionChoice choice)
    {
        if (worldContent.Buildings.FirstOrDefault(definition => definition.CanonicalId == choice.DefinitionId) is not { } definition) return null;
        return choice.Site is { } site ? PlanConstruction(actor, definition, site) : new ConstructionGoalActionPlan(actor, definition);
    }

    private ActionCheck CheckConstructionGoal(ConstructionGoalActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        return worldContent.Buildings.Any(definition => definition.CanonicalId == plan.Definition.CanonicalId) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building goal is no longer available");
    }

    private void CopyRecipeProjectPlansFrom(WorldRuntime source)
    {
        if (ReferenceEquals(this, source)) return;
        productionSystem.CopyRecipePlansFrom(source.productionSystem, (actor, started, instruction, plan) =>
            agents.TryGetValue(actor, out var person) && person.Project is { Stage: not ("completed" or "cancelled") } project &&
            project.StartedTick == started && project.CandidateId == plan.CandidateId &&
            instruction == RecipeProjectInstructionId(actor, plan.Recipe.CanonicalId));
    }

    private RecipeProjectActionPlan? RetainedRecipeProject(string actor, AgentProject project) =>
        productionSystem.RecipePlan(actor, project, TownConstructionCandidateIds.TryParse(project.CandidateId, out var choice)
            ? RecipeProjectInstructionId(actor, choice.DefinitionId) : null);

    private string? RecipeProjectInstructionId(string actor, string recipeId) =>
        PendingInstructionFor(actor) is { Task.Action: "produce_item" } instruction && instruction.Task.TargetRecipeId == recipeId
            ? instruction.InstructionId : null;

    private static class ConstructionActions
    {
        internal static readonly ActionDefinition<ConstructionChoice, ConstructionProjectActionPlan> Building = new(
            DecodeReferenceChoice(ConstructionChoice.Decode), static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, _) => world.PlanConstructionStart(actor, choice),
            static (world, plan, policy) => plan switch
            {
                ConstructionActionPlan placed => world.CheckConstruction(placed, policy: policy),
                ConstructionGoalActionPlan goal => world.CheckConstructionGoal(goal, policy),
                _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the building plan is not available"),
            },
            static (world, plan, action, _) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: plan);
                return true;
            },
            PlanCapabilities: static plan => plan is ConstructionActionPlan placed ? ConstructionCapabilities(placed.Definition) : ProjectGoalCapabilities);

        internal static readonly ActionDefinition<ExpansionChoice, ExpansionActionPlan> Expansion = new(
            DecodeReferenceChoice(ExpansionChoice.Decode), static choice => choice.Encode(), ExpansionCapabilities,
            static (world, actor, choice, _) => world.worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == choice.BuildingId) is { } building
                ? world.PlanExpansion(actor, building) : null,
            static (world, plan, policy) => world.CheckExpansion(plan, policy),
            static (world, plan, action, _) =>
            {
                world.ApplyBuildingExpansionCandidate(action.AgentId, action.State, plan.Choice.BuildingId, originalPlan: plan);
                return true;
            });
    }

    private static class ProductionActions
    {
        internal static readonly ActionDefinition<RecipeChoice, RecipeActionPlan> Recipe = new(
            DecodeReferenceChoice(RecipeChoice.Decode), static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, _) => world.productionSystem.PlanRecipeStart(world.RecipeWorld, actor, choice),
            static (world, plan, policy) => plan switch
            {
                RecipeProjectActionPlan placed => world.productionSystem.CheckRecipeProject(world.RecipeWorld, placed, policy),
                RecipeGoalActionPlan goal => ProductionSystem.CheckRecipeGoal(world.RecipeWorld, goal, policy),
                _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the recipe plan is not available"),
            },
            static (world, plan, action, _) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: plan);
                return true;
            },
            PlanCapabilities: static plan => plan is RecipeProjectActionPlan placed ? ProductionSystem.RecipeCapabilities(placed.Recipe) : ProjectGoalCapabilities);
    }

    private void CancelProductionJobs(WorldProductionJob[] cancelled, string reason) =>
        productionSystem.CancelProductionJobs(ProductionWorld, cancelled, reason);

    private static ChoiceDecoder<TChoice> DecodeReferenceChoice<TChoice>(Func<string, TChoice?> decode) where TChoice : class =>
        (string candidate, out TChoice choice) => { choice = decode(candidate)!; return choice is not null; };

    private static class WorkshopActions
    {
        internal static readonly ActionDefinition<SmithDeliveryKind, SmithDeliveryActionPlan> Delivery = new(
            SmithDeliveryActionPlan.TryDecode, SmithDeliveryActionPlan.Candidate, SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanSmithDelivery(actor, choice),
            static (world, plan, policy) => world.CheckSmithDeliveryPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedSmithDeliveryPlan(plan); return true; });

        internal static readonly ActionDefinition<SmithGatherChoice, SmithGatherActionPlan> Gather = new(
            DecodeReferenceChoice(SmithGatherChoice.Decode), static choice => choice.Encode(), SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanSmithGather(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckSmithGatherPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedSmithGatherPlan(plan); return true; });

        internal static readonly ActionDefinition<ToolCollectionChoice, ToolCollectionActionPlan> Collection = new(
            DecodeReferenceChoice(ToolCollectionChoice.Decode), static choice => choice.Encode(), SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanToolCollectionCandidate(actor, choice.Encode()),
            static (world, plan, policy) => world.CheckToolCollectionPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyPlannedEquipmentPickup(plan.Actor, plan.Pickup); return true; });

        internal static readonly ActionDefinition<ToolRepairChoice, ToolRepairActionPlan> Repair = new(
            DecodeReferenceChoice(ToolRepairChoice.Decode), static choice => choice.Encode(), SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanToolRepair(actor, choice.ToolId),
            static (world, plan, policy) => world.CheckToolRepairPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedToolRepairPlan(plan); return true; });

        internal static readonly ActionDefinition<RareMiningChoice, RareMiningActionPlan> Mining = new(
            DecodeReferenceChoice(RareMiningChoice.Decode), static choice => choice.Encode(), RareMiningPolicy,
            static (world, actor, choice, _) => world.PlanRareMining(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckRareMiningPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedRareMiningPlan(plan); return true; });

        internal static readonly ActionDefinition<WorkstationSupplyChoice, WorkstationSupplyActionPlan> Supply = new(
            DecodeReferenceChoice(WorkstationSupplyChoice.Decode), static choice => choice.Encode(), WorkstationSupplyPolicy,
            static (world, actor, choice, _) => world.PlanWorkstationSupply(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckWorkstationSupplyPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedWorkstationSupplyPlan(plan); return true; });

        internal static readonly ActionDefinition<StoreStockChoice, StoreStockActionPlan> Store = new(
            StoreStockChoice.TryDecode, static _ => StoreStockChoice.Encode(), StoreStockPolicy,
            static (world, actor, _, _) => world.PlanStoreStock(actor),
            static (world, plan, policy) => world.CheckStoreStockPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedStoreStockPlan(plan); return true; });
    }
}
