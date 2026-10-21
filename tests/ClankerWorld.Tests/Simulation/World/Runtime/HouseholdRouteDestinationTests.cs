using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HouseholdRouteDestinationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MovementOrderReachesItsOwnHouseWithOrWithoutAHousemateAcrossReplay(bool occupied)
    {
        Func<string, IDecisionProvider> providers = _ => new ActionCoverageRecorder(chooseIdle: true);
        using var generated = NormalPathWorld.CreateGenerated("household-route-destination", providers);
        var state = generated.ExportState();
        const string actor = NormalPathWorld.Founder1;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var members = state.Society.Society.GetHousehold(household).MemberIds.ToArray();
        var housemate = members.First(id => id != actor);
        var house = state.WorldSimulation!.Buildings.Single(building => building.HouseholdId == household &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        var definition = state.WorldContent!.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
        var entrance = BuildingMovementRules.EntranceFor(definition, house);
        var buildings = state.WorldSimulation.Buildings.Select(building => building.Position).ToHashSet();
        var free = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
            !buildings.Contains(point) && state.Map.FootDistance(point, house.Position) > 3).Take(state.Agents.Count).ToArray();
        Assert.Equal(state.Agents.Count, free.Length);
        state = WeatherTestFixture.WithWeather(state with
        {
            Agents = state.Agents.Select((person, index) => person with
            {
                Position = person.AgentId == actor ? entrance :
                    person.AgentId == housemate && occupied ? house.Position : free[index],
                FullnessBasisPoints = 10_000,
                TravelCooldownTicks = 0,
                Project = null,
                LastDecisionContext = null,
            }).ToArray(),
        }, WeatherKind.Clear);
        var housematePosition = state.Agents.Single(person => person.AgentId == housemate).Position;
        using var world = WorldRuntime.Restore(state, providers);
        var receipt = world.SubmitInstruction(new("home-destination", "owner:test", actor, OwnerInstructionKind.Order,
            FormattableString.Invariant($"move to {house.Position.X},{house.Position.Y}")));
        OrderTask Order(WorldRuntime instance) => instance.ExportState().Instructions!
            .Single(instruction => instruction.InstructionId == receipt.InstructionId).Task!;
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), providers);
        for (var tick = 0; tick < 10 && Order(world).Status != "finished"; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal("finished", Order(world).Status);
        Assert.Equal(1, Order(world).CompletedUnits);
        Assert.Equal(house.Position, world.Agents.Single(person => person.AgentId == actor).Position);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), providers);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(members, restored.Society.GetHousehold(household).MemberIds);
        Assert.Equal(housematePosition, restored.Agents.Single(person => person.AgentId == housemate).Position);
        restored.Validate();
    }
}
