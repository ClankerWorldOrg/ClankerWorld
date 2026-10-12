using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    // How far from an agent the built-in choice looks for open planting ground.
    private const int PlantingSearchRadius = 10;
    private const string OrchardReplantingPrefix = "orchard-replant:";

    internal static string OrchardReplantingReservationId(string lotId) => DerivedId.Create(OrchardReplantingPrefix + lotId);

    internal static int PlantingSeedQuantity(IFarmingWorld world, InventoryLot lot) => lot.FreshnessBasisPoints <= 0 || lot.ConditionBasisPoints <= 0 ? 0 : world.AvailableLotQuantity(lot) +
        world.Society.Inventory.Reservations.Where(reservation => reservation.LotId == lot.Id &&
            reservation.OwnerId == lot.OwnerId && reservation.State is (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) &&
            reservation.ExpiryTick >= world.WorldTick && reservation.Id == OrchardReplantingReservationId(lot.Id))
        .Sum(reservation => reservation.Quantity);

    internal static InventoryLot? CarriedPlantingSeed(IFarmingWorld world, string actor, string kind) => world.Society.Inventory.Lots
        .Where(lot => lot.OwnerId == actor && lot.ItemKind == kind && lot.ContainerLotId is null && lot.GroundPosition is null &&
            lot.StorageBuildingId is null && lot.DeliveryBuildingId is null && PlantingSeedQuantity(world, lot) > 0)
        .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static bool IsPlantedTree(MapResource resource) =>
        resource.Id.StartsWith(TreeGrowthRules.PlantedTreeIdPrefix, StringComparison.Ordinal);

    internal static void AddTreePlantingCandidates(IFarmingWorld world, List<CognitionCandidate> candidates, string actor,
        AgentState state)
    {
        var carried = world.HasCarriedOwnItem(actor, TreeGrowthRules.TreeSeedItem);
        var orchardCarried = CarriedPlantingSeed(world, actor, TreeGrowthRules.OrchardSeedItem) is not null;
        var collecting = world.FreeCarryCapacity(actor) > 0;
        var treeAvailable = carried || collecting && world.SharedItem(TreeGrowthRules.TreeSeedItem, actor) is not null;
        var orchardAvailable = orchardCarried || collecting && world.SharedItem(TreeGrowthRules.OrchardSeedItem, actor) is not null;
        // Keep agents without seeds out of the planting-site and stump scans.
        if (!treeAvailable && !orchardAvailable) return;
        var site = PlantingSite(world, actor, state.Position);
        if (orchardAvailable && site is { } orchardSite && (orchardCarried ||
             world.SharedItem(TreeGrowthRules.OrchardSeedItem, actor, orchardSite, 1) is not null))
            candidates.Add(new CognitionCandidate("plant_orchard", "Plant an orchard seed on open ground.", 31));
        if (!treeAvailable) return;
        if (ReplantableTree(world, actor, state.Position) is { } tree && (carried ||
            world.SharedItem(TreeGrowthRules.TreeSeedItem, actor, tree.Position, 1) is not null))
            candidates.Add(new CognitionCandidate("replant_tree",
                "Carry a tree seed to a stump and replant it.", 32, tree.Id));
        if (site is { } plantingSite && (carried ||
            world.SharedItem(TreeGrowthRules.TreeSeedItem, actor, plantingSite, 1) is not null))
            candidates.Add(new CognitionCandidate("plant_tree",
                "Plant a tree seed on open ground outside the Town.", 34));
    }

    internal static MapResource? ReplantableTree(IFarmingWorld world, string actor, GridPoint origin)
    {
        var depleted = world.Environment.Ecology.Resources.Where(ecology =>
                ecology.Quantity == 0 && !ecology.IsPlanted)
            .Select(ecology => ecology.Id).ToHashSet(StringComparer.Ordinal);
        return world.Map.Resources
            .Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) && resource.IsRenewable &&
                depleted.Contains(resource.Id) && world.CanReachByFootOrSwimming(actor, origin, resource.Position))
            .OrderBy(resource => world.Map.FootDistance(origin, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(resource => world.IsWithinInteractionRange(origin, resource.Position, 1) ||
                world.FindUnoccupiedRoute(actor, origin, resource.Position, 1).Count > 0);
    }

    internal static void ReplantTree(IFarmingWorld world, string actor, AgentState state)
    {
        var tree = ReplantableTree(world, actor, state.Position);
        if (tree is null) return;
        if (!world.HasCarriedOwnItem(actor, TreeGrowthRules.TreeSeedItem))
        {
            world.CollectEquipment(actor, state, TreeGrowthRules.TreeSeedItem, tree.Position, 1);
            return;
        }
        if (!world.IsWithinInteractionRange(state.Position, tree.Position, 1))
        {
            world.MoveToward(actor, state, tree.Position, "replant_tree", 1);
            return;
        }

        var seed = CarriedPlantingSeed(world, actor, TreeGrowthRules.TreeSeedItem);
        if (seed is null) return;
        var ecology = world.Environment.Ecology.GetResource(tree.Id);
        if (ecology.Quantity != 0 || ecology.IsPlanted) return;
        world.ApplySocietyTransition(checkpoint => SocietyRules.ConsumeInventory(checkpoint,
            actor, seed.Id, 1, "tree_replanting"));
        var day = WorldCalendarRules.FromTick(world.WorldTick, world.Environment.Config).DayIndex;
        var planted = ecology with
        {
            State = EcologyResourceState.Regenerating,
            IsPlanted = true,
            NextRegenerationDay = day + TreeGrowthRules.SaplingGrowthDays,
        };
        world.ReplaceEcologyResource(tree.Id, planted);
        world.SyncEcologyResourceStates();
        world.AppendEvent("tree_replanted", $"{actor}:{tree.Id}:{tree.TreeKind}");
    }

    internal static TreePlantingResult PlantTreeCore(IFarmingWorld world, string planterId, string species, string seedLotId, GridPoint destination)
    {
        if (!TreeGrowthRules.IsPlantableSpecies(species))
            return TreePlantingResult.Refused(TreePlantingRefusal.UnsupportedSpecies);
        if (!world.Agents.TryGetValue(planterId, out var planter) || !world.IsAdult(planterId))
            return TreePlantingResult.Refused(TreePlantingRefusal.PlanterUnavailable);
        var seed = world.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == seedLotId);
        if (seed is null)
            return TreePlantingResult.Refused(TreePlantingRefusal.NoSeedLeft);
        if (seed.ItemKind != TreeGrowthRules.SeedItem(species))
            return TreePlantingResult.Refused(TreePlantingRefusal.NotATreeSeed);
        if (seed.OwnerId != planterId || seed.GroundPosition is not null || seed.StorageBuildingId is not null || seed.DeliveryBuildingId is not null)
            return TreePlantingResult.Refused(TreePlantingRefusal.SeedNotOwned);
        if (PlantingSeedQuantity(world, seed) < 1)
            return TreePlantingResult.Refused(TreePlantingRefusal.NoSeedLeft);
        var obstacles = PlantingObstacles(world);
        if (PlantingSiteRefusal(world, destination, obstacles) is { } site)
            return TreePlantingResult.Refused(site);
        if (!world.IsWithinInteractionRange(planter.Position, destination, 1))
            return TreePlantingResult.Refused(TreePlantingRefusal.TooFar);
        if (TreeAreaFull(world, destination))
            return TreePlantingResult.Refused(TreePlantingRefusal.AreaFull);

        if (species == TreeGrowthRules.Orchard)
            foreach (var reservation in world.Society.Inventory.Reservations.Where(item => item.LotId == seed.Id &&
                         item.State is (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) && item.Id == OrchardReplantingReservationId(seed.Id)).ToArray())
                world.ApplyInventoryTransition(inventory => InventoryAuthority.ReleaseReservation(inventory, reservation.Id));
        world.ApplySocietyTransition(checkpoint => SocietyRules.ConsumeInventory(checkpoint,
            planterId, seed.Id, 1, "tree_planting"));
        var tree = new MapResource(TreeGrowthRules.PlantedTreeId(destination), TreeGrowthRules.ResourceKind(species), destination, true,
            species);
        var plantedMap = world.Map with
        {
            Resources = world.Map.Resources.Append(tree).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
        };
        world.SetMap(plantedMap with { ManifestDigest = MapManifestCodec.Digest(plantedMap) });
        var day = WorldCalendarRules.FromTick(world.WorldTick, world.Environment.Config).DayIndex;
        var chunk = world.Environment.Chunks.Single(item =>
            item.Coordinate == ChunkRules.ToChunkCoordinate(destination, item.ChunkSize));
        world.PlantTree(tree, day, chunk);
        world.SyncEcologyResourceStates();
        world.RecordKnowledgeFact(planterId, destination);
        world.AppendEvent("tree_planted", $"{planterId}:{tree.Id}:{species}");
        return TreePlantingResult.Success(tree.Id, species);
    }

    internal static void PlantTreeNearby(IFarmingWorld world, string actor, AgentState state, bool orchard = false)
    {
        if (PlantingSite(world, actor, state.Position) is not { } site) return;
        var seedKind = orchard ? TreeGrowthRules.OrchardSeedItem : TreeGrowthRules.TreeSeedItem;
        if (CarriedPlantingSeed(world, actor, seedKind) is not { } seed)
        {
            world.CollectEquipment(actor, state, seedKind, site, 1);
            return;
        }
        if (!world.IsWithinInteractionRange(state.Position, site, 1))
        {
            world.MoveToward(actor, state, site, "plant_tree", 1);
            return;
        }
        var result = PlantTreeCore(world, actor, orchard ? TreeGrowthRules.Orchard : PlantingSpecies(world, site), seed.Id, site);
        if (!result.Planted)
            world.AppendEvent("tree_planting_refused", $"{actor}:{result.Refusal}");
    }

    /// <summary>
    /// The built-in planting choice: the nearest reachable open tile outside
    /// every Town border, so new trees do not block Town building sites.
    /// </summary>
    internal static GridPoint? PlantingSite(IFarmingWorld world, string actor, GridPoint origin)
    {
        var obstacles = PlantingObstacles(world);
        var townTiles = world.Towns.SelectMany(town => town.BorderTiles ?? []).ToHashSet();
        var sites = new List<GridPoint>();
        for (var dy = -PlantingSearchRadius; dy <= PlantingSearchRadius; dy++)
            for (var dx = -PlantingSearchRadius; dx <= PlantingSearchRadius; dx++)
            {
                var x = origin.X + dx;
                if (world.Map.WrapsEastWest) x = (x % world.Map.Width + world.Map.Width) % world.Map.Width;
                var point = new GridPoint(x, origin.Y + dy);
                if (!world.Map.Contains(point) || townTiles.Contains(point) ||
                    PlantingSiteRefusal(world, point, obstacles) is not null || !world.CanReachByFootOrSwimming(actor, origin, point))
                    continue;
                sites.Add(point);
            }
        return sites
            .OrderBy(point => world.Map.FootDistance(origin, point))
            .ThenBy(point => point.Y)
            .ThenBy(point => point.X)
            .Where(point => !TreeAreaFull(world, point))
            .Select(point => (GridPoint?)point)
            .FirstOrDefault(point => world.IsWithinInteractionRange(origin, point!.Value, 1) ||
                world.FindUnoccupiedRoute(actor, origin, point.Value, 1).Count > 0);
    }

    /// <summary>A tree seed grows into the species of the nearest wood tree, or by climate when none is near.</summary>
    internal static string PlantingSpecies(IFarmingWorld world, GridPoint site) =>
        world.Map.Resources.Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) &&
                world.Map.FootDistance(site, resource.Position) <= PlantingSearchRadius)
            .OrderBy(resource => world.Map.FootDistance(site, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .Select(resource => resource.TreeKind!)
            .FirstOrDefault() ??
        (world.Map.ClimateAt(site) is ClimateZone.Cold or ClimateZone.Polar
            ? TreeGrowthRules.Conifer : TreeGrowthRules.Broadleaf);

    internal static (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) PlantingObstacles(IFarmingWorld world)
    {
        var ground = world.Ground;
        var buildings = GroundOccupancy.Combine(ground.PlantingBuildingFootprints,
            ground.ExpansionSites, ground.ProtectedProjectSites(), ground.MarketSites);
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources, ground.Fields);
        return (buildings, occupied);
    }

    internal static TreePlantingRefusal? PlantingSiteRefusal(IFarmingWorld world, GridPoint point,
        (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) obstacles)
    {
        if (TreeGrowthRules.GroundRefusal(world.Map, point) is { } ground) return ground;
        if (obstacles.Buildings.Contains(point)) return TreePlantingRefusal.Building;
        if (world.RoadAndBridgeTiles().Contains(point)) return TreePlantingRefusal.Road;
        if (obstacles.Occupied.Contains(point)) return TreePlantingRefusal.Occupied;
        return null;
    }

    // Planted trees share the bounded per-chunk resource budget with
    // generated sites, so a crowded area cannot grow without limit.
    internal static bool TreeAreaFull(IFarmingWorld world, GridPoint point)
    {
        var config = world.Environment.Config;
        var chunk = world.Environment.Chunks.FirstOrDefault(item =>
            item.Coordinate == ChunkRules.ToChunkCoordinate(point, item.ChunkSize));
        return chunk is null || chunk.Resources.Count >= config.MaxResourcesPerChunk ||
            world.Environment.Ecology.Resources.Count >= config.MaxResourcesPerChunk * config.MaxChunkCount;
    }

    internal static void ValidatePlantedTrees(IFarmingWorld world)
    {
        var obstacles = PlantingObstacles(world);
        var ecology = world.Environment.Ecology.Resources.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var tree in world.Map.Resources.Where(IsPlantedTree))
        {
            if (!TreeGrowthRules.IsValidPlantedTree(world.Map, tree) || obstacles.Buildings.Contains(tree.Position) ||
                world.RoadAndBridgeTiles().Contains(tree.Position) ||
                world.Map.MapObjects.Any(item => item.Position == tree.Position) ||
                !ecology.TryGetValue(tree.Id, out var growth) || growth.Kind != tree.Kind ||
                growth.Position != tree.Position || !growth.IsRenewable)
                throw new InvalidDataException($"The planted tree '{tree.Id}' is invalid.");
        }
    }
}
