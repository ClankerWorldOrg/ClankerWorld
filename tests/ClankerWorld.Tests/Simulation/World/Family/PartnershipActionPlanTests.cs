using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class FamilyTests
{
    [Theory]
    [InlineData(PartnershipChoiceKind.Accept, SocietyRelationshipState.Accepted)]
    [InlineData(PartnershipChoiceKind.Refuse, SocietyRelationshipState.Rejected)]
    public async Task APartnershipResponseRetainsItsConsentReceiptAcrossRollbackReplayAndReload(
        PartnershipChoiceKind kind, SocietyRelationshipState expected)
    {
        var state = await RequestedPartnershipState();
        var proposal = Assert.Single(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
        var chooser = new PartnershipPlanChooser(proposal.TargetId, kind, choose: true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new PartnershipPlanChooser(proposal.TargetId, kind, choose: true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<PartnershipActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == proposal.TargetId).ActionPlan);
        Assert.True(RelationshipPlan.Matches(plan.Original, proposal));
        Assert.Equal(plan.Choice, PartnershipChoice.Decode(plan.CandidateId));
        Assert.Equal(expected, world.Society.GetRelationship(proposal.Id).State);
        Assert.Empty(world.Society.Births);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task APartnershipResponseRefusesAWithdrawnReceiptWithoutChangingTheWorld()
    {
        var state = await RequestedPartnershipState();
        var proposal = Assert.Single(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
        var chooser = new PartnershipPlanChooser(proposal.TargetId, PartnershipChoiceKind.Accept, choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<PartnershipActionPlan>(chooser.Plan);
        using var society = SocietyRuntime.Restore(capture.ExportState().Society);
        society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, proposal.Id, proposal.ProposerId));
        using var changed = WorldRuntime.Restore(capture.ExportState() with { Society = society.ExportState() });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
    }

    private static async Task<WorldRuntimeState> RequestedPartnershipState()
    {
        var state = await PreparedState();
        var proposer = state.Agents[0].AgentId;
        using var requested = WorldRuntime.Restore(state, id => new PrefixChoiceProvider(id == proposer ? "partner_propose:" : "safe_idle"));
        Assert.True((await requested.AdvanceOneTickAsync()).Advanced);
        return requested.ExportState() with
        { Agents = requested.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
    }

    private sealed class PartnershipPlanChooser(string actor, PartnershipChoiceKind kind, bool choose) : IDecisionProvider
    {
        public PartnershipActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is PartnershipActionPlan plan && plan.Choice.Kind == kind) : null;
            if (candidate is not null) Plan = Assert.IsType<PartnershipActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
