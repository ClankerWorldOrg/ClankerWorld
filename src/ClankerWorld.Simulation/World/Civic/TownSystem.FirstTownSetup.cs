using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal FirstTownLayout AcceptFirstTownLayout(ITownWorld world, GridPoint roughSite)
    {
        if (!world.HasGeneratedGeography || founderSetup is not { Started: false, FounderIds.Count: 0 } ||
            !world.Society.IsPaused || world.WorldTick != 0 ||
            world.WorldSimulation.ProductionJobs.Count != 0 || (world.WorldSimulation.CropBuilds?.Count ?? 0) != 0 ||
            (world.WorldSimulation.BuildingExpansions?.Count ?? 0) != 0)
            throw new InvalidOperationException("Choose the first Town layout during paused setup before placing founders.");
        var existing = world.WorldSimulation.Buildings;
        if (world.Bridges.Count != 0)
            throw new InvalidOperationException("Other building work prevents replacing the initial layout.");
        var plan = FirstTownLayoutPlanner.Plan(world.Map, roughSite)
            ?? throw new ArgumentException("No connected five-building layout fits near this rough site.", nameof(roughSite));
        var definitions = world.WorldContent.BuildingDefinitions;
        if (plan.Buildings.Any(building => !definitions.ContainsKey(building.DefinitionId)))
            throw new InvalidOperationException("The initial building definitions are not active.");

        var placed = plan.Buildings.Select(building => new PlacedBuilding(
            "first-town-" + building.Role, building.DefinitionId, building.Position, 0,
            TownBorderRules.FirstTownId,
            // Each starting household holds one productive building; the
            // Warehouse stays communal Town property.
            building.Role switch
            {
                "house-a" or "farmhouse" => WorldRuntime.CivicSharedRules.HouseholdId,
                "house-b" or "blacksmith" => WorldRuntime.CivicSharedRules.SecondHouseholdId,
                _ => null,
            }, building.Entrance)).OrderBy(building => building.InstanceId, StringComparer.Ordinal).ToArray();
        // Removing an empty building can leave only part of the starter
        // layout. Only its known identities and unexpanded definitions may
        // be reset; Redo also restores any reassigned starter's initial owner.
        if (existing.Any(building => building.PlacedTick != 0 || building.Footprint is not null ||
            !placed.Any(starter => starter.InstanceId == building.InstanceId &&
                starter.DefinitionId == building.DefinitionId && starter.TownId == building.TownId)))
            throw new InvalidOperationException("Other building work prevents replacing the initial layout.");
        var town = TownBorderRules.CreateFirstTown(world.Map, originSite: roughSite);
        town = town with
        {
            AssignedBuildingIds = placed.Select(building => building.InstanceId).Order(StringComparer.Ordinal).ToArray(),
            FirstBuildingCompletedTick = 0,
            BorderTiles = TownBorderRules.Expand(world.Map, town, placed
                .SelectMany(building => WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building))
                .Concat(plan.RoadTiles)),
        };
        var starterInventory = PrepareFirstTownStock(world.Society.Inventory);
        var firstTownWasUnplaced = towns.Count == 0;
        world.ReplaceFirstTownBuildings(placed);
        RestoreTowns([town]);
        townLandTitles = TownLandRightsRules.InitialTitles(world.Map, town, world.WorldTick).ToList();
        householdLandUseRights = TownLandRightsRules.InitialUseRights(world.Map, town.Id,
            placed.Where(building => building.HouseholdId is not null)
                .GroupBy(building => building.HouseholdId!, StringComparer.Ordinal)
                .Select(group => (group.Key, (IEnumerable<GridPoint>)group.SelectMany(building =>
                    WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)))),
            world.WorldTick).ToList();
        householdLandUseRequests = [];
        world.RoadTiles = plan.RoadTiles.ToHashSet();
        world.RunInventoryCommand(InventorySystem.AcceptFirstTownLayout(starterInventory));
        if (firstTownWasUnplaced) world.AppendEvent("town_founding_started", TownBorderRules.FirstTownId);
        world.AppendEvent(existing.Count == 0 ? "first_town_layout_accepted" : "first_town_layout_redone",
            $"{roughSite.X},{roughSite.Y}:buildings:{placed.Length}:roads:{world.RoadTiles.Count}");
        return plan;
    }

    internal static InventoryCheckpoint PrepareFirstTownStock(InventoryCheckpoint source)
    {
        const string firstHouse = "first-town-house-a";
        const string secondHouse = "first-town-house-b";
        const string warehouse = "first-town-warehouse";
        var foodLocations = new Dictionary<string, (string OwnerId, string BuildingId)>(StringComparer.Ordinal)
        {
            [WorldRuntime.CivicSharedRules.FoodLotId] = (WorldRuntime.CivicSharedRules.HouseholdId, firstHouse),
            ["food:second"] = (WorldRuntime.CivicSharedRules.SecondHouseholdId, secondHouse),
        };
        foreach (var (lotId, location) in foodLocations)
        {
            if (!source.Lots.Any(lot => lot.Id == lotId && lot.ItemKind == "food" &&
                    lot.OwnerId == location.OwnerId && lot.Quantity > 0 &&
                    lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0))
                throw new InvalidOperationException("The starting households need usable food before a Town site can be accepted.");
        }

        // Each starting agent's garment is kept in their household's House,
        // so nobody is cold while the first Tailor Shop is built.
        var garmentLocations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clothing:first"] = firstHouse,
            ["clothing:second"] = secondHouse,
        };
        var stock = InventoryStore.PrepareLots(source, source.Lots.Select(lot => foodLocations.TryGetValue(lot.Id, out var location)
                ? InventoryStore.PrepareStoredStock(lot, location.BuildingId)
                : garmentLocations.TryGetValue(lot.Id, out var house) && lot.ItemKind == "clothing"
                    ? InventoryStore.PrepareStoredStock(lot, house)
                    : lot).ToArray());
        if (!stock.Lots.Any(lot => lot.Id == "first-town-wooden-axe"))
            stock = InventoryAuthority.AddLot(stock, "first-town-wooden-axe", "wooden_axe",
                TownBorderRules.FirstTownId, 1, storageBuildingId: warehouse);
        if (!stock.Lots.Any(lot => lot.Id == "first-town-wooden-pickaxe"))
            stock = InventoryAuthority.AddLot(stock, "first-town-wooden-pickaxe", "wooden_pickaxe",
                TownBorderRules.FirstTownId, 1, storageBuildingId: warehouse);
        if (!stock.Lots.Any(lot => lot.Id == "first-town-wooden-hoe"))
            stock = InventoryAuthority.AddLot(stock, "first-town-wooden-hoe", "wooden_hoe",
                TownBorderRules.FirstTownId, 1, storageBuildingId: warehouse);
        return stock;
    }

}
