using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Fact]
    public async Task APendingToolWithdrawalPreservesTheSameRequestsNativeProductionProgress()
    {
        var (state, buyer, seller, _) = Prepared();
        using var placing = Restore(state, buyer, seller, new RequestChoices("tool_request_place:"), new RequestChoices());
        await Until(placing, world => world.ToolMakingRequests.Count == 1, 96);
        using var accepting = Restore(Roundtrip(placing), buyer, seller, new RequestChoices(), new RequestChoices("tool_request_accept:"));
        await Until(accepting, world => Assert.Single(world.ToolMakingRequests).Status == ToolMakingRequestStatus.Accepted, 96);
        Assert.Null(Assert.Single(accepting.ToolMakingRequests).JobId);
        var provider = new HeldToolWithdrawalProvider();
        using var world = WorldRuntime.Restore(Roundtrip(accepting), id => id == buyer ? provider :
            id == seller ? new RequestChoices(DecisionProviderKind.Deterministic, "tool_request_work:") : new RequestChoices());
        world.SubmitInstruction(new("tool-withdrawal-guidance", "owner:test", buyer,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var plan = await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(ToolMakingRequestStatus.Accepted, plan.Original!.Status);
            Assert.Null(plan.Original.JobId);
            for (var tick = 0; tick < 160 && Assert.Single(world.ToolMakingRequests).JobId is null; tick++)
                Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var current = Assert.Single(world.ToolMakingRequests);
            Assert.NotNull(current.JobId);
            Assert.Equal(ToolMakingRequestStatus.Accepted, current.Status);
            Assert.Equal((plan.Original.Id, buyer, seller, plan.Original.RecipeId),
                (current.Id, current.RequesterId, current.WorkerId, current.RecipeId));
            var job = Assert.Single(world.WorldSimulation.ProductionJobs, item => item.JobId == current.JobId);
            Assert.Equal(WorldProductionJobState.Running, job.State);
            var before = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.True(world.CheckActionPlan(plan).Allowed);
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));

            provider.Release.TrySetResult();
            await GrownAgentHelperMemoryTests.WaitForRequests(world);
            var step = await world.AdvanceOneTickNonBlockingAsync();
            var decision = Assert.Single(step.Decisions, item => item.AgentId == buyer);
            Assert.Same(plan, decision.ActionPlan);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Null(decision.PlanRefusal);
            var withdrawn = Assert.Single(world.ToolMakingRequests);
            Assert.Equal(current with { Status = ToolMakingRequestStatus.Withdrawn, LastTransitionTick = world.WorldTick, Blocker = null }, withdrawn);
            Assert.Equal(job.JobId, Assert.Single(world.WorldSimulation.ProductionJobs).JobId);
            Assert.Equal(3, world.Society.Inventory.GetLot("request-payment").Quantity);
            Assert.Empty(world.Society.Inventory.Offers);
            before = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
            _ = Roundtrip(world);
        }
        finally { provider.Release.TrySetResult(); }
    }

    private sealed class HeldToolWithdrawalProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public TaskCompletionSource<ToolRequestConsentActionPlan> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = Assert.Single(request.Observation.Candidates, item => item.ActionPlan is ToolRequestConsentActionPlan plan &&
                plan.Choice.Kind == ToolRequestConsentKind.Withdraw);
            Started.TrySetResult(Assert.IsType<ToolRequestConsentActionPlan>(candidate.ActionPlan));
            await Release.Task.WaitAsync(cancellationToken);
            return new(request.RequestId, request.Observation.AgentId, Kind, request.ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                candidate.Id, 1, new Dictionary<string, double> { [candidate.Id] = 1 });
        }
    }
}
