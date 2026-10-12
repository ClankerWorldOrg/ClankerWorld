using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using static ClankerWorld.Tests.HouseRelocationTestWorld;

namespace ClankerWorld.Tests;

public sealed class TwoHouseRelocationTests
{
    [Fact]
    public async Task ResolvingOneOvercrowdedHouseKeepsTheOtherHousesSavedNoticeAndDeadline()
    {
        const string secondHousehold = "household:camp-beta";
        var state = Crowded(residents: 8);
        var society = state.Society.Society;
        var secondResidents = society.GetHousehold(Household).MemberIds.Order(StringComparer.Ordinal).TakeLast(4).ToArray();
        foreach (var id in secondResidents)
        {
            society = SocietyRules.LeaveHousehold(society, id).Checkpoint;
            society = SocietyRules.JoinHouseholdCareGroup(society, id, secondHousehold).Checkpoint;
        }
        state = state with { Society = state.Society with { Society = society } };
        using var world = Restore(state);
        Assert.Equal(4, world.Society.GetHousehold(Household).MemberIds.Count);
        Assert.Equal(4, world.Society.GetHousehold(secondHousehold).MemberIds.Count);
        await AdvanceTo(world, 1);
        var notices = Noticed(world);
        Assert.Equal(2, notices.Length);
        var first = Assert.Single(notices, person => person.Housing!.Relocation!.HouseholdId == Household);
        var second = Assert.Single(notices, person => person.Housing!.Relocation!.HouseholdId == secondHousehold);
        var firstNotice = first.Housing!.Relocation!;
        var secondNotice = second.Housing!.Relocation!;
        Assert.Equal(Day, firstNotice.DeadlineTick - firstNotice.NoticeTick);
        Assert.Equal(Day, secondNotice.DeadlineTick - secondNotice.NoticeTick);

        using var restored = Restore(world.ExportState());
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(firstNotice, restored.Agents.Single(person => person.AgentId == first.AgentId).Housing!.Relocation);
        Assert.Equal(secondNotice, restored.Agents.Single(person => person.AgentId == second.AgentId).Housing!.Relocation);
        await AdvanceTo(restored, 4);
        var departing = restored.Society.GetHousehold(Household).MemberIds.First(id => id != first.AgentId);
        Assert.True(restored.DisplaceAdult(departing));
        await AdvanceTo(restored, 5);
        Assert.Equal(3, restored.Society.GetHousehold(Household).MemberIds.Count);
        Assert.Equal(4, restored.Society.GetHousehold(secondHousehold).MemberIds.Count);
        Assert.Equal(Household, restored.Society.GetAgent(first.AgentId).HouseholdId);
        Assert.Null(restored.Agents.Single(person => person.AgentId == first.AgentId).Departures);
        Assert.Equal(secondNotice, Assert.Single(Noticed(restored)).Housing!.Relocation);
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "relocation_cancelled" &&
            item.Detail == $"{first.AgentId}|{Household}|room");
        Assert.DoesNotContain(restored.ExportState().Events, item => item.Kind == "relocation_cancelled" &&
            item.Detail.StartsWith(second.AgentId + "|", StringComparison.Ordinal));

        using var continued = Restore(restored.ExportState());
        await AdvanceTo(continued, secondNotice.DeadlineTick - 1);
        Assert.Equal(secondHousehold, continued.Society.GetAgent(second.AgentId).HouseholdId);
        Assert.Equal(secondNotice, Assert.Single(Noticed(continued)).Housing!.Relocation);
        await AdvanceTo(continued, secondNotice.DeadlineTick);
        Assert.Null(continued.Society.GetAgent(second.AgentId).HouseholdId);
        var departure = Assert.Single(continued.Agents.Single(person => person.AgentId == second.AgentId).Departures!);
        Assert.Equal("displaced", departure.Cause);
        Assert.Equal(secondNotice.DeadlineTick, departure.Tick);
        Assert.Equal(3, continued.Society.GetHousehold(Household).MemberIds.Count);
        Assert.Equal(3, continued.Society.GetHousehold(secondHousehold).MemberIds.Count);
        Assert.Empty(Noticed(continued));
        Assert.Equal(2, continued.Agents.Sum(person => person.Departures?.Count ?? 0));
    }
}
