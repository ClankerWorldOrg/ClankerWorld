using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

/// <summary>
/// Builds a world the way the game does: generated map, first Town site,
/// four placed founders and Start World. No roles are assigned.
/// </summary>
internal static class NormalPathWorld
{
    public static WorldRuntime CreateGenerated(string seed, Func<string, IDecisionProvider> providerFactory,
        GridPoint? roughTownSite = null, WorldSizePreset size = WorldSizePreset.Small, int? ticksPerDay = null, int calendarOffsetTicks = 0)
    {
        var options = new GeographyOptions(seed, size);
        var world = new WorldRuntime(options.Seed, providerFactory,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var map = world.ExportState().Map;
        var anchor = roughTownSite ?? FindStartingTownSite(map);
        world.InitializeFirstTownContent();
        world.AcceptFirstTownLayout(anchor);
        var buildings = world.WorldSimulation.Buildings.SelectMany(building =>
            WorldContentSimulationRules.Footprint(world.WorldContent.Buildings.Single(item =>
                item.CanonicalId == building.DefinitionId), building.Position)).ToHashSet();
        var roads = world.RoadTiles.ToHashSet();
        var placements = map.Tiles.Where(tile => Math.Abs(tile.Position.X - anchor.X) <= 5 &&
                Math.Abs(tile.Position.Y - anchor.Y) <= 5 && map.IsBuildable(tile.Position) &&
                !buildings.Contains(tile.Position) && !roads.Contains(tile.Position) &&
                !map.MapObjects.Any(item => item.Position == tile.Position) &&
                !map.Resources.Any(item => item.Position == tile.Position))
            .Take(WorldRuntime.RequiredFounders).Select(tile => tile.Position).ToArray();
        for (var index = 0; index < placements.Length; index++)
            world.PlaceFounder($"founder:{index + 1:D32}", placements[index]);
        if (ticksPerDay is { } day)
        {
            // Controlled short-day fixtures choose their clock before Start World opens its first election.
            var state = world.ExportState();
            var society = state.Society.Society;
            var oldDay = society.Config.TicksPerWorldDay;
            var prepared = WorldRuntime.Restore(state with
            {
                WorldSystems = RegionalWeatherRules.Initialize(state.WorldSystems! with
                { Config = state.WorldSystems.Config with { TicksPerDay = day, CalendarOffsetTicks = calendarOffsetTicks }, RegionalWeather = null }, state.Map),
                Society = state.Society with
                {
                    Society = society with
                    {
                        Config = society.Config with { TicksPerWorldDay = day },
                        Inhabitants = society.Inhabitants.Select(person => person with
                        {
                            BirthTick = person.BirthTick / oldDay * day,
                            BirthLifeTick = person.BirthLifeTick is { } birth ? birth / oldDay * day : null,
                        }).ToArray(),
                    },
                },
            }, providerFactory);
            world.Dispose();
            world = prepared;
        }
        world.StartWorld();
        return world;
    }

    /// <summary>Keep candidate-less failed election windows consistent with a fixture's new day length.</summary>
    internal static WorldRuntimeState ReclockCandidateLessCouncilAttempts(WorldRuntimeState state, int day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        return state with
        {
            Towns = state.Towns?.Select(town =>
            {
                if (town.Governance is not { } council) return town;
                Assert.Null(council.Election);
                Assert.Empty(council.Candidates);
                Assert.All(council.ElectionHistory, election =>
                {
                    Assert.Equal("failed", election.Stage);
                    Assert.Empty(election.Candidates);
                    Assert.Empty(election.Ballots);
                    Assert.Empty(election.SettledSeats);
                    Assert.Empty(election.DrawOrder);
                });
                // Keep the actual IDs, opening ticks and resident rosters when a fixture changes day length.
                return town with
                {
                    Governance = council with
                    {
                        ElectionHistory = council.ElectionHistory.Select(election => election with
                        { DeadlineTick = election.OpenedTick + day }).ToArray(),
                    },
                };
            }).ToArray(),
        };
    }

    /// <summary>Choose a feasible site near the camp without changing generated terrain or resources.</summary>
    internal static GridPoint FindStartingTownSite(WorldMap map)
    {
        var startingSite = map.Resources.Single(item => item.Id == "berry-patch").Position;
        var occupied = map.Resources.Select(item => item.Position)
            .Concat(map.MapObjects.Select(item => item.Position)).ToHashSet();
        foreach (var point in map.Tiles.Select(tile => tile.Position)
            .Where(point => map.IsBuildable(point) && !occupied.Contains(point))
            .OrderBy(point => Math.Abs(point.X - startingSite.X) + Math.Abs(point.Y - startingSite.Y))
            .ThenBy(point => point.Y).ThenBy(point => point.X).Prepend(startingSite))
        {
            if (FirstTownLayoutPlanner.Plan(map, point) is not { } layout) continue;
            var taken = occupied.Concat(layout.RoadTiles).Concat(layout.Buildings.SelectMany(building =>
                Enumerable.Range(0, building.Height).SelectMany(y => Enumerable.Range(0, building.Width)
                    .Select(x => new GridPoint(building.Position.X + x, building.Position.Y + y)))))
                .ToHashSet();
            if (map.Tiles.Count(tile => Math.Abs(tile.Position.X - point.X) <= 5 &&
                    Math.Abs(tile.Position.Y - point.Y) <= 5 && map.IsBuildable(tile.Position) &&
                    !taken.Contains(tile.Position)) >= WorldRuntime.RequiredFounders)
                return point;
        }
        throw new InvalidOperationException("The generated map has no feasible first Town site with room for its founders.");
    }

    /// <summary>A stable action family for a candidate, e.g. <c>recipe:mill-grain</c>.</summary>
    public static string Family(string candidateId, DeclarativeWorldContentState content)
    {
        if (TownConstructionCandidateIds.TryParse(candidateId, out var selection))
        {
            var localId = selection.IsBuilding
                ? content.Buildings.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId)?.LocalId
                : content.Recipes.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId)?.LocalId;
            return (selection.IsBuilding ? "building:" : "recipe:") + (localId ?? "unknown");
        }
        var separator = candidateId.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? candidateId : candidateId[..separator];
    }
}

