using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownGovernmentRuntimeTests
{
    [Fact]
    public async Task AcceptingAddedDutiesKeepsAnotherResidentsOriginalProtectedVote()
    {
        var state = WorldRuntimeCodec.Decode(await ElectedMayorCheckpoint.Value);
        var town = state.Towns![0];
        var adults = town.ResidentIds;
        var council = town.Council!;
        var government = town.Government!;
        var office = Assert.Single(government.Offices, item => item.Mandates == "land");
        (council, government) = TownGovernmentRules.Propose(council, government, town.Id, adults[1],
            government.Arrangement with { NonLand = TownArrangementRules.Mayor }, false, adults, state.Society.Society.WorldTick, Day,
            "land", office.ElectionId);
        var change = government.Changes[^1];
        Assert.Equal("voting", change.Status);
        Assert.Null(change.NonLandExtension!.ConsentTick);
        foreach (var actor in adults.Take(2))
            council = TownCouncilRules.LearnNotices(council, actor, council.Notices.Select(notice => notice.Id), state.Society.Society.WorldTick);
        state = state with { Towns = [town with { Council = council, Government = government }] };
        var provider = new GovernmentConsentVoteProvider(adults[0], adults[1], change.Id);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before),
            _ => new GovernmentConsentVoteProvider(adults[0], adults[1], change.Id));

        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = provider.Plans[adults[1]];
        Assert.Equal(change.Id, plan.Change!.Id);
        Assert.Null(plan.Change.NonLandExtension!.ConsentTick);
        Assert.Contains("|government_accept_non_land|", provider.Selections[adults[0]], StringComparison.Ordinal);
        foreach (var actor in adults.Take(2))
        {
            var decision = Assert.Single(step.Decisions, item => item.AgentId == actor);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
        }
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == adults[1]).ActionPlan);
        var current = world.Towns[0].Government!;
        var voted = current.Changes.Single(item => item.Id == change.Id);
        Assert.Equal(world.WorldTick, voted.NonLandExtension!.ConsentTick);
        var vote = Assert.Single(voted.Votes);
        Assert.Equal(adults[1], vote.AgentId);
        Assert.True(vote.Yes);
        Assert.Equal(change.NonLandExtension with { ConsentTick = world.WorldTick }, voted.NonLandExtension);
        Assert.Equal(change.OpenedTick, voted.OpenedTick);
        Assert.Equal(change.DeadlineTick, voted.DeadlineTick);
        Assert.Equal(change.OpeningVoters, voted.OpeningVoters);
        Assert.Equal("voting", voted.Status);
        Assert.Equal(office, Assert.Single(current.Offices, item => item.Mandates == "land"));
        Assert.Equal(TownArrangementRules.NoOffice, current.Arrangement.NonLand);
        Assert.DoesNotContain(step.Events, item => item.Kind == "choice_refused");
        var after = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(after, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(after, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after));
        Assert.Equal(after, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Validate();
    }

    private sealed class GovernmentConsentVoteProvider(string holder, string voter, string proposal) : IDecisionProvider
    {
        public ConcurrentDictionary<string, GovernmentProposalActionPlan> Plans { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, string> Selections { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var actor = request.Observation.AgentId;
            var candidate = actor == holder
                ? request.Observation.Candidates.FirstOrDefault(item => item.Id.Contains("|government_accept_non_land|", StringComparison.Ordinal) &&
                    item.Id.EndsWith("|" + proposal + "|", StringComparison.Ordinal))
                : actor == voter ? request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is GovernmentProposalActionPlan
                { Choice.Kind: GovernmentProposalKind.Yes } plan && plan.Change!.Id == proposal) : null;
            if (candidate?.ActionPlan is GovernmentProposalActionPlan plan) Plans[actor] = plan;
            var selected = candidate?.Id ?? "safe_idle";
            Selections[actor] = selected;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, actor, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
