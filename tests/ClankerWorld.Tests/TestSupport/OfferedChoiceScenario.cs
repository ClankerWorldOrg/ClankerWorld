using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

/// <summary>Controlled, strictly valid stock and paid assets on an unchanged generated map.</summary>
internal static class OfferedChoiceScenario
{
    internal static WorldRuntimeState Prepare(WorldRuntimeState state, int seed)
    {
        var marketDefinition = MarketContent.Hall2x2();
        var site = FreeSite(state, MarketContent.SiteTiles);
        state = PaidProject(state, new("Marché 🌿", marketDefinition.CanonicalId, site,
            MarketContent.HallEntrance(site), marketDefinition.BuildCosts), out var marketProject);
        var marketId = MarketContent.MarketId(marketProject.Id);
        var stalls = MarketContent.StarterSlotIndexes.Select(slot => new MarketStallState(
            MarketContent.StarterStallBuildingId(marketProject.Id, slot), slot, marketProject.Id, state.Society.Society.WorldTick)).ToArray();
        var stallBuildings = stalls.Select(stall => new PlacedBuilding(stall.BuildingId,
            MarketContent.Stall1x1().CanonicalId, MarketContent.StallSite(site, stall.SlotIndex),
            state.Society.Society.WorldTick, TownBorderRules.FirstTownId, Entrance: MarketContent.StallEntrance(site, stall.SlotIndex))).ToArray();
        state = WithBuildings(state, stallBuildings);
        var seller = NormalPathWorld.Founder1;
        var home = state.Society.Society.GetAgent(seller).HouseholdId!;
        var entrance = MarketContent.StallEntrance(site, stalls[0].SlotIndex);
        var market = new TownMarketState(marketId, marketProject.Id, marketProject.CompletedBuildingId!, site, stalls,
            [new("sweep-stall-borrowing", stalls[0].BuildingId, seller, state.Society.Society.WorldTick) { SellerHouseholdId = home }], []);
        state = state with
        {
            Towns = state.Towns!.Select(town => town with { Markets = town.Markets.Append(market).ToArray() }).ToArray(),
            Agents = state.Agents.Select(person => person.AgentId == seller ? person with { Position = entrance } : person).ToArray(),
        };
        var inventory = state.Society.Society.Inventory;
        var stock = DerivedId.Create("sweep-stock:" + new string('s', DerivedId.MaximumLength - "sweep-stock:".Length));
        inventory = InventoryAuthority.AddLot(inventory, stock, "wood", home, 6, groundPosition: new(entrance.X, entrance.Y));
        inventory = InventoryAuthority.Transfer(inventory, "sweep-stock-split", home, seller, stock, 2, "partial collection");
        inventory = InventoryAuthority.AddLot(inventory, "sweep-broken-pot", "storage_pot", home, 1,
            groundPosition: new(entrance.X, entrance.Y));
        inventory = InventoryAuthority.AddLot(inventory, "sweep-pot-grain", "grain", home, 2, containerLotId: "sweep-broken-pot");
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == "sweep-broken-pot" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, "sweep-full-load", "stone", NormalPathWorld.Founder2, 8);
        state = WithInventory(state, inventory);
        state = Rider(state);
        state = Passenger(state, seed);
        var swimming = state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => SwimmingRules.IsSwimmingWater(state.Map, point) && !state.Agents.Any(person => person.Position == point))
            .OrderBy(point => state.Map.FootDistance(entrance, point)).First();
        state = state with { Agents = state.Agents.Select(person => person.AgentId == NormalPathWorld.Founder2 ? person with { Position = swimming } : person).ToArray() };
        // Keep all four adult choice queues due, and make needs meaningful without starvation.
        state = state with
        {
            Agents = state.Agents.Select(person => person with
            { FullnessBasisPoints = person.AgentId == NormalPathWorld.Founder4 ? 5_000 : 9_000, LastDecisionContext = null }).ToArray(),
        };
        return state;
    }

    private static WorldRuntimeState Rider(WorldRuntimeState state)
    {
        var actor = NormalPathWorld.Founder3;
        var home = state.Society.Society.GetAgent(actor).HouseholdId!;
        var definition = state.WorldContent!.Buildings.First(item => item.Tags.Contains(AnimalContent.YardTag));
        var site = FreeSite(state, point => WorldContentSimulationRules.Footprint(definition, point));
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definition.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, DerivedId.Create("sweep-yard-input:" + cost.ResourceId),
                cost.ResourceId, home, cost.Amount);
        using var placing = WorldRuntime.Restore(WithInventory(state, inventory), _ => new SweepChoiceProvider());
        var result = placing.PlaceBuilding("sweep-paid-yard", definition.CanonicalId, site, home);
        Assert.True(result.Applied, result.Failure);
        state = placing.ExportState();
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "sweep-saddle", "saddle", home, 1,
            groundPosition: new(site.X, site.Y));
        inventory = InventoryAuthority.Reserve(inventory, "sweep-saddle-hold", home, "sweep-saddle", 1,
            "animal-saddle:sweep-horse", long.MaxValue, isExclusive: true);
        var day = state.WorldSystems!.Config.TicksPerDay;
        var horse = new AnimalState("sweep-horse", "Éclair 🐎", "horse", "female",
            -(long)AnimalRules.Definition("horse").AdultDays * day, site, DerivedId.Create("household:" + home), home,
            "sweep-paid-yard", CareUntilTick: state.Society.Society.WorldTick + day, RiderId: actor,
            SaddleLotId: "sweep-saddle")
        { SaddleReservationId = Identity.Parse("sweep-saddle-hold") };
        return WithInventory(state, inventory) with
        {
            AnimalWorld = new(true, [horse], []),
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = site } : person).ToArray(),
        };
    }

    private static WorldRuntimeState Passenger(WorldRuntimeState state, int seed)
    {
        var occupied = Occupied(state);
        var ports = (from point in state.Map.Tiles.Select(tile => tile.Position)
                     from definition in PortContent.Definitions
                     where PortNavigationRules.Fits(state.Map, definition, point, occupied, out _)
                     select (Point: point, Definition: definition)).ToArray();
        Assert.True(ports.Length >= 2, $"seed={seed}: no pair of clear Port sites");
        var first = ports[seed % ports.Length];
        var geometry = PortNavigationRules.Geometry(state.Map, first.Definition, first.Point);
        state = PaidProject(state, new("Port départ ⛵", first.Definition.CanonicalId, first.Point,
            geometry.ApproachTiles.First(state.Map.IsBuildable), first.Definition.BuildCosts), out var origin);
        occupied = Occupied(state);
        var second = ports.First(port => PortNavigationRules.Fits(state.Map, port.Definition, port.Point, occupied, out _) &&
            PortNavigationRules.WaterRoute(state.Map, geometry.DockingTiles[0],
                PortNavigationRules.Geometry(state.Map, port.Definition, port.Point).DockingTiles, occupied).Count > 1);
        var secondGeometry = PortNavigationRules.Geometry(state.Map, second.Definition, second.Point);
        state = PaidProject(state, new("Port arrivée 🧭", second.Definition.CanonicalId, second.Point,
            secondGeometry.ApproachTiles.First(state.Map.IsBuildable), second.Definition.BuildCosts), out var destination);
        state = PaidProject(state, new("Bateau ⛵", first.Definition.CanonicalId, first.Point,
            geometry.ApproachTiles.First(state.Map.IsBuildable), PortContent.BoatCosts)
        { BoatPortId = origin.CompletedBuildingId }, out var paidBoat);
        var path = PortNavigationRules.WaterRoute(state.Map, geometry.DockingTiles[0], secondGeometry.DockingTiles, Occupied(state));
        var requestId = DerivedId.Numbered("boat-request:", 1);
        var actor = NormalPathWorld.Founder4;
        var tick = state.Society.Society.WorldTick;
        var journey = new BoatJourney(requestId, actor, origin.CompletedBuildingId!, destination.CompletedBuildingId!,
            path[^1], path, 0, tick, tick + 2);
        var boat = new BoatState(paidBoat.CompletedBoatId!, TownBorderRules.FirstTownId, paidBoat.Id, path[0], Journey: journey);
        var request = new BoatTripRequest(requestId, 1, actor, TownBorderRules.FirstTownId, journey.OriginPortId,
            journey.DestinationPortId, tick, "underway", boat.Id);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "sweep-passenger-food", "food", actor, 2);
        return WithInventory(state, inventory) with
        {
            BoatTransport = new(1, [boat], [request]),
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = path[0] } : person).ToArray(),
        };
    }

    private static WorldRuntimeState PaidProject(WorldRuntimeState state, TownProjectPayload plan, out TownProject project)
    {
        var town = state.Towns![0];
        var tick = state.Society.Society.WorldTick;
        var council = TownCouncilRules.SubmitProposal(town.Council!, town.Id, NormalPathWorld.Founder1,
            "project", null, "", "sweep", town.Council!.Members, tick, state.WorldSystems!.Config.TicksPerDay, project: plan);
        var proposal = council.Proposals[^1];
        foreach (var voter in proposal.Voters.Take(proposal.RequiredYes))
            council = TownCouncilRules.VoteProposal(council, proposal.Id, voter, true, tick);
        proposal = council.Proposals.Single(item => item.Id == proposal.Id);
        var id = TownProjectRules.ProjectId(proposal);
        var inventory = state.Society.Society.Inventory;
        var deliveries = new List<TownProjectDelivery>();
        foreach (var cost in plan.Budget)
        {
            var lot = DerivedId.Create($"sweep-paid-input:{id}:{cost.ResourceId}");
            var delivery = TownProjectRules.DeliveryId(id, NormalPathWorld.Founder1, lot, tick, deliveries.Count);
            var reservation = TownRecordIds.DeliveryInput(delivery);
            inventory = InventoryAuthority.AddLot(inventory, lot, cost.ResourceId, town.Id, cost.Amount,
                groundPosition: new(TownProjectRules.WorkSite(plan).X, TownProjectRules.WorkSite(plan).Y));
            inventory = InventoryAuthority.Reserve(inventory, reservation, town.Id, lot, cost.Amount,
                TownProjectRules.ReservationPurpose(id), long.MaxValue);
            inventory = InventoryAuthority.ConsumeReservation(inventory, reservation);
            deliveries.Add(new(delivery, NormalPathWorld.Founder1, lot, lot, cost.ResourceId, cost.Amount, tick, tick, Identity.Parse(reservation)));
        }
        var buildingId = TownProjectRules.BuildingId(id);
        project = new(id, proposal.Id, plan, tick, "completed", TownProjectRules.RequiredWork(plan), tick, deliveries,
            CompletedBuildingId: plan.BoatPortId is null ? buildingId : null)
        { CompletedBoatId = plan.BoatPortId is not null ? buildingId : null };
        var completed = project;
        state = WithInventory(state, inventory) with { Towns = [town with { Council = council, Projects = town.Projects.Append(completed).ToArray() }] };
        return plan.BoatPortId is not null ? state : WithBuildings(state,
            [new(buildingId, plan.DefinitionId, plan.Site, tick, town.Id, Entrance: plan.Entrance)]);
    }

    private static WorldRuntimeState WithBuildings(WorldRuntimeState state, PlacedBuilding[] buildings) => state with
    {
        WorldSimulation = state.WorldSimulation! with { Buildings = state.WorldSimulation.Buildings.Concat(buildings).OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray() },
        Towns = state.Towns!.Select(town => town with
        {
            AssignedBuildingIds = town.AssignedBuildingIds.Concat(buildings.Select(item => item.InstanceId)).Order(StringComparer.Ordinal).ToArray(),
            BorderTiles = TownBorderRules.Expand(state.Map, town, buildings.SelectMany(building =>
                WorldContentSimulationRules.Footprint(state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building))),
        }).ToArray(),
    };

    private static HashSet<GridPoint> Occupied(WorldRuntimeState state) => state.WorldSimulation!.Buildings.SelectMany(building =>
        WorldContentSimulationRules.Footprint(state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building))
        .Concat(state.Map.Resources.Select(item => item.Position)).Concat(state.Map.MapObjects.Select(item => item.Position))
        .Concat(state.Agents.Select(person => person.Position)).Concat(state.RoadTiles ?? [])
        .Concat((state.Fields ?? []).Select(field => field.Position))
        .Concat(state.Towns!.SelectMany(town => town.Markets).SelectMany(market => MarketContent.SiteTiles(market.Site)))
        .ToHashSet();

    private static GridPoint FreeSite(WorldRuntimeState state, Func<GridPoint, IEnumerable<GridPoint>> footprint)
    {
        var occupied = Occupied(state);
        var anchor = state.Towns![0].OriginSite!.Value;
        return state.Map.Tiles.Select(tile => tile.Position).OrderBy(point => state.Map.FootDistance(anchor, point))
            .First(point => footprint(point).All(tile => state.Map.Contains(tile) && state.Map.IsBuildable(tile) && !occupied.Contains(tile)));
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
}
