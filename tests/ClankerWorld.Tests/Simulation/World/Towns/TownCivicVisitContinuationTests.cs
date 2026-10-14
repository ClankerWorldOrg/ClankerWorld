using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownCivicVisitContinuationTests
{
    [Fact]
    public async Task OnePersonalVisitChoiceWalksToTheNoticePlaceWithoutRepeatingCivicActsOrModelCalls()
    {
        using var source = NormalPathWorld.CreateGenerated("civic-visit-continuation", _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = source.ExportState();
        var town = initial.Towns![0];
        var visitor = town.ResidentIds[0];
        Assert.True(source.RenameAgent(visitor, "Notice visitor 🧭 Élodie"));
        initial = source.ExportState();
        town = initial.Towns![0];
        var board = town.OriginSite!.Value;
        var start = ReachableDistantPosition(initial, visitor, board);
        var council = TownCouncilRules.SubmitProposal(town.Council!, town.Id, town.ResidentIds[1],
            "law", null, "Post harvest dates for the Town.", "same", town.ResidentIds, 0,
            initial.WorldSystems!.Config.TicksPerDay);
        var provider = new VisitOnceProvider();
        using var world = WorldRuntime.Restore(initial with
        {
            Towns = [town with { Council = council }],
            Agents = initial.Agents.Select(person => person with
            {
                Position = person.AgentId == visitor ? start : person.Position,
                FullnessBasisPoints = 8_000,
                TravelCooldownTicks = 0,
            }).ToArray(),
        }, id => id == visitor ? provider : new ActionCoverageRecorder(chooseIdle: true));

        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        provider.Observations.Clear();
        provider.Reset();
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == visitor ? new VisitOnceProvider() : new ActionCoverageRecorder(chooseIdle: true));
        var first = await world.AdvanceOneTickAsync();
        Assert.True(first.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var decision = Assert.Single(first.Decisions, result => result.AgentId == visitor);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        var firstObservation = Assert.Single(provider.Observations);
        Assert.Contains(firstObservation.Candidates, candidate => candidate.Id == provider.VisitId);
        var plan = Assert.IsType<CivicVisitActionPlan>(firstObservation.Candidates.Single(candidate => candidate.Id == provider.VisitId).ActionPlan);
        Assert.Same(plan, decision.ActionPlan);
        Assert.Equal(board, plan.NoticePlace);
        Assert.NotEmpty(plan.Route);
        Assert.Equal(plan.Choice, CivicVisitChoice.Decode(plan.CandidateId));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        Assert.Contains(visitor, world.Towns[0].ResidentIds);
        Assert.True(world.ExportState().Map.FootDistance(start, board) >= 6);
        Assert.Equal(1, VisitActionCount(world.ExportState(), town.Id, visitor));
        AssertNoFormalCivicChanges(world, council);

        var positions = new HashSet<GridPoint> { start, Position(world, visitor) };
        // This reachable short trip fits within the ordinary 30-tick decision
        // interval. Every later step therefore belongs to the admitted visit.
        for (var tick = 0; tick < 28 && world.ExportState().Map.FootDistance(Position(world, visitor), board) > 1; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            positions.Add(Position(world, visitor));
            Assert.Single(provider.Observations);
            Assert.Equal(1, VisitActionCount(world.ExportState(), town.Id, visitor));
            AssertNoFormalCivicChanges(world, council);
        }

        var arrived = Position(world, visitor);
        Assert.True(world.ExportState().Map.FootDistance(arrived, board) <= 1);
        Assert.True(positions.Count > 2, "The visitor must take local walking steps after the first personal choice.");
        var arrivedBytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(arrivedBytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(arrivedBytes, WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(arrivedBytes)));

        // Arrival removes visit from the offered choices and prompts an
        // ordinary fresh choice. Choosing idle must not read or vote for them.
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(2, provider.Observations.Count);
        var atBoard = provider.Observations.Last();
        Assert.DoesNotContain(atBoard.Candidates, candidate => candidate.Id == provider.VisitId);
        Assert.Contains(atBoard.Candidates, candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal));
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(arrived, Position(world, visitor));
            Assert.Equal(1, VisitActionCount(world.ExportState(), town.Id, visitor));
            AssertNoFormalCivicChanges(world, council);
        }
        world.Validate();
    }

    private static GridPoint Position(WorldRuntime world, string visitor) =>
        world.ExportState().Agents.Single(person => person.AgentId == visitor).Position;

    private static int VisitActionCount(WorldRuntimeState state, string town, string visitor) =>
        state.Events.Count(item => item.Kind == "town_civic_action" && item.Detail == $"{town}|{visitor}|visit");

    private static void AssertNoFormalCivicChanges(WorldRuntime world, TownCouncilState original)
    {
        var current = world.Towns[0].Council!;
        Assert.Empty(current.Knowledge);
        Assert.Empty(current.Candidates);
        Assert.Null(current.Election);
        Assert.Equal(original.ElectionHistory, current.ElectionHistory);
        Assert.Equal(original.Notices, current.Notices);
        var proposal = Assert.Single(current.Proposals);
        Assert.Equal(original.Proposals[0], proposal);
        Assert.Empty(proposal.Votes);
        Assert.Equal("pending", proposal.Status);
    }

    private static GridPoint ReachableDistantPosition(WorldRuntimeState state, string visitor, GridPoint board)
    {
        var occupied = state.Agents.Where(person => person.AgentId != visitor)
            .Select(person => person.Position).ToHashSet();
        foreach (var point in state.Map.Tiles.Select(tile => tile.Position)
                     .Where(point => state.Map.IsBuildable(point) && !occupied.Contains(point) &&
                         state.Map.FootDistance(point, board) is >= 6 and <= 9 &&
                         !state.Map.Resources.Any(resource => resource.Position == point) &&
                         !state.Map.MapObjects.Any(item => item.Position == point))
                     .OrderBy(point => state.Map.FootDistance(point, board)).ThenBy(point => point.Y).ThenBy(point => point.X))
        {
            if (!DeterministicRouteFinder.TryFind(state.Map, point, board, out var route) ||
                route.Any(occupied.Contains)) continue;
            var steps = route.Zip(route.Skip(1)).ToArray();
            if (steps.Any(step => state.Map.IsDiagonalFootStep(step.First, step.Second) &&
                    (occupied.Contains(new GridPoint(step.Second.X, step.First.Y)) ||
                     occupied.Contains(new GridPoint(step.First.X, step.Second.Y))))) continue;
            if (steps.Sum(step => (state.Map.FootStepCost(step.First, step.Second) + 99) / 100) <= 20)
                return point;
        }
        throw new InvalidOperationException("The generated fixture needs a short unobstructed walk to the Town notice place.");
    }

    private sealed class VisitOnceProvider : IDecisionProvider
    {
        public ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public string? VisitId { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public void Reset() => VisitId = null;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Observations.Enqueue(observation);
            var selected = VisitId is null
                ? observation.Candidates.Single(candidate => candidate.Id.Contains("|visit|", StringComparison.Ordinal))
                : observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            VisitId ??= selected.Id;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId,
                Kind, ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                selected.Id, 1, observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
