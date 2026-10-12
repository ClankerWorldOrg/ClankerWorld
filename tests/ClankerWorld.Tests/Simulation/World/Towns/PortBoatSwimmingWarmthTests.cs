using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class PortBoatRuntimeTests
{
    [Fact]
    public async Task NativeFreshwaterBoatPassengerKeepsOrdinaryWeatherExposureAcrossReload()
    {
        using var voyage = new BoatScenario(WorldRuntimeCodec.Decode(await BuildPaidBoatAsync(freshwater: true)),
            new() { Trips = true });
        await voyage.UntilAsync(() => voyage.World.Boats[0].Journey is not null, 120);
        var state = voyage.World.ExportState();
        var actor = BoatPolicy.Author;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Survival = new SurvivalCondition(8_000), Equipment = null } : person).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Rain);
        using var world = new BoatScenario(state, new() { IdleActors = { actor } });
        var before = WorldRuntimeCodec.Encode(world.World.ExportState());
        Assert.False((await world.World.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.World.ExportState()));
        using var replay = new BoatScenario(WorldRuntimeCodec.Decode(before), new() { IdleActors = { actor } });
        var previousWarmth = 8_000;
        for (var tick = 0; tick < 4; tick++)
        {
            Assert.True((await world.World.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.World.AdvanceOneTickAsync()).Advanced);
            var boat = Assert.Single(world.World.Boats);
            Assert.Equal(actor, boat.Journey!.PassengerId);
            var passenger = world.World.Agents.Single(person => person.AgentId == actor);
            Assert.Equal(boat.Position, passenger.Position);
            Assert.True(SwimmingRules.IsSwimmingWater(world.World.ExportState().Map, passenger.Position));
            var warmth = passenger.Survival!.WarmthBasisPoints;
            Assert.InRange(previousWarmth - warmth, 25, 40);
            previousWarmth = warmth;
            Assert.Equal(WorldRuntimeCodec.Encode(world.World.ExportState()), WorldRuntimeCodec.Encode(replay.World.ExportState()));
        }
        var saved = WorldRuntimeCodec.Encode(world.World.ExportState());
        using var loaded = new BoatScenario(WorldRuntimeCodec.Decode(saved), new());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.World.ExportState()));
        world.World.Validate();
    }
}
