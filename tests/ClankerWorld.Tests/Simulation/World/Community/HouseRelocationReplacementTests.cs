using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Kernel;
using static ClankerWorld.Tests.HouseRelocationTestWorld;

namespace ClankerWorld.Tests;

public sealed class HouseRelocationReplacementTests
{
    [Theory]
    [InlineData("at")]
    [InlineData("before")]
    [InlineData("overdue-save")]
    public async Task CaregiverReplacementRespectsFutureAndExpiredDeadlinesAcrossReload(string timing)
    {
        var late = timing != "before";
        using var original = Restore(Crowded());
        await AdvanceTo(original, 1);
        var caregiver = Assert.Single(Noticed(original));
        var inherited = caregiver.Housing!.Relocation!;
        await AdvanceTo(original, inherited.DeadlineTick - (late ? 1 : 2));
        var child = original.Society.GetHousehold(Household).MemberIds.First(id => id != caregiver.AgentId);
        var prepared = WithDependent(original.ExportState(), caregiver.AgentId, child);
        if (timing == "overdue-save")
            // A valid saved notice can already be overdue on reload. Keep its full-day
            // duration and test resumption without fabricating an advanced world clock.
            prepared = prepared with
            {
                Agents = prepared.Agents.Select(person => person.AgentId == caregiver.AgentId
                    ? person with
                    {
                        Housing = person.Housing! with
                        {
                            Relocation = inherited with
                            { NoticeTick = inherited.NoticeTick - 1, DeadlineTick = inherited.DeadlineTick - 1 }
                        }
                    } : person).ToArray(),
            };
        using var changed = Restore(prepared);
        using var replay = Restore(changed.ExportState());
        var notifiedTick = changed.WorldTick + 1;
        await AdvanceTo(changed, notifiedTick);
        await AdvanceTo(replay, notifiedTick);
        Assert.Equal(WorldRuntimeCodec.Encode(changed.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var replacement = Assert.Single(Noticed(changed));
        Assert.NotEqual(caregiver.AgentId, replacement.AgentId);
        var expected = late ? notifiedTick + Day : inherited.DeadlineTick;
        Assert.Equal(expected, replacement.Housing!.Relocation!.DeadlineTick);
        Assert.Equal(Household, changed.Society.GetAgent(caregiver.AgentId).HouseholdId);
        Assert.Equal(Household, changed.Society.GetAgent(child).HouseholdId);
        Assert.Equal(caregiver.AgentId, changed.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Null(changed.Agents.Single(person => person.AgentId == replacement.AgentId).Departures);
        changed.Pause();
        var paused = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.False((await changed.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(paused, WorldRuntimeCodec.Encode(changed.ExportState()));
        using var restored = Restore(changed.ExportState());
        await AdvanceTo(changed, expected - 1);
        await AdvanceTo(restored, expected - 1);
        Assert.Equal(Household, changed.Society.GetAgent(replacement.AgentId).HouseholdId);
        await AdvanceTo(changed, expected);
        await AdvanceTo(restored, expected);
        Assert.Equal(WorldRuntimeCodec.Encode(changed.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Null(changed.Society.GetAgent(replacement.AgentId).HouseholdId);
        var departed = changed.Agents.Single(person => person.AgentId == replacement.AgentId);
        Assert.Equal(expected, Assert.Single(departed.Departures!).Tick);
        Assert.Empty(Noticed(changed));
    }

    [Fact]
    public async Task MajorityAfterTwoVolunteersLeaveGivesThePreviouslyReplacedResidentAFreshDay()
    {
        var state = Crowded(residents: 7);
        var ids = state.Society.Society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "a-replacement-food", "food", Household, 7,
            storageBuildingId: HouseId);
        inventory = InventoryAuthority.AddLot(inventory, "replacement-coat", "padded_coat", ids[3], 1, storageBuildingId: HouseId);
        inventory = InventoryAuthority.AddLot(inventory, "replacement-loan", "wooden_axe", Household, 1);
        inventory = InventoryAuthority.Relocate(inventory, "replacement-borrow", "replacement-loan", Household, 1, ids[3]);
        using var initial = Restore(WithInventory(state, inventory));
        await AdvanceTo(initial, 1);
        var deadline = Assert.Single(Noticed(initial).Select(person => person.Housing!.Relocation!.DeadlineTick).Distinct());
        Assert.Equal(ids.Take(4), Noticed(initial).Select(person => person.AgentId));
        await AdvanceTo(initial, 2);
        state = await VolunteerAsync(initial.ExportState(), ids[4], deadline);
        Assert.Null(state.Agents.Single(person => person.AgentId == ids[3]).Housing?.Relocation);
        state = await VolunteerAsync(state, ids[5], deadline);
        Assert.Null(state.Agents.Single(person => person.AgentId == ids[2]).Housing?.Relocation);
        using var waiting = Restore(state);
        await AdvanceTo(waiting, deadline - 1);
        state = waiting.ExportState();
        // Controlled family formation immediately before the due tick. The notices
        // and both prior volunteer choices were produced through native decisions.
        var family = ids.Take(3).ToHashSet(StringComparer.Ordinal);
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => family.Contains(person.Id)
                        ? person with { DomesticFamilyUnitId = "late-replacement-family" } : person).ToArray(),
                }
            },
        };
        var residents = state.Society.Society.Agents.Where(person => person.HouseholdId == Household).ToArray();
        Assert.Null(HouseRelocationRules.DominantFamily(residents));
        Assert.Null(HouseRelocationRules.DominantFamily(residents.Where(person => person.Id != ids[4]).ToArray()));
        Assert.Equal("late-replacement-family", HouseRelocationRules.DominantFamily(residents.Where(person =>
            person.Id != ids[4] && person.Id != ids[5]).ToArray()));
        using var world = Restore(state);
        using var replay = Restore(state);
        await AdvanceTo(world, deadline);
        await AdvanceTo(replay, deadline);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        foreach (var volunteer in ids.Skip(4).Take(2))
        {
            Assert.Null(world.Society.GetAgent(volunteer).HouseholdId);
            Assert.Equal(deadline, Assert.Single(world.Agents.Single(person => person.AgentId == volunteer).Departures!).Tick);
        }
        var replacement = Assert.Single(Noticed(world));
        Assert.Equal(ids[3], replacement.AgentId);
        Assert.Equal(deadline, replacement.Housing!.Relocation!.NoticeTick);
        Assert.Equal(deadline + Day, replacement.Housing.Relocation.DeadlineTick);
        Assert.Equal(Household, world.Society.GetAgent(ids[3]).HouseholdId);
        Assert.All(ids.Take(3), id => Assert.Equal(Household, world.Society.GetAgent(id).HouseholdId));
        Assert.Equal(5, world.Society.GetHousehold(Household).MemberIds.Count);
        using var reloaded = Restore(world.ExportState());
        await AdvanceTo(world, deadline + Day - 1);
        await AdvanceTo(reloaded, deadline + Day - 1);
        Assert.Equal(Household, world.Society.GetAgent(ids[3]).HouseholdId);
        await AdvanceTo(world, deadline + Day);
        await AdvanceTo(reloaded, deadline + Day);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var departed = Assert.Single(world.Agents.Single(person => person.AgentId == ids[3]).Departures!);
        Assert.Equal((deadline + Day, 2), (departed.Tick, departed.AllowancePortions));
        Assert.Equal((ids[3], HouseId), (world.Society.Inventory.GetLot("replacement-coat").OwnerId,
            world.Society.Inventory.GetLot("replacement-coat").StorageBuildingId));
        Assert.Equal((Household, ids[3]), (world.Society.Inventory.GetLot("replacement-loan").OwnerId,
            world.Society.Inventory.GetLot("replacement-loan").CarrierId));
        Assert.Equal(4, world.Society.GetHousehold(Household).MemberIds.Count);
        Assert.Empty(Noticed(world));
    }

    private static async Task<WorldRuntimeState> VolunteerAsync(WorldRuntimeState state, string actor, long deadline)
    {
        var choices = new Choices();
        choices.Wanted[actor] = "household_volunteer";
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
            ? person with { LastDecisionContext = null } : person).ToArray()
        };
        using var world = Restore(state, choices);
        for (var attempt = 0; attempt < 4 && world.Agents.Single(person => person.AgentId == actor).Housing?.Relocation?.Reason != HouseRelocationRules.Volunteer; attempt++)
            await AdvanceTo(world, world.WorldTick + 1);
        Assert.True(choices.Offered.TryGetValue(actor, out var offered), "The volunteer must receive a native decision frame.");
        Assert.Contains("household_volunteer", offered);
        var notice = world.Agents.Single(person => person.AgentId == actor).Housing!.Relocation!;
        Assert.Equal(HouseRelocationRules.Volunteer, notice.Reason);
        Assert.Equal(deadline, notice.DeadlineTick);
        return world.ExportState();
    }

}
