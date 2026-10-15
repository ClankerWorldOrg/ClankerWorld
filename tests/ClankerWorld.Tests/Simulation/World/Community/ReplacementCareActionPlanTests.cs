using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ReplacementCareActionPlanTests
{
    [Fact]
    public async Task ReplacementCareRetainsTheBornChildPrimaryReceiptAcrossRollbackReplayAndReload()
    {
        var (state, actor, child) = PreparedReplacement();
        var chooser = new ReplacementChooser(actor, child, choose: true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True(world.RenameAgent(child, "Élodie🐦 " + AgentNameRules.SurnameKey(world.Society.GetAgent(child).Name)));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ReplacementChooser(actor, child, choose: true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ReplacementCareActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal((child, GuardianPlacementTestFixture.DestinationHousehold), (plan.Choice.ChildId, plan.HouseholdId));
        Assert.Equal(plan.Choice, ReplacementCareChoice.Decode(plan.CandidateId));
        Assert.Equal(actor, world.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Single(world.ExportState().Events, item => item.Kind == "replacement_care_accepted" && item.Detail == actor + "|" + child);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task ReplacementCareRefusesAChangedPrimaryReceiptWithoutTakingCareAgain()
    {
        var (state, actor, child) = PreparedReplacement();
        var chooser = new ReplacementChooser(actor, child, choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ReplacementCareActionPlan>(chooser.Plan);
        using var society = SocietyRuntime.Restore(capture.ExportState().Society);
        var result = society.Apply(checkpoint => SocietyRules.AcceptReplacementCare(checkpoint, actor, child));
        Assert.NotNull(result.CreatedId);
        using var changed = WorldRuntime.Restore(capture.ExportState() with { Society = society.ExportState() });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
    }

    private static (WorldRuntimeState State, string Actor, string Child) PreparedReplacement()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var state = scenario.State;
        var child = state.Society.Society.Births.Single(birth => birth.RequestId == "guardian-placement-resident-0").ChildId;
        var original = state.Society.Society.GetAgent(child).PrimaryCaregiverId;
        var actor = scenario.Guardians.Single(id => id != original);
        return (state with
        {
            Agents = state.Agents.Select(person => person with { LastDecisionContext = null }).ToArray(),
            Society = state.Society with { Cognition = state.Society.Cognition with { MaxDispatchPerCycle = state.Agents.Count } },
        }, actor, child);
    }

    private sealed class ReplacementChooser(string actor, string child, bool choose) : IDecisionProvider
    {
        public ReplacementCareActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is ReplacementCareActionPlan plan && plan.Choice.ChildId == child) : null;
            if (candidate is not null) Plan = Assert.IsType<ReplacementCareActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
