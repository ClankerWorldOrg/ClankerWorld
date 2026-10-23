using System.Buffers;
using System.Collections.Frozen;
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
        FrozenSet<GridPoint> Occupied, IReadOnlyDictionary<GridPoint, int> FootCosts);

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
            occupied.ToFrozenSet(), FindUnoccupiedFootCosts(actor, origin, town, occupied, selectedSite).ToFrozenDictionary());
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
            protectedTiles: TownProjectProtectedSites(townProjectId).Concat(bridges.SelectMany(item => item.Entrances))
                .Concat(Ground.BuildingEntrances()));
    }

    /// <summary>A Silo stands near its household's Farmhouse; no Farmhouse means no legal Silo site.</summary>
    private GridPoint[] SiloNeighborTiles(string actor,
        System.Collections.ObjectModel.ReadOnlyDictionary<string, BuildingDefinition> definitions) =>
        society.Checkpoint.GetAgent(actor).HouseholdId is { } householdId &&
        FarmhouseForHousehold(householdId) is { } farmhouse
            ? WorldContentSimulationRules.Footprint(definitions[farmhouse.DefinitionId], farmhouse.Position).ToArray()
            : [];

    internal Dictionary<GridPoint, int> FindUnoccupiedFootCosts(
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
        var open = new PriorityQueue<GridPoint, (int Cost, int TileIndex)>();
        // Layout searches can visit most of a generated map. The rented array
        // stores tentative costs, -1 for unseen tiles and -2 for settled tiles,
        // avoiding a second map-sized dictionary and settled membership lookups.
        var tileCount = checked(map.Width * map.Height);
        var settled = new Dictionary<GridPoint, int>();
        var viableLegacyAnchors = 0;
        var buildings = MovementBuildings;
        var best = ArrayPool<int>.Shared.Rent(tileCount);
        try
        {
            Array.Fill(best, -1, 0, tileCount);
            best[origin.Y * map.Width + origin.X] = 0;
            open.Enqueue(origin, (0, origin.Y * map.Width + origin.X));
            while (open.TryDequeue(out var current, out var priority))
            {
                if (!settled.TryAdd(current, priority.Cost))
                    continue;
                best[current.Y * map.Width + current.X] = -2;
                if (pendingAnchors is null && map.IsBuildable(current) && !occupiedSites.Contains(current))
                    viableLegacyAnchors++;
                pendingAnchors?.Remove(current);
                if (selectedSite is not null ? current == selectedSite || pendingAnchors?.Count == 0 :
                    pendingAnchors?.Count == 0 || pendingAnchors is null && viableLegacyAnchors >= 32)
                    break;
                foreach (var next in map.NeighborPositionsForSearch(current))
                {
                    var index = next.Y * map.Width + next.X;
                    if (best[index] == -2 || !map.CanFootStep(current, next) ||
                        !buildings.CanStep(map, current, next) || occupied.Contains(next) ||
                        map.IsDiagonalFootStep(current, next) &&
                        (occupied.Contains(new GridPoint(next.X, current.Y)) ||
                         occupied.Contains(new GridPoint(current.X, next.Y))))
                        continue;

                    var cost = checked(priority.Cost + LegalRoadStepCost(current, next));
                    if (best[index] >= 0 && best[index] <= cost)
                        continue;
                    best[index] = cost;
                    open.Enqueue(next, (cost, index));
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
    private sealed record BuildingPlacementTiles(HashSet<GridPoint> Occupied, HashSet<GridPoint> Market, HashSet<GridPoint> Entrances,
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
                return new(occupied, market, [], $"Placed building '{placed.InstanceId}' references an unavailable definition.");
            }

            foreach (var existingPoint in WorldContentSimulationRules.Footprint(existingDefinition, placed))
            {
                occupied.Add(existingPoint);
            }
        }

        occupied.UnionWith(worldSimulation.Buildings.Where(building => Port(building.InstanceId) is not null)
            .Select(PortGeometryFor).SelectMany(geometry => geometry.DockingTiles));
        return new(occupied, market, ground.BuildingEntrances().ToHashSet());
    }

    private bool CanPlaceBuilding(
        BuildingDefinition definition,
        GridPoint position,
        out string failure,
        out GridPoint? entrance,
        string? townProjectId = null,
        BuildingPlacementWorld? world = null,
        bool mayFaceRoad = false,
        GridPoint? requiredEntrance = null)
    {
        entrance = null;
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
        if (footprint.Any(tiles.Entrances.Contains))
        {
            failure = "The building would cover another building's doorway.";
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

        entrance = BuildingMovementRules.FindEntrance(map, definition, position, mayFaceRoad,
            tiles.Occupied, roadTiles, required: requiredEntrance);
        if (entrance is null)
        {
            failure = "The building needs a clear doorway on buildable ground.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private void RunInventoryCommand(InventorySystem.Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        society.Apply(checkpoint => InventorySystem.Execute(InventoryExecutionWorld, checkpoint, command));
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

    private void CompleteRecipeInventory(WorldProductionJob job, RecipeDefinition recipe, ToolWorkPlan? knifePlan, long targetTick)
    {
        var productionBuilding = worldSimulation.Buildings
            .FirstOrDefault(building => building.InstanceId == job.BuildingInstanceId);
        var productionOwner = job.OwnerId ?? throw new InvalidDataException("A production job has no recorded owner.");
        RunInventoryCommand(InventorySystem.CompleteRecipeInventory(InventoryRuntimeWorld, job, recipe, productionOwner, targetTick, productionBuilding, knifePlan));
    }
}
