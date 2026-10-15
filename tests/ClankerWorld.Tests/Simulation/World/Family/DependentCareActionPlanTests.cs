using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class DependentCareActionPlanTests
{
    [Fact]
    public async Task GuardianAcceptanceKeepsTheBornChildSearchAndHomeAcrossRollbackReplayAndReload()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var actor = scenario.Guardians[0];
        var child = scenario.Children[0];
        var state = FullDispatch(scenario.State);
        var chooser = new CareAuthorityChooser(actor, child, DependentCareChoiceKind.Accept, choose: true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True(world.RenameAgent(child, "Élodie🐦 " + AgentNameRules.SurnameKey(world.Society.GetAgent(child).Name)));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new CareAuthorityChooser(actor, child, DependentCareChoiceKind.Accept, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 4; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && chooser.Plan is not null && ReferenceEquals(decision.ActionPlan, chooser.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (world.Society.GetAgent(child).PrimaryCaregiverId == actor) break;
        }
        var plan = Assert.IsType<DependentCareActionPlan>(chooser.Plan);
        Assert.True(samePlan);
        Assert.True(plan.AcceptsGuardianSearch);
        Assert.Equal((child, scenario.House.InstanceId, GuardianPlacementTestFixture.DestinationHousehold),
            (plan.Choice.TargetId, plan.HouseId, plan.ActorHousehold));
        Assert.Equal(actor, world.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Single(world.ExportState().Events, item => item.Kind == "guardian_assigned" && item.Detail == child);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AnAcceptancePlanRefusesARemovedOriginalHouseWithoutTakingCareOrChangingState()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var actor = scenario.Guardians[0];
        var child = scenario.Children[0];
        var chooser = new CareAuthorityChooser(actor, child, DependentCareChoiceKind.Accept, choose: false);
        using var world = WorldRuntime.Restore(FullDispatch(scenario.State), _ => chooser);
        for (var tick = 0; tick < 4 && chooser.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<DependentCareActionPlan>(chooser.Plan);
        var result = world.RemoveBuilding(scenario.House.InstanceId, scenario.House.TownId, scenario.House.HouseholdId);
        Assert.True(result.Applied, result.Failure);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Null(world.Society.GetAgent(child).PrimaryCaregiverId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.False(reopened.CheckActionPlan(plan).Allowed);
        reopened.Validate();
    }

    [Fact]
    public async Task AnAcceptancePlanRefusesANativeReplacementOfItsSelectedHouse()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var actor = scenario.Guardians[0];
        var child = scenario.Children[0];
        var inventory = InventoryAuthority.AddLot(scenario.State.Society.Society.Inventory, "guardian-rebuild-timber",
            "wood", GuardianPlacementTestFixture.DestinationHousehold, 8);
        var state = FullDispatch(scenario.State) with
        {
            Society = scenario.State.Society with
            { Society = scenario.State.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
        };
        var chooser = new CareAuthorityChooser(actor, child, DependentCareChoiceKind.Accept, choose: false);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => chooser);
        for (var tick = 0; tick < 4 && chooser.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<DependentCareActionPlan>(chooser.Plan);
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var house = scenario.House;
        Assert.True(world.RemoveBuilding(house.InstanceId, house.TownId, house.HouseholdId).Applied);
        var rebuilt = world.PlaceBuilding(house.InstanceId, house.DefinitionId, house.Position, house.HouseholdId);
        Assert.True(rebuilt.Applied, rebuilt.Failure);
        var replacement = world.WorldSimulation.Buildings.Single(item => item.InstanceId == house.InstanceId);
        Assert.Equal((house.InstanceId, house.DefinitionId, house.Position, house.TownId, house.HouseholdId),
            (replacement.InstanceId, replacement.DefinitionId, replacement.Position, replacement.TownId, replacement.HouseholdId));
        Assert.True(replacement.PlacedTick > house.PlacedTick);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Null(world.Society.GetAgent(child).PrimaryCaregiverId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.False(reopened.CheckActionPlan(plan).Allowed);
        reopened.Validate();
    }

    [Fact]
    public async Task AWithdrawalPlanRetainsTheOriginalRelationshipAcrossCopiedReceiptsAndItsNativeEnd()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var actor = scenario.Guardians[0];
        var child = scenario.Children[0];
        using var accepting = WorldRuntime.Restore(FullDispatch(scenario.State), _ =>
            new CareAuthorityChooser(actor, child, DependentCareChoiceKind.Accept, choose: true));
        for (var tick = 0; tick < 4 && accepting.Society.GetAgent(child).PrimaryCaregiverId != actor; tick++)
            Assert.True((await accepting.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(actor, accepting.Society.GetAgent(child).PrimaryCaregiverId);
        var state = accepting.ExportState() with
        { Agents = accepting.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        var chooser = new CareAuthorityChooser(actor, child, DependentCareChoiceKind.End, choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        for (var tick = 0; tick < 4 && chooser.Plan is null; tick++) Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<DependentCareActionPlan>(chooser.Plan);
        var bytes = WorldRuntimeCodec.Encode(capture.ExportState());
        using var copied = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.True(copied.CheckActionPlan(plan).Allowed);
        var ending = new CareAuthorityChooser(actor, child, DependentCareChoiceKind.End, choose: true);
        using var ended = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes) with
        { Agents = capture.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() }, _ => ending);
        for (var tick = 0; tick < 4 && ended.Society.GetRelationship(plan.Relationship!.Id).State == SocietyRelationshipState.Accepted; tick++)
            Assert.True((await ended.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(SocietyRelationshipState.Revoked, ended.Society.GetRelationship(plan.Relationship!.Id).State);
        Assert.False(ended.CheckActionPlan(plan).Allowed);
        Assert.Single(ended.ExportState().Events, item => item.Kind == "caregiver_ended" && item.Detail == actor);
        ended.Validate();
    }

    private static WorldRuntimeState FullDispatch(WorldRuntimeState state) => state with
    { Society = state.Society with { Cognition = state.Society.Cognition with { MaxDispatchPerCycle = state.Agents.Count } } };

    private sealed class CareAuthorityChooser(string actor, string child, DependentCareChoiceKind kind, bool choose) : IDecisionProvider
    {
        public DependentCareActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is DependentCareActionPlan plan && plan.Choice.Kind == kind &&
                (kind == DependentCareChoiceKind.End ? plan.Relationship?.TargetId == child : plan.Choice.TargetId == child)) : null;
            if (candidate is not null) Plan = Assert.IsType<DependentCareActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
