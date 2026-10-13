using ClankerWorld.Simulation;
using System.Diagnostics;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using static ClankerWorld.Tests.ShelterOrderTestFixture;

namespace ClankerWorld.Tests;

public sealed partial class WorldShelterOrderTests
{
    [Theory]
    [InlineData("same_id_rebuilt")]
    [InlineData("moved")]
    [InlineData("reassigned")]
    public async Task AChangedHouseDoesNotRetargetOrCreditTheOriginalShelterOrder(string change)
    {
        var state = Prepared();
        var actor = Actor(state);
        var home = House(state);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "shelter-rebuild-wood", "wood", Alpha, 8);
        state = AwayFromHouse(WithInventory(state, inventory), actor, home);
        using var world = Restore(state);
        var receipt = Submit(world, actor, "pinned-shelter", AtText("shelter in my House", home.Position));
        await Tick(world);
        Assert.Equal(0, Order(world, receipt).CompletedUnits);
        var binding = Order(world, receipt).ShelterBinding;
        AssertBuildingBinding(Order(world, receipt), home);
        if (change == "reassigned")
        {
            var other = House(state, Beta);
            Assert.True(world.RemoveBuilding(other.InstanceId, other.TownId, other.HouseholdId).Applied);
            var reassigned = world.ReassignBuilding(home.InstanceId, home.TownId, home.HouseholdId, null, Beta);
            Assert.True(reassigned.Applied, reassigned.Failure);
        }
        else
        {
            Assert.True(world.RemoveBuilding(home.InstanceId, home.TownId, home.HouseholdId).Applied);
            var sites = change == "same_id_rebuilt" ? [home.Position] :
                Enumerable.Range(-2, 5).SelectMany(y => Enumerable.Range(-2, 5)
                    .Select(x => new GridPoint(home.Position.X + x, home.Position.Y + y))).Where(point => point != home.Position).ToArray();
            Assert.Contains(sites, site => world.PlaceBuilding(home.InstanceId, home.DefinitionId, site, Alpha).Applied);
            var replacement = world.WorldSimulation.Buildings.Single(building => building.InstanceId == home.InstanceId);
            Assert.NotEqual(home.PlacedTick, replacement.PlacedTick);
            if (change == "same_id_rebuilt") Assert.Equal(home.Position, replacement.Position);
        }
        using var replay = Reload(world);
        for (var tick = 0; tick < 3; tick++) await TickTogether(world, replay);
        Assert.Equal(("blocked", 0, binding), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits,
            Order(world, receipt).ShelterBinding));
        Assert.Null(Order(world, receipt).ShelterCompletion);
        Assert.Null(Order(world, receipt).LastEffectId);
        Assert.NotEmpty(Order(world, receipt).BlockedReason!);
    }

    [Fact]
    public async Task CompletedShelterArrivalRemainsHistoricalWhenTheBuildingIsRemoved()
    {
        var state = Prepared();
        var actor = Actor(state);
        var home = House(state);
        using var world = Restore(state);
        var receipt = Submit(world, actor, "shelter-history", "shelter in my House");
        await Tick(world);
        Assert.Equal(("finished", 1), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits));
        var completed = JsonSerializer.Serialize(Order(world, receipt));
        Assert.True(world.RemoveBuilding(home.InstanceId, home.TownId, home.HouseholdId).Applied);
        using var replay = Reload(world);
        await TickTogether(world, replay);
        Assert.Equal(completed, JsonSerializer.Serialize(Order(world, receipt)));
        Assert.DoesNotContain(world.WorldSimulation.Buildings, building => building.InstanceId == home.InstanceId);
    }

    [Theory]
    [InlineData("storm_ends")]
    [InlineData("tree_cut")]
    public async Task NaturalCoverMustStillExistAndProtectAgainstAStormAtArrival(string change)
    {
        var state = WithStorm(Prepared(), change == "storm_ends" ? 2 : null);
        var actor = Actor(state);
        var tree = state.Map.Resources.First(site => site.TreeKind is "broadleaf" or "conifer" &&
            state.Map.VegetationAt(site.Position) != VegetationCover.Forest &&
            state.Map.IsPassable(site.Position) &&
            state.WorldSimulation!.Buildings.All(building => state.Map.FootDistance(building.Position, site.Position) > 10));
        state = At(state, actor, Approach(state, tree.Position, 1));
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { TravelCooldownTicks = 1 } : person).ToArray(),
        };
        using var world = Restore(state);
        var receipt = Submit(world, actor, "temporary-cover", AtText("take cover", tree.Position));
        // A legitimate movement cooldown gives weather or harvesting a chance to invalidate the bound target.
        await Tick(world);
        var selected = Assert.IsType<OwnerShelterBinding>(Order(world, receipt).ShelterBinding);
        Assert.Equal("natural", selected.Kind);
        Assert.Equal(0, Order(world, receipt).CompletedUnits);
        Assert.Equal(tree.Position, selected.Position);
        var beforeLoss = world.ExportState();
        if (change == "tree_cut")
        {
            var harvested = EcologyRules.Harvest(beforeLoss.WorldSystems!.Ecology.GetResource(tree.Id), 1);
            Assert.True(harvested.IsValid);
            Assert.Equal(0, harvested.Resource!.Quantity);
            beforeLoss = beforeLoss with
            {
                Resources = beforeLoss.Resources.Select(resource => resource.ResourceId == tree.Id
                    ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
                WorldSystems = beforeLoss.WorldSystems with
                {
                    Ecology = beforeLoss.WorldSystems.Ecology with
                    {
                        Resources = beforeLoss.WorldSystems.Ecology.Resources.Select(resource => resource.Id == tree.Id
                            ? harvested.Resource : resource).ToArray(),
                    },
                },
            };
        }
        using var changed = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(beforeLoss)));
        using var replay = Reload(changed);
        for (var tick = 0; tick < 3; tick++) await TickTogether(changed, replay);
        Assert.Equal(("blocked", 0, selected), (Order(changed, receipt).Status, Order(changed, receipt).CompletedUnits,
            Order(changed, receipt).ShelterBinding));
        Assert.Null(Order(changed, receipt).ShelterCompletion);
        Assert.NotEqual(selected.Position, Person(changed, actor).Position);
    }

    [Fact]
    public async Task UrgentFoodIsConsumedBeforeResumingTheSameColdShelterOrder()
    {
        var state = WithStorm(Prepared());
        var actor = Actor(state);
        var home = House(state);
        state = AwayFromHouse(state, actor, home);
        state = WithCondition(state, actor, warmth: 3_000, fullness: 1_000);
        state = WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory, "shelter-meal", "food", actor, 1));
        var original = state.Agents.Single(person => person.AgentId == actor).Position;
        using var world = Restore(state);
        var receipt = Submit(world, actor, "hungry-cold-shelter", "shelter in my House");
        await Tick(world);
        Assert.Equal(("interrupted", 0), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits));
        Assert.Equal(original, Person(world, actor).Position);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "shelter-meal");
        Assert.True(Person(world, actor).FullnessBasisPoints >= 2_000);
        Assert.Null(Order(world, receipt).ShelterCompletion);
        using var replay = Reload(world);
        await FinishTogether(world, replay, receipt);
        Assert.Equal(home.Position, Person(world, actor).Position);
        Assert.Equal(1, Order(world, receipt).CompletedUnits);
    }

    [Fact]
    public async Task CancellationPreservesTheUnfinishedJourneyAndAllowsTheQueuedShelterToComplete()
    {
        var state = Prepared();
        var actor = Actor(state);
        var home = House(state);
        state = AwayFromHouse(state, actor, home);
        using var world = Restore(state);
        var first = Submit(world, actor, "cancelled-shelter", "shelter in my House");
        var second = Submit(world, actor, "queued-shelter", "shelter in my House", queue: true);
        await Tick(world);
        Assert.NotNull(Order(world, first).ShelterBinding);
        Assert.Equal(("queued", 0), (Order(world, second).Status, Order(world, second).CompletedUnits));
        Assert.True(world.CancelOrder(new("cancel-shelter", "owner:test", world.Society.WorldId, actor, first.InstructionId)).Changed);
        Assert.Equal(0, Order(world, first).CompletedUnits);
        using var replay = Reload(world);
        await FinishTogether(world, replay, second);
        Assert.Equal(("cancelled", 0), (Order(world, first).Status, Order(world, first).CompletedUnits));
        Assert.Null(Order(world, first).ShelterCompletion);
        Assert.Null(Order(world, first).LastEffectId);
        Assert.Equal(home.Position, Person(world, actor).Position);
        Assert.Equal(1, Order(world, second).CompletedUnits);
    }

    [Fact]
    public async Task ADelayedModelReplyCannotReviveACancelledShelterOrder()
    {
        var state = Prepared();
        var actor = Actor(state);
        var home = House(state);
        state = AwayFromHouse(state, actor, home);
        var provider = new ShelterChoices(DecisionProviderKind.LargeLanguageModel, hold: true);
        using var world = WorldRuntime.Restore(state, id => id == actor ? provider : new ShelterChoices());
        var receipt = Submit(world, actor, "held-shelter", "shelter in my House");
        try
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            Assert.True(world.CancelOrder(new("cancel-held-shelter", "owner:test", world.Society.WorldId, actor, receipt.InstructionId)).Changed);
            var stopped = Person(world, actor).Position;
            provider.Release.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitAsync(provider.Returned.Task);
            var admitted = false;
            for (var admissionTick = 0; admissionTick < 20 && !admitted; admissionTick++)
            {
                var step = await world.TickModelWorkAsync();
                Assert.True(step.Advanced);
                admitted = step.Decisions.Any(item => item.AgentId == actor && item.Admission.Accepted);
                if (!admitted) await ModelWorkHarness.For(world).SettleAsync();
            }
            Assert.True(admitted, "A post-release decision must actually be admitted before checking the cancelled order.");
            Assert.Equal(("cancelled", 0), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits));
            Assert.Equal(stopped, Person(world, actor).Position);
            Assert.Null(Order(world, receipt).LastEffectId);
            Assert.Null(Order(world, receipt).ShelterCompletion);
            using var replay = Reload(world);
        }
        finally { provider.Release.TrySetResult(true); }
    }

    [Fact]
    public async Task StrictReloadRejectsMalformedShelterProofsAndUnsupportedRequestsDoNotQueryTheModel()
    {
        var state = Prepared();
        var actor = Actor(state);
        using var world = Restore(state);
        var receipt = Submit(world, actor, "shelter-proof", "shelter in my House");
        await Tick(world);
        var completed = Order(world, receipt);
        Assert.Equal(1, completed.CompletedUnits);
        var saved = world.ExportState();
        foreach (var corrupt in new[]
        {
            completed with { ShelterBinding = null },
            completed with { ShelterCompletion = null },
            completed with { ShelterBinding = completed.ShelterBinding! with { BuildingPlacedTick = world.WorldTick + 1 } },
            completed with { ShelterBinding = completed.ShelterBinding! with { OwnerId = "household:missing" } },
            completed with { ShelterCompletion = completed.ShelterCompletion! with { WorldTick = world.WorldTick + 1 } },
            completed with { ShelterCompletion = completed.ShelterCompletion! with { FuelReservationId = Identity.Parse("invented-fuel") } },
        })
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(saved with
            {
                Instructions = saved.Instructions!.Select(item => item.InstructionId == receipt.InstructionId
                    ? item with { Task = corrupt } : item).ToArray(),
            }));
        var provider = new ShelterChoices(DecisionProviderKind.LargeLanguageModel);
        using var unsupported = WorldRuntime.Restore(state, id => id == actor ? provider : new ShelterChoices());
        var unknown = Submit(unsupported, actor, "unknown-shelter", "shelter inside the moon");
        Assert.Equal("not_understood", Order(unsupported, unknown).Status);
        Assert.Empty(provider.Requests);
        Assert.Contains(unknown.InstructionId.Value, unsupported.ExportState().CompletedInstructionIds!);
    }
}
