using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // The occupied tiles and foot costs a layout reads. Building them gathers everything in the world and
    // walks the map from the actor, so a caller laying out several buildings of one kind builds them once.
    // A household building avoids only other households' land; any other building avoids all of it.
    private sealed record TownLayoutBasis(string Actor, GridPoint? SelectedSite, bool HouseholdBuilding, string? TownProjectId,
        HashSet<GridPoint> Occupied, Dictionary<GridPoint, int> FootCosts);

    private static bool IsHouseholdLayout(BuildingDefinition? building) =>
        building is null || building.Tags.Any(IsHouseholdBuildingTag);

    private TownLayoutBasis CreateTownLayoutBasis(string actor, GridPoint? selectedSite = null,
        BuildingDefinition? building = null, string? townProjectId = null)
    {
        var origin = agents[actor].Position;
        var town = towns.SingleOrDefault(item => item.ResidentIds.Contains(actor, StringComparer.Ordinal));
        var ground = Ground;
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources,
            ground.RoadsAndBridgeEntrances, ground.Fields, ground.ProtectedProjectSites(townProjectId),
            ground.MarketSites, ground.ExpansionSites, ground.CheckedBuildingFootprints,
            agents.Values.Where(person => person.AgentId != actor).Select(person => person.Position),
            // A household builds only on land no other household holds or has asked for; Town buildings avoid it all.
            HouseholdLandHeldByOthers(IsHouseholdLayout(building) ? HouseholdFor(actor) : null));
        return new(actor, selectedSite, IsHouseholdLayout(building), townProjectId,
            occupied, FindUnoccupiedFootCosts(actor, origin, town, occupied, selectedSite));
    }

    private TownLayoutContext CreateTownLayoutContext(string actor, GridPoint? selectedSite = null,
        BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null,
        TownLayoutBasis? basis = null)
    {
        if (basis is not null && (basis.Actor != actor || basis.SelectedSite != selectedSite ||
                basis.HouseholdBuilding != IsHouseholdLayout(building) || basis.TownProjectId != townProjectId))
            throw new InvalidOperationException("A Town layout was given a basis built for another layout.");
        basis ??= CreateTownLayoutBasis(actor, selectedSite, building, townProjectId);
        var town = towns.SingleOrDefault(item => item.ResidentIds.Contains(actor, StringComparer.Ordinal));
        var definitions = worldContent.BuildingDefinitions;
        var resourcesForLayout = map.Resources.Select(resource => new TownLayoutResource(
            resource,
            resources.GetValueOrDefault(resource.Id) == ResourceState.Available));
        var buildingsForLayout = worldSimulation.Buildings
            .Where(building => definitions.ContainsKey(building.DefinitionId))
            .Select(building => new TownLayoutBuilding(building, BuildingStorageRules.EffectiveDefinition(definitions[building.DefinitionId], building)));
        return new TownLayoutContext(
            map,
            town,
            basis.Occupied,
            basis.FootCosts,
            resourcesForLayout,
            buildingsForLayout,
            roadTiles: roadTiles,
            requiredNeighborTiles: building is not null && HouseholdBuildingKind(building) == "silo" ? SiloNeighborTiles(actor, definitions) : null,
            requiredLandTiles: forTownProject && town is not null ? TownProjectLandTiles(town) : null,
            requiredEntranceOffset: forTownProject
                ? building?.Tags.Contains(MarketContent.HallTag, StringComparer.Ordinal) == true ? new GridPoint(1, 2) : new GridPoint(1, 4)
                : null,
            requiredFootprintOffsets: forTownProject && building?.Tags.Contains(MarketContent.HallTag, StringComparer.Ordinal) == true
                ? MarketContent.SiteTiles(new(0, 0)) : null,
            permittedRoadOffsets: forTownProject && building?.Tags.Contains(MarketContent.HallTag, StringComparer.Ordinal) == true
                ? MarketContent.PlazaTiles(new(0, 0)).Except(Enumerable.Range(0, MarketContent.MaximumStalls)
                    .Select(slot => MarketContent.StallSite(new(0, 0), slot))) : null,
            protectedTiles: TownProjectProtectedSites(townProjectId).Concat(bridges.SelectMany(item => item.Entrances)));
    }

    /// <summary>A Silo stands near its household's Farmhouse; no Farmhouse means no legal Silo site.</summary>
    private GridPoint[] SiloNeighborTiles(string actor,
        System.Collections.ObjectModel.ReadOnlyDictionary<string, BuildingDefinition> definitions) =>
        society.Checkpoint.GetAgent(actor).HouseholdId is { } householdId &&
        FarmhouseForHousehold(householdId) is { } farmhouse
            ? WorldContentSimulationRules.Footprint(definitions[farmhouse.DefinitionId], farmhouse.Position).ToArray()
            : [];

    private Dictionary<GridPoint, int> FindUnoccupiedFootCosts(
        string agentId, GridPoint origin, TownRuntimeState? town,
        HashSet<GridPoint> occupiedSites, GridPoint? selectedSite)
    {
        if (selectedSite is { } invalidSite &&
            (!map.IsBuildable(invalidSite) || occupiedSites.Contains(invalidSite)))
            return new Dictionary<GridPoint, int> { [origin] = 0 };
        var occupied = agents.Values
            .Where(item => item.AgentId != agentId)
            .Select(item => item.Position)
            .ToHashSet();
        // A Town has bounded construction anchors. Legacy worlds without a
        // Town consider the nearest 32 viable anchors; an already chosen site
        // remains a mandatory route target, however far away it was saved.
        IReadOnlyList<GridPoint>? anchors = town is null ? null : TownLayoutContext.CandidateBounds(map, town);
        if (selectedSite is { } chosenSite && anchors is not null)
            anchors = anchors.Contains(chosenSite) ? [chosenSite] : [];
        var pendingAnchors = anchors?.Where(point => map.IsBuildable(point) && !occupiedSites.Contains(point))
            .ToHashSet();
        var open = new PriorityQueue<GridPoint, (int Cost, int Y, int X, int Order)>();
        // Layout searches can visit most of a generated map. Keep tentative
        // costs in a rented tile array rather than allocating a second map-sized
        // dictionary alongside the settled costs every time a layout is built.
        var tileCount = checked(map.Width * map.Height);
        var settled = new Dictionary<GridPoint, int>();
        var viableLegacyAnchors = 0;
        var selectedSiteReached = selectedSite is null ||
            pendingAnchors is not null && pendingAnchors.Count == 0;
        var order = 0;
        var best = ArrayPool<int>.Shared.Rent(tileCount);
        try
        {
            Array.Fill(best, -1, 0, tileCount);
            best[origin.Y * map.Width + origin.X] = 0;
            open.Enqueue(origin, (0, origin.Y, origin.X, order++));
            while (open.TryDequeue(out var current, out var priority))
            {
                if (priority.Cost != best[current.Y * map.Width + current.X])
                    continue;
                settled[current] = priority.Cost;
                if (current == selectedSite)
                    selectedSiteReached = true;
                if (pendingAnchors is null)
                {
                    if (map.IsBuildable(current) && !occupiedSites.Contains(current))
                        viableLegacyAnchors++;
                }
                else
                {
                    pendingAnchors.Remove(current);
                }
                if (selectedSite is not null ? selectedSiteReached :
                    pendingAnchors?.Count == 0 || pendingAnchors is null && viableLegacyAnchors >= 32)
                    break;
                foreach (var next in map.FootNeighborsForSearch(current))
                {
                    if (occupied.Contains(next) ||
                        map.IsDiagonalFootStep(current, next) &&
                        (occupied.Contains(new GridPoint(next.X, current.Y)) ||
                         occupied.Contains(new GridPoint(current.X, next.Y))))
                        continue;

                    var cost = checked(priority.Cost + LegalRoadStepCost(current, next));
                    var index = next.Y * map.Width + next.X;
                    if (best[index] >= 0 && best[index] <= cost)
                        continue;
                    best[index] = cost;
                    open.Enqueue(next, (cost, next.Y, next.X, order++));
                }
            }
            return settled;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(best);
        }
    }

    private bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null) =>
        productionSystem.TryFindRecipeSite(RecipeWorld, recipe, out siteId, out position, actorId);

    private static bool IsGenericFoodRecipe(RecipeDefinition recipe) => ProductionSystem.IsGenericFoodRecipe(recipe);

    // Workstation output has no destination vessel; fresh water cannot be a loose lot.
    private static bool HasUnsupportedWaterOutput(RecipeDefinition recipe) =>
        ProductionSystem.HasUnsupportedWaterOutput(recipe);

    private bool HasAvailableQuantities(IReadOnlyList<ContentQuantity> quantities, string? ownerId = null)
    {
        var inventory = society.Checkpoint.Inventory;
        foreach (var requested in quantities)
        {
            var available = FindGoods(inventory, new(GoodsUse.Holdings, null,
                    GoodsOwners.One(ownerId ?? HouseholdId), GoodsKinds.One(requested.ResourceId)))
                .Matches.Where(match => !IsHandcartCargo(inventory, match.Lot)).Sum(match => (long)match.Quantity);
            if (available < requested.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasCarriedUnreservedQuantities(string actor, IReadOnlyList<ContentQuantity> quantities)
    {
        var inventory = society.Checkpoint.Inventory;
        foreach (var requested in quantities.GroupBy(item => item.ResourceId, StringComparer.Ordinal))
        {
            var available = inventory.Lots
                .Where(lot => lot.OwnerId == actor && PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor) &&
                    lot.DeliveryBuildingId is null && lot.ItemKind == requested.Key)
                .Sum(lot => (long)AvailableLotQuantity(lot));
            if (available < requested.Sum(item => (long)item.Amount))
                return false;
        }

        return true;
    }

    private string BuildingConstructionOwner(string actor, BuildingDefinition definition)
    {
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (household is null) return actor;
        if (!definition.Tags.Any(IsHouseholdBuildingTag)) return HouseholdId;
        if (definition.Tags.Contains("house", StringComparer.Ordinal) && HouseForHousehold(household) is null &&
            HasCarriedUnreservedQuantities(actor, definition.BuildCosts))
            return actor;

        // The household stages a multi-load first House at the shared storage spot. When its
        // complete cost is already on the builder, preserve direct delivery.
        return household;
    }

    private string BuildInstanceId(string agentId, BuildingDefinition definition)
    {
        // Preserve valid legacy IDs; descendant identities contain separators
        // that are legal society IDs but invalid content instance IDs.
        var original = AutomaticBuildingId(agentId, definition);
        var candidate = original;
        // Historical order bindings keep their identity. Ordinary paid rebuilding
        // selects a stable replacement without changing those bindings or payments.
        for (var replacement = 1; BuildingIdentityIsReserved(candidate); replacement++)
            candidate = ReplacementBuildingId(original, replacement);
        return candidate;
    }

    // A synchronous proposal query checks many sites against the same objects.
    // Keep Market tiles separate: only an extra stall may use its own slot,
    // and that exception never opens a tile occupied by another kind of object.
    private sealed record BuildingPlacementTiles(HashSet<GridPoint> Occupied, HashSet<GridPoint> Market,
        string? Failure = null);

    private sealed record BuildingPlacementWorld(string? ProjectId, Lazy<BuildingPlacementTiles> Tiles);

    private BuildingPlacementWorld CreateBuildingPlacementWorld(string? townProjectId) =>
        new(townProjectId, new(() => GatherBuildingPlacementTiles(townProjectId), LazyThreadSafetyMode.None));

    private BuildingPlacementTiles GatherBuildingPlacementTiles(string? townProjectId)
    {
        var ground = Ground;
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources,
            ground.RoadsAndBridgeEntrances, ground.Fields, ground.ProtectedProjectSites(townProjectId), ground.ExpansionSites);
        var market = ground.MarketSites.ToHashSet();
        var buildingDefinitions = worldContent.BuildingDefinitions;
        foreach (var placed in worldSimulation.Buildings)
        {
            if (!buildingDefinitions.TryGetValue(placed.DefinitionId, out var existingDefinition))
            {
                return new(occupied, market, $"Placed building '{placed.InstanceId}' references an unavailable definition.");
            }

            foreach (var existingPoint in WorldContentSimulationRules.Footprint(existingDefinition, placed))
            {
                occupied.Add(existingPoint);
            }
        }

        occupied.UnionWith(worldSimulation.Buildings.Where(building => Port(building.InstanceId) is not null)
            .Select(PortGeometryFor).SelectMany(geometry => geometry.DockingTiles));
        return new(occupied, market);
    }

    private bool CanPlaceBuilding(
        BuildingDefinition definition,
        GridPoint position,
        out string failure,
        string? townProjectId = null,
        BuildingPlacementWorld? world = null)
    {
        var footprint = WorldContentSimulationRules.Footprint(definition, position).ToArray();
        if (!PortNavigationRules.IsPort(definition) && footprint.Any(point => !map.IsBuildable(point)))
        {
            failure = "Every building footprint tile must be on buildable ground; mountains and peaks cannot hold buildings.";
            return false;
        }
        if (world is not null && world.ProjectId != townProjectId)
            throw new InvalidOperationException("A building placement check was given tiles gathered for another project.");
        world ??= CreateBuildingPlacementWorld(townProjectId);
        var tiles = world.Tiles.Value;
        if (tiles.Failure is { } contentFailure)
        {
            failure = contentFailure;
            return false;
        }
        if (PortNavigationRules.IsPort(definition))
        {
            var occupied = tiles.Occupied.Concat(tiles.Market).ToHashSet();
            var fits = PortNavigationRules.Fits(map, definition, position, occupied, out var portFailure, roadTiles);
            failure = portFailure ?? string.Empty;
            return fits;
        }
        if (footprint.Any(point => tiles.Occupied.Contains(point) || tiles.Market.Contains(point) &&
                (point != position || !definition.Tags.Contains(MarketContent.StallTag, StringComparer.Ordinal))))
        {
            failure = "The building footprint overlaps an existing object, resource, Road, bridge end, or building.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        society.Apply(checkpoint =>
        {
            var equipmentBefore = agents.Values.ToDictionary(person => person.AgentId,
                person => person.Equipment, StringComparer.Ordinal);
            var updated = InventoryStorageHistory.RecordTransition(checkpoint.Inventory, MarketStockHolds.Update(transition(checkpoint.Inventory), towns));
            foreach (var building in worldSimulation.Buildings)
            {
                var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
                if (BuildingStorageRules.Capacity(definition, building) is not { } capacity) continue;
                var before = checkpoint.Inventory.Lots.Where(lot => lot.StorageBuildingId == building.InstanceId).Sum(lot => lot.Quantity) +
                    ReservedShopStorageSpace(building.InstanceId, checkpoint.Inventory);
                var after = updated.Lots.Where(lot => lot.StorageBuildingId == building.InstanceId).Sum(lot => lot.Quantity) +
                    ReservedShopStorageSpace(building.InstanceId, updated);
                if (after > capacity && after > before)
                    throw new InvalidOperationException("The building's storage is full; carry the remaining stock or expand it first.");
            }
            foreach (var person in agents.Values)
            {
                var before = PersonalEquipmentRules.CarriedQuantity(checkpoint.Inventory, person.AgentId, equipmentBefore[person.AgentId]) +
                    ReservedShopCarrySpace(person.AgentId, checkpoint.Inventory);
                var after = PersonalEquipmentRules.CarriedQuantity(updated, person.AgentId, person.Equipment) +
                    ReservedShopCarrySpace(person.AgentId, updated);
                if (after > before && after > PersonalEquipmentRules.Capacity(updated, person.AgentId, person.Equipment) + HorseCargoCapacity(person.AgentId))
                    throw new InvalidOperationException("The person is carrying as much as they can; store or set down a load first.");
            }
            return new SocietyOperationResult(checkpoint with { Inventory = updated }, null, []);
        });
    }

    private static InventoryCheckpoint ConsumeConstructionInputs(InventoryCheckpoint inventory,
        IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner)
    {
        var current = inventory;
        foreach (var input in inputs)
        {
            var reservationId = ProductionInputReservationId(purpose, input.QuantityIndex, input.LotId);
            current = InventoryAuthority.Reserve(current, reservationId, owner, input.LotId, input.Quantity,
                purpose, current.WorldTick, holdAccess: InventoryHoldRules.BorrowedBy(owner));
            current = InventoryAuthority.ConsumeReservation(current, reservationId);
        }
        return current;
    }

    private const string MissingHouseholdIngredientsPrefix = ProductionSystem.MissingHouseholdIngredientsPrefix;

    private static bool IsIngredientBlocker(string? blocker) =>
        blocker == "Waiting for ingredients at this household building" ||
        blocker?.StartsWith(MissingHouseholdIngredientsPrefix, StringComparison.Ordinal) == true;

    private string MissingProductionIngredients(RecipeDefinition recipe, string owner, string buildingId) =>
        ProductionSystem.MissingProductionIngredients(RecipeWorld, recipe, owner, buildingId);

    private bool HasIngredientsAtBuilding(IReadOnlyList<ContentQuantity> inputs, string ownerId, string buildingId) =>
        ProductionSystem.HasIngredientsAtBuilding(RecipeWorld, inputs, ownerId, buildingId);

    private static GoodsRequest ProductionGoodsRequest(string owner, string kind, string? building = null) =>
        ProductionSystem.GoodsRequestForRecipe(owner, kind, building);

    private IEnumerable<GoodsMatch> ProductionGoods(InventoryCheckpoint inventory, GoodsRequest request,
        bool requireCarried = false) => FindGoods(inventory, request).Matches.Where(match =>
        !IsHandcartCargo(inventory, match.Lot) && !OnBorrowedMarketStall(match.Lot) &&
        (!requireCarried || ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, match.Lot.OwnerId)));

    private static InventoryCheckpoint ReserveRecipeInputs(InventoryCheckpoint inventory, RecipeProjectActionPlan plan,
        string purpose, long expiryTick, out IReadOnlyList<string> reservationIds)
    {
        var current = inventory;
        var created = new List<string>();
        foreach (var input in plan.Inputs!)
        {
            var reservationId = ProductionInputReservationId(purpose, input.QuantityIndex, input.LotId);
            current = InventoryAuthority.Reserve(current, reservationId, plan.OwnerId, input.LotId,
                input.Quantity, purpose, expiryTick, holdAccess: InventoryHoldRules.BorrowedBy(plan.OwnerId));
            created.Add(reservationId);
        }
        reservationIds = created.ToArray();
        return current;
    }

    private void ProcessProduction(long targetTick) => productionSystem.ProcessProduction(ProductionWorld, targetTick);

    private bool CompleteProductionJob(WorldProductionJob job, RecipeDefinition recipe, long targetTick) =>
        ProductionSystem.CompleteProductionJob(ProductionWorld, job, recipe, targetTick);

    private void CompleteRecipeInventory(WorldProductionJob job, RecipeDefinition recipe, ToolWorkPlan? knifePlan, long targetTick)
    {
        var productionBuilding = worldSimulation.Buildings
            .FirstOrDefault(building => building.InstanceId == job.BuildingInstanceId);
        var productionOwner = job.OwnerId ?? throw new InvalidDataException("A production job has no recorded owner.");
        ApplyInventoryTransition(inventory =>
        {
            var current = inventory;
            foreach (var reservationId in job.InputReservationIds.Order(StringComparer.Ordinal))
            {
                current = InventoryAuthority.ConsumeReservation(current, reservationId);
            }

            for (var outputIndex = 0; outputIndex < recipe.Outputs.Count; outputIndex++)
            {
                var output = recipe.Outputs[outputIndex];
                var outputLotId = ProductionOutputLotId(job.JobId, outputIndex);
                var vessel = InventoryContainerRules.IsContainer(output.ResourceId);
                for (var unitIndex = 0; unitIndex < (vessel ? output.Amount : 1); unitIndex++)
                {
                    current = InventoryAuthority.AddLot(
                        current,
                        unitIndex == 0 ? outputLotId : ProductionVesselUnitId(outputLotId, unitIndex),
                        output.ResourceId,
                        output.ResourceId == InventoryContainerRules.Handcart ? job.WorkerId : productionOwner,
                        vessel ? 1 : output.Amount,
                        targetTick,
                        storageBuildingId: output.ResourceId != InventoryContainerRules.Handcart && productionBuilding?.HouseholdId is not null
                            ? productionBuilding.InstanceId : null,
                        groundPosition: output.ResourceId == InventoryContainerRules.Handcart
                            ? new InventoryGroundPosition(productionBuilding!.Position.X, productionBuilding.Position.Y) : null);
                }
            }

            return knifePlan is null ? current : ApplyToolWorkToInventory(current, job.WorkerId,
                targetTick, [knifePlan]);
        });
    }
}
