using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OrnamentActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WearKeepsItsOwnedOrSharedStockWhenAnotherOrnamentAppearsAndRefusesAClaim(bool shared)
    {
        var (state, actor, _, house) = await Fixture();
        var owner = shared ? house.HouseholdId! : actor;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "ornament-plan-stock", "gold_ornament", owner, 2,
            storageBuildingId: shared ? house.InstanceId : null);
        var choices = new Choices(actor);
        using var world = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(state, inventory), _ => choices);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.Single(choices.Plans.OfType<WearOrnamentActionPlan>());
        Assert.Equal("ornament-plan-stock", plan.Selected.StockId);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-ornament", "diamond_ornament", owner, 1,
            storageBuildingId: shared ? house.InstanceId : null);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "ornament-plan-claim", owner, plan.Selected.StockId, 2, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GiftAndRemovalKeepTheirExactWornUnitAndRecipient(bool worn)
    {
        var (state, actor, recipient, _) = await Fixture();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "ornament-plan-gift", "gold_ornament", actor, 1);
        var choices = new Choices(actor);
        using var world = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(state, inventory), _ => choices);
        if (worn) Assert.True(world.WearOrnament(actor, "ornament-plan-gift").Applied);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var gift = Assert.Single(choices.Plans.OfType<GiftOrnamentActionPlan>(), plan => plan.Selected.RecipientId == recipient);
        Assert.Equal("ornament-plan-gift", gift.Selected.StockId);
        var checkpoint = world.ExportState();
        var person = checkpoint.Agents.Single(item => item.AgentId == recipient);
        var room = PersonalEquipmentRules.FreeCapacity(checkpoint.Society.Society.Inventory, recipient, person.Equipment);
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "ornament-plan-full-recipient", "stone", recipient, room);
        using var full = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.TargetUnavailable, full.CheckActionPlan(gift).Refusal!.Reason);
        Assert.Equal(recipient, gift.Selected.RecipientId);
        Assert.True(world.GiveOrnament(actor, recipient, gift.Selected.StockId).Applied);
        Assert.Equal(worn ? ActionRefusalReason.TargetUnavailable : ActionRefusalReason.StockUnavailable, world.CheckActionPlan(gift).Refusal!.Reason);
        if (worn)
        {
            var remove = Assert.Single(choices.Plans.OfType<RemoveOrnamentActionPlan>());
            Assert.Equal("ornament-plan-gift", remove.StockId);
            Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(remove).Refusal!.Reason);
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private static async Task<(WorldRuntimeState State, string Actor, string Recipient, PlacedBuilding House)> Fixture()
    {
        using var initial = NormalPathWorld.CreateGenerated("ornament-action-plan", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        var members = state.Society.Society.Agents.Where(person => person.HouseholdId == house.HouseholdId).Select(person => person.Id).ToArray();
        var actor = members[0];
        var recipient = members[1];
        var inventory = FoodCapacityTestFixture.WithoutPersonalCargo(state, actor);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor || person.AgentId == recipient ? house.Position : person.Position,
                Equipment = person.AgentId == actor ? null : person.Equipment,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new(),
            }).ToArray(),
        };
        return (state, actor, recipient, house);
    }

    private sealed class Choices(string actor) : IDecisionProvider
    {
        public List<OrnamentActionPlan> Plans { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == actor)
                Plans.AddRange(request.Observation.Candidates.Select(candidate => candidate.ActionPlan).OfType<OrnamentActionPlan>());
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                "safe_idle", 1, new Dictionary<string, double> { ["safe_idle"] = 1 }));
        }
    }
}
