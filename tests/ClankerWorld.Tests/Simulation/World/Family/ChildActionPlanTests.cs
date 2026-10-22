using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ChildActionPlanTests
{
    [Fact]
    public async Task EarlierActorMovementRefusesTheOriginalChildPlanAcrossRollbackAndReplay()
    {
        var (state, child, _, source) = await ChildHouseholdHelpingTests.Prepared("food");
        var target = state.Agents.Where(person => person.AgentId != child)
            .OrderBy(person => person.AgentId, StringComparer.Ordinal).First().AgentId;
        Assert.True(StringComparer.Ordinal.Compare(target, child) < 0);
        var position = state.Map.Tiles.Where(tile => state.Map.IsPassable(tile.Position) &&
                state.Map.FootDistance(source.Position, tile.Position) is >= 3 and <= 6 &&
                state.Map.Resources.Where(resource => resource.Kind is "food" or "fruit")
                    .All(resource => state.Map.FootDistance(resource.Position, tile.Position) > 1))
            .OrderBy(tile => state.Map.FootDistance(source.Position, tile.Position)).First().Position;
        var inventory = state.Society.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == target && FoodRules.IsEdible(lot.ItemKind) && lot.ContainerLotId is null).ToArray())
            inventory = InventoryAuthority.Transfer(inventory, "child-plan-food-" + lot.Id, target,
                state.Society.Society.GetAgent(target).HouseholdId!, lot.Id, lot.Quantity, "fixture_food");
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == target ? person with
            { Position = position, FullnessBasisPoints = 1_000, LastDecisionContext = null } : person).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
                Cognition = state.Society.Cognition with { MaxDispatchPerCycle = 64 },
            },
        };
        var provider = new OrderedChoices(child, target);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        provider.Plan = null;
        var result = await world.AdvanceOneTickAsync();
        Assert.True(result.Advanced);
        var plan = Assert.IsType<ChildActionPlan>(provider.Plan);
        Assert.Equal(position, plan.Destination);
        Assert.NotEqual(position, world.Agents.Single(person => person.AgentId == target).Position);
        Assert.Same(plan, result.Decisions.Single(decision => decision.AgentId == child).ActionPlan);
        var refusal = Assert.Single(result.Events, item => item.Kind == "choice_refused");
        var payload = Assert.IsType<ChoiceRefusedEventPayload>(refusal.Payload);
        Assert.Equal(child, payload.ActorId);
        Assert.Contains("the person is no longer available there", refusal.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Events, item => item.Kind == "child_play");
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new OrderedChoices(child, target));
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        var accepted = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(accepted, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(accepted));
        Assert.Equal(accepted, WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedStockAndQuantityStayFixedWhenOtherStockBecomesAvailable(bool reserveSelected)
    {
        var (state, child, _, source) = await ChildHouseholdHelpingTests.Prepared("food");
        var household = state.Society.Society.GetAgent(child).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "child-plan-stock", "wood", household, 4,
            groundPosition: new(source.Position.X, source.Position.Y));
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        var provider = new PlanRecorder("child_carry:child-plan-stock");
        using var world = WorldRuntime.Restore(state, id => id == child ? provider : new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 8 && provider.Plan is null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ChildActionPlan>(provider.Plan);
        Assert.Equal(("child-plan-stock", 4), (plan.StockId, plan.Quantity));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());

        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-other-child-stock", "wood", household, 4,
            groundPosition: new(source.Position.X, source.Position.Y));
        if (reserveSelected)
            inventory = InventoryAuthority.Reserve(inventory, "child-plan-reservation", household, plan.StockId!, 1,
                "other_work", checkpoint.Society.Society.WorldTick + 100);
        checkpoint = checkpoint with { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } };
        using var changed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(checkpoint)),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var check = changed.CheckActionPlan(plan);
        Assert.Equal(!reserveSelected, check.Allowed);
        if (reserveSelected)
        {
            Assert.Equal(ActionRefusalReason.StockUnavailable, Assert.IsType<ActionRefusal>(check.Refusal).Reason);
            Assert.NotEmpty(check.Refusal.Explanation);
        }
        Assert.Equal(("child-plan-stock", 4), (plan.StockId, plan.Quantity));
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        changed.Validate();
    }

    [Fact]
    public void PlansNeverEnterCandidateJsonOrChangeModelText()
    {
        var target = NormalPathWorld.Founder1;
        foreach (var kind in Enum.GetValues<ChildActionKind>().Where(kind => kind != ChildActionKind.HelpFood))
        {
            var choice = new ChildChoice(kind, target);
            Assert.EndsWith(target, choice.Id, StringComparison.Ordinal);
            Assert.Equal(choice, ChildChoice.Parse(choice.Id));
        }
        var candidate = new CognitionCandidate("child_help_food", "Carry one spare food serving back to the household store.", 26);
        var planned = candidate with
        {
            ActionPlan = new ChildActionPlan("child", new(ChildActionKind.HelpFood), new(1, 2), 0,
                Array.AsReadOnly(new[] { new GridPoint(1, 2) }), StockId: "private-stock", Quantity: 1),
        };
        Assert.Equal(JsonSerializer.Serialize(candidate), JsonSerializer.Serialize(planned));
        Assert.Null(JsonSerializer.Deserialize<CognitionCandidate>(JsonSerializer.Serialize(planned))!.ActionPlan);
    }

    private sealed class PlanRecorder(string wanted) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Plan ??= request.Observation.Candidates.SingleOrDefault(candidate => candidate.Id == wanted)?.ActionPlan;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == "safe_idle" ? 1d : 0d, StringComparer.Ordinal)));
        }
    }

    private sealed class OrderedChoices(string child, string target) : IDecisionProvider
    {
        public ActionPlan? Plan { get; set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var wanted = request.Observation.AgentId == child ? new ChildChoice(ChildActionKind.Play, target).Id :
                request.Observation.AgentId == target ? "seek_food" : "safe_idle";
            var candidate = request.Observation.Candidates.SingleOrDefault(item => item.Id == wanted);
            if (request.Observation.AgentId == child || request.Observation.AgentId == target) Assert.NotNull(candidate);
            if (request.Observation.AgentId == child) Plan = candidate!.ActionPlan;
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
