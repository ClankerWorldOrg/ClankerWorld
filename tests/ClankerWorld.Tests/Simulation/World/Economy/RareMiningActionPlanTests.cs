using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RareMiningActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MiningKeepsItsOriginalPickDepositAndYieldAndRefusesTheirLoss(bool depositLost)
    {
        using var initial = NormalPathWorld.CreateGenerated("tool-rare-mining", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var actor = state.Agents[0].AgentId;
        var source = state.Map.Resources.Where(resource => resource.Kind == "gold_ore" && !resource.IsRenewable &&
                state.Map.IsPassable(resource.Position) && state.Agents.All(person => person.AgentId == actor || person.Position != resource.Position))
            .OrderBy(resource => resource.Id, StringComparer.Ordinal).First();
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), "rare-plan-pick", "iron_pickaxe", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "rare-plan-basket", "basket", actor, 1);
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = source.Position,
                Equipment = new(CarryAidLotId: "rare-plan-basket"),
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(),
            } : person).ToArray(),
        };
        var choices = new Choices(actor);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<RareMiningActionPlan>(choices.Plan);
        Assert.Equal((source.Id, "rare-plan-pick", 8), (plan.Source.Id, plan.Harvest.ToolLotId, plan.Harvest.Quantity));
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-rare-pick", "iron_pickaxe", actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        var changed = depositLost ? checkpoint with
        {
            Resources = checkpoint.Resources.Select(item => item.ResourceId == source.Id ? item with { State = ResourceState.Depleted } : item).ToArray(),
            WorldSystems = checkpoint.WorldSystems! with
            {
                Ecology = checkpoint.WorldSystems.Ecology with
                {
                    Resources = checkpoint.WorldSystems.Ecology.Resources.Select(item => item.Id == source.Id
                        ? EcologyRules.Harvest(item, item.Quantity).Resource! : item).ToArray(),
                },
            },
        } : FoodCapacityTestFixture.WithInventory(checkpoint, InventoryAuthority.Reserve(inventory,
            "rare-plan-pick-claim", actor, "rare-plan-pick", 1, "other_work", long.MaxValue));
        using var unavailable = WorldRuntime.Restore(changed);
        Assert.Equal(ActionRefusalReason.StockUnavailable, unavailable.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal((source.Id, "rare-plan-pick", 8), (plan.Source.Id, plan.Harvest.ToolLotId, plan.Harvest.Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(unavailable.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(unavailable.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private sealed class Choices(string actor) : IDecisionProvider
    {
        public RareMiningActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == actor && request.Observation.Candidates.SingleOrDefault(item => item.Id == "gather_rare_material:gold_ore") is { } candidate)
                Plan = Assert.IsType<RareMiningActionPlan>(candidate.ActionPlan);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                "safe_idle", 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == "safe_idle" ? 1d : 0d)));
        }
    }
}
