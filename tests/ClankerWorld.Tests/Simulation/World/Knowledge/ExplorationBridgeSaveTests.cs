using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using Xunit.Abstractions;

namespace ClankerWorld.Tests;

public sealed class ExplorationBridgeSaveTests(ITestOutputHelper output)
{
    private const string Seed = "exploration-bridge-audit-1";
    private const string BridgeId = "bridge-0-102-ew-2";
    private const string WorkshopId = "exploration-road-workshop";
    private static readonly GridPoint Start = new(0, 101);
    private static readonly GridPoint FirstWater = new(0, 102);
    private static readonly GridPoint SecondWater = new(0, 103);
    private static readonly GridPoint WorkshopSite = new(3, 102);

    [Fact]
    public async Task APaidRoadBridgePreservesAnActuallyWalkedExplorationPathAndRecoverySave()
    {
        var returnRequested = false;
        IDecisionProvider ChooseProvider(string actor) => new ScoutProvider(
            actor == "founder:00000000000000000000000000000001", () => returnRequested);
        using var world = CreateScoutingWorld(ChooseProvider);
        var actor = world.Agents[0].AgentId;
        // The seeded visited memory starts at tick zero, so let the ordinary
        // scouting cooldown and idle admission expire without resetting them.
        for (var tick = 0; tick < 400 && Scout(world, actor).Exploration!.OutingPath.Count < 3; tick++)
        {
            var previous = Scout(world, actor).Position;
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var current = Scout(world, actor).Position;
            if (previous != current) Assert.True(world.ExportState().Map.CanFootStep(previous, current));
        }
        var before = world.ExportState();
        var exploration = Assert.IsType<SettlementExploration>(Scout(world, actor).Exploration);
        Assert.Equal([Start, FirstWater, SecondWater], exploration.OutingPath);
        Assert.True(before.Map.CanFootStep(Start, FirstWater));
        Assert.True(before.Map.CanFootStep(FirstWater, SecondWater));
        Assert.Empty(world.Bridges);
        Assert.Empty(before.Knowledge!.Artifacts);
        Assert.Contains(before.Events, item => item.Kind == "exploration_started" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        Assert.Contains(before.Knowledge!.Facts, item => item.OwnerId == actor && item.Position == FirstWater);
        var beforeBytes = WorldRuntimeCodec.Encode(before);
        using (var validated = WorldRuntime.Restore(WorldRuntimeCodec.Decode(beforeBytes)))
            Assert.Equal(beforeBytes, WorldRuntimeCodec.Encode(validated.ExportState()));

        var directory = Directory.CreateTempSubdirectory("clankerworld-exploration-bridge-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), ChooseProvider);
            file.Save(world);
            var workshop = world.WorldContent.Buildings.Single(item => item.LocalId == "workshop");
            var beforeStock = Totals(world.ExportState());
            var placed = world.PlaceBuilding(WorkshopId, workshop.CanonicalId, WorkshopSite);
            Assert.True(placed.Applied, placed.Failure);
            var bridge = Assert.Single(world.Bridges, item => item.Id == BridgeId);
            Assert.Equal(BridgeId, bridge.Id);
            Assert.Equal(BridgeTriggers.Road, bridge.Trigger);
            Assert.Equal($"road:{TownBorderRules.FirstTownId}:{WorkshopId}", bridge.RouteId);
            Assert.True(bridge.BuiltTick > exploration.LastOutingTick);
            var after = world.ExportState();
            var difference = beforeStock.ToDictionary(item => item.Key, item => item.Value - Totals(after).GetValueOrDefault(item.Key));
            Assert.Equal(workshop.BuildCosts.ToDictionary(item => item.ResourceId, item => item.Amount),
                difference.Where(item => item.Value != 0).ToDictionary());
            Assert.Equal(exploration, Scout(world, actor).Exploration);
            Assert.Equal(before.Knowledge.Facts, after.Knowledge!.Facts);
            Assert.False(after.Map.CanFootStep(Start, FirstWater));
            Assert.False(after.Map.CanFootStep(FirstWater, SecondWater));
            output.WriteLine($"Actual path={string.Join(" -> ", exploration.OutingPath)}; outing started={exploration.LastOutingTick}; paid bridge={bridge.Id}; built={bridge.BuiltTick}; current old edges illegal; stock costs exact.");

            // The path is historical; it must remain valid checkpoint data even
            // though future movement must obey the newly built bridge's axis.
            var previousRecovery = File.ReadAllBytes(file.Path);
            byte[]? saved = null;
            WorldRuntime? direct = null;
            var encodeFailure = Record.Exception(() => saved = WorldRuntimeCodec.Encode(after));
            var restoreFailure = Record.Exception(() => direct = WorldRuntime.Restore(after, ChooseProvider));
            var saveFailure = Record.Exception(() => file.Save(world));
            output.WriteLine($"Encode={encodeFailure?.Message ?? "success"}; direct restore={restoreFailure?.Message ?? "success"}; recovery write={saveFailure?.Message ?? "success"}.");
            if (saveFailure is not null) Assert.Equal(previousRecovery, File.ReadAllBytes(file.Path));
            if (direct is not null)
            {
                using (direct)
                    if (saved is not null) Assert.Equal(saved, WorldRuntimeCodec.Encode(direct.ExportState()));
            }
            Assert.Null(encodeFailure);
            Assert.Null(restoreFailure);
            Assert.Null(saveFailure);
            Assert.NotNull(saved);
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), ChooseProvider);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
            using var recovery = file.LoadOrCreate(Seed);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(recovery.ExportState()));
            // A discarded prepared tick cannot move the committed scout or
            // alter its saved historical record after this topology change.
            var committed = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(committed, WorldRuntimeCodec.Encode(world.ExportState()));
            var returnSuggestion = new OwnerInstructionRequest("bridge-scout-return", "owner:test", actor,
                OwnerInstructionKind.Suggestive, "Please return along your scouting route.");
            returnRequested = true;
            world.SubmitInstruction(returnSuggestion);
            recovery.SubmitInstruction(returnSuggestion);
            for (var tick = 0; tick < 128 && Scout(world, actor).Exploration!.OutingPath.Count > 0; tick++)
            {
                var previous = Scout(world, actor).Position;
                var map = world.ExportState().Map;
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                Assert.True((await recovery.AdvanceOneTickAsync()).Advanced);
                var current = Scout(world, actor).Position;
                if (previous != current) Assert.True(map.CanFootStep(previous, current));
                Assert.All(exploration.VisitedTiles, point => Assert.Contains(point, Scout(world, actor).Exploration!.VisitedTiles));
                Assert.All(before.Knowledge.Facts, fact => Assert.Contains(fact, world.ExportState().Knowledge!.Facts));
                Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(recovery.ExportState()));
            }
            var result = world.ExportState();
            var returned = Scout(world, actor).Exploration!;
            Assert.Empty(returned.OutingPath);
            var terminal = Assert.Single(result.Events, item =>
                item.Kind == "exploration_completed" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal) ||
                item.Kind == "exploration_aborted" && item.Detail == actor + ":return_blocked");
            if (terminal.Kind == "exploration_completed") Assert.Equal(Start, Scout(world, actor).Position);
            Assert.DoesNotContain(result.Events, item => item.Kind == "exploration_aborted" && item.Detail == actor + ":interrupted_movement");
            Assert.Empty(result.Knowledge!.Artifacts);
            Assert.NotEmpty(returned.OutingDiscoveries!);
            Assert.All(returned.OutingDiscoveries!.Distinct(), point =>
            {
                var fact = Assert.Single(result.Knowledge.Facts, fact => fact.OwnerId == actor && fact.Position == point);
                Assert.Equal(actor, fact.OwnerId);
                Assert.Equal(actor, fact.DiscovererId);
                Assert.Equal("firsthand", fact.Acquisition);
            });
            Assert.DoesNotContain(result.Society.Society.Inventory.Lots,
                lot => lot.ItemKind is "field_record" or "field_map" or "book");
            output.WriteLine($"Actual terminal={terminal.Kind}; discoveries={returned.OutingDiscoveries!.Count}; learned facts preserved without creating physical goods.");
            Assert.Equal(WorldRuntimeCodec.Encode(result), WorldRuntimeCodec.Encode(recovery.ExportState()));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static AgentState Scout(WorldRuntime world, string actor) =>
        world.Agents.Single(item => item.AgentId == actor);

    internal static WorldRuntime CreateScoutingWorld(Func<string, IDecisionProvider>? chooseProvider = null)
    {
        chooseProvider ??= Provider;
        var geography = new GeographyOptions(Seed, WorldSizePreset.Small, WaterPercent: 50, HydrologyVersion: 1);
        using var setup = new WorldRuntime(Seed, chooseProvider, startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        setup.InitializeFirstTownContent();
        var map = setup.ExportState().Map;
        var anchor = NormalPathWorld.FindStartingTownSite(map);
        setup.AcceptFirstTownLayout(anchor);
        var occupied = setup.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                setup.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building.Position))
            .Concat(map.Resources.Select(item => item.Position)).Concat(map.MapObjects.Select(item => item.Position))
            .Concat(setup.RoadTiles).ToHashSet();
        var founders = map.Tiles.Select(tile => tile.Position)
            .Where(point => map.IsBuildable(point) && !occupied.Contains(point) &&
                Math.Abs(point.X - anchor.X) <= 5 && Math.Abs(point.Y - anchor.Y) <= 5)
            .Take(WorldRuntime.RequiredFounders).ToArray();
        Assert.Equal(WorldRuntime.RequiredFounders, founders.Length);
        for (var index = 0; index < founders.Length; index++) setup.PlaceFounder($"founder:{index + 1:D32}", founders[index]);
        setup.StartWorld();
        var state = setup.ExportState();
        var actor = state.Agents[0].AgentId;
        Assert.True(RiverBridgeRules.TryResolve(map, BridgeId, out var crossing));
        Assert.Contains(FirstWater, crossing!.Span);
        var roadEnd = crossing.Entrances.Single(point => point.X == map.Width - 1);
        Assert.True(map.IsBuildable(roadEnd));
        Assert.True(map.IsBuildable(WorkshopSite));
        // This validated disposable fixture gives the existing Town a street
        // end beside the generated crossing. No terrain, resource, bridge or
        // outward breadcrumb is fabricated; the scout walks both later edges.
        var roads = setup.RoadTiles.Append(roadEnd).Distinct().OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
        var town = Assert.Single(state.Towns!);
        var visited = map.FootNeighbors(Start).Concat(map.FootNeighbors(FirstWater))
            .Where(point => point != FirstWater && point != SecondWater).Append(Start).Distinct().ToArray();
        state = state with
        {
            RoadTiles = roads,
            Towns = [town with { BorderTiles = TownBorderRules.Expand(map, town, [roadEnd, WorkshopSite]) }],
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = Start,
                HungerBasisPoints = 9_500,
                Exploration = new SettlementExploration(visited, [], 0, false),
            } : person).ToArray(),
        };
        return WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), chooseProvider);
    }

    private static ScoutProvider Provider(string actor) => new ScoutProvider(actor == "founder:00000000000000000000000000000001");

    private static Dictionary<string, int> Totals(WorldRuntimeState state) => state.Society.Society.Inventory.Lots
        .GroupBy(lot => lot.ItemKind, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Sum(lot => lot.Quantity), StringComparer.Ordinal);

    private sealed class ScoutProvider(bool scout, Func<bool>? returnRequested = null) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var returning = returnRequested?.Invoke() == true;
            var choice = request.Observation.Candidates.FirstOrDefault(item => scout && item.Id == (returning ? "explore_return" : "explore"))
                ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
