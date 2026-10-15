using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record BuildingFootprintRevision(int Width, int Height, int Revision);

public sealed record BuildingExpansionJob(
    string JobId, string BuildingInstanceId, string WorkerId, string OwnerId,
    int ExpectedRevision, GridPoint ExpectedPosition, GridPoint TargetPosition,
    BuildingFootprintRevision TargetFootprint, long StartedTick, long CompletionTick,
    WorldProductionJobState State, IReadOnlyList<string> InputReservationIds,
    string? Failure = null,
    string? DefinitionId = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PausedAtTick { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrderInstructionId { get; init; }
}

public static class BuildingStorageRules
{
    // Trial stock units per tile. These are storage sizes, never occupant or work-slot limits.
    public const int UnitsPerTile = 64;
    public const int NearlyFullPercent = 80;

    public static BuildingDefinition EffectiveDefinition(BuildingDefinition definition, PlacedBuilding building) =>
        building.Footprint is { } footprint
            ? WithSize(definition, footprint.Width, footprint.Height) : definition;

    public static BuildingDefinition WithSize(BuildingDefinition definition, int width, int height) =>
        new(definition.PackageDigest, definition.LocalId, definition.Version, definition.DisplayName,
            width, height, definition.Capacity, definition.BuildCosts, definition.Tags);

    public static int? Capacity(BuildingDefinition definition, PlacedBuilding building) =>
        definition.Tags.Contains(AnimalContent.YardTag, StringComparer.Ordinal) ? 16 :
        definition.Tags.Contains("farmhouse", StringComparer.Ordinal) ? FarmFieldRules.FarmStorageCapacity :
        definition.Tags.Any(tag => tag is "house" or "warehouse" or "blacksmith" or "tailor" or "store" or "restaurant" or "clinic" or "silo")
            ? UnitsPerTile * (building.Footprint?.Width ?? definition.Width) *
                (building.Footprint?.Height ?? definition.Height) : null;

    public static IReadOnlyList<ContentQuantity> ExpansionCosts(BuildingDefinition definition,
        PlacedBuilding building, BuildingFootprintRevision target)
    {
        var current = EffectiveDefinition(definition, building);
        var extraTiles = target.Width * target.Height - current.Width * current.Height;
        if (definition.Tags.Contains(AnimalContent.YardTag, StringComparer.Ordinal)) return [new("wood", 8), new("rope", 2)];
        return definition.Tags.Contains("warehouse", StringComparer.Ordinal)
            ? [new("wood", 4 * extraTiles), new("stone", 2 * extraTiles)] : [new("wood", 4 * extraTiles)];
    }

    public static bool IsSupported(BuildingDefinition definition, BuildingFootprintRevision footprint) =>
        definition.Tags.Contains("house", StringComparer.Ordinal)
            ? (footprint.Width, footprint.Height, footprint.Revision) is (1, 2, 1) or (2, 1, 1) or (2, 2, 2)
            : definition.Tags.Contains(AnimalContent.YardTag, StringComparer.Ordinal)
                ? (footprint.Width, footprint.Height, footprint.Revision) is (2, 4, 1) or (4, 2, 1)
            : definition.Tags.Contains("warehouse", StringComparer.Ordinal) &&
                (footprint.Width, footprint.Height, footprint.Revision) is (2, 3, 1) or (3, 2, 1);
}

public sealed partial class WorldRuntime
{
    private const string ExpandBuildingPrefix = ExpansionChoice.Prefix;
    // Provisional duration; paid model calls do not advance construction.

    private int StoredQuantity(string buildingId, InventoryCheckpoint? inventory = null) =>
        InventoryIndex.For(inventory ?? society.Checkpoint.Inventory).StoredAt(buildingId).Sum(lot => lot.Quantity);

    private int ReservedStorageGrowth(string buildingId, InventoryCheckpoint? inventory = null) => worldSimulation.ProductionJobs
        .Where(job => job.BuildingInstanceId == buildingId && (job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused))
        .Sum(job =>
        {
            var recipe = worldContent.Recipes.Single(item => item.CanonicalId == job.RecipeId);
            return Math.Max(0, recipe.Outputs.Where(item => item.ResourceId != InventoryContainerRules.Handcart)
                .Sum(item => item.Amount) - job.InputReservationIds
                .Select((inventory ?? society.Checkpoint.Inventory).GetReservation).Where(reservation =>
                    (inventory ?? society.Checkpoint.Inventory).GetLot(reservation.LotId).StorageBuildingId == buildingId)
                .Sum(reservation => reservation.Quantity));
        });

    private static string ExpansionOwner(PlacedBuilding building) => ConstructionSystem.ExpansionOwner(building);

    private static InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building) =>
        new(building.Position.X, building.Position.Y);

    private IEnumerable<InventoryLot> ExpansionMaterialLots(string actor, PlacedBuilding building)
    {
        var owner = ExpansionOwner(building);
        var site = ExpansionGroundPosition(building);
        return society.Checkpoint.Inventory.Lots.Where(lot =>
            lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) && lot.DeliveryBuildingId is null ||
            lot.OwnerId == owner && lot.StorageBuildingId == building.InstanceId ||
            lot.OwnerId == owner && lot.GroundPosition == site);
    }

    private bool HasExpansionMaterials(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) =>
        costs.All(cost => ExpansionMaterialLots(actor, building).Where(lot => lot.ItemKind == cost.ResourceId)
            .Sum(AvailableLotQuantity) >= cost.Amount);

    private InventoryLot? ExpansionDelivery(string actor, PlacedBuilding building, string itemKind, string? lotId = null) =>
        society.Checkpoint.Inventory.Lots
            .Where(lot => lot.OwnerId == actor && lot.ItemKind == itemKind &&
                lot.DeliveryBuildingId == building.InstanceId && AvailableLotQuantity(lot) > 0 && (lotId is null || lot.Id == lotId))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private int ExpansionInboundQuantity(PlacedBuilding building, string itemKind) =>
        society.Checkpoint.Inventory.Lots
            .Where(lot => lot.ItemKind == itemKind && lot.DeliveryBuildingId == building.InstanceId)
            .Sum(AvailableLotQuantity);

    private InventoryLot? ExpansionSharedMaterialSource(string actor, PlacedBuilding building, string itemKind)
    {
        var owner = ExpansionOwner(building);
        var site = ExpansionGroundPosition(building);
        // Town Warehouse stock follows the usual Warehouse access rules: the
        // actor's own Town, or an empty Town's Warehouse anyone may recover.
        var warehouseStock = building.HouseholdId is not null
            ? AvailableWarehouseStock(actor, itemKind) : Enumerable.Empty<InventoryLot>();
        return FindGoods(DeliveryGoodsRequest(actor, owner, itemKind, building.InstanceId, GoodsDeliveryPurpose.Expansion)).Matches.Select(match => match.Lot)
            .Where(lot => lot.StorageBuildingId != building.InstanceId && lot.CarrierId is null &&
                lot.GroundPosition != site && lot.ContainerLotId is null)
            .Concat(warehouseStock)
            .Where(lot => RecheckGoods(DeliveryGoodsRequest(actor, lot.OwnerId, itemKind, building.InstanceId,
                GoodsDeliveryPurpose.Expansion), lot.Id) is not null)
            .FirstOrDefault();
    }

    private (InventoryLot? Source, int Quantity) ExpansionSharedMaterialPickup(
        string actor, PlacedBuilding building, ContentQuantity cost)
    {
        var source = ExpansionSharedMaterialSource(actor, building, cost.ResourceId);
        if (source is null) return (null, 0);

        var alreadyAtSite = ExpansionMaterialLots(actor, building)
            .Where(lot => lot.ItemKind == cost.ResourceId).Sum(AvailableLotQuantity);
        var inbound = ExpansionInboundQuantity(building, cost.ResourceId);
        var remaining = Math.Max(0, cost.Amount - alreadyAtSite - inbound);
        var quantity = Math.Min(HouseHaulLoadQuantity, Math.Min(remaining,
            Math.Min(AvailableLotQuantity(source), PickupCarryCapacity(actor, source, building.Position))));
        return (source, quantity);
    }

    private bool CanAcquireExpansionMaterial(string actor, PlacedBuilding building, ContentQuantity cost)
    {
        if (HasExpansionMaterials(actor, building, [cost]) ||
            ExpansionDelivery(actor, building, cost.ResourceId) is not null)
            return true;

        var alreadyAtSite = ExpansionMaterialLots(actor, building)
            .Where(lot => lot.ItemKind == cost.ResourceId).Sum(AvailableLotQuantity);
        if (cost.Amount - alreadyAtSite <= ExpansionInboundQuantity(building, cost.ResourceId))
            return false;

        if (ExpansionSharedMaterialPickup(actor, building, cost).Quantity > 0)
            return true;
        return MaterialSource(cost.ResourceId, actor, building.Position) is { } source &&
            FreeCarryCapacity(actor) >= ProjectMaterialCarryUnits(actor, cost.ResourceId, source);
    }

    private bool CanAcquireExpansionMaterials(string actor, PlacedBuilding building,
        IReadOnlyList<ContentQuantity> costs)
    {
        var missing = costs.Where(cost => !HasExpansionMaterials(actor, building, [cost])).ToArray();
        if (missing.Length == 0) return true;

        // Let an actor complete any delivery already on their back even when
        // they have no remaining carrying room for the next load.
        if (missing.Any(cost => ExpansionDelivery(actor, building, cost.ResourceId) is not null))
            return true;
        return missing.All(cost => CanAcquireExpansionMaterial(actor, building, cost));
    }

    private static InventoryCheckpoint ReserveExpansionInputs(InventoryCheckpoint inventory, IReadOnlyList<ExpansionMaterialInput> inputs,
        string actor, string jobId, long completion, out IReadOnlyList<string> reservationIds)
    {
        var ids = new List<string>();
        foreach (var input in inputs)
        {
            var id = ExpansionMaterialReservationId(jobId, ids.Count);
            inventory = InventoryAuthority.Reserve(inventory, id, input.OwnerId, input.LotId, input.Quantity,
                jobId, completion, holdAccess: InventoryHoldRules.BorrowedBy(actor));
            ids.Add(id);
        }
        reservationIds = ids;
        return inventory;
    }

    private bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure) =>
        constructionSystem.MayExpandBuilding(ExpansionPlanningWorld, actor, building, out failure);

    private IEnumerable<(GridPoint Position, BuildingFootprintRevision Footprint)> ExpansionShapes(PlacedBuilding building)
    {
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var current = BuildingStorageRules.EffectiveDefinition(definition, building);
        var revision = (building.Footprint?.Revision ?? 0) + 1;
        (int Width, int Height)[] sizes = definition.Tags.Contains("house", StringComparer.Ordinal)
            ? revision == 1 ? [(1, 2), (2, 1)] : revision == 2 ? [(2, 2)] : []
            : definition.Tags.Contains(AnimalContent.YardTag, StringComparer.Ordinal)
                ? revision == 1 ? [(2, 4), (4, 2)] : []
            : revision == 1 ? [(2, 3), (3, 2)] : [];
        foreach (var size in sizes)
        {
            for (var dy = current.Height - size.Height; dy <= 0; dy++)
                for (var dx = current.Width - size.Width; dx <= 0; dx++)
                    yield return (new GridPoint(building.Position.X + dx, building.Position.Y + dy),
                        new BuildingFootprintRevision(size.Width, size.Height, revision));
        }
    }

    private bool CanFitExpansion(PlacedBuilding building, GridPoint position,
        BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true)
    {
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        failure = "The expansion overlaps terrain, a resource, a Road, a field, another building, or another expansion.";
        var target = BuildingStorageRules.WithSize(definition, footprint.Width, footprint.Height);
        if (!BuildingStorageRules.IsSupported(definition, footprint) ||
            !WorldContentSimulationRules.Fits(map, worldSimulation.Buildings
                .Where(item => item.InstanceId != building.InstanceId)
                .Select(item => (item, BuildingStorageRules.EffectiveDefinition(
                    worldContent.Buildings.Single(value => value.CanonicalId == item.DefinitionId), item))), target, position))
            return false;
        var tiles = WorldContentSimulationRules.Footprint(target, position).ToHashSet();
        var original = WorldContentSimulationRules.Footprint(definition, building);
        if (!original.All(tiles.Contains) || tiles.Any(RoadAndBridgeTiles().Contains) || TownProjectProtectedSites().Any(tiles.Contains) ||
            MarketSiteTiles().Any(tiles.Contains) ||
            fields.Any(field => tiles.Contains(field.Position)) ||
            (worldSimulation.BuildingExpansions ?? []).Where(job => (job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused) &&
                job.JobId != ownJobId).Any(job => ExpansionTiles(job).Any(tiles.Contains))) return false;
        var town = towns.SingleOrDefault(item => item.Id == building.TownId);
        if (town is not null && (!TownBorderRules.IsWithinOrAdjacent(town, position, target.Width, target.Height) ||
            towns.Where(item => item.Id != town.Id).Any(item => item.BorderTiles.Any(tiles.Contains))))
        {
            failure = "The expansion does not fit the building's Town boundary or overlaps another Town.";
            return false;
        }
        if (building.Entrance is { } entrance && !WorldContentSimulationRules.IsEntrance(target, position, entrance))
        {
            failure = "The expansion would obstruct the building's entrance.";
            return false;
        }
        var extraTiles = tiles.Except(original).ToArray();
        if (requireLandRights && extraTiles.Any(tile => !MayExpandOntoLand(building, tile)))
        {
            failure = "The expansion lacks the required Town title or household use permission.";
            return false;
        }
        failure = string.Empty;
        return true;
    }

    private bool MayExpandOntoLand(PlacedBuilding building, GridPoint tile)
    {
        var title = townLandTitles.SingleOrDefault(item => item.Tiles.Contains(tile));
        if (building.TownId is { } townId && title?.TownId != townId) return false;
        // Unaffiliated construction on untitled land keeps its physical rules;
        // it creates no title. Unassigning a building cannot bypass a title.
        if (title is null) return true;
        return building.HouseholdId is { } household
            ? householdLandUseRights.Any(right => right.TownId == title.TownId &&
                right.HouseholdId == household && right.Tiles.Contains(tile))
            : building.TownId == title.TownId && !HouseholdLandHeldByOthers(null).Contains(tile);
    }

    private static IEnumerable<GridPoint> ExpansionTiles(BuildingExpansionJob job) =>
        GroundOccupancy.ExpansionTiles(job);

    private void AddBuildingExpansionCandidates(List<CognitionCandidate> candidates, string actor,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache,
        Lazy<PlacedBuilding[]> accessibleWarehouses)
    {
        foreach (var building in worldSimulation.Buildings)
        {
            if (!MayExpandBuilding(actor, building, out _) || !ExpansionShapes(building).Any(shape =>
                    CanFitExpansion(building, shape.Position, shape.Footprint, out _))) continue;
            var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
            var shape = ExpansionShapes(building).First();
            var costs = BuildingStorageRules.ExpansionCosts(definition, building, shape.Footprint);
            if (!CanAcquireProjectInputs(costs, HouseholdFor(actor), actor, reachableToolCache, accessibleWarehouses) ||
                !CanAcquireExpansionMaterials(actor, building, costs)) continue;
            var reason = definition.Tags.Contains("house", StringComparer.Ordinal) &&
                building.HouseholdId is { } householdId &&
                HouseResidentCapacity(householdId) is { } capacity && capacity.ResidentCount >= capacity.Limit
                    ? "to make more resident places"
                    : "to add storage";
            var plan = PlanExpansion(actor, building);
            if (plan is not null && CheckActionPlan(plan).Allowed) candidates.Add(new(plan.CandidateId,
                $"Bring the missing materials to the {definition.DisplayName}, then expand it {reason} without moving its stock.",
                24, building.InstanceId)
            { ActionPlan = plan });
        }
    }

    private void ApplyBuildingExpansionCandidate(string actor, AgentState state, string buildingId,
        OwnerQueuedInstruction? orderInstruction = null, ActionPlan? originalPlan = null)
    {
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == buildingId);
        if (building is null || !MayExpandBuilding(actor, building, out _)) return;
        var plan = originalPlan as ExpansionActionPlan ?? PlanExpansion(actor, building, orderInstruction);
        if (plan is null) return;
        if (CheckExpansion(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (orderInstruction is not null && ExpansionOrderBuilding(orderInstruction)?.InstanceId != buildingId) return;
        switch (plan.MaterialStep)
        {
            case ExpansionDeliveryStep delivery:
                if (state.Position != building.Position)
                {
                    MoveToward(actor, state, building.Position, "building_expansion_delivery", 0);
                    return;
                }
                var owner = ExpansionOwner(building);
                if (delivery.Store)
                {
                    ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                        $"expansion-delivery:{WorldTick}:{actor}:{building.InstanceId}:{delivery.LotId}", actor, owner,
                        delivery.LotId, delivery.Quantity, "building_expansion_material_delivered",
                        destinationStorageBuildingId: building.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, building.InstanceId)));
                    AppendEvent("building_expansion_material_delivered",
                        $"{actor}:{delivery.ItemKind}:{delivery.Quantity}:{building.InstanceId}:stored");
                }
                else
                {
                    ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                        $"expansion-ground-delivery:{WorldTick}:{actor}:{building.InstanceId}:{delivery.LotId}", actor,
                        owner, delivery.LotId, delivery.Quantity,
                        "building_expansion_material_delivered", destinationGroundPosition: ExpansionGroundPosition(building),
                        holdAccess: InventoryHoldRules.DeliveryFor(actor, building.InstanceId)));
                    AppendEvent("building_expansion_material_delivered",
                        $"{actor}:{delivery.ItemKind}:{delivery.Quantity}:{building.InstanceId}:ground");
                }
                return;
            case ExpansionPickupStep pickup:
                if (!IsWithinInteractionRange(state.Position, pickup.Position, pickup.InteractionRange))
                {
                    MoveToward(actor, state, pickup.Position, "building_expansion_material", pickup.InteractionRange);
                    return;
                }
                if (pickup.Quantity > 0)
                {
                    ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                        $"expansion-pickup:{WorldTick}:{actor}:{building.InstanceId}:{pickup.LotId}",
                        pickup.OwnerId, actor, pickup.LotId, pickup.Quantity, "building_expansion_material_picked_up",
                        destinationDeliveryBuildingId: building.HouseholdId is null ? null : building.InstanceId,
                        holdAccess: InventoryHoldRules.BorrowedBy(actor)));
                    AppendEvent("building_expansion_material_picked_up",
                        $"{actor}:{pickup.LotId}:{pickup.Quantity}:{building.InstanceId}");
                }
                return;
            case ExpansionGatherStep gather:
                GatherPlannedProjectMaterial(actor, state, gather.ItemKind, gather.Source, gather.Harvest);
                return;
        }
        if (state.Position != building.Position) MoveToward(actor, state, building.Position, "building_expansion", 0);
        else StartBuildingExpansionCore(actor, buildingId, orderInstruction, plan);
    }

    public ProductionStartResult StartBuildingExpansion(string actor, string buildingId)
    {
        gate.Wait();
        try { return StartBuildingExpansionCore(actor, buildingId); }
        finally { gate.Release(); }
    }

    private ProductionStartResult StartBuildingExpansionCore(string actor, string buildingId,
        OwnerQueuedInstruction? orderInstruction = null, ExpansionActionPlan? originalPlan = null) =>
        constructionSystem.StartExpansion(ExpansionAdmissionWorld, actor, buildingId, orderInstruction, originalPlan);

    private void ProcessBuildingExpansions(long targetTick) => constructionSystem.ProcessExpansions(ExpansionCompletionWorld, targetTick);
}
