using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Theory]
    [InlineData("initiator")]
    [InlineData("acceptor")]
    public async Task AParentAcceptanceRetainsItsRequestCaregiverAndHomeAcrossRollbackReplayAndReload(string selector)
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        using var requested = WorldRuntime.Restore(state, id => new ParentProvider(id == first ? "parent_propose:" : "safe_idle"));
        Assert.True((await requested.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("requested", requested.Agents.Single(person => person.AgentId == first).Parenthood!.Stage);
        state = requested.ExportState() with
        { Agents = requested.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        var chooser = new ParentPlanChooser(second, ParenthoodChoiceKind.Accept, selector, choose: true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ParentPlanChooser(second, ParenthoodChoiceKind.Accept, selector, choose: true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ParenthoodActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == second).ActionPlan);
        Assert.Equal((first, second, selector == "initiator" ? first : second), (plan.OwnerId, plan.PartnerId, plan.CaregiverId));
        Assert.Equal(plan.Choice, ParenthoodChoice.Decode(plan.CandidateId));
        var preparing = world.Agents.Single(person => person.AgentId == first).Parenthood!;
        Assert.Equal(("preparing", plan.CaregiverId, plan.IntendedHouseholdId),
            (preparing.Stage, preparing.PrimaryCaregiverId, preparing.IntendedHouseholdId));
        Assert.Empty(world.Society.Births);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AParenthoodPlanRefusesAChangedRequestWithoutMutatingTheNewPlan()
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        using var requested = WorldRuntime.Restore(state, id => new ParentProvider(id == first ? "parent_propose:" : "safe_idle"));
        Assert.True((await requested.AdvanceOneTickAsync()).Advanced);
        state = requested.ExportState() with
        { Agents = requested.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        var chooser = new ParentPlanChooser(second, ParenthoodChoiceKind.Accept, "initiator", choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ParenthoodActionPlan>(chooser.Plan);
        using var changed = WorldRuntime.Restore(capture.ExportState() with
        {
            Agents = capture.Agents.Select(person => person.AgentId == first ? person with
            { Parenthood = person.Parenthood! with { Stage = "postponed", LastTransitionTick = capture.WorldTick } } : person).ToArray(),
        });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Empty(changed.Society.Births);
    }

    private sealed class ParentPlanChooser(string actor, ParenthoodChoiceKind kind, string? selector, bool choose) : IDecisionProvider
    {
        public ParenthoodActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is ParenthoodActionPlan plan && plan.Choice.Kind == kind && plan.Choice.Selector == selector) : null;
            if (candidate is not null) Plan = Assert.IsType<ParenthoodActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
