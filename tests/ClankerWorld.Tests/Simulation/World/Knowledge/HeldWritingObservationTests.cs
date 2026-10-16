using ClankerWorld.Simulation.Cognition;
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
        var provider = new ScriptedProvider();
        var held = provider.Hold();
        using var world = WorldRuntime.Restore(WithPaper(state, actor), id => id == actor ? provider : new Idle());
        var harness = ModelWorkHarness.For(world);
        RequestWriting(world, actor, "held-original-observation");
        try
        {
            Assert.True((await harness.TickAsync()).Advanced);
            Assert.True(held.IsEntered);
            var plan = Assert.IsType<KnowledgeWritingActionPlan>(Assert.Single(held.Request.Observation.Candidates,
                candidate => candidate.Id == "knowledge_write:field_record").ActionPlan);
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
            held.Reply(plan.CandidateId);
            await harness.WaitForDecisionAsync(actor);
            var step = await harness.TickAsync();
            Assert.True(step.Advanced);
            var decision = Assert.Single(step.Decisions, item => item.AgentId == actor && item.ActionPlan == plan);
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
        finally
        {
            if (held.IsEntered) held.Reply("safe_idle");
        }
    }
}
