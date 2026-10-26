using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// Keeps a household workstation stocked for its own recipes. Recipes there
/// use only on-site stock, so an adult of the holding household carries the
/// inputs in: from what they carry, from the household's own stock, or by
/// gathering from a reachable source. The Farmhouse, Blacksmith and House
/// keep their older, dedicated hauling.
/// </summary>
public sealed partial class WorldRuntime
{
    // The common workstation supply path gathers and carries herbs, fuel and
    // whole water jugs. Production reserves their actual on-site contents.
    private const string SupplyWorkstationPrefix = WorkstationSupplyChoice.Prefix;

    /// <summary>Enough on-site stock for two batches of the largest recipe needing the item.</summary>
    private const int SupplyBatches = 2;

    internal sealed record WorkstationSupplyNeed(PlacedBuilding Building, BuildingDefinition Definition,
        string ItemKind, int Missing, InventoryLot? Carried, InventoryLot? HouseholdStock, MapResource? Source);

    private static bool HasDedicatedSupply(BuildingDefinition definition) =>
        definition.Tags.Any(tag => tag is "house" or "farmhouse" or "blacksmith");

    /// <summary>The source station's live two-batch input need, independent of available supply actions.</summary>
    private int WorkstationInputTarget(PlacedBuilding building, string itemKind) => building.HouseholdId is { } householdId
        ? worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.DefinitionId &&
                !recipe.IsCrop && !IsGenericFoodRecipe(recipe) && NeedsRecipeOutput(recipe, householdId))
            .SelectMany(recipe => recipe.Inputs).Where(input => input.ResourceId == itemKind)
            .Select(input => input.Amount).DefaultIfEmpty(0).Max() * SupplyBatches
        : 0;

    private static int UsableWorkstationQuantity(InventoryCheckpoint inventory, InventoryLot lot) =>
        InventoryRules.HasUsableContainer(InventoryIndex.For(inventory), lot)
            ? Math.Max(0, InventoryRules.AvailableQuantity(InventoryIndex.For(inventory), lot)) : 0;

    private static int WorkstationOnsiteQuantity(InventoryCheckpoint inventory, PlacedBuilding building, string itemKind) =>
        building.HouseholdId is { } householdId ? InventoryIndex.For(inventory).StoredAt(building.InstanceId).Where(lot => lot.OwnerId == householdId &&
                lot.StorageBuildingId == building.InstanceId && lot.ItemKind == itemKind)
            .Sum(lot => UsableWorkstationQuantity(inventory, lot)) : 0;

    private int WorkstationIncomingQuantity(InventoryCheckpoint inventory, PlacedBuilding building, string itemKind) =>
        building.HouseholdId is { } householdId ? WorkstationIncomingLots(inventory, building.InstanceId).Where(lot =>
                lot.ItemKind == itemKind && (lot.OwnerId == householdId ||
                    society.Checkpoint.Agents.Any(person => person.Id == lot.OwnerId &&
                        person.HouseholdId == householdId && person.Status == SocietyAgentStatus.Active)) &&
                CanCreditWorkstationIncoming(inventory, lot))
            .Sum(lot => UsableWorkstationQuantity(inventory, lot)) : 0;

    private static bool CanCreditWorkstationIncoming(InventoryCheckpoint inventory, InventoryLot lot)
    {
        var root = lot.ContainerLotId is { } containerId ? inventory.GetLot(containerId) : lot;
        return !InventoryContainerRules.IsContainer(root.ItemKind) ||
            !UnusableDeliveryStock(inventory, root) && !HasActiveContainerReservation(inventory, root.Id);
    }

    private int WorkstationSourceSurplus(InventoryCheckpoint inventory, InventoryLot resource)
    {
        if (resource.StorageBuildingId is not { } sourceId ||
            worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == sourceId &&
                building.HouseholdId == resource.OwnerId) is not { } source)
            return int.MaxValue;
        var available = WorkstationOnsiteQuantity(inventory, source, resource.ItemKind);
        var incoming = WorkstationIncomingQuantity(inventory, source, resource.ItemKind);
        return Math.Max(0, available + incoming - WorkstationInputTarget(source, resource.ItemKind));
    }

    /// <summary>Actual available loose stock above its held source station's live input reserve.</summary>
    private int WorkstationStockSurplus(InventoryCheckpoint inventory, InventoryLot resource) =>
        Math.Min(UsableWorkstationQuantity(inventory, resource),
            WorkstationSourceSurplus(inventory, resource));

    /// <summary>A whole vessel may leave only when every moved ingredient is source surplus.</summary>
    private bool CanRemoveWorkstationStock(InventoryCheckpoint inventory, InventoryLot root, int quantity)
    {
        if (quantity <= 0) return false;
        if (!InventoryContainerRules.IsContainer(root.ItemKind))
            return quantity <= WorkstationStockSurplus(inventory, root);
        if (quantity != 1 || UnusableDeliveryStock(inventory, root) || HasActiveContainerReservation(inventory, root.Id))
            return false;
        return InventoryIndex.For(inventory).ContentsOf(root.Id)
            .GroupBy(lot => lot.ItemKind).All(group =>
                group.Sum(lot => UsableWorkstationQuantity(inventory, lot)) <=
                    WorkstationSourceSurplus(inventory, group.First()));
    }

    private IEnumerable<WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null,
        string? buildingId = null, int maximumResourceQuantity = int.MaxValue, string? sourceLotId = null)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId)
            yield break;
        var definitions = worldContent.BuildingDefinitions;
        foreach (var building in worldSimulation.Buildings.Where(item => item.HouseholdId == householdId &&
                     (buildingId is null || item.InstanceId == buildingId))
                     .OrderBy(item => item.InstanceId, StringComparer.Ordinal))
        {
            if (!definitions.TryGetValue(building.DefinitionId, out var definition) ||
                HouseholdBuildingKind(definition) is null)
                continue;
            var recipes = WorkstationSupplyRecipes(definition, householdId);
            foreach (var input in recipes.SelectMany(recipe => recipe.Inputs).GroupBy(input => input.ResourceId))
            {
                if (itemKind is not null && input.Key != itemKind) continue;
                var inventory = society.Checkpoint.Inventory;
                var missing = WorkstationSupplyMissing(building, input.Key, recipes);
                if (missing <= 0)
                    continue;
                var deliveryRoom = DestinationRoom(inventory, building.InstanceId);
                var carried = FindGoods(new(GoodsUse.CarriedInputs, actor, new([actor, householdId]), GoodsKinds.One(input.Key))).Matches.Select(match => match.Lot)
                    // Water travels inside a carried jug, so look through the vessel to its contents.
                    .Select(lot => lot.ContainerLotId is { } containerId
                        ? inventory.GetLot(containerId) : lot)
                    .Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) &&
                        (sourceLotId is null || lot.Id == sourceLotId) &&
                        lot.DeliveryBuildingId is null && deliveryRoom > 0 && TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, lot) > 0 &&
                        (!InventoryContainerRules.IsContainer(lot.ItemKind) ||
                         ContainerFamilyQuantity(inventory, lot.Id) <= deliveryRoom) &&
                        (!InventoryContainerRules.IsContainer(lot.ItemKind) ||
                         DeliveryResourceQuantity(lot, input.Key) <= maximumResourceQuantity) &&
                        (!InventoryContainerRules.IsContainer(lot.ItemKind) ||
                         !HasActiveContainerReservation(inventory, lot.Id)))
                    .DistinctBy(lot => lot.Id)
                    .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
                var stock = carried is not null ? null : SpareHouseholdStock(actor, householdId, input.Key, building,
                    maximumResourceQuantity, sourceLotId) ??
                    AvailableWarehouseStock(actor, input.Key).FirstOrDefault(lot =>
                        RecheckGoods(DeliveryGoodsRequest(actor, lot.OwnerId, lot.ItemKind, building.InstanceId), lot.Id) is not null &&
                        (sourceLotId is null || lot.Id == sourceLotId) &&
                        WorkstationPickupQuantity(actor, inventory, lot, building.InstanceId, int.MaxValue) > 0 &&
                        (!InventoryContainerRules.IsContainer(lot.ItemKind) ||
                         DeliveryResourceQuantity(lot, input.Key) <= maximumResourceQuantity));
                var source = carried is not null || stock is not null ? null : MaterialSource(input.Key, actor, building.Position);
                if (source is not null && (deliveryRoom <= 0 ||
                    ProjectMaterialCarryUnits(actor, input.Key, source) > FreeCarryCapacity(actor)))
                    source = null;
                if (carried is null && stock is null && source is null)
                    continue;
                if (stock is not null && FreeCarryCapacity(actor) == 0 || source is not null &&
                    FreeCarryCapacity(actor) < ProjectMaterialCarryUnits(actor, input.Key, source))
                    continue;
                yield return new WorkstationSupplyNeed(building, definition, input.Key, missing, carried, stock, source);
            }
        }
    }

    private int WorkstationPickupQuantity(string actor, InventoryCheckpoint inventory, InventoryLot stock,
        string destinationId, int missing)
    {
        var destination = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == destinationId);
        var capacity = destination is null ? 0 : Math.Min(PickupCarryCapacity(actor, stock, destination.Position),
            DestinationRoom(inventory, destinationId));
        if (capacity <= 0)
            return 0;
        return InventoryContainerRules.IsContainer(stock.ItemKind)
            ? ContainerFamilyQuantity(inventory, stock.Id) <= capacity &&
                CanRemoveWorkstationStock(inventory, stock, 1) ? 1 : 0
            : Math.Min(HouseHaulLoadQuantity, Math.Min(capacity, Math.Min(missing,
                WorkstationStockSurplus(inventory, stock))));
    }

    private InventoryLot? SpareHouseholdStock(string actor, string householdId, string itemKind,
        PlacedBuilding destination, int maximumResourceQuantity = int.MaxValue, string? sourceLotId = null)
    {
        var inventory = society.Checkpoint.Inventory;
        return FindGoods(DeliveryGoodsRequest(actor, householdId, itemKind, destination.InstanceId)).Matches
            .Select(match => match.Root)
            .Where(lot => lot.CarrierId is null &&
                (sourceLotId is null || lot.Id == sourceLotId) &&
                (!InventoryContainerRules.IsContainer(lot.ItemKind) ||
                 DeliveryResourceQuantity(lot, itemKind) <= maximumResourceQuantity) &&
                (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                    building.InstanceId == lot.StorageBuildingId && worldContent.Buildings.Any(definition =>
                        definition.CanonicalId == building.DefinitionId &&
                        definition.Tags.Any(tag => tag is "house" or "silo" or "farmhouse" or "restaurant" or "animal-yard")))))
            .Where(lot => WorkstationPickupQuantity(actor, inventory, lot, destination.InstanceId, int.MaxValue) > 0)
            .DistinctBy(lot => lot.Id)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private void AddWorkstationSupplyCandidate(List<CognitionCandidate> candidates, string actor)
    {
        if (PlanWorkstationSupply(actor) is not { } plan || !CheckActionPlan(plan).Allowed) return;
        var definition = worldContent.BuildingDefinitions[plan.DefinitionId];
        candidates.Add(new CognitionCandidate(plan.CandidateId,
            $"Bring {plan.ItemKind} into the household {definition.DisplayName} for its work.",
            24, plan.BuildingId)
        { ActionPlan = plan });
    }

    private DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (!IsAdult(actor) || HouseholdFor(actor) is not { } householdId) return null;
        var inventory = society.Checkpoint.Inventory;
        foreach (var need in WorkstationSupplyNeeds(actor, order.TargetItemKind,
                     order.TargetStorageBuildingId, maximumQuantity, order.DeliveryLotId)
                     .Where(need => DeliveryDestinationMatches(order, need.Building)))
        {
            var carrier = need.Carried ?? need.HouseholdStock;
            if (carrier is null) continue;
            var direct = need.Carried is not null;
            var container = InventoryContainerRules.IsContainer(carrier.ItemKind);
            var quantity = direct
                ? container ? ContainerFamilyQuantity(inventory, carrier.Id) <=
                    DestinationRoom(inventory, need.Building.InstanceId) ? 1 : 0
                    : Math.Min(maximumQuantity, Math.Min(DestinationRoom(inventory, need.Building.InstanceId),
                        Math.Min(need.Missing, TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, carrier))))
                : WorkstationPickupQuantity(actor, inventory, carrier, need.Building.InstanceId,
                    Math.Min(need.Missing, maximumQuantity));
            if (quantity <= 0) continue;
            var resourceQuantity = container ? DeliveryResourceQuantity(carrier, need.ItemKind) : quantity;
            if (resourceQuantity <= 0 || resourceQuantity > maximumQuantity) continue;
            var resource = carrier.ItemKind == need.ItemKind ? carrier : InventoryIndex.For(inventory).ContentsOf(carrier.Id)
                .Where(lot => lot.ItemKind == need.ItemKind)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal).First();
            return new DeliveryOrderPlan("workstation_input", need.Building, householdId,
                carrier, resource, quantity, resourceQuantity, DirectDelivery: direct);
        }
        return null;
    }

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
            Math.Min(need.Missing, TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, stock!))) : kind == WorkstationSupplyKind.Pickup
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
                plan.Kind == WorkstationSupplyKind.Carried && TransportSystem.SpareCarriedQuantity(this.HandcartWorld, plan.Actor, match.Lot) < plan.Quantity ||
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
