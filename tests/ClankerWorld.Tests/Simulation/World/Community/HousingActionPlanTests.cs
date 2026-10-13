using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HouseholdJoinRequestTests
{
    [Theory]
    [InlineData(HousingChoiceKind.Admit)]
    [InlineData(HousingChoiceKind.Refuse)]
    public async Task AHousingResponseRetainsItsRequestAndElectorateAcrossRollbackReplayAndReload(HousingChoiceKind kind)
    {
        var (state, applicant) = await RequestedHousingState();
        var chooser = new HousingPlanChooser(kind);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        chooser.Plans.Clear();
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new HousingPlanChooser(kind));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.NotEmpty(chooser.Plans);
        foreach (var plan in chooser.Plans)
        {
            Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == plan.Actor).ActionPlan);
            Assert.Equal(plan.Choice, HousingChoice.Decode(plan.CandidateId));
            Assert.Equal((applicant, Alpha), (plan.ApplicantId, plan.HouseholdId));
            Assert.Equal(state.Agents.Single(person => person.AgentId == applicant).Housing!.Request!.Members, plan.AdultMembers);
        }
        Assert.Null(Housing(world, applicant)?.Request);
        Assert.Equal(kind == HousingChoiceKind.Admit ? Alpha : null, world.Society.GetAgent(applicant).HouseholdId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AHousingResponseRefusesAReplacedRequestWithoutRecordingConsent()
    {
        var (state, applicant) = await RequestedHousingState();
        var chooser = new HousingPlanChooser(HousingChoiceKind.Admit, choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HousingActionPlan>(chooser.Plans.First());
        var changedState = WithHousing(capture.ExportState(), applicant, housing => housing with
        { Request = housing.Request! with { RequestedTick = capture.WorldTick, ExpiryTick = capture.WorldTick + WorldRuntime.HousingRequestTicks } });
        using var changed = WorldRuntime.Restore(changedState);
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Empty(Housing(changed, applicant)!.Request!.Approvals);
    }

    private static async Task<(WorldRuntimeState State, string Applicant)> RequestedHousingState()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-plan-café-🌲", _ => provider);
        const string applicant = "agent:dddddddddddddddddddddddddddddddd";
        Assert.Null(world.AddAgent(applicant, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[applicant] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request is not null);
        return (world.ExportState() with
        { Agents = world.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() }, applicant);
    }

    private sealed class HousingPlanChooser(HousingChoiceKind kind, bool choose = true) : IDecisionProvider
    {
        public List<HousingActionPlan> Plans { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is HousingActionPlan plan && plan.Choice.Kind == kind);
            if (candidate is not null) Plans.Add(Assert.IsType<HousingActionPlan>(candidate.ActionPlan));
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
