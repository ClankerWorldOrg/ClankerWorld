using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldConstructionOrderTests
{
    [Fact]
    public async Task AConstructionOrderCannotPlanThroughTheBuildersHouseWalls()
    {
        var setup = await Baseline.Value;
        var state = MovementTestWorld.ClearOtherBuildingAccess(Decode(setup), setup.Actor);
        var house = Home(state);
        var blocker = state.Agents.First(person => person.AgentId != setup.Actor).AgentId;
        var aside = MovementTestWorld.OpenGround(state, blocker, house.Position,
            allowed: point => state.Map.FootDistance(point, house.Position) > 2);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == setup.Actor ? person with { Position = house.Position }
                : person.AgentId == blocker ? person with { Position = house.Entrance!.Value } : person).ToArray(),
        };
        using var world = Restore(state);
        var receipt = Submit(world, setup.Actor, "closed-builder-door", BuildClinic(setup.Site));
        await Tick(world);
        Assert.Equal("blocked", Order(world, receipt).Status);
        Assert.Contains("can't be reached", Order(world, receipt).BlockedReason, StringComparison.Ordinal);
        Assert.Null(Person(world, setup.Actor).Project);
        AssertInputs(world, 10, 4);
        using var replay = Reload(world);
        await TickTogether(world, replay);
        Assert.Equal("blocked", Order(world, receipt).Status);
        AssertInputs(world, 10, 4);

        using var open = Restore(state with
        {
            Agents = state.Agents.Select(person => person.AgentId == blocker ? person with { Position = aside } : person).ToArray(),
        });
        var allowed = Submit(open, setup.Actor, "open-builder-door", BuildClinic(setup.Site));
        await Tick(open);
        Assert.Equal(setup.CandidateId, Person(open, setup.Actor).Project!.CandidateId);
        Assert.NotEqual("blocked", Order(open, allowed).Status);
        AssertInputs(open, 10, 4);
    }
}
