using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class BuildingExpansionTests
{
    [Fact]
    public async Task AnotherRequestForAlternativeHouseTilesStopsTheOriginalExpansionFiling()
    {
        using var prepared = PreparedWorld("first-town-house-a", out _, out var house);
        var state = prepared.ExportState();
        var adults = state.Society.Society.Agents.Where(person => person.HouseholdId == house.HouseholdId &&
            LifeStageRules.IsAdult(person.AgeBand)).Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, adults.Length);
        state = state with
        {
            HouseholdLandUseRights = state.HouseholdLandUseRights!.Where(right => right.GrantSource != "expansion_test_fixture").ToArray(),
            Agents = state.Agents.Select(person => adults.Contains(person.AgentId, StringComparer.Ordinal)
                ? person with { Position = house.Position, FullnessBasisPoints = 9_000 } : person).ToArray(),
        };
        var provider = new AlternativeHouseRequestProvider(state, house, adults[0], adults[1]);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before),
            _ => new AlternativeHouseRequestProvider(state, house, adults[0], adults[1]));

        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var expansion = provider.Plans[adults[1]];
        Assert.Equal(house, expansion.Building);
        Assert.Single(expansion.SelectedTiles!);
        Assert.DoesNotContain(provider.RequestedTile, expansion.SelectedTiles!);
        foreach (var actor in adults)
        {
            var decision = Assert.Single(step.Decisions, item => item.AgentId == actor);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
        }
        Assert.Same(expansion, Assert.Single(step.Decisions, item => item.AgentId == adults[1]).ActionPlan);
        var first = Assert.Single(world.HouseholdLandUseRequests, request => request.RequestedByAgentId == adults[0]);
        Assert.Equal(new[] { provider.RequestedTile }, first.Tiles);
        Assert.Equal(house.HouseholdId, first.HouseholdId);
        Assert.Equal("pending", first.Status);
        Assert.NotNull(first.CouncilProposalId);
        Assert.Single(world.HouseholdLandUseRequests);
        Assert.Single(step.Events, item => item.Kind == "choice_refused");
        Assert.Equal(state.HouseholdLandUseRights, world.HouseholdLandUseRights);
        Assert.Equal(house, Assert.Single(world.WorldSimulation.Buildings, item => item.InstanceId == house.InstanceId));
        var after = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(expansion).Refusal!.Reason);
        Assert.Equal(after, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(after, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after));
        Assert.Equal(after, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Validate();
    }

    private sealed class AlternativeHouseRequestProvider(WorldRuntimeState state, PlacedBuilding house,
        string filer, string expander) : IDecisionProvider
    {
        public ConcurrentDictionary<string, HouseholdLandFilingActionPlan> Plans { get; } = new(StringComparer.Ordinal);
        public GridPoint RequestedTile { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var actor = request.Observation.AgentId;
            CognitionCandidate? candidate = null;
            IReadOnlyList<CognitionLandTile>? tiles = null;
            if (actor == filer || actor == expander)
            {
                var offered = Assert.Single(request.Observation.Candidates, item => item.ActionPlan is HouseholdLandFilingActionPlan
                { Building: { } building } && building.InstanceId == house.InstanceId);
                var plan = Assert.IsType<HouseholdLandFilingActionPlan>(offered.ActionPlan);
                Plans[actor] = plan;
                if (actor == expander) candidate = offered;
                else
                {
                    RequestedTile = new[]
                    {
                        new GridPoint(house.Position.X, house.Position.Y - 1), new GridPoint(house.Position.X, house.Position.Y + 1),
                        new GridPoint(house.Position.X - 1, house.Position.Y), new GridPoint(house.Position.X + 1, house.Position.Y),
                    }.First(tile => tile != house.Entrance && !plan.SelectedTiles!.Contains(tile) && state.Map.IsLand(tile) &&
                        state.TownLandTitles!.Any(title => title.TownId == house.TownId && title.Tiles.Contains(tile)) &&
                        !state.HouseholdLandUseRights!.Any(right => right.HouseholdId == house.HouseholdId && right.Tiles.Contains(tile)));
                    candidate = Assert.Single(request.Observation.Candidates, item => item.ActionPlan is HouseholdLandFilingActionPlan
                    { Choice.BuildingId: null } filing && filing.Choice.TownId == house.TownId);
                    tiles = [new(RequestedTile.X, RequestedTile.Y)];
                }
            }
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, actor, Kind, request.ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }, CivicLandTiles: tiles));
        }
    }
}
