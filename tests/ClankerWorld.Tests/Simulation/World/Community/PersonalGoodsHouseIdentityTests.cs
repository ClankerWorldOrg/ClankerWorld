using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class PersonalGoodsHouseIdentityTests
{
    [Theory]
    [InlineData(PersonalGoodsKind.Store, false)]
    [InlineData(PersonalGoodsKind.Store, true)]
    [InlineData(PersonalGoodsKind.Return, false)]
    [InlineData(PersonalGoodsKind.Return, true)]
    public async Task AHeldPersonalGoodsChoiceRequiresTheOriginalHouse(PersonalGoodsKind kind, bool rebuild)
    {
        using var setup = NormalPathWorld.CreateGenerated("personal-storage-orders", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = setup.ExportState();
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == "household:first").Id;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId ==
            (kind == PersonalGoodsKind.Store ? "first-town-house-a" : "first-town-house-b"));
        var removed = state.Society.Society.Inventory.Lots.Where(lot => lot.StorageBuildingId == house.InstanceId)
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => !removed.Contains(lot.Id)).ToArray(),
            Reservations = state.Society.Society.Inventory.Reservations.Where(item => !removed.Contains(item.LotId)).ToArray(),
        };
        var owner = kind == PersonalGoodsKind.Store ? actor : house.HouseholdId!;
        inventory = InventoryAuthority.AddLot(inventory, "held-personal-cloth", "cloth", owner, 2);
        inventory = InventoryAuthority.Relocate(inventory, "carry-held-personal-cloth", "held-personal-cloth", owner, 2, carrierId: actor);
        inventory = InventoryAuthority.AddLot(inventory, "personal-house-rebuild-timber", "wood", house.HouseholdId!, 8);
        state = state with
        {
            JevEnabled = true,
            Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? house.Position : person.Position,
                FullnessBasisPoints = 10_000,
                Survival = new(),
                LastDecisionContext = null,
            }).ToArray(),
        };
        var provider = new HeldGoods(kind);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? provider : new ActionCoverageRecorder(chooseIdle: true));
        world.SubmitInstruction(new("hold-personal-goods", "owner:test", actor, OwnerInstructionKind.Suggestion,
            "Consider putting away the carried belongings."));
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var plan = await provider.Captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal((actor, owner, "held-personal-cloth", 2, house.InstanceId, house.Position),
                (plan.Actor, plan.OwnerId, plan.Choice.LotId, plan.Quantity, plan.HouseId, plan.Target));
            Assert.True(world.CheckActionPlan(plan).Allowed);
            if (rebuild)
            {
                Assert.True(world.RemoveBuilding(house.InstanceId, house.TownId, house.HouseholdId).Applied);
                var placed = world.PlaceBuilding(house.InstanceId, house.DefinitionId, house.Position, house.HouseholdId);
                Assert.True(placed.Applied, placed.Failure);
                var replacement = world.WorldSimulation.Buildings.Single(item => item.InstanceId == house.InstanceId);
                Assert.Equal((house.InstanceId, house.DefinitionId, house.Position, house.HouseholdId, house.TownId),
                    (replacement.InstanceId, replacement.DefinitionId, replacement.Position, replacement.HouseholdId, replacement.TownId));
                Assert.True(replacement.PlacedTick > house.PlacedTick);
                Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "personal-house-rebuild-timber");
            }
            var bytes = WorldRuntimeCodec.Encode(world.ExportState());
            var check = world.CheckActionPlan(plan);
            Assert.Equal(!rebuild, check.Allowed);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            using (var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes)))
            {
                Assert.Equal(!rebuild, reopened.CheckActionPlan(plan).Allowed);
                reopened.Validate();
            }
            provider.Release.TrySetResult(true);
            await provider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(30));
            for (var spin = 0; spin < 1_000 && !world.ModelDecisionsFinished(actor); spin++) await Task.Delay(1);
            Assert.True(world.ModelDecisionsFinished(actor));
            SocietyCognitionDispatchResult? decision = null;
            for (var tick = 0; tick < 8 && decision is null; tick++)
            {
                var step = await world.AdvanceOneTickNonBlockingAsync();
                Assert.True(step.Advanced);
                decision = step.Decisions.SingleOrDefault(item => item.AgentId == actor && item.ActionPlan == plan);
            }
            Assert.NotNull(decision);
            Assert.Same(plan, decision.ActionPlan);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
            if (rebuild) Assert.Equal(ActionRefusalReason.TargetUnavailable, decision.PlanRefusal!.Reason);
            else Assert.Null(decision.PlanRefusal);
            var cloth = Assert.Single(world.Society.Inventory.Lots, lot => lot.ItemKind == "cloth" && lot.OwnerId == owner);
            Assert.Equal((2, owner, rebuild && kind == PersonalGoodsKind.Return ? actor : null, rebuild ? null : house.InstanceId),
                (cloth.Quantity, cloth.OwnerId, cloth.CarrierId, cloth.StorageBuildingId));
            Assert.Equal(rebuild ? 0 : 1, world.ExportState().Events.Count(item => item.Kind ==
                (kind == PersonalGoodsKind.Store ? "personal_goods_stored" : "borrowed_goods_returned")));
            world.Validate();
            using var finalReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
            finalReload.Validate();
        }
        finally { provider.Release.TrySetResult(true); }
    }

    private sealed class HeldGoods(PersonalGoodsKind kind) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public TaskCompletionSource<PersonalGoodsActionPlan> Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is PersonalGoodsActionPlan plan &&
                plan.Choice == new PersonalGoodsChoice(kind, "held-personal-cloth"));
            if (candidate?.ActionPlan is PersonalGoodsActionPlan selected && !Captured.Task.IsCompleted)
            {
                Captured.TrySetResult(selected);
                await Release.Task.WaitAsync(cancellationToken);
            }
            else candidate = request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            var response = new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                candidate.Id, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == candidate.Id ? 1d : 0d, StringComparer.Ordinal));
            Returned.TrySetResult(true);
            return response;
        }
    }
}
