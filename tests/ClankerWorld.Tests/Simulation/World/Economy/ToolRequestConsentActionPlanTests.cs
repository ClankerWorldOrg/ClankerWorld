using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Theory]
    [InlineData(ToolRequestConsentKind.Accept, ToolMakingRequestStatus.Accepted)]
    [InlineData(ToolRequestConsentKind.Refuse, ToolMakingRequestStatus.Refused)]
    [InlineData(ToolRequestConsentKind.Withdraw, ToolMakingRequestStatus.Withdrawn)]
    public async Task ToolRequestConsentRetainsItsRequestSmithAndRecipeAcrossRollbackReplayAndReload(
        ToolRequestConsentKind kind, ToolMakingRequestStatus expected)
    {
        var (state, buyer, seller, shop) = Prepared();
        using var placing = Restore(state, buyer, seller, new RequestChoices("tool_request_place:"), new RequestChoices());
        await Until(placing, world => world.ToolMakingRequests.Count == 1, 96);
        state = Roundtrip(placing) with
        { Agents = placing.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        var actor = kind == ToolRequestConsentKind.Withdraw ? buyer : seller;
        var chooser = new ToolPlanChooser<ToolRequestConsentActionPlan>(actor,
            item => item.ActionPlan is ToolRequestConsentActionPlan selectedPlan && selectedPlan.Choice.Kind == kind,
            kind: DecisionProviderKind.LargeLanguageModel);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ToolPlanChooser<ToolRequestConsentActionPlan>(actor,
            item => item.ActionPlan is ToolRequestConsentActionPlan selectedPlan && selectedPlan.Choice.Kind == kind,
            kind: DecisionProviderKind.LargeLanguageModel));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ToolRequestConsentActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal(plan.Choice, ToolRequestConsentChoice.Decode(plan.CandidateId));
        Assert.Equal((shop.InstanceId, plan.Original!.RecipeId), (plan.Shop.InstanceId, plan.Recipe.CanonicalId));
        Assert.Equal(expected, Assert.Single(world.ToolMakingRequests).Status);
        Assert.Empty(world.Society.Inventory.Offers);
        Assert.Equal(3, world.Society.Inventory.GetLot("smith-input").Quantity);
        _ = Roundtrip(world);
    }

    [Fact]
    public async Task ToolRequestAcceptanceRefusesAWithdrawnRequestWithoutTakingMaterials()
    {
        var (state, buyer, seller, _) = Prepared();
        using var placing = Restore(state, buyer, seller, new RequestChoices("tool_request_place:"), new RequestChoices());
        await Until(placing, world => world.ToolMakingRequests.Count == 1, 96);
        state = Roundtrip(placing) with
        { Agents = placing.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        var chooser = new ToolPlanChooser<ToolRequestConsentActionPlan>(seller,
            item => item.ActionPlan is ToolRequestConsentActionPlan selectedPlan && selectedPlan.Choice.Kind == ToolRequestConsentKind.Accept,
            choose: false, kind: DecisionProviderKind.LargeLanguageModel);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ToolRequestConsentActionPlan>(chooser.Plan);
        var captured = capture.ExportState();
        using var changed = WorldRuntime.Restore(captured with
        {
            ToolMakingRequests = captured.ToolMakingRequests!.Select(request => request.Id == plan.Original!.Id
                ? request with { Status = ToolMakingRequestStatus.Withdrawn, LastTransitionTick = capture.WorldTick } : request).ToArray(),
        });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Equal(3, changed.Society.Inventory.GetLot("smith-input").Quantity);
        Assert.Empty(changed.WorldSimulation.ProductionJobs);
    }

    private sealed class ToolPlanChooser<TPlan>(string actor, Func<CognitionCandidate, bool> matches,
        bool choose = true, DecisionProviderKind kind = DecisionProviderKind.Deterministic) : IDecisionProvider
        where TPlan : ActionPlan
    {
        public TPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => kind;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(matches) : null;
            if (candidate is not null) Plan = Assert.IsType<TPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
