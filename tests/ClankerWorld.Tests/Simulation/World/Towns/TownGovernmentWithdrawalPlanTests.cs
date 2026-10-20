using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownGovernmentRuntimeTests
{
    [Fact]
    public async Task AQueuedGovernmentWithdrawalKeepsItsProposalWhenAnEarlierWithdrawalOpensItsVote()
    {
        using var initial = NewWorld(_ => new ActionCoverageRecorder(chooseIdle: true), removeFoundingOffice: true);
        var state = initial.ExportState();
        var town = state.Towns![0];
        var adults = town.ResidentIds;
        var targets = TownArrangementRules.Supported.Where(target => target != town.Government!.Arrangement).Take(2).ToArray();
        Assert.Equal(2, targets.Length);
        var first = TownGovernmentRules.Propose(town.Council!, town.Government!, town.Id, adults[0], targets[0], false,
            adults, initial.WorldTick, Day);
        var both = TownGovernmentRules.Propose(first.Council, first.Government, town.Id, adults[1], targets[1], false,
            adults, initial.WorldTick, Day);
        var changes = both.Government.Changes.TakeLast(2).ToArray();
        Assert.Equal("voting", changes[0].Status);
        Assert.Equal("queued", changes[1].Status);
        var council = both.Council;
        foreach (var actor in adults.Take(2))
            council = TownCouncilRules.LearnNotices(council, actor, council.Notices.Select(notice => notice.Id), initial.WorldTick);
        state = state with { Towns = [town with { Council = council, Government = both.Government }] };
        var responses = adults.Take(2).Select((actor, index) => (actor, changes[index].Id))
            .ToDictionary(item => item.actor, item => item.Id, StringComparer.Ordinal);
        var provider = new GovernmentWithdrawalProvider(responses);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before), _ => new GovernmentWithdrawalProvider(responses));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var queuedPlan = provider.Plans[adults[1]];
        Assert.Equal("queued", queuedPlan.Change!.Status);
        Assert.Equal(changes[1].Id, queuedPlan.Change.Id);
        foreach (var actor in adults.Take(2))
        {
            var decision = Assert.Single(step.Decisions, decision => decision.AgentId == actor);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
            Assert.Same(provider.Plans[actor], decision.ActionPlan);
        }
        var current = world.Towns[0].Government!.Changes.TakeLast(2).ToArray();
        Assert.All(current, change => Assert.Equal("withdrawn", change.Status));
        Assert.Equal(world.WorldTick, current[1].OpenedTick);
        Assert.Equal(world.WorldTick + Day, current[1].DeadlineTick);
        Assert.Equal(adults, current[1].OpeningVoters);
        Assert.Equal(changes[1].SubmittedTick, current[1].SubmittedTick);
        Assert.Equal(changes[1].RequestKey, current[1].RequestKey);
        Assert.Equal(changes[1].Target, current[1].Target);
        Assert.DoesNotContain(step.Events, item => item.Kind == "choice_refused");
        var after = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(queuedPlan).Refusal!.Reason);
        Assert.Equal(after, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(after, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after));
        Assert.Equal(after, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Validate();
    }

    private sealed class GovernmentWithdrawalProvider(IReadOnlyDictionary<string, string> responses) : IDecisionProvider
    {
        public ConcurrentDictionary<string, GovernmentProposalActionPlan> Plans { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = responses.TryGetValue(request.Observation.AgentId, out var proposal)
                ? request.Observation.Candidates.FirstOrDefault(candidate => candidate.ActionPlan is GovernmentProposalActionPlan
                { Choice.Kind: GovernmentProposalKind.Withdraw } plan && plan.Change!.Id == proposal) : null;
            if (candidate is not null) Plans[request.Observation.AgentId] = Assert.IsType<GovernmentProposalActionPlan>(candidate.ActionPlan);
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
