using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class BlacksmithSupplyRefreshTests
{
    [Fact]
    public async Task ModelRequestsDoNotOfferSuppliesConsumedEarlierInTheTick()
    {
        var (state, smith, actors) = DeliveryState();
        WorldRuntime? current = null;
        var requests = new List<AgentObservation>();
        var stale = new List<string>();
        var provider = new SupplyChoices(request =>
        {
            requests.Add(request.Observation);
            foreach (var candidate in request.Observation.Candidates)
            {
                if (candidate.ActionPlan is not (SmithDeliveryActionPlan or SmithGatherActionPlan)) continue;
                if (!current!.CheckActionPlan(candidate.ActionPlan).Allowed)
                    stale.Add($"{request.Observation.AgentId}: {candidate.Id}: {current.CheckActionPlan(candidate.ActionPlan).Refusal}");
            }
        });
        using var world = WorldRuntime.Restore(state, id => id == actors[0] ? new SupplyChoices(_ => { }, model: false) : provider);
        current = world;
        var harness = ModelWorkHarness.For(world);
        for (var tick = 0; tick < 32; tick++)
            Assert.True((await harness.TickAsync()).Advanced);
        Assert.NotEmpty(requests);
        Assert.DoesNotContain(requests.SelectMany(request => request.Candidates), candidate =>
            candidate.ActionPlan is SmithDeliveryActionPlan { StockId: "running-out-smith-wood" });
        Assert.Contains(world.ExportState().Events, item => item.Kind == "smith_input_picked_up");
        Assert.Empty(stale);
        var refusals = world.ExportState().Events.Where(item => item.Kind == "choice_refused" &&
            item.Payload is ChoiceRefusedEventPayload payload && actors.Contains(payload.ActorId, StringComparer.Ordinal)).ToArray();
        Assert.True(refusals.Length <= 1, string.Join("\n", refusals.Select(item => item.Detail)));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task ModelRequestsDoNotOfferSourcesHarvestedEarlierInTheTick()
    {
        var (state, first, tree) = await SmithGatherActionPlanTests.Fixture();
        var household = state.Society.Society.GetAgent(first).HouseholdId!;
        var second = state.Society.Society.GetHousehold(household).MemberIds.Single(id => id != first);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "other-smith-axe", "wooden_axe", second, 1);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == second ? person with
            { Position = tree.Position, FullnessBasisPoints = 10_000, Project = null, LastDecisionContext = null } : person).ToArray(),
        };
        WorldRuntime? current = null;
        var requests = new List<AgentObservation>();
        var sources = new List<string>();
        var stale = new List<string>();
        var provider = new SupplyChoices(request =>
        {
            requests.Add(request.Observation);
            foreach (var candidate in request.Observation.Candidates)
            {
                if (candidate.ActionPlan is not SmithGatherActionPlan plan) continue;
                sources.Add(plan.Source.Id);
                if (!current!.CheckActionPlan(plan).Allowed)
                    stale.Add($"{plan.Actor}: {plan.Source.Id}: {current.CheckActionPlan(plan).Refusal}");
            }
        });
        using var world = WorldRuntime.Restore(state, id => id == first ? new SupplyChoices(_ => { }, model: false) : provider);
        current = world;
        var harness = ModelWorkHarness.For(world);
        for (var tick = 0; tick < 16; tick++)
            Assert.True((await harness.TickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Payload is MaterialGatheredEventPayload
            { ActorId: var actor, ItemKind: "wood" } && actor == first);
        Assert.Equal(ResourceState.Depleted, world.ExportState().Resources.Single(item => item.ResourceId == tree.Id).State);
        Assert.Contains(requests, request => request.AgentId == second && request.WorldTick == state.Society.Society.WorldTick + 1);
        Assert.DoesNotContain(tree.Id, sources);
        Assert.Empty(stale);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task AReplyHeldWhileTheLastSupplyIsTakenRefusesOnceThenMakesAFreshChoice()
    {
        var (state, smith, actors) = DeliveryState();
        var firstProvider = new ScriptedProvider();
        var laterProvider = new ScriptedProvider();
        var first = firstProvider.Hold();
        var later = laterProvider.Hold();
        using var world = WorldRuntime.Restore(state, id => id == actors[0] ? firstProvider :
            id == actors[1] ? laterProvider : new ScriptedProvider());
        var harness = ModelWorkHarness.For(world);
        Assert.True((await harness.TickAsync()).Advanced);
        Assert.True(first.IsEntered);
        Assert.True(later.IsEntered);
        var selected = Assert.IsType<SmithDeliveryActionPlan>(later.Request.Observation.Candidates
            .Single(candidate => candidate.Id == "haul_smith_input").ActionPlan);
        Assert.Equal("running-out-smith-wood", selected.StockId);
        Assert.True(world.CheckActionPlan(selected).Allowed);

        first.Reply("haul_smith_input");
        await harness.WaitForDecisionAsync(actors[0]);
        Assert.True((await harness.TickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "smith_input_picked_up");
        Assert.False(world.CheckActionPlan(selected).Allowed);
        later.Reply("haul_smith_input");
        await harness.WaitForDecisionAsync(actors[1]);
        var refused = await harness.TickAsync();
        var decision = Assert.Single(refused.Decisions, item => item.AgentId == actors[1]);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        Assert.Same(selected, decision.ActionPlan);
        Assert.Equal(ActionRefusalReason.StockUnavailable, decision.PlanRefusal!.Reason);
        Assert.Null(world.ExportState().Society.Cognition.Runtimes.Single(item => item.AgentId == actors[1]).CurrentIntention);

        var fresh = laterProvider.Hold();
        for (var tick = 0; tick < 8 && !fresh.IsEntered; tick++)
            Assert.True((await harness.TickAsync()).Advanced);
        Assert.True(fresh.IsEntered);
        Assert.DoesNotContain(fresh.Request.Observation.Candidates, candidate =>
            candidate.ActionPlan is SmithDeliveryActionPlan { StockId: "running-out-smith-wood" });
        fresh.Reply("safe_idle");
        await harness.WaitForDecisionAsync(actors[1]);
        var resumed = await harness.TickAsync();
        var next = Assert.Single(resumed.Decisions, item => item.AgentId == actors[1]);
        Assert.True(next.Admission.Accepted);
        Assert.False(next.Admission.FellBack);
        Assert.Equal("safe_idle", next.Admission.Intention!.CandidateId);
        for (var tick = 0; tick < 8; tick++)
            Assert.True((await harness.TickAsync()).Advanced);
        Assert.Single(world.ExportState().Events, item => item.Kind == "choice_refused" &&
            item.Payload is ChoiceRefusedEventPayload payload && payload.ActorId == actors[1]);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    private static (WorldRuntimeState State, PlacedBuilding Smith, string[] Actors) DeliveryState()
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var warehouse = state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var actors = state.Society.Society.GetHousehold(smith.HouseholdId!).MemberIds.Order(StringComparer.Ordinal).ToArray();
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, "running-out-smith-wood", "wood", smith.HouseholdId!, 4,
            groundPosition: new(warehouse.Position.X, warehouse.Position.Y));
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = actors.Contains(person.AgentId, StringComparer.Ordinal) ? warehouse.Position : person.Position,
                FullnessBasisPoints = 10_000,
                Project = null,
                LastDecisionContext = null,
            }).ToArray(),
        };
        return (state, smith, actors);
    }

    private sealed class SupplyChoices(Action<CognitionDecisionRequest> observe, bool model = true) : IDecisionProvider
    {
        public DecisionProviderKind Kind => model ? DecisionProviderKind.LargeLanguageModel : DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            observe(request);
            var choice = request.Observation.Candidates.FirstOrDefault(item => item.Id == "haul_smith_input")?.Id ??
                request.Observation.Candidates.FirstOrDefault(item => item.Id == "haul_household_stock")?.Id ??
                request.Observation.Candidates.FirstOrDefault(item => item.Id == "gather_smith_input:wood")?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                choice, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice ? 1d : 0d)));
        }
    }
}
