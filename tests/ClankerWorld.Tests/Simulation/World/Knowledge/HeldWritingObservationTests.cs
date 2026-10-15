using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class OrchardObservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AHeldOriginalWritingChoiceRequiresItsRecordedFactVersion(bool refresh)
    {
        var (state, actor, target, _) = await ObserveEmptyTile();
        var known = Assert.Single(state.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == target);
        var provider = new HeldOriginalWriting();
        using var world = WorldRuntime.Restore(WithPaper(state, actor), id => id == actor ? provider : new Idle());
        RequestWriting(world, actor, "held-original-observation");
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var plan = await provider.Captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(actor, plan.Actor);
            Assert.Equal("knowledge_write:field_record", plan.CandidateId);
            Assert.Null(plan.Source);
            Assert.Null(plan.Project);
            Assert.Null(plan.Pickup);
            Assert.Equal(known, Assert.Single(plan.Facts));
            Assert.True(world.CheckActionPlan(plan).Allowed);
            Assert.Empty(world.Knowledge.Artifacts);
            Assert.Empty(world.Knowledge.WritingProjects);
            if (refresh)
            {
                // The public native planting boundary consumes the real seed and
                // records the planter's new observation, as in the orchard tests.
                var planted = world.PlantTree(actor, TreeGrowthRules.Orchard, "orchard-observation-seed", target);
                Assert.True(planted.Planted, planted.Message);
                Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "orchard-observation-seed");
                var current = Assert.Single(world.Knowledge.Facts, fact => fact.OwnerId == actor && fact.Position == target);
                Assert.Equal(known.Id, current.Id);
                Assert.Contains("fruit", current.ResourceKinds);
                Assert.NotEqual(known, current);
                Assert.Empty(world.Knowledge.EarlierFacts);
            }
            world.Validate();
            AssertReload(world.ExportState());
            provider.Release.TrySetResult(true);
            await provider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(30));
            SocietyCognitionDispatchResult? decision = null;
            for (var tick = 0; tick < 32 && decision is null; tick++)
            {
                var step = await world.AdvanceOneTickNonBlockingAsync();
                Assert.True(step.Advanced);
                decision = step.Decisions.SingleOrDefault(item => item.AgentId == actor && item.ActionPlan == plan);
                if (decision is null) await Task.Yield();
            }
            Assert.NotNull(decision);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
            world.Validate();
            AssertReload(world.ExportState());
            if (refresh)
            {
                Assert.NotNull(decision.PlanRefusal);
                Assert.Equal(ActionRefusalReason.TargetUnavailable, decision.PlanRefusal!.Reason);
                Assert.Empty(world.Knowledge.WritingProjects);
                Assert.Equal(1, world.Society.Inventory.GetLot("orchard-record-paper").Quantity);
            }
            else
            {
                Assert.Null(decision.PlanRefusal);
                Assert.Equal(plan.Facts, Assert.Single(world.Knowledge.WritingProjects).Facts);
            }
        }
        finally { provider.Release.TrySetResult(true); }
    }

    private sealed class HeldOriginalWriting : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public TaskCompletionSource<KnowledgeWritingActionPlan> Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "knowledge_write:field_record");
            if (selected?.ActionPlan is KnowledgeWritingActionPlan plan && !Captured.Task.IsCompleted)
            {
                Captured.TrySetResult(plan);
                await Release.Task.WaitAsync(cancellationToken);
            }
            else selected = request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            var response = new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected.Id, 1d, request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal));
            Returned.TrySetResult(true);
            return response;
        }
    }
}
