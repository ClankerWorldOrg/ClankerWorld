using System.Collections.Concurrent;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

/// <summary>
/// Builds a world the way the game does: generated map, first Town site,
/// four placed founders and Start World. No roles are assigned.
/// </summary>
internal static class NormalPathWorld
{
    internal const string Founder1 = "founder:000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001";
    internal const string Founder2 = "founder:000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002";
    internal const string Founder3 = "founder:000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000003";
    internal const string Founder4 = "founder:000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000004";

    internal static string FounderId(int ordinal) => ordinal switch
    {
        1 => Founder1,
        2 => Founder2,
        3 => Founder3,
        4 => Founder4,
        _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
    };

    internal static string FounderName(int ordinal) => ordinal switch
    {
        1 => "Élodie 🌿 Vale",
        2 => "Jose\u0301 🧭 Reed",
        3 => "Åsa 🐎 Birch",
        4 => "Noe\u0308l ⛵ Moss",
        _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
    };
    public static WorldRuntime CreateGenerated(string seed, Func<string, IDecisionProvider> providerFactory,
        GridPoint? roughTownSite = null, WorldSizePreset size = WorldSizePreset.Small, int? ticksPerDay = null, int calendarOffsetTicks = 0)
    {
        var options = new GeographyOptions(seed, size);
        var world = new WorldRuntime(options.Seed, _ => new DeterministicDecisionProvider(),
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
        var preparedFounders = WorldRuntime.Restore(WithHardFounders(world.ExportState()), providerFactory);
        world.Dispose();
        world = preparedFounders;
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
                        Agents = society.Agents.Select(person => person with
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

    /// <summary>Hardens a paused founder setup without changing the game's GUID placement grammar.</summary>
    internal static WorldRuntimeState WithHardFounders(WorldRuntimeState placed)
    {
        Assert.False(placed.FounderSetup!.Started);
        var placements = placed.Agents;
        var societyBefore = placed.Society.Society;
        var hardened = societyBefore with
        {
            Agents = [],
            Relationships = [],
            Households = societyBefore.Households.Select(home => home with { MemberIds = [] }).ToArray(),
        };
        for (var index = 0; index < placements.Count; index++)
        {
            var original = societyBefore.Agents[index];
            var founder = SocietyRules.CreateFounder(FounderId(index + 1), FounderName(index + 1),
                config: hardened.Config, startingAge: societyBefore.AgeAt(original, 0));
            hardened = SocietyRules.PlaceFounder(hardened, founder, original.HouseholdId!).Checkpoint;
        }
        string ReplaceFounder(string text)
        {
            for (var index = 0; index < placements.Count; index++)
                text = text.Replace(societyBefore.Agents[index].Id, FounderId(index + 1), StringComparison.Ordinal);
            return text;
        }
        return placed with
        {
            Society = placed.Society with { Society = hardened },
            FounderSetup = placed.FounderSetup! with { FounderIds = Enumerable.Range(1, placements.Count).Select(FounderId).ToArray() },
            Agents = placed.Agents.Select(person => person with { AgentId = ReplaceFounder(person.AgentId) }).ToArray(),
            Towns = placed.Towns!.Select(town => town with
            {
                ResidentIds = town.ResidentIds.Select(ReplaceFounder).ToArray(),
                Council = town.Council is { } council ? council with
                {
                    Members = council.Members.Select(ReplaceFounder).ToArray(),
                    ElectionHistory = council.ElectionHistory.Select(election => election with
                    { Voters = election.Voters.Select(ReplaceFounder).ToArray() }).ToArray(),
                } : null,
            }).ToArray(),
            Events = placed.Events.Select(entry => entry with
            {
                Payload = JsonSerializer.Deserialize<WorldEventPayload>(
                    ReplaceFounder(JsonSerializer.Serialize(entry.Payload, JsonSerializerOptions.Web)), JsonSerializerOptions.Web)!,
            }).ToArray(),
        };
    }

    /// <summary>Keep candidate-less failed election windows consistent with a fixture's new day length.</summary>
    internal static WorldRuntimeState ReclockCandidateLessCouncilAttempts(WorldRuntimeState state, int day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        return state with
        {
            Towns = state.Towns?.Select(town =>
            {
                if (town.Council is not { } council) return town;
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
                    Council = council with
                    {
                        ElectionHistory = council.ElectionHistory.Select(election => election with
                        { DeadlineTick = election.OpenedTick + day }).ToArray(),
                    },
                };
            }).ToArray(),
        };
    }

    /// <summary>Choose a feasible site near the starting site without changing generated terrain or resources.</summary>
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
        var offered = OfferedByAgent.GetOrAdd(request.Observation.AgentId,
            _ => new ConcurrentDictionary<string, int>(StringComparer.Ordinal));
        foreach (var candidate in request.Observation.Candidates)
        {
            offered.AddOrUpdate(candidate.Id, 1, (_, count) => count + 1);
            FirstOfferedTick.TryAdd((request.Observation.AgentId, candidate.Id), request.Observation.WorldTick);
            if (candidate.Id.StartsWith("supply_workstation:", StringComparison.Ordinal) &&
                candidate.DestinationId is { } destination)
                WorkstationSupplyOffers.TryAdd((request.Observation.AgentId, destination), 0);
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
