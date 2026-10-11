using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Tests;

public sealed class MultiInputCollectionRouteTests
{
    private static readonly Lazy<Task<byte[]>> Fixture = new(async () =>
    {
        using var world = NormalPathWorld.CreateGenerated("cart-set-unfinished", _ => new ActionCoverageRecorder(chooseIdle: true),
            size: WorldSizePreset.Small);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var actor = world.Inhabitants[0];
        world.SubmitInstruction(new("learn-for-writing", "owner:test", actor.InhabitantId, OwnerInstructionKind.MustDo,
            $"move to ({actor.Position.X},{actor.Position.Y})"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.Knowledge.Facts, fact => fact.OwnerId == actor.InhabitantId);
        return PrivateWorldRuntimeCodec.Encode(SettlementWeatherTestFixture.WithWeather(world.ExportState(), WeatherKind.Clear));
    });

    [Theory]
    [InlineData("book", "heavy")]
    [InlineData("cart", "heavy")]
    [InlineData("tool", "heavy")]
    [InlineData("book", "light")]
    [InlineData("cart", "light")]
    [InlineData("tool", "light")]
    [InlineData("book", "land")]
    [InlineData("cart", "land")]
    [InlineData("tool", "land")]
    [InlineData("book", "carried")]
    [InlineData("cart", "carried")]
    [InlineData("tool", "carried")]
    [InlineData("book", "impossible")]
    [InlineData("cart", "impossible")]
    [InlineData("tool", "impossible")]
    [InlineData("book", "split")]
    [InlineData("book", "split-reverse")]
    public async Task PreparationKeepsACollectionSequenceThatCanReachEveryRemainingInput(string task, string scenario)
    {
        var state = PrivateWorldRuntimeCodec.Decode(await Fixture.Value);
        var building = task == "tool"
            ? state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-blacksmith")
            : state.WorldSimulation!.Buildings.First(item => item.HouseholdId == state.Society.Society.GetInhabitant(
                state.Inhabitants[0].InhabitantId).HouseholdId && state.WorldContent!.Buildings.Single(
                definition => definition.CanonicalId == item.DefinitionId).Tags.Contains("house"));
        var actor = task == "tool"
            ? state.Society.Society.Inhabitants.First(person => person.HouseholdId == building.HouseholdId).Id
            : state.Inhabitants[0].InhabitantId;
        var household = state.Society.Society.GetInhabitant(actor).HouseholdId!;
        var remote = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
                !state.Map.IsReachableOnFoot(point, building.Position) &&
                SwimmingRules.IsReachable(state.Map, point, building.Position))
            .OrderBy(point => state.Map.FootDistance(point, building.Position)).First();
        var land = state.Map.FootNeighbors(building.Position).First(point => state.Map.IsPassable(point) &&
            state.Inhabitants.All(person => person.Position != point));
        var source = scenario == "land" ? land : remote;
        var start = task == "book" ? source : building.Position;
        Assert.Equal(scenario == "land", state.Map.IsReachableOnFoot(source, building.Position));
        var localKind = task == "book" ? "paper" : "wood";
        var remoteKind = task == "book" ? "cloth" : task == "cart" ? "iron_fittings" : "iron";
        var localQuantity = task == "book" ? 2 : 1;
        var ballast = (task == "tool" ? 2 : 3) - (scenario == "light" ? 1 : 0) + (scenario == "impossible" ? 1 : 0);
        var inventory = state.Society.Society.Inventory;
        var removed = inventory.Lots.Where(lot => lot.OwnerId == actor).Select(lot => lot.Id).ToHashSet();
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !removed.Contains(lot.Id)).Select(lot =>
                lot.OwnerId == household && (lot.ItemKind == localKind || lot.ItemKind == remoteKind ||
                    task == "cart" && lot.ItemKind == "rope") ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray(),
            Reservations = inventory.Reservations.Where(reservation => !removed.Contains(reservation.LotId)).ToArray(),
        };
        inventory = InventoryFixture.AddLot(inventory, "plan-ballast", "stone", actor, ballast);
        var split = scenario is "split" or "split-reverse";
        var localPosition = scenario == "split-reverse" ? building.Position : start;
        inventory = InventoryFixture.AddLot(inventory, "plan-local", localKind, household, split ? 1 : localQuantity,
            groundPosition: new(localPosition.X, localPosition.Y));
        if (split)
        {
            var extraPosition = scenario == "split-reverse" ? start : building.Position;
            inventory = InventoryFixture.AddLot(inventory, "plan-local-extra", "paper", household, 1,
                groundPosition: new(extraPosition.X, extraPosition.Y));
        }
        var remotePosition = task == "book" ? building.Position : source;
        inventory = InventoryFixture.AddLot(inventory, "plan-remote", remoteKind,
            scenario == "carried" ? actor : household, 1,
            groundPosition: scenario == "carried" ? null : new(remotePosition.X, remotePosition.Y));
        if (task == "cart")
        {
            inventory = InventoryFixture.AddLot(inventory, "plan-rope", "rope", household, 1,
                groundPosition: new(start.X, start.Y));
            inventory = InventoryFixture.AddLot(inventory, "plan-cart", "handcart", actor, 1,
                groundPosition: new(start.X, start.Y), conditionBasisPoints: 3_000);
        }
        if (task == "tool")
            inventory = InventoryFixture.AddLot(inventory, "plan-axe", "iron_axe", actor, 1, conditionBasisPoints: 3_000);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor ? person with
            {
                Position = start,
                HungerBasisPoints = 10_000,
                Survival = new(10_000),
                Equipment = null,
                Project = null,
                LastDecisionContext = null,
                TravelCooldownTicks = 0,
            } : person).ToArray(),
        };
        using var world = PrivateWorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        var instruction = task == "book" ? "Write 1 book" : task == "cart" ? "Repair cart plan-cart" : "repair my iron axe";
        var receipt = world.SubmitInstruction(new("plan-order", "owner:test", actor, OwnerInstructionKind.MustDo, instruction));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var first = world.ExportState();
        var order = Assert.Single(first.Instructions!, item => item.InstructionId == receipt.InstructionId).Order!;
        if (scenario == "impossible")
        {
            for (var tick = 0; tick < 6; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(household, world.Society.Inventory.GetLot("plan-local").OwnerId);
            Assert.Equal(localQuantity, world.Society.Inventory.GetLot("plan-local").Quantity);
            Assert.Equal(household, world.Society.Inventory.GetLot("plan-remote").OwnerId);
            Assert.Equal(1, world.Society.Inventory.GetLot("plan-remote").Quantity);
            Assert.Equal(ballast, world.Society.Inventory.GetLot("plan-ballast").Quantity);
            Assert.Equal(0, world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!.CompletedUnits);
            Assert.Empty(world.Knowledge.Artifacts);
            if (task != "book") Assert.Equal(3_000, world.Society.Inventory.GetLot(task == "cart" ? "plan-cart" : "plan-axe").ConditionBasisPoints);
            var blockedBytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
            using var blockedReload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(blockedBytes));
            Assert.Equal(blockedBytes, PrivateWorldRuntimeCodec.Encode(blockedReload.ExportState()));
            return;
        }
        Assert.Equal("doing", order.Status);
        // A heavy swimmer must fetch the far ingredient before the local load closes that route.
        Assert.Equal(scenario is "heavy" or "split-reverse" ? household : actor, world.Society.Inventory.GetLot("plan-local").OwnerId);
        var bytes = PrivateWorldRuntimeCodec.Encode(first);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var reloaded = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(reloaded.ExportState()));
        for (var tick = 0; tick < 6; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(PrivateWorldRuntimeCodec.Encode(world.ExportState()), PrivateWorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        order = reloaded.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!;
        for (var tick = 0; tick < 140 && order.Status != "finished"; tick++)
        {
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            order = reloaded.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!;
        }
        Assert.Equal("finished", order.Status);
        Assert.Equal(1, order.CompletedUnits);
        Assert.Equal(ballast, reloaded.Society.Inventory.GetLot("plan-ballast").Quantity);
        Assert.DoesNotContain(reloaded.Society.Inventory.Lots, lot => lot.Id == "plan-local" || lot.Id == "plan-local-extra" || lot.Id == "plan-remote" ||
            task == "cart" && lot.Id == "plan-rope");
        if (task == "book") Assert.Single(reloaded.Knowledge.Artifacts);
        else Assert.Equal(10_000, reloaded.Society.Inventory.GetLot(task == "cart" ? "plan-cart" : "plan-axe").ConditionBasisPoints);
        reloaded.Validate();
        var final = PrivateWorldRuntimeCodec.Encode(reloaded.ExportState());
        using var finalReload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(final));
        Assert.Equal(final, PrivateWorldRuntimeCodec.Encode(finalReload.ExportState()));
    }
}
