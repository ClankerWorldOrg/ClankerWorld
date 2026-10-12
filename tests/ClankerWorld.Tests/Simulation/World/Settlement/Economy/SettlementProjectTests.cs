using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class SettlementProjectTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task FirstHouseMaterialsAreDeliveredInLoadsAcrossReloadWithoutExceedingCarryCapacity()
    {
        using var seed = new WorldRuntime("settlement-acquisition", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 6; tick++)
            _ = await seed.AdvanceOneTickAsync();
        var initial = seed.ExportState();
        var actor = initial.Agents.First(person => initial.Society.Society.GetAgent(person.AgentId)
            .HouseholdId == "household:camp-alpha").AgentId;
        var house = seed.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var site = initial.Map.Tiles.Select(tile => tile.Position).First(point => initial.Map.IsBuildable(point) &&
            !initial.Map.MapObjects.Any(item => item.Position == point) &&
            !initial.Map.Resources.Any(item => item.Position == point) &&
            !initial.Agents.Any(person => person.Position == point));
        var inventory = initial.Society.Society.Inventory with
        {
            Lots = initial.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor && lot.ItemKind != "wood").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "first-house-load", "wood", actor, 4);
        inventory = InventoryAuthority.AddLot(inventory, "first-house-axe", "wooden_axe", actor, 1);
        initial = initial with
        {
            Society = initial.Society with { Society = initial.Society.Society with { Inventory = inventory } },
            Agents = initial.Agents.Select(person => person.AgentId == actor ? person with
            {
                HungerBasisPoints = 9_500,
                Equipment = null,
                Project = new SettlementProject(TownConstructionCandidateIds.Building(house.CanonicalId, site),
                    house.DisplayName, seed.WorldTick, "acquiring", LastTransitionTick: seed.WorldTick),
            } : person).ToArray(),
        };
        var world = WorldRuntime.Restore(initial, _ => new IdleProvider());
        try
        {
            var restartedAfterDelivery = false;
            for (var tick = 0; tick < 200 && !world.WorldSimulation.Buildings.Any(item => item.HouseholdId == "household:camp-alpha"); tick++)
            {
                _ = await world.AdvanceOneTickAsync();
                var state = world.ExportState();
                var person = state.Agents.Single(item => item.AgentId == actor);
                Assert.InRange(PersonalEquipmentRules.CarriedQuantity(state.Society.Society.Inventory, actor, person.Equipment), 0, 8);
                if (!restartedAfterDelivery && state.Events.Any(item => item.Kind == "project_material_delivered"))
                {
                    var staged = state.Society.Society.Inventory.GetLot("first-house-load");
                    var startingSite = state.Map.GetObject("storage").Position;
                    Assert.Equal("household:camp-alpha", staged.OwnerId);
                    Assert.Equal(new InventoryGroundPosition(startingSite.X, startingSite.Y), staged.GroundPosition);
                    Assert.Equal(4, staged.Quantity);
                    world.Dispose();
                    world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
                        _ => new IdleProvider());
                    restartedAfterDelivery = true;
                }
            }
            Assert.True(restartedAfterDelivery);
            Assert.Single(world.WorldSimulation.Buildings, item => item.DefinitionId == house.CanonicalId &&
                item.HouseholdId == "household:camp-alpha");
            var paid = world.Society.Inventory.Reservations.Where(item => item.Purpose.StartsWith("building:", StringComparison.Ordinal)).ToArray();
            Assert.Equal(8, paid.Sum(item => item.Quantity));
            Assert.Contains(paid, item => item.LotId == "first-house-load" && item.Quantity == 4);
            Assert.All(paid, item =>
            {
                Assert.Equal("household:camp-alpha", item.OwnerId);
                Assert.Equal(InventoryReservationState.Completed, item.State);
            });
            Assert.Equal(1, world.Society.Inventory.GetLot("first-house-axe").Quantity);
            var gathered = world.ExportState().Events.Where(item => item.Kind == "material_gathered" &&
                    item.Detail.StartsWith(actor + ":wood:", StringComparison.Ordinal))
                .Sum(item => int.Parse(item.Detail.Split(':')[2], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(4 + gathered - 8, world.Society.Inventory.Lots.Where(item => item.ItemKind == "wood").Sum(item => item.Quantity));
            world.Validate();
        }
        finally { world.Dispose(); }
    }

    [Fact]
    public async Task ExistingHouseEndsAStaleSecondHouseProjectWithoutConsumingMaterials()
    {
        using var seed = new WorldRuntime("one-house-per-household-project", _ => new IdleProvider());
        Assert.True(seed.StageStarterContent());
        for (var tick = 0; tick < 6; tick++)
            Assert.True((await seed.AdvanceOneTickAsync()).Advanced);
        var state = seed.ExportState();
        var actor = state.Society.Society.Agents.First(person =>
            person.HouseholdId == "household:camp-alpha").Id;
        var sites = state.Map.Tiles.Select(tile => tile.Position).Where(point =>
            state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point)).Take(2).ToArray();
        Assert.Equal(2, sites.Length);
        var house = seed.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        Assert.True(seed.PlaceBuilding("existing-alpha-house", house.CanonicalId, sites[0],
            "household:camp-alpha").Applied);
        state = seed.ExportState();
        var woodBefore = seed.Society.Inventory.Lots.Where(lot =>
            lot.OwnerId == "household:camp-alpha" && lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with
                {
                    HungerBasisPoints = 9_000,
                    Project = new SettlementProject(TownConstructionCandidateIds.Building(house.CanonicalId, sites[1]),
                        house.DisplayName, seed.WorldTick, "acquiring", LastTransitionTick: seed.WorldTick),
                }
                : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var project = world.Agents.Single(person => person.AgentId == actor).Project!;
        Assert.Equal("cancelled", project.Stage);
        Assert.Equal("This household already has a House.", project.Blocker);
        Assert.Single(world.WorldSimulation.Buildings, building => building.HouseholdId == "household:camp-alpha");
        Assert.Equal(woodBefore, world.Society.Inventory.Lots.Where(lot =>
            lot.OwnerId == "household:camp-alpha" && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "project_progress" &&
            item.Detail.Contains("already has a House", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CarriedProjectWoodAndHouseholdHelpAreDeliveredAtHome()
    {
        using var seed = new WorldRuntime("project-material-home", _ => new IdleProvider());
        Assert.True(seed.StageStarterContent());
        for (var tick = 0; tick < 6; tick++)
            Assert.True((await seed.AdvanceOneTickAsync()).Advanced);

        var state = seed.ExportState();
        var actor = state.Society.Society.Agents[0].Id;
        var homeSite = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point));
        var house = seed.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        var placed = seed.PlaceBuilding("project-home", house.CanonicalId, homeSite, "household:camp-alpha");
        Assert.True(placed.Applied, placed.Failure);
        state = seed.ExportState();
        var buildSite = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            point != homeSite && state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point));
        var projectBuilding = seed.WorldContent.Buildings.Single(building => building.LocalId == "workshop");
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot =>
                lot.OwnerId != "household:camp-alpha" || lot.ItemKind != "wood").ToArray(),
        };
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with
                {
                    Position = state.Map.GetObject("storage").Position,
                    HungerBasisPoints = 9_000,
                    Project = new SettlementProject(
                        TownConstructionCandidateIds.Building(projectBuilding.CanonicalId, buildSite),
                        projectBuilding.DisplayName, seed.WorldTick, "acquiring", LastTransitionTick: seed.WorldTick),
                } : person).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(inventory, "personal-project-wood", "wood", actor, 4),
                },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        for (var tick = 0; tick < 20 && !world.ExportState().Events.Any(item =>
                 item.Kind == "project_material_delivered" && item.Detail == actor + ":wood"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var delivery = world.ExportState().Events.Single(item =>
            item.Kind == "project_material_delivered" && item.Detail == actor + ":wood");
        Assert.Equal(homeSite, delivery.Position);
        Assert.Contains(world.Society.Inventory.Lots, lot => lot.OwnerId == "household:camp-alpha" &&
            lot.ItemKind == "wood" && lot.Quantity == 4 && lot.StorageBuildingId == "project-home");
        Assert.Contains(new OwnerWorldObservationStore(world).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "project-home").StoredItems!,
            item => item.Kind == "wood" && item.Quantity == 4);

        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Contains(restored.Society.Inventory.Lots, lot => lot.OwnerId == "household:camp-alpha" &&
            lot.ItemKind == "wood" && lot.Quantity == 4 && lot.StorageBuildingId == "project-home");

        var helper = state.Society.Society.Agents.First(person => person.Id != actor).Id;
        var helping = world.ExportState();
        helping = helping with
        {
            Agents = helping.Agents.Select(person => person.AgentId == helper
                ? person with { Position = homeSite, HungerBasisPoints = 9_000, LastDecisionContext = null } : person).ToArray(),
            Society = helping.Society with
            {
                Society = helping.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(helping.Society.Society.Inventory,
                        "helper-project-wood", "wood", helper, 4),
                },
            },
        };
        using var helped = WorldRuntime.Restore(helping,
            id => id == helper ? new PreferredCandidateProvider("assist:wood") : new IdleProvider());
        Assert.True((await helped.AdvanceOneTickAsync()).Advanced);
        var contribution = helped.ExportState().Events.Single(item =>
            item.Kind == "project_request_fulfilled" &&
            item.Detail == $"{helper}:{actor}:wood:4");
        Assert.Equal(homeSite, contribution.Position);
        Assert.Contains(helped.Society.Inventory.Lots, lot => lot.Id == "helper-project-wood" &&
            lot.OwnerId == "household:camp-alpha" &&
            lot.ItemKind == "wood" && lot.Quantity == 4 && lot.StorageBuildingId == "project-home" &&
            lot.DeliveryBuildingId is null);
        using var resumed = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(helped.ExportState())));
        Assert.Contains(resumed.Society.Inventory.Lots, lot => lot.Id == "helper-project-wood" &&
            lot.OwnerId == "household:camp-alpha" &&
            lot.ItemKind == "wood" && lot.StorageBuildingId == "project-home" &&
            lot.DeliveryBuildingId is null);
    }

    [Fact]
    public async Task HouseLessHelperHandsMaterialsToTheRequesterInPersonAcrossReload()
    {
        using var seed = WorldRuntime.Restore(
            GeographyGeneratorTests.StartedGeneratedWorld(new GeographyOptions("project-help-without-house", WorldSizePreset.Small)),
            _ => new IdleProvider());
        _ = seed.StageStarterContent();
        var initial = seed.ExportState();
        var startingSiteResidents = initial.Society.Society.Agents
            .Where(item => item.HouseholdId == "household:camp-alpha").OrderBy(item => item.Id, StringComparer.Ordinal).Take(2).ToArray();
        Assert.Equal(2, startingSiteResidents.Length);
        var requester = startingSiteResidents[0];
        var helper = startingSiteResidents[1];
        const string requesterHouseholdId = "household:no-house-requester";
        const string helperHouseholdId = "household:helper";
        var society = initial.Society.Society;
        var requesterMembership = society.Relationships.Single(item => item.Type == SocietyRelationshipType.HouseholdMembership &&
            item.State == SocietyRelationshipState.Accepted && item.TargetId == requester.Id);
        society = SocietyRules.RevokeRelationship(society, requesterMembership.Id, requester.Id).Checkpoint;
        society = SocietyRules.CreateHousehold(society, requesterHouseholdId, "Requester household", [requester.Id]).Checkpoint;
        var membership = society.Relationships.Single(item => item.Type == SocietyRelationshipType.HouseholdMembership &&
            item.State == SocietyRelationshipState.Accepted && item.TargetId == helper.Id);
        society = SocietyRules.RevokeRelationship(society, membership.Id, helper.Id).Checkpoint;
        society = SocietyRules.CreateHousehold(society, helperHouseholdId, "Helper household", [helper.Id]).Checkpoint;
        Assert.DoesNotContain(initial.WorldSimulation!.Buildings,
            building => building.HouseholdId == requesterHouseholdId &&
                seed.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
                    .Tags.Contains("house", StringComparer.Ordinal));

        var available = initial.Map.Tiles.Select(tile => tile.Position)
            .Where(point => initial.Map.IsPassable(point) &&
                !initial.Map.MapObjects.Any(item => item.Position == point) &&
                !initial.Map.Resources.Any(item => item.Position == point) &&
                !initial.Society.Society.Agents.Where(item => item.Id != requester.Id && item.Id != helper.Id)
                    .Any(item => initial.Agents.Single(person => person.AgentId == item.Id).Position == point))
            .ToArray();
        var startingSite = initial.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-warehouse").Position;
        var helperPosition = initial.Map.FootNeighbors(startingSite).First(available.Contains);
        var requesterPosition = available.Where(point => point != helperPosition &&
                initial.Map.IsReachableOnFoot(helperPosition, point) &&
                initial.Map.FootDistance(helperPosition, point) >= 4)
            .OrderBy(point => initial.Map.FootDistance(helperPosition, point))
            .ThenBy(point => point.Y).ThenBy(point => point.X).First();
        var house = seed.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var site = initial.Map.Tiles.Select(tile => tile.Position).First(point =>
            initial.Map.IsBuildable(point) &&
            !initial.Map.MapObjects.Any(item => item.Position == point) &&
            !initial.Map.Resources.Any(item => item.Position == point) &&
            point != requesterPosition && point != helperPosition);
        var inventory = society.Inventory;
        var removedWoodIds = inventory.Lots.Where(lot => lot.ItemKind == "wood" &&
                (lot.OwnerId == requesterHouseholdId || lot.OwnerId == requester.Id))
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !removedWoodIds.Contains(lot.Id)).ToArray(),
            Reservations = inventory.Reservations.Where(item => !removedWoodIds.Contains(item.LotId)).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "no-house-helper-wood", "wood", helper.Id, 4);
        society = society with { Inventory = inventory };
        var state = initial with
        {
            Society = initial.Society with { Society = society },
            Agents = initial.Agents.Select(person => person.AgentId == requester.Id
                ? person with
                {
                    Position = requesterPosition,
                    HungerBasisPoints = 9_000,
                    Project = new SettlementProject(TownConstructionCandidateIds.Building(house.CanonicalId, site),
                        house.DisplayName, initial.Society.Society.WorldTick, "acquiring",
                        LastTransitionTick: initial.Society.Society.WorldTick),
                }
                : person.AgentId == helper.Id
                    ? person with { Position = helperPosition, HungerBasisPoints = 9_000, LastDecisionContext = null }
                    : person).ToArray(),
        };
        using var helped = WorldRuntime.Restore(state,
            id => id == helper.Id ? new PreferredCandidateProvider("assist:wood") : new IdleProvider());
        Assert.True((await helped.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(helper.Id, helped.Society.Inventory.GetLot("no-house-helper-wood").OwnerId);
        Assert.DoesNotContain(helped.ExportState().Events, item => item.Kind == "project_request_fulfilled" &&
            item.Detail == $"{helper.Id}:{requester.Id}:wood:4");
        var traveling = WorldRuntimeCodec.Encode(helped.ExportState());
        using var arrived = WorldRuntime.Restore(WorldRuntimeCodec.Decode(traveling),
            id => id == helper.Id ? new PreferredCandidateProvider("assist:wood") : new IdleProvider());
        Assert.Equal(traveling, WorldRuntimeCodec.Encode(arrived.ExportState()));
        for (var tick = 0; tick < 60 && arrived.Society.Inventory.GetLot("no-house-helper-wood").OwnerId == helper.Id; tick++)
            Assert.True((await arrived.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(arrived.ExportState().Events, item => item.Kind == "project_request_fulfilled" &&
            item.Detail == $"{helper.Id}:{requester.Id}:wood:4");
        var carried = Assert.Single(arrived.Society.Inventory.Lots, lot => lot.Id == "no-house-helper-wood");
        Assert.Equal((requester.Id, 4), (carried.OwnerId, carried.Quantity));
        Assert.Null(carried.StorageBuildingId);
        Assert.Null(carried.DeliveryBuildingId);
        Assert.Null(carried.GroundPosition);
        Assert.InRange(initial.Map.FootDistance(arrived.Agents.Single(person => person.AgentId == helper.Id).Position,
            arrived.Agents.Single(person => person.AgentId == requester.Id).Position), 0, 1);

        var saved = WorldRuntimeCodec.Encode(arrived.ExportState());
        using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new IdleProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(resumed.ExportState()));
        Assert.Contains(resumed.Society.Inventory.Lots, lot => lot.Id == "no-house-helper-wood" &&
            lot.OwnerId == requester.Id && lot.Quantity == 4 && lot.StorageBuildingId is null &&
            lot.DeliveryBuildingId is null && lot.GroundPosition is null);
    }

    [Fact]
    public async Task FinishedWorkBuildsAtTheCurrentValidSiteInsteadOfRetargetingAnEarlierTile()
    {
        using var seed = new WorldRuntime("stable-project-site", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var actor = state.Agents[0];
        var position = state.Map.Tiles.Last(tile => state.Map.IsBuildable(tile.Position) &&
            !state.Map.MapObjects.Any(item => item.Position == tile.Position) &&
            !state.Map.Resources.Any(item => item.Position == tile.Position) &&
            !state.Agents.Any(person => person.Position == tile.Position)).Position;
        var definition = seed.WorldContent.Buildings.Single(building => building.LocalId == "fire");
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "site-tool", "tool", actor.AgentId, 1),
                },
            },
            Agents = state.Agents.Select(person => person == actor ? person with
            {
                Position = position,
                HungerBasisPoints = 9_000,
                Project = new(TownConstructionCandidateIds.Building(definition.CanonicalId, position),
                    definition.DisplayName, seed.WorldTick, "working", 10,
                    LastTransitionTick: seed.WorldTick),
            } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        await world.AdvanceOneTickAsync();
        Assert.True(world.WorldSimulation.Buildings.Any(building => building.DefinitionId == definition.CanonicalId && building.Position == position),
            System.Text.Json.JsonSerializer.Serialize(world.Agents.Single(person => person.AgentId == actor.AgentId)) +
            System.Text.Json.JsonSerializer.Serialize(world.ExportState().Events.TakeLast(12)));
        Assert.Equal("completed", world.Agents.Single(person => person.AgentId == actor.AgentId).Project!.Stage);
    }

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == "safe_idle").ToArray() },
            }, cancellationToken);
    }

    private sealed class PreferredCandidateProvider(string candidateId) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == candidateId) ??
                request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected.Id, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected.Id ? 1d : 0d)));
        }
    }

    [Fact]
    public async Task UnregisteredMapAdditionIsRejectedEvenWithARecomputedDigest()
    {
        using var world = new WorldRuntime("invalid-settlement-map");
        world.StageStarterContent();
        for (var tick = 0; tick < 10; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var state = world.ExportState();
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with { Content = null }));
        var forgedMap = state.Map with
        {
            Resources = state.Map.Resources.Select(resource => resource.Id == "settlement-stone"
                ? resource with { Id = "unregistered-stone" } : resource).ToArray(),
        };
        forgedMap = forgedMap with { ManifestDigest = MapManifestCodec.Digest(forgedMap) };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with { Map = forgedMap }));
    }

    [Theory]
    [InlineData("build:", "working", 0)]
    [InlineData("build:recipe:example", "working", 11)]
    public void InvalidProjectCheckpointFailsClosed(string candidate, string stage, int work)
    {
        using var world = new WorldRuntime("invalid-project");
        var state = world.ExportState();
        var person = state.Agents[0];
        var invalid = state with
        {
            Agents = state.Agents.Select(item => item == person
                ? item with { Project = new SettlementProject(candidate, "Project", 0, stage, work) }
                : item).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalid));
    }

    [Fact]
    public async Task DefaultSettlementGathersDifferentInputsSharesAndCompletesVisibleProjects()
    {
        var provider = new ObservingProvider();
        using var setup = new WorldRuntime("living-settlement", _ => provider);
        setup.StageStarterContent();
        var initial = setup.ExportState();
        var inventory = initial.Society.Society.Inventory;
        // This compact project fixture has no river or lake. Supply physical
        // paper so learned discoveries can be written and naturally stored;
        // the generated-world pipeline test covers manufacturing those sheets.
        foreach (var person in initial.Society.Society.Agents.Where(person =>
                     person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder))
            inventory = InventoryAuthority.AddLot(inventory, "settlement-writing-paper:" + person.Id,
                "paper", person.Id, 2, initial.Society.Society.WorldTick);
        using var world = WorldRuntime.Restore(initial with
        {
            Society = initial.Society with { Society = initial.Society.Society with { Inventory = inventory } },
        }, _ => provider);
        var storedBelongings = new Dictionary<string, (string Owner, string House, string Kind)>();
        for (var tick = 0; tick < 1000; tick++)
        {
            await world.AdvanceOneTickAsync();
            AssertBelongingsStayStored(world, storedBelongings);
            foreach (var lot in world.Society.Inventory.Lots.Where(lot => lot.StorageBuildingId is not null &&
                         world.Society.Agents.Any(person => person.Id == lot.OwnerId)))
                storedBelongings.TryAdd(lot.Id, (lot.OwnerId, lot.StorageBuildingId!, lot.ItemKind));
        }
        Assert.Contains(storedBelongings.Values, stored => stored.Kind is "field_map" or "field_record");
        var state = world.ExportState();
        var storedArtifacts = state.Knowledge!.Artifacts.Where(artifact => storedBelongings.ContainsKey(artifact.LotId)).ToArray();
        Assert.NotEmpty(storedArtifacts);
        Assert.All(storedArtifacts, artifact =>
        {
            var paper = artifact.Materials.Where(material => material.ItemKind == "paper").ToArray();
            Assert.NotEmpty(paper);
            Assert.All(paper, material =>
            {
                Assert.StartsWith("settlement-writing-paper:", material.LotId);
                Assert.Equal(InventoryReservationState.Completed, state.Society.Society.Inventory.GetReservation(material.ReservationId).State);
            });
        });
        Assert.DoesNotContain(state.Map.MapObjects, item => item.Kind == "bedroll");
        Assert.DoesNotContain(world.WorldContent.Recipes,
            recipe => recipe.Outputs.Any(output => output.ResourceId == "bedding"));
        Assert.NotEmpty(world.WorldSimulation.Buildings);
        Assert.Contains(state.Events, item => item.Kind == "build_completed");
        Assert.Contains(state.Events, item => item.Kind == "household_food_collected");
        var gathered = state.Events.Where(item => item.Kind == "material_gathered")
            .Select(item => item.Detail.Split(':')[^2]).ToHashSet(StringComparer.Ordinal);
        Assert.True(gathered.Count >= 3);
        Assert.Contains("stone", gathered);
        Assert.Contains("fiber", gathered);
        Assert.Contains("wood", gathered);
        Assert.Contains(state.Events, item => item.Kind == "project_request_fulfilled");
        Assert.Contains(state.Events, item => item.Kind == "social_standing_changed");
        Assert.Contains(state.Agents, person => person.SocialStanding?.Any(item => item.Trust >= 2) == true);
        Assert.Contains(state.Events, item => item.Kind == "project_progress" && item.Detail.Contains(":completed:", StringComparison.Ordinal));
        Assert.Contains(state.Events, item => item.Kind == "food_consumed");
        Assert.True(provider.MeaningfulProjectChoiceSeen);
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        Assert.DoesNotContain("EnergyBasisPoints", JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
        Assert.NotEmpty(snapshot.Stockpiles);
        Assert.Contains(snapshot.Agents, person => person.Project is not null);
        Assert.Contains(snapshot.Agents, person => person.SocialNotes.Count > 0);
        Assert.Contains(snapshot.Agents, person => person.SocialStanding.Count > 0);
        Assert.NotNull(snapshot.Council?.StewardName);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)));
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(restored.ExportState()));
        for (var tick = 0; tick < 3; tick++)
        {
            await world.AdvanceOneTickAsync();
            await restored.AdvanceOneTickAsync();
            AssertBelongingsStayStored(restored, storedBelongings);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
                WorldRuntimeCodec.Encode(restored.ExportState()));
        }
    }

    private static void AssertBelongingsStayStored(WorldRuntime world,
        IReadOnlyDictionary<string, (string Owner, string House, string Kind)> storedBelongings)
    {
        foreach (var (lotId, stored) in storedBelongings)
        {
            // A lot used up or a House removed since storage is no longer at home.
            var lot = world.Society.Inventory.Lots.SingleOrDefault(item => item.Id == lotId);
            var house = world.WorldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == stored.House);
            if (lot is not null && house is not null && lot.OwnerId == stored.Owner &&
                world.Society.GetAgent(stored.Owner).HouseholdId == house.HouseholdId)
                Assert.True(lot.StorageBuildingId == stored.House,
                    $"Stored {lot.ItemKind} {lotId} was needlessly collected at tick {world.WorldTick}.");
        }
    }

    private sealed class ObservingProvider : IDecisionProvider
    {
        public bool MeaningfulProjectChoiceSeen { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            MeaningfulProjectChoiceSeen |= request.Observation.Candidates.Count(candidate => candidate.Id.StartsWith("build:", StringComparison.Ordinal)) > 1;
            return new DeterministicDecisionProvider().DecideAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task AChosenProjectAcquiresAndUsesCarriedWoodWhenThereIsNoHouse()
    {
        using var seed = new WorldRuntime("settlement-acquisition");
        var initial = seed.ExportState();
        using var world = WorldRuntime.Restore(initial with
        {
            Society = initial.Society with
            {
                Society = initial.Society.Society with
                {
                    Inventory = initial.Society.Society.Inventory with
                    {
                        Lots = initial.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind != "wood").ToArray(),
                    },
                },
            },
        });
        world.StageStarterContent();
        for (var tick = 0; tick < 200; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Contains(world.ExportState().Events, item => item.Kind == "project_chosen");
        Assert.Contains(world.ExportState().Events, item => item.Kind == "material_gathered" && item.Detail.Contains(":wood:", StringComparison.Ordinal));
        if (!world.Society.Inventory.Reservations.Any(item => item.Purpose.StartsWith("building:", StringComparison.Ordinal)))
            output.WriteLine(CapacityDiagnostic(world));
        var completed = world.Society.Inventory.Reservations.Where(reservation =>
            reservation.Purpose.StartsWith("building:", StringComparison.Ordinal) &&
            reservation.State == InventoryReservationState.Completed).ToArray();
        Assert.NotEmpty(completed);
        foreach (var reservation in completed)
        {
            var built = Assert.Single(world.WorldSimulation.Buildings,
                building => "building:" + building.InstanceId == reservation.Purpose);
            var household = world.Society.Agents.FirstOrDefault(person => person.Id == reservation.OwnerId)
                ?.HouseholdId ?? reservation.OwnerId;
            Assert.Equal(household, built.HouseholdId);
        }
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Validate();
    }

    private static string CapacityDiagnostic(WorldRuntime world) => System.Text.Json.JsonSerializer.Serialize(new
    {
        People = world.Agents.Select(person => new
        {
            person.AgentId,
            Household = world.Society.GetAgent(person.AgentId).HouseholdId,
            person.HungerBasisPoints,
            person.Survival,
            person.Project,
            person.LastDecisionContext,
            Cargo = PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, person.AgentId, person.Equipment),
            Capacity = PersonalEquipmentRules.Capacity(world.Society.Inventory, person.AgentId, person.Equipment),
        }),
        Lots = world.Society.Inventory.Lots.Select(lot => new
        {
            lot.Id,
            lot.ItemKind,
            lot.OwnerId,
            lot.Quantity,
            lot.StorageBuildingId,
            lot.DeliveryBuildingId,
            lot.GroundPosition,
        }),
        Events = world.ExportState().Events.TakeLast(16),
    });
}
