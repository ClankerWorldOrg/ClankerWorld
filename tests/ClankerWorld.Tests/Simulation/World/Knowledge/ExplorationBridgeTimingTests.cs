using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ExplorationBridgeTimingTests
{
    private static readonly GridPoint Start = new(0, 101);
    private static readonly GridPoint Water = new(0, 102);

    [Fact]
    public async Task ABridgePlacedAfterTheFirstActualStepInTheSameTickKeepsThatStepSaveable()
    {
        using var world = ExplorationBridgeSaveTests.CreateScoutingWorld();
        var actor = world.Agents[0].AgentId;
        for (var tick = 0; tick < 400 && world.Agents[0].Exploration!.OutingPath.Count < 2; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var before = world.ExportState();
        var exploration = before.Agents.Single(person => person.AgentId == actor).Exploration!;
        Assert.Equal([Start, Water], exploration.OutingPath);
        Assert.Equal(world.WorldTick, exploration.LastOutingTick);
        Assert.True(before.Map.CanFootStep(Start, Water));
        var beforeBytes = WorldRuntimeCodec.Encode(before);
        using (var unbridged = WorldRuntime.Restore(WorldRuntimeCodec.Decode(beforeBytes)))
            Assert.Equal(beforeBytes, WorldRuntimeCodec.Encode(unbridged.ExportState()));

        var workshop = world.WorldContent.Buildings.Single(item => item.LocalId == "workshop");
        var placed = world.PlaceBuilding("same-tick-road-workshop", workshop.CanonicalId, new(3, 102));
        Assert.True(placed.Applied, placed.Failure);
        var bridge = Assert.Single(world.Bridges, item => item.Id == "bridge-0-102-ew-2");
        Assert.Equal("bridge-0-102-ew-2", bridge.Id);
        Assert.Equal(exploration.LastOutingTick, bridge.BuiltTick);
        var after = world.ExportState();
        Assert.Equal(exploration, after.Agents.Single(person => person.AgentId == actor).Exploration);
        Assert.Equal(before.Knowledge!.Facts, after.Knowledge!.Facts);
        Assert.False(after.Map.CanFootStep(Start, Water));
        foreach (var cost in workshop.BuildCosts)
            Assert.Equal(cost.Amount, before.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity)
                - after.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity));
        var saved = WorldRuntimeCodec.Encode(after);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        using var direct = WorldRuntime.Restore(after);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(direct.ExportState()));
    }

    [Theory]
    [InlineData("nonadjacent")]
    [InlineData("river_diagonal")]
    [InlineData("out_of_bounds")]
    public async Task HistoricalPathValidationStillRefusesMalformedTravel(string scenario)
    {
        using var world = await CreateWorldWithLaterPaidBridge();
        var state = world.ExportState();
        var destination = scenario switch
        {
            "nonadjacent" => new GridPoint(0, 103),
            "river_diagonal" => new GridPoint(1, 102),
            "out_of_bounds" => new GridPoint(-1, 101),
            _ => Start,
        };
        Assert.False(state.Map.CanFootStep(Start, destination));
        if (scenario != "out_of_bounds") Assert.True(state.Map.IsPassable(destination));
        var actor = state.Agents[0].AgentId;
        var tampered = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Exploration = new ExplorationState([Start], [Start, destination], 0, true, []),
            } : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(tampered));
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(tampered));
    }

    private static async Task<WorldRuntime> CreateWorldWithLaterPaidBridge()
    {
        var world = ExplorationBridgeSaveTests.CreateScoutingWorld();
        try
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var before = world.ExportState();
            var workshop = world.WorldContent.Buildings.Single(item => item.LocalId == "workshop");
            var placed = world.PlaceBuilding("later-malformed-control-workshop", workshop.CanonicalId, new(3, 102));
            Assert.True(placed.Applied, placed.Failure);
            var bridge = Assert.Single(world.Bridges, item => item.Id == "bridge-0-102-ew-2");
            Assert.Equal("bridge-0-102-ew-2", bridge.Id);
            Assert.Equal(1, bridge.BuiltTick);
            Assert.True(bridge.BuiltTick > 0); // Every damaged outing below claims start tick zero.
            var state = world.ExportState();
            foreach (var cost in workshop.BuildCosts)
                Assert.Equal(cost.Amount, before.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity)
                    - state.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity));
            var bytes = WorldRuntimeCodec.Encode(state);
            using var valid = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(valid.ExportState()));
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }
}