/// <summary>
/// Records every candidate offered to each agent, then lets the built-in rules
/// choose, or always stays idle so the world changes as little as possible.
/// </summary>
internal sealed class ActionCoverageRecorder(bool chooseIdle = false) : IDecisionProvider
{
    private readonly DeterministicDecisionProvider chooser = new();

    public ConcurrentDictionary<string, ConcurrentDictionary<string, int>> OfferedByAgent { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, int> Chosen { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<(string AgentId, string DestinationId), byte> WorkstationSupplyOffers { get; } = new();

    /// <summary>The world tick at which each agent was first offered each candidate.</summary>
    public ConcurrentDictionary<(string AgentId, string CandidateId), long> FirstOfferedTick { get; } = new();

    public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;

    public long ProviderEpoch => 0;

    public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var offered = OfferedByAgent.GetOrAdd(request.Observation.InhabitantId,
            _ => new ConcurrentDictionary<string, int>(StringComparer.Ordinal));
        foreach (var candidate in request.Observation.Candidates)
        {
            offered.AddOrUpdate(candidate.Id, 1, (_, count) => count + 1);
            FirstOfferedTick.TryAdd((request.Observation.InhabitantId, candidate.Id), request.Observation.WorldTick);
            if (candidate.Id.StartsWith("supply_workstation:", StringComparison.Ordinal) &&
                candidate.DestinationId is { } destination)
                WorkstationSupplyOffers.TryAdd((request.Observation.InhabitantId, destination), 0);
        }
        var choice = chooseIdle
            ? request with
            {
                Observation = request.Observation with
                {
                    Candidates = [request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle")],
                },
            }
            : request;
        var response = await chooser.DecideAsync(choice, cancellationToken);
        Chosen.AddOrUpdate(response.SelectedCandidateId, 1, (_, count) => count + 1);
        return response;
    }

    public IReadOnlySet<string> FamiliesOfferedTo(string agentId, DeclarativeWorldContentState content) =>
        OfferedByAgent.TryGetValue(agentId, out var offered)
            ? offered.Keys.Select(id => NormalPathWorld.Family(id, content)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> FamiliesOffered(DeclarativeWorldContentState content) =>
        OfferedByAgent.Values.SelectMany(offered => offered.Keys)
            .Select(id => NormalPathWorld.Family(id, content)).ToHashSet(StringComparer.Ordinal);
}
