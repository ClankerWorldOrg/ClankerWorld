using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class GuardianTravelActionPlanTests
{
    [Fact]
    public async Task AGuardianJourneyKeepsItsChildCareAndHomeAcrossNativeRollbackReplayAndReload()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var actor = scenario.Guardians[0];
        var child = scenario.Children[0];
        using var accepting = GuardianPlacementTestFixture.Restore(scenario.State, GuardianPlacementTestFixture.Accepting(scenario));
        await GuardianPlacementTestFixture.Accepted(accepting, scenario);
        var state = accepting.ExportState() with
        { Agents = accepting.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        state = state with
        { Society = state.Society with { Cognition = state.Society.Cognition with { MaxDispatchPerCycle = state.Agents.Count } } };
        var chooser = new TravelChooser(actor, child);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new TravelChooser(actor, child));
        var samePlan = false;
        for (var tick = 0; tick < 4; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && chooser.Plan is not null && ReferenceEquals(decision.ActionPlan, chooser.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (chooser.Plan is not null) break;
        }
        var plan = Assert.IsType<GuardianTravelActionPlan>(chooser.Plan);
        Assert.True(samePlan);
        Assert.Equal((child, actor, scenario.House.InstanceId), (plan.ChildId, plan.Placement.CaregiverId, plan.Placement.HouseId));
        Assert.Equal(plan.Placement.CareRelationshipId, world.Society.Relationships.Single(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted).Id);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task ATravelPlanRefusesItsRemovedHomeWithoutMovingOrChangingMembership()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var actor = scenario.Guardians[0];
        var child = scenario.Children[0];
        using var accepting = GuardianPlacementTestFixture.Restore(scenario.State, GuardianPlacementTestFixture.Accepting(scenario));
        await GuardianPlacementTestFixture.Accepted(accepting, scenario);
        var chooser = new TravelChooser(actor, child);
        var state = accepting.ExportState() with
        { Agents = accepting.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        state = state with
        { Society = state.Society with { Cognition = state.Society.Cognition with { MaxDispatchPerCycle = state.Agents.Count } } };
        using var world = WorldRuntime.Restore(state, _ => chooser);
        for (var tick = 0; tick < 4 && chooser.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<GuardianTravelActionPlan>(chooser.Plan);
        var removed = world.RemoveBuilding(scenario.House.InstanceId, scenario.House.TownId, scenario.House.HouseholdId);
        Assert.True(removed.Applied, removed.Failure);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(GuardianPlacementTestFixture.SourceHousehold, world.Society.GetAgent(child).HouseholdId);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "guardian_placement_completed");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.False(reopened.CheckActionPlan(plan).Allowed);
        reopened.Validate();
    }

    private sealed class TravelChooser(string actor, string child) : IDecisionProvider
    {
        public GuardianTravelActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is GuardianTravelActionPlan plan && plan.ChildId == child && plan.Choice.Kind == GuardianTravelKind.Relocate) : null;
            if (candidate is not null) Plan = Assert.IsType<GuardianTravelActionPlan>(candidate.ActionPlan);
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
