using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MultiInputCollectionRouteTests
{
    private static readonly Lazy<Task<byte[]>> Fixture = new(async () =>
    {
        using var world = NormalPathWorld.CreateGenerated("cart-set-unfinished", _ => new ActionCoverageRecorder(chooseIdle: true),
            roughTownSite: new(110, 50), size: WorldSizePreset.Small);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var actor = world.Agents[0];
        world.SubmitInstruction(new("learn-for-writing", "owner:test", actor.AgentId, OwnerInstructionKind.Order,
            $"move to ({actor.Position.X},{actor.Position.Y})"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.Knowledge.Facts, fact => fact.OwnerId == actor.AgentId);
        return WorldRuntimeCodec.Encode(WeatherTestFixture.WithWeather(world.ExportState(), WeatherKind.Clear));
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
    [InlineData("tool", "alternate-stock")]
    public async Task PreparationKeepsACollectionSequenceThatCanReachEveryRemainingInput(string task, string scenario)
    {
        var state = WorldRuntimeCodec.Decode(await Fixture.Value);
        var building = task == "tool"
            ? state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-blacksmith")
            : state.WorldSimulation!.Buildings.First(item => item.HouseholdId == state.Society.Society.GetAgent(
                state.Agents[0].AgentId).HouseholdId && state.WorldContent!.Buildings.Single(
                definition => definition.CanonicalId == item.DefinitionId).Tags.Contains("house"));
        var actor = task == "tool"
            ? state.Society.Society.Agents.First(person => person.HouseholdId == building.HouseholdId).Id
            : state.Agents[0].AgentId;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var remote = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
                !state.Map.IsReachableOnFoot(point, building.Position) &&
                SwimmingRules.IsReachable(state.Map, point, building.Position))
            .OrderBy(point => state.Map.FootDistance(point, building.Position)).First();
        var land = state.Map.FootNeighbors(building.Position).First(point => state.Map.IsPassable(point) &&
            state.Agents.All(person => person.Position != point));
        var source = scenario == "land" ? land : remote;
        var start = task == "book" ? source : building.Position;
        Assert.Equal(scenario == "land", state.Map.IsReachableOnFoot(source, building.Position));
        var localKind = task == "book" ? "paper" : "wood";
        var remoteKind = task == "book" ? "cloth" : task == "cart" ? "iron_fittings" : "iron";
        var localQuantity = task == "book" ? 2 : 1;
        var ballast = (task == "tool" ? 2 : 3) - (scenario == "light" ? 1 : 0) +
            (scenario is "impossible" or "alternate-stock" ? 1 : 0);
        var inventory = state.Society.Society.Inventory;
        var removed = inventory.Lots.Where(lot => lot.OwnerId == actor).Select(lot => lot.Id).ToHashSet();
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !removed.Contains(lot.Id)).Select(lot =>
                lot.OwnerId == household && (lot.ItemKind == localKind || lot.ItemKind == remoteKind ||
                    task == "cart" && lot.ItemKind == "rope") ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray(),
            Reservations = inventory.Reservations.Where(reservation => !removed.Contains(reservation.LotId)).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "plan-ballast", "stone", actor, ballast);
        var split = scenario is "split" or "split-reverse";
        var localPosition = scenario == "split-reverse" ? building.Position : start;
        inventory = InventoryAuthority.AddLot(inventory, "plan-local", localKind, household, split ? 1 : localQuantity,
            groundPosition: new(localPosition.X, localPosition.Y));
        if (split)
        {
            var extraPosition = scenario == "split-reverse" ? start : building.Position;
            inventory = InventoryAuthority.AddLot(inventory, "plan-local-extra", "paper", household, 1,
                groundPosition: new(extraPosition.X, extraPosition.Y));
        }
        var remotePosition = task == "book" ? building.Position : source;
        inventory = InventoryAuthority.AddLot(inventory, "plan-remote", remoteKind,
            scenario == "carried" ? actor : household, 1,
            groundPosition: scenario == "carried" ? null : new(remotePosition.X, remotePosition.Y));
        if (scenario == "alternate-stock")
            inventory = InventoryAuthority.AddLot(inventory, "plan-remote-alternative", remoteKind, household, 1,
                groundPosition: new(land.X, land.Y));
        if (task == "cart")
        {
            inventory = InventoryAuthority.AddLot(inventory, "plan-rope", "rope", household, 1,
                groundPosition: new(start.X, start.Y));
            inventory = InventoryAuthority.AddLot(inventory, "plan-cart", "handcart", actor, 1,
                groundPosition: new(start.X, start.Y), conditionBasisPoints: 3_000);
        }
        if (task == "tool")
            inventory = InventoryAuthority.AddLot(inventory, "plan-axe", "iron_axe", actor, 1, conditionBasisPoints: 3_000);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = start,
                FullnessBasisPoints = 10_000,
                Survival = new(10_000),
                Equipment = null,
                Project = null,
                LastDecisionContext = null,
                TravelCooldownTicks = 0,
            } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        if (scenario == "alternate-stock")
        {
            var query = typeof(WorldRuntime).GetMethod("SharedPreparationItem",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Assert.Equal("plan-remote", Assert.IsType<InventoryLot>(query.Invoke(world, [remoteKind, actor, null, 0])).Id);
            Assert.Equal("plan-remote-alternative", Assert.IsType<InventoryLot>(query.Invoke(world,
                [remoteKind, actor, building.Position, 0])).Id);
        }
        var instruction = task == "book" ? "Write 1 book" : task == "cart" ? "Repair cart plan-cart" : "repair my iron axe";
        var receipt = world.SubmitInstruction(new("plan-order", "owner:test", actor, OwnerInstructionKind.Order, instruction));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var first = world.ExportState();
        var order = Assert.Single(first.Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
        if (scenario == "impossible")
        {
            for (var tick = 0; tick < 6; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(household, world.Society.Inventory.GetLot("plan-local").OwnerId);
            Assert.Equal(localQuantity, world.Society.Inventory.GetLot("plan-local").Quantity);
            Assert.Equal(household, world.Society.Inventory.GetLot("plan-remote").OwnerId);
            Assert.Equal(1, world.Society.Inventory.GetLot("plan-remote").Quantity);
            Assert.Equal(ballast, world.Society.Inventory.GetLot("plan-ballast").Quantity);
            Assert.Equal(0, world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Task!.CompletedUnits);
            Assert.Empty(world.Knowledge.Artifacts);
            if (task != "book") Assert.Equal(3_000, world.Society.Inventory.GetLot(task == "cart" ? "plan-cart" : "plan-axe").ConditionBasisPoints);
            var blockedBytes = WorldRuntimeCodec.Encode(world.ExportState());
            using var blockedReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(blockedBytes));
            Assert.Equal(blockedBytes, WorldRuntimeCodec.Encode(blockedReload.ExportState()));
            return;
        }
        Assert.Equal("doing", order.Status);
        // A heavy swimmer must fetch the far ingredient before the local load closes that route.
        Assert.Equal(scenario is "heavy" or "split-reverse" ? household : actor, world.Society.Inventory.GetLot("plan-local").OwnerId);
        var bytes = WorldRuntimeCodec.Encode(first);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        for (var tick = 0; tick < 6; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        order = reloaded.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Task!;
        for (var tick = 0; tick < 140 && order.Status != "finished"; tick++)
        {
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            order = reloaded.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Task!;
        }
        Assert.Equal("finished", order.Status);
        Assert.Equal(1, order.CompletedUnits);
        Assert.Equal(ballast, reloaded.Society.Inventory.GetLot("plan-ballast").Quantity);
        Assert.DoesNotContain(reloaded.Society.Inventory.Lots, lot => lot.Id == "plan-local" || lot.Id == "plan-local-extra" ||
            lot.Id == (scenario == "alternate-stock" ? "plan-remote-alternative" : "plan-remote") ||
            task == "cart" && lot.Id == "plan-rope");
        if (scenario == "alternate-stock")
        {
            Assert.Equal(household, reloaded.Society.Inventory.GetLot("plan-remote").OwnerId);
            Assert.Equal(1, reloaded.Society.Inventory.GetLot("plan-remote").Quantity);
        }
        if (task == "book") Assert.Single(reloaded.Knowledge.Artifacts);
        else Assert.Equal(10_000, reloaded.Society.Inventory.GetLot(task == "cart" ? "plan-cart" : "plan-axe").ConditionBasisPoints);
        reloaded.Validate();
        var final = WorldRuntimeCodec.Encode(reloaded.ExportState());
        using var finalReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final));
        Assert.Equal(final, WorldRuntimeCodec.Encode(finalReload.ExportState()));
    }
}
