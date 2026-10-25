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

    internal static int PlantingSeedQuantity(IFarmingWorld world, InventoryLot lot) => world.PlantingSeedQuantity(lot);

    internal static InventoryLot? CarriedPlantingSeed(IFarmingWorld world, string actor, string kind) =>
        world.FindGoods(new(GoodsUse.TreePlanting, actor, GoodsOwners.One(actor), GoodsKinds.One(kind)))
            .Matches.Select(match => match.Lot).OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

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
        if (orchardAvailable && site is { } orchardSite)
            AddTreeCandidate(world, candidates, actor, TreeChoice.Orchard, "Plant an orchard seed on open ground.", 31, orchardSite);
        if (!treeAvailable) return;
        if (ReplantableTree(world, actor, state.Position) is { } tree)
            AddTreeCandidate(world, candidates, actor, TreeChoice.Replant, "Carry a tree seed to a stump and replant it.", 32, tree.Position, tree);
        if (site is { } plantingSite)
            AddTreeCandidate(world, candidates, actor, TreeChoice.Plant, "Plant a tree seed on open ground outside the Town.", 34, plantingSite);
    }

    private static void AddTreeCandidate(IFarmingWorld world, List<CognitionCandidate> candidates, string actor,
        TreeChoice choice, string description, int priority, GridPoint site, MapResource? stump = null)
    {
        if (PlanTreeAction(world, actor, choice, site, stump) is { } plan && CheckTreePlan(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, priority, stump?.Id) { ActionPlan = plan });
    }

    internal static MapResource? ReplantableTree(IFarmingWorld world, string actor, GridPoint origin)
    {
        var depleted = world.Environment.Ecology.Resources.Where(ecology =>
                ecology.Quantity == 0 && !ecology.IsPlanted)
            .Select(ecology => ecology.Id).ToHashSet(StringComparer.Ordinal);
        return world.Map.Resources
            .Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) && resource.IsRenewable &&
                depleted.Contains(resource.Id) && world.Reach(actor, resource.Position, ReachPurpose.TreePlanting, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0)
            .OrderBy(resource => world.Map.FootDistance(origin, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(resource => world.IsWithinInteractionRange(origin, resource.Position, 1) ||
                world.Reach(actor, resource.Position, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }

    private static void CompleteTreeReplanting(IFarmingWorld world, string actor, MapResource tree, InventoryLot seed)
    {
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
        world.AppendEvent(new TreeReplantedEventPayload(actor, tree.Id, tree.TreeKind));
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
                world.RunInventoryCommand(InventorySystem.PlantTreeCore(reservation));
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
        world.AppendEvent(new TreePlantedEventPayload(planterId, tree.Id, species));
        return TreePlantingResult.Success(tree.Id, species);
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
                    PlantingSiteRefusal(world, point, obstacles) is not null || world.Reach(actor, point, ReachPurpose.TreePlanting, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count == 0)
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
                world.Reach(actor, point.Value, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0);
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

    internal static ActorCapabilityPolicy TreePolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal static TreeActionPlan? PlanTreeAction(IFarmingWorld world, string actor, TreeChoice choice,
        GridPoint? selectedSite = null, MapResource? selectedStump = null, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        if (!ActorCapabilities.Check(world.CapabilitiesFor(actor), TreeActionPlan.Candidate(choice), capabilityPolicy ?? TreePolicy).Allowed) return null;
        var origin = world.Agents[actor].Position;
        var stump = choice == TreeChoice.Replant ? selectedStump ?? ReplantableTree(world, actor, origin) : null;
        var position = choice == TreeChoice.Replant ? stump?.Position : selectedSite ?? PlantingSite(world, actor, origin);
        if (position is not { } site) return null;
        var kind = choice == TreeChoice.Orchard ? TreeGrowthRules.OrchardSeedItem : TreeGrowthRules.TreeSeedItem;
        var seed = CarriedPlantingSeed(world, actor, kind) ?? world.SharedItem(kind, actor, site, 1);
        if (seed is null) return null;
        var claim = seed.OwnerId == actor ? OrchardClaim(world, seed) : null;
        return new(actor, choice, site, stump?.TreeKind ?? (choice == TreeChoice.Orchard ? TreeGrowthRules.Orchard : PlantingSpecies(world, site)),
            stump?.Id, new(seed.Id, seed.OwnerId, kind, seed.OwnerId == actor ? origin : world.HouseholdStockPosition(seed),
                seed.OwnerId == actor ? 0 : WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(seed)), claim,
            world.Reach(actor, site, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable));
    }

    private static PlannedOrchardClaim? OrchardClaim(IFarmingWorld world, InventoryLot seed)
    {
        var claim = WorldRuntime.OrchardPlantingClaim(InventoryIndex.For(world.Society.Inventory), seed, world.WorldTick);
        return claim is null ? null : new(claim.Id, claim.Quantity);
    }

    internal static ActionCheck CheckTreePlan(IFarmingWorld world, TreeActionPlan plan, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, capabilityPolicy ?? TreePolicy);
        if (!capability.Allowed) return capability;
        var seed = plan.Seed;
        var use = seed.OwnerId == plan.Actor ? GoodsUse.TreePlanting : GoodsUse.Collect;
        if (world.RecheckGoods(new(use, plan.Actor, GoodsOwners.One(seed.OwnerId), GoodsKinds.One(seed.ItemKind)), seed.StockId) is not { Lot: var lot } ||
            seed.OwnerId == plan.Actor && OrchardClaim(world, lot) != plan.Claim ||
            seed.OwnerId != plan.Actor && (lot.ContainerLotId is not null || lot.CarrierId is not null ||
                world.HouseholdStockPosition(lot) != seed.Position || WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(lot) != seed.InteractionRange ||
                world.PickupCarryCapacity(plan.Actor, lot, plan.Position, 1) < 1))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected planting seed is no longer available");
        if (plan.Choice == TreeChoice.Replant)
        {
            var stump = world.Map.Resources.SingleOrDefault(item => item.Id == plan.StumpId);
            var growth = world.Environment.Ecology.Resources.SingleOrDefault(item => item.Id == plan.StumpId);
            if (stump is null || stump.Position != plan.Position || stump.TreeKind != plan.Species || !stump.IsRenewable ||
                growth is null || growth.Quantity != 0 || growth.IsPlanted)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected stump no longer needs replanting");
        }
        else if (world.Towns.Any(town => (town.BorderTiles ?? []).Contains(plan.Position)) ||
            PlantingSiteRefusal(world, plan.Position, PlantingObstacles(world)) is not null || TreeAreaFull(world, plan.Position))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected planting ground is no longer available");
        return world.Reach(plan.Actor, plan.Position, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected planting site is out of reach now");
    }

    internal static void ApplyTreePlan(IFarmingWorld world, TreeActionPlan plan)
    {
        if (plan.Seed.OwnerId != plan.Actor)
        {
            world.PickUpEquipment(plan.Actor, plan.Seed);
            return;
        }
        var state = world.Agents[plan.Actor];
        if (!world.IsWithinInteractionRange(state.Position, plan.Position, 1))
        {
            world.MoveToward(plan.Actor, state, plan.Position, plan.Choice == TreeChoice.Replant ? "replant_tree" : "plant_tree", 1);
            return;
        }
        if (plan.Choice == TreeChoice.Replant)
            CompleteTreeReplanting(world, plan.Actor, world.Map.GetResource(plan.StumpId!), world.Society.Inventory.GetLot(plan.Seed.StockId));
        else PlantTreeCore(world, plan.Actor, plan.Species, plan.Seed.StockId, plan.Position);
    }
}
