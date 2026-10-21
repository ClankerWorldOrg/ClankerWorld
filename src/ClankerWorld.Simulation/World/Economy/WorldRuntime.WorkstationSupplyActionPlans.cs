using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy WorkstationSupplyPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private RecipeDefinition[] WorkstationSupplyRecipes(BuildingDefinition definition, string household) =>
        worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == definition.CanonicalId &&
                NeedsRecipeOutput(recipe, household) &&
                (!HasDedicatedSupply(definition) || HouseToolsContent.IsCrudeToolRecipe(recipe) ||
                 recipe.Tags.Any(tag => tag is "pottery" or "care" or "named-meal" or "knowledge")))
            .OrderBy(recipe => HouseToolsContent.IsCrudeToolRecipe(recipe) ? 0 : recipe.Tags.Contains("pottery") ? 1 : 2)
            .ThenBy(recipe => recipe.CanonicalId, StringComparer.Ordinal).ToArray();

    private int WorkstationSupplyMissing(PlacedBuilding building, string itemKind, IReadOnlyList<RecipeDefinition>? recipes = null)
    {
        if (building.HouseholdId is not { } household || !worldContent.BuildingDefinitions.TryGetValue(building.DefinitionId, out var definition) ||
            HouseholdBuildingKind(definition) is null) return 0;
        recipes ??= WorkstationSupplyRecipes(definition, household);
        return recipes.SelectMany(recipe => recipe.Inputs)
            .Where(input => input.ResourceId == itemKind).Select(input => input.Amount).DefaultIfEmpty(0).Max() * SupplyBatches -
            WorkstationOnsiteQuantity(society.Checkpoint.Inventory, building, itemKind) -
            WorkstationIncomingQuantity(society.Checkpoint.Inventory, building, itemKind);
    }

    private WorkstationSupplyActionPlan? PlanWorkstationSupplyCandidate(string actor, string candidateId) =>
        WorkstationSupplyChoice.Decode(candidateId) is { } choice ? PlanWorkstationSupply(actor, choice.ItemKind) : null;

    private WorkstationSupplyActionPlan? PlanWorkstationSupply(string actor, string? itemKind = null)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), new WorkstationSupplyChoice(itemKind ?? "supplies").Encode(), WorkstationSupplyPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            WorkstationSupplyNeeds(actor, itemKind).FirstOrDefault(need => CanReach(actor, need.Building.Position, ReachPurpose.WorkstationSupply, 0, 0, SwimmingPolicy.WhenCapable)) is not { } need)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var stock = need.Carried ?? need.HouseholdStock;
        var kind = need.Carried is not null ? WorkstationSupplyKind.Carried : stock is not null ? WorkstationSupplyKind.Pickup : WorkstationSupplyKind.Gather;
        var vessel = stock is not null && InventoryContainerRules.IsContainer(stock.ItemKind);
        var harvest = need.Source is { } source ? ProjectMaterialHarvest(actor, need.ItemKind, source) : null;
        var quantity = kind == WorkstationSupplyKind.Carried ? vessel ? 1 : Math.Min(DestinationRoom(need.Building.InstanceId),
            Math.Min(need.Missing, SpareCarriedQuantity(actor, stock!))) : kind == WorkstationSupplyKind.Pickup
            ? WorkstationPickupQuantity(actor, inventory, stock!, need.Building.InstanceId, need.Missing) : harvest?.Quantity ?? 0;
        if (quantity <= 0) return null;
        var physical = vessel ? ContainerFamilyQuantity(inventory, stock!.Id) : quantity + (harvest?.TreeSeedQuantity ?? 0);
        var target = kind == WorkstationSupplyKind.Carried ? need.Building.Position : stock is not null
            ? HouseholdStockPosition(stock) : need.Source!.Position;
        var range = kind == WorkstationSupplyKind.Carried ? 0 : stock is not null ? HouseholdStockInteractionRange(stock) : ResourceInteractionRange;
        var route = Reach(actor, target, ReachPurpose.WorkstationSupply, range, 0, SwimmingPolicy.WhenCapable);
        var returnRoute = kind == WorkstationSupplyKind.Gather ? HarvestReturnRoute(actor, need.Source!, need.Building.Position,
            ReachPurpose.MaterialGathering, physical) : kind == WorkstationSupplyKind.Pickup
            ? ReachLeg(actor, target, need.Building.Position, ReachPurpose.WorkstationSupply, 0, 0, SwimmingPolicy.WhenCapable)
            : Array.AsReadOnly<GridPoint>([]);
        return new(actor, new(need.ItemKind), kind, household, need.Building.InstanceId, need.Definition.CanonicalId,
            need.Building.Position, stock?.Id, stock?.OwnerId, stock?.ItemKind, quantity, physical, target, range,
            vessel ? GoodsPlanFamily.Capture(inventory, stock!) : null, need.Source, harvest,
            harvest?.ToolLotId is { } toolId ? inventory.GetLot(toolId).ItemKind : null,
            harvest?.ToolLotId is { } selectedTool ? inventory.GetLot(selectedTool).OwnerId : null, route, returnRoute);
    }

    private ActionCheck CheckWorkstationSupplyPlan(WorkstationSupplyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? WorkstationSupplyPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or delivery work has changed");
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.BuildingId);
        if (building is null || building.HouseholdId != plan.HouseholdId || building.Position != plan.Destination || building.DefinitionId != plan.DefinitionId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected workstation has changed");
        var missing = WorkstationSupplyMissing(building, plan.ItemKind);
        if (missing <= 0 || plan.Kind != WorkstationSupplyKind.Gather && plan.Family is null && missing < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected workstation no longer needs this load");
        var inventory = society.Checkpoint.Inventory;
        if (plan.StockId is { } stockId)
        {
            var request = plan.Kind == WorkstationSupplyKind.Carried
                ? new GoodsRequest(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.StockOwnerId!), GoodsKinds.One(plan.StockKind!))
                : DeliveryGoodsRequest(plan.Actor, plan.StockOwnerId!, plan.StockKind!, plan.BuildingId);
            if (RecheckGoods(request, stockId) is not { } match || match.Root.Id != stockId || match.Quantity < plan.Quantity ||
                !GoodsPlanFamily.Matches(inventory, match.Lot, plan.Family) ||
                plan.Kind == WorkstationSupplyKind.Carried && SpareCarriedQuantity(plan.Actor, match.Lot) < plan.Quantity ||
                plan.Kind == WorkstationSupplyKind.Pickup && (match.Lot.CarrierId is not null ||
                    HouseholdStockPosition(match.Lot) != plan.Target || HouseholdStockInteractionRange(match.Lot) != plan.InteractionRange ||
                    WorkstationPickupQuantity(plan.Actor, inventory, match.Lot, plan.BuildingId, missing) < plan.Quantity))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supplies are no longer available");
        }
        else
        {
            if (plan.Source is not { } source || plan.Harvest is not { } harvest ||
                !map.Resources.Contains(source) || resources.GetValueOrDefault(source.Id) != ResourceState.Available ||
                !EcologyRules.Harvest(worldSystems.Ecology.GetResource(source.Id), 1).IsValid)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected material source is no longer available");
            if (harvest.ToolLotId is { } toolId)
            {
                if (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId!), GoodsKinds.One(plan.ToolKind!)), toolId) is not { Lot: var tool } ||
                    !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gathering tool is no longer available");
            }
            if (FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected harvest");
            if (HarvestReturnRoute(plan.Actor, source, plan.Destination, ReachPurpose.MaterialGathering, plan.PhysicalQuantity).Count == 0)
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected harvest cannot reach the workstation now");
        }
        if (DestinationRoom(plan.BuildingId) < (plan.Kind == WorkstationSupplyKind.Gather ? 1 : plan.PhysicalQuantity))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected workstation has no room for supplies");
        if (!CanReach(plan.Actor, building.Position, ReachPurpose.WorkstationSupply, 0, 0, SwimmingPolicy.WhenCapable) || !IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.InteractionRange) &&
            Reach(plan.Actor, plan.Target, ReachPurpose.WorkstationSupply, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected supply trip is out of reach now");
        return new();
    }

    private void ApplyWorkstationSupplyPlan(WorkstationSupplyActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedWorkstationSupplyPlan(plan);
    }

    private void ApplyCheckedWorkstationSupplyPlan(WorkstationSupplyActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Target, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Target, plan.Kind == WorkstationSupplyKind.Gather ? "materials" : "supply_workstation", plan.InteractionRange);
            return;
        }
        if (plan.Kind == WorkstationSupplyKind.Gather)
        {
            GatherPlannedProjectMaterial(plan.Actor, state, plan.ItemKind, plan.Source!, plan.Harvest!);
            return;
        }
        var operationId = DerivedId.Create($"{(plan.Kind == WorkstationSupplyKind.Carried ? "workstation-supply" : "workstation-pickup")}:{WorldTick}:{plan.Actor}");
        RunInventoryCommand(InventorySystem.ApplyCheckedWorkstationSupplyPlan(plan, operationId));
        AppendEvent(plan.Kind == WorkstationSupplyKind.Carried
            ? new WorkstationSuppliedEventPayload(plan.Actor, plan.StockId!, plan.Quantity, plan.BuildingId)
            : new WorkstationInputPickedUpEventPayload(plan.Actor, plan.StockId!, plan.Quantity, plan.BuildingId));
    }
}
