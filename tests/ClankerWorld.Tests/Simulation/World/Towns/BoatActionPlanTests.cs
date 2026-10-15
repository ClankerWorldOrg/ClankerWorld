using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class PortBoatRuntimeTests
{
    [Fact]
    public async Task ABoatRequestKeepsItsPortsAndGeographyAcrossRollbackReplayAndReload()
    {
        var state = WorldRuntimeCodec.Decode(await PaidBoat.Value);
        var boat = state.BoatTransport.Boats.Single();
        var origin = state.WorldSimulation!.Buildings.Single(port => port.InstanceId == boat.DockedPortId);
        state = BoatPlanState(state, origin.Entrance!.Value);
        var chooser = new BoatPlanChooser();
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new BoatPlanChooser());
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<BoatTripActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == BoatPolicy.Author).ActionPlan);
        Assert.Equal((boat.Id, boat.Position), (plan.ProofBoatId, plan.ProofBoatPosition));
        Assert.NotEmpty(plan.WaterRoute);
        var request = Assert.Single(world.BoatRequests);
        Assert.Equal((plan.Choice.TownId, plan.Choice.OriginPortId, plan.Choice.DestinationPortId),
            (request.BoatTownId, request.OriginPortId, request.DestinationPortId));
        Assert.Single(world.ExportState().Events, item => item.Kind == "boat_trip_requested");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(BoatWaitingKind.Cancel)]
    [InlineData(BoatWaitingKind.Queue)]
    public async Task ABoatWaitingPlanKeepsTheNativeRequestAcrossReplayAndRefusesItsReplacement(BoatWaitingKind kind)
    {
        var policy = new BoatPolicy { Trips = true };
        using var setup = new BoatScenario(WorldRuntimeCodec.Decode(await PaidBoat.Value), policy);
        var boat = setup.World.Boats.Single();
        var destination = setup.World.Towns[0].Projects.Single(project => project.CompletedBuildingId is not null &&
            project.CompletedBuildingId != boat.DockedPortId).CompletedBuildingId!;
        AddLandingBlockers(setup, destination);
        await setup.UntilAsync(() => setup.World.BoatRequests.Any(request => request.Status == "waiting"), 120);
        var state = setup.World.ExportState();
        var request = state.BoatTransport.Requests.Single();
        if (kind == BoatWaitingKind.Queue) state = BoatPlanState(state, FreeSpotAwayFromPorts(setup.World));
        else state = BoatPlanState(state, state.Agents.Single(person => person.AgentId == BoatPolicy.Author).Position);
        var chooser = new BoatPlanChooser(kind);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new BoatPlanChooser(kind));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<BoatWaitingActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == BoatPolicy.Author).ActionPlan);
        Assert.Equal(request, plan.Request);
        Assert.Equal(kind == BoatWaitingKind.Cancel ? "cancelled" : "waiting", world.BoatRequests.Single().Status);
        Assert.Null(world.Boats.Single().Journey);
        using var changed = WorldRuntime.Restore(state with
        {
            BoatTransport = state.BoatTransport with
            { Requests = [request with { Status = "cancelled", SettledTick = state.Society.Society.WorldTick }] },
        });
        var unchanged = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(unchanged, WorldRuntimeCodec.Encode(changed.ExportState()));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    private static WorldRuntimeState BoatPlanState(WorldRuntimeState state, GridPoint position) =>
        WeatherTestFixture.WithWeather(state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == BoatPolicy.Author ? position : person.Position,
                FullnessBasisPoints = 9_500,
                Survival = new(),
                LastDecisionContext = null,
                TravelCooldownTicks = 0,
            }).ToArray(),
        }, WeatherKind.Clear);

    private sealed class BoatPlanChooser(BoatWaitingKind? waiting = null) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == BoatPolicy.Author ? request.Observation.Candidates.FirstOrDefault(item =>
                waiting is null ? item.ActionPlan is BoatTripActionPlan : item.ActionPlan is BoatWaitingActionPlan plan && plan.Choice.Kind == waiting) : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
