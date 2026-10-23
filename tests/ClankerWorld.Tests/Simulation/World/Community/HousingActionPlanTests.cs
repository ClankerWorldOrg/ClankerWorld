using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
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

    [Fact]
    public async Task AHousingResponseRefusesARebuiltHouseWithTheSameIdentity()
    {
        var (state, applicant) = await RequestedHousingState();
        var house = state.WorldSimulation!.Buildings.Single(item => item.HouseholdId == Alpha &&
            item.InstanceId == "first-town-house-a");
        var stored = state.Society.Society.Inventory.Lots.Where(lot =>
            lot.StorageBuildingId == house.InstanceId || lot.DeliveryBuildingId == house.InstanceId)
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => !stored.Contains(lot.Id) &&
                (lot.ContainerLotId is null || !stored.Contains(lot.ContainerLotId))).ToArray(),
            Reservations = state.Society.Society.Inventory.Reservations
                .Where(reservation => !stored.Contains(reservation.LotId)).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "housing-rebuild-timber", "wood", Alpha, 8);
        state = FarmFieldTests.WithInventory(state, inventory);
        var chooser = new HousingPlanChooser(HousingChoiceKind.Admit, choose: false);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HousingActionPlan>(chooser.Plans.First());
        Assert.True(world.CheckActionPlan(plan).Allowed);
        Assert.True(world.RemoveBuilding(house.InstanceId, house.TownId, Alpha).Applied);
        var rebuilt = world.PlaceBuilding(house.InstanceId, house.DefinitionId, house.Position, Alpha);
        Assert.True(rebuilt.Applied, rebuilt.Failure);
        var replacement = world.WorldSimulation.Buildings.Single(item => item.InstanceId == house.InstanceId);
        Assert.Equal((house.InstanceId, house.DefinitionId, house.Position, house.TownId, Alpha),
            (replacement.InstanceId, replacement.DefinitionId, replacement.Position, replacement.TownId, replacement.HouseholdId));
        Assert.True(replacement.PlacedTick > house.PlacedTick);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var check = world.CheckActionPlan(plan);
        Assert.False(check.Allowed);
        Assert.Equal(ActionRefusalReason.TargetUnavailable, check.Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Empty(Housing(world, applicant)!.Request!.Approvals);
        Assert.Null(world.Society.GetAgent(applicant).HouseholdId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.False(reopened.CheckActionPlan(plan).Allowed);
        reopened.Validate();
    }

    [Fact]
    public async Task AHousingResponseKeepsTheSameHouseAfterANativeExpansionMovesItsAnchor()
    {
        var (state, applicant) = await RequestedHousingState();
        var original = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var unavailable = state.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building))
            .Concat(state.Map.Resources.Select(resource => resource.Position))
            .Concat(state.Map.MapObjects.Select(item => item.Position)).Concat(state.RoadTiles!)
            .Concat(state.Fields!.Select(field => field.Position)).ToHashSet();
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsBuildable(point) && state.Map.IsBuildable(new(point.X, point.Y - 1)) &&
            state.Map.IsBuildable(new(point.X, point.Y + 1)) &&
            !unavailable.Contains(point) && !unavailable.Contains(new(point.X, point.Y - 1)) &&
            !unavailable.Contains(new(point.X, point.Y + 1)));
        var house = original with { Position = site, Entrance = new(site.X, site.Y + 1) };
        var builder = state.Society.Society.GetHousehold(Alpha).MemberIds[0];
        var stored = state.Society.Society.Inventory.Lots.Where(lot => lot.StorageBuildingId == house.InstanceId).Sum(lot => lot.Quantity);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "housing-expansion-timber", "wood", Alpha,
            56 - stored, storageBuildingId: house.InstanceId);
        state = state with
        {
            WorldSimulation = state.WorldSimulation with
            { Buildings = state.WorldSimulation.Buildings.Select(building => building.InstanceId == house.InstanceId ? house : building).ToArray() },
            Agents = state.Agents.Select(person => person.AgentId == builder ? person with { Position = site } : person).ToArray(),
            Society = state.Society with
            { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
        };
        state = ExpansionLandFixture.WithRights(state, house, [site, new(site.X, site.Y - 1)]);
        var chooser = new HousingPlanChooser(HousingChoiceKind.Admit, choose: false);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HousingActionPlan>(chooser.Plans.First());
        var started = world.StartBuildingExpansion(builder, house.InstanceId);
        Assert.True(started.Applied, started.Failure);
        var job = world.WorldSimulation.BuildingExpansions!.Single(item => item.JobId == started.JobId);
        Assert.NotEqual(house.Position, job.TargetPosition);
        while (world.WorldTick < job.CompletionTick) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldProductionJobState.Completed, world.WorldSimulation.BuildingExpansions!.Single(item => item.JobId == job.JobId).State);
        var expanded = world.WorldSimulation.Buildings.Single(item => item.InstanceId == house.InstanceId);
        Assert.Equal((house.InstanceId, house.DefinitionId, house.PlacedTick, house.HouseholdId, house.TownId),
            (expanded.InstanceId, expanded.DefinitionId, expanded.PlacedTick, expanded.HouseholdId, expanded.TownId));
        Assert.NotEqual(house.Position, expanded.Position);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.True(world.CheckActionPlan(plan).Allowed);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Empty(Housing(world, applicant)!.Request!.Approvals);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.True(reopened.CheckActionPlan(plan).Allowed);
        reopened.Validate();
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
