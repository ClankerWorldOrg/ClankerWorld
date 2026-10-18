using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldConstructionOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnOrdinaryConstructionOffersAFreshChoiceAfterItsSelectedStockIsSpent(bool spendStock)
    {
        var setup = await Baseline.Value;
        var state = Decode(setup);
        var clinic = state.WorldContent!.Buildings.Single(item => item.CanonicalId == setup.DefinitionId);
        var smith = state.WorldContent.Buildings.Single(item => item.LocalId == "blacksmith-1x2");
        Assert.Contains(smith.BuildCosts, cost => cost.ResourceId == "wood" && cost.Amount > 0);
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in clinic.BuildCosts.Concat(smith.BuildCosts).GroupBy(cost => cost.ResourceId))
            inventory = InventoryAuthority.AddLot(inventory, "z-replacement-" + cost.Key, cost.Key, Alpha,
                cost.Sum(item => item.Amount), storageBuildingId: House);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == setup.Actor
                ? person with { LastDecisionContext = null } : person).ToArray(),
        };
        var provider = new FreshConstructionChoices(setup.DefinitionId, setup.CandidateId);
        using var world = WorldRuntime.Restore(state, id => id == setup.Actor ? provider : new ConstructionChoices());
        var admission = await world.AdvanceOneTickAsync();
        Assert.True(admission.Advanced);
        var decision = Assert.Single(admission.Decisions,
            item => item.AgentId == setup.Actor && item.ActionPlan is ConstructionActionPlan);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        Assert.Null(decision.PlanRefusal);
        var original = Assert.IsType<ConstructionActionPlan>(decision.ActionPlan);
        Assert.Equal((setup.Actor, Alpha), (original.Actor, original.MaterialOwnerId));
        Assert.NotNull(original.Inputs);
        Assert.Contains(original.Inputs, input => input.LotId == "construction-wood");
        Assert.Null(Person(world, setup.Actor).Project!.OrderInstructionId);
        var started = Person(world, setup.Actor).Project!.StartedTick;
        Assert.DoesNotContain(world.WorldSimulation.Buildings, building => building.InstanceId == original.InstanceId);
        var chosen = original;

        if (spendStock)
        {
            var placed = false;
            foreach (var tile in state.Map.Tiles.Where(tile => state.Map.IsBuildable(tile.Position) &&
                         state.Map.FootDistance(tile.Position, original.Site) > 4))
            {
                var result = world.PlaceBuilding("competing-stock-use", smith.CanonicalId, tile.Position, Alpha);
                if (!result.Applied) continue;
                placed = true;
                break;
            }
            Assert.True(placed, "a separate paid Blacksmith must have a legal site");
            Assert.Contains(world.Society.Inventory.Reservations, reservation =>
                reservation.Purpose == "building:competing-stock-use" && reservation.LotId == "construction-wood" &&
                reservation.State == InventoryReservationState.Completed && reservation.Quantity > 0);
            Assert.True(world.Society.Inventory.Lots.Where(lot => lot.OwnerId == Alpha && lot.ItemKind == "wood")
                .Sum(lot => lot.Quantity) >= clinic.BuildCosts.Single(cost => cost.ResourceId == "wood").Amount);
            var beforeCheck = WorldRuntimeCodec.Encode(world.ExportState());
            var refusal = world.CheckActionPlan(original).Refusal;
            Assert.NotNull(refusal);
            Assert.Equal(ActionRefusalReason.StockUnavailable, refusal.Reason);
            Assert.Equal(beforeCheck, WorldRuntimeCodec.Encode(world.ExportState()));
            world.Validate();
            using (var reloaded = Reload(world)) reloaded.Validate();

            for (var tick = 0; tick < 8 && !Person(world, setup.Actor).Project!.RequiresFreshChoice; tick++)
                await Tick(world);
            Assert.Equal("paused", Person(world, setup.Actor).Project!.Stage);
            Assert.True(Person(world, setup.Actor).Project!.RequiresFreshChoice);
            Assert.DoesNotContain(world.WorldSimulation.Buildings, building => building.InstanceId == original.InstanceId);
            using (var reloaded = Reload(world)) reloaded.Validate();

            ConstructionActionPlan? fresh = null;
            for (var tick = 0; tick < 64 && fresh is null; tick++)
            {
                var step = await world.AdvanceOneTickAsync();
                Assert.True(step.Advanced);
                var renewed = step.Decisions.FirstOrDefault(item => item.AgentId == setup.Actor &&
                    item.ActionPlan is ConstructionActionPlan plan && plan.Definition.CanonicalId == setup.DefinitionId);
                if (renewed is null) continue;
                Assert.True(renewed.Admission.Accepted);
                Assert.False(renewed.Admission.FellBack);
                Assert.Null(renewed.PlanRefusal);
                fresh = Assert.IsType<ConstructionActionPlan>(renewed.ActionPlan);
            }
            Assert.NotNull(fresh);
            Assert.NotNull(fresh.Inputs);
            Assert.Contains(fresh.Inputs, input => input.LotId == "z-replacement-wood");
            Assert.Equal(started, Person(world, setup.Actor).Project!.StartedTick);
            Assert.False(Person(world, setup.Actor).Project!.RequiresFreshChoice);
            Assert.Equal(ActionRefusalReason.StockUnavailable, world.CheckActionPlan(original).Refusal!.Reason);
            chosen = fresh;
        }

        for (var tick = 0; tick < 80 && !world.WorldSimulation.Buildings.Any(building => building.InstanceId == chosen.InstanceId); tick++)
            await Tick(world);
        var built = Assert.Single(world.WorldSimulation.Buildings, building => building.InstanceId == chosen.InstanceId);
        Assert.Equal((chosen.Definition.CanonicalId, chosen.Site, chosen.HouseholdId, chosen.TownId),
            (built.DefinitionId, built.Position, built.HouseholdId, built.TownId));
        var paid = world.Society.Inventory.Reservations.Where(reservation => reservation.Purpose == "building:" + chosen.InstanceId).ToArray();
        Assert.Equal(chosen.Inputs!.OrderBy(input => input.LotId).Select(input => (input.LotId, input.Quantity)),
            paid.OrderBy(input => input.LotId).Select(input => (input.LotId, input.Quantity)));
        Assert.All(paid, reservation => Assert.Equal(InventoryReservationState.Completed, reservation.State));
        Assert.False(world.CheckActionPlan(chosen).Allowed);
        Assert.Empty(world.WorldSimulation.ConstructionReceipts ?? []);
        world.Validate();
        using var finalReload = Reload(world);
        finalReload.Validate();
    }

    private sealed class FreshConstructionChoices(string definitionId, string firstCandidate) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            var observation = request.Observation;
            var selected = observation.Candidates.FirstOrDefault(candidate => candidate.Id == firstCandidate) ??
                observation.Candidates.FirstOrDefault(candidate => candidate.ActionPlan is ConstructionActionPlan plan &&
                    plan.Definition.CanonicalId == definitionId) ?? observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind,
                request.ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                selected.Id, 1, new Dictionary<string, double> { [selected.Id] = 1 }));
        }
    }
}
