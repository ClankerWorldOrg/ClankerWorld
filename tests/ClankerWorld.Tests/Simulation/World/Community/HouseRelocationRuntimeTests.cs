using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using static ClankerWorld.Tests.HouseRelocationTestWorld;

namespace ClankerWorld.Tests;

public sealed class HouseRelocationRuntimeTests
{
    [Fact]
    public async Task NoticeUsesOneWorldDayAndPausingReloadingAndReplayDoNotRestartIt()
    {
        using var world = Restore(Crowded());
        await AdvanceTo(world, 1);
        var actor = Assert.Single(Noticed(world));
        var notice = actor.Housing!.Relocation!;
        Assert.Equal(world.WorldTick, notice.NoticeTick);
        Assert.Equal(Day, notice.DeadlineTick - notice.NoticeTick);
        await AdvanceTo(world, notice.DeadlineTick - 1);
        Assert.Equal(Household, world.Society.GetAgent(actor.AgentId).HouseholdId);
        Assert.Equal(notice, Assert.Single(Noticed(world)).Housing!.Relocation);
        world.Pause();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(world.ExportState());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
        await AdvanceTo(world, notice.DeadlineTick);
        await AdvanceTo(replay, notice.DeadlineTick);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Null(world.Society.GetAgent(actor.AgentId).HouseholdId);
        var departure = Assert.Single(world.Agents.Single(person => person.AgentId == actor.AgentId).Departures!);
        Assert.Equal("displaced", departure.Cause);
        Assert.Equal(notice.DeadlineTick, departure.Tick);
        Assert.Empty(Noticed(world));
        Assert.Equal(3, world.Society.GetHousehold(Household).MemberIds.Count);
    }

    [Fact]
    public async Task DueNoticesMoveOnlyTheAdultsNeededToMakeTheRemainingHouseFit()
    {
        using var world = Restore(Crowded(residents: 5));
        await AdvanceTo(world, 1);
        var noticed = Noticed(world);
        Assert.Equal(2, noticed.Length);
        var deadline = noticed[0].Housing!.Relocation!.DeadlineTick;
        Assert.All(noticed, person => Assert.Equal(deadline, person.Housing!.Relocation!.DeadlineTick));
        await AdvanceTo(world, deadline);
        Assert.Equal(3, world.Society.GetHousehold(Household).MemberIds.Count);
        Assert.All(noticed, person => Assert.Null(world.Society.GetAgent(person.AgentId).HouseholdId));
        Assert.Equal(2, world.Agents.Sum(person => person.Departures?.Count ?? 0));
        Assert.Empty(Noticed(world));
        await AdvanceTo(world, deadline + Day);
        Assert.Equal(3, world.Society.GetHousehold(Household).MemberIds.Count);
        Assert.Equal(2, world.Agents.Sum(person => person.Departures?.Count ?? 0));
    }

    [Fact]
    public async Task AnotherMembersDepartureCancelsTheUnneededNoticeBeforeItsDeadline()
    {
        using var world = Restore(Crowded());
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        var deadline = noticed.Housing!.Relocation!.DeadlineTick;
        await AdvanceTo(world, deadline - 1);
        var leaving = world.Society.GetHousehold(Household).MemberIds.First(id => id != noticed.AgentId);
        Assert.True(world.DisplaceAdult(leaving));
        await AdvanceTo(world, deadline);
        Assert.Equal(Household, world.Society.GetAgent(noticed.AgentId).HouseholdId);
        Assert.Empty(Noticed(world));
        Assert.Null(world.Agents.Single(person => person.AgentId == noticed.AgentId).Departures);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "relocation_cancelled" &&
            item.Detail == $"{noticed.AgentId}|{Household}|room");
    }

    [Fact]
    public async Task NewFamilyMajorityCancelsTheNoticeWhenTheCompletedHouseNowFits()
    {
        using var world = Restore(Crowded());
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        var notice = noticed.Housing!.Relocation!;
        await AdvanceTo(world, notice.DeadlineTick - 1);
        var state = world.ExportState();
        var family = state.Society.Society.Agents.Take(3).Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => family.Contains(person.Id)
                        ? person with { DomesticFamilyUnitId = "relocation-new-family" } : person).ToArray(),
                },
            },
        };
        using var changed = Restore(state);
        await AdvanceTo(changed, notice.DeadlineTick + 1);
        Assert.Empty(Noticed(changed));
        Assert.Equal(4, changed.Society.GetHousehold(Household).MemberIds.Count);
        Assert.All(changed.Agents, person => Assert.Null(person.Departures));
    }

    [Fact]
    public async Task BecomingASoleCaregiverCancelsAnExistingForcedNotice()
    {
        using var world = Restore(Crowded());
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        var notice = noticed.Housing!.Relocation!;
        await AdvanceTo(world, 5);
        var child = world.Society.GetHousehold(Household).MemberIds.First(id => id != noticed.AgentId);
        using var changed = Restore(WithDependent(world.ExportState(), noticed.AgentId, child));
        await AdvanceTo(changed, 6);
        var replacement = Assert.Single(Noticed(changed));
        Assert.NotEqual(noticed.AgentId, replacement.AgentId);
        Assert.Equal(notice.DeadlineTick, replacement.Housing!.Relocation!.DeadlineTick);
        await AdvanceTo(changed, notice.DeadlineTick);
        Assert.Equal(Household, changed.Society.GetAgent(noticed.AgentId).HouseholdId);
        Assert.Equal(Household, changed.Society.GetAgent(child).HouseholdId);
        Assert.Equal(noticed.AgentId, changed.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Null(changed.Agents.Single(person => person.AgentId == noticed.AgentId).Housing?.Relocation);
        Assert.Null(changed.Agents.Single(person => person.AgentId == noticed.AgentId).Departures);
        Assert.Contains(changed.ExportState().Events, item => item.Kind == "relocation_cancelled" &&
            item.Detail == $"{noticed.AgentId}|{Household}|care");
    }

    [Fact]
    public async Task DisplacedAdultsKeepPropertyAndCollectionRightsWithoutAnotherAllowanceAfterReload()
    {
        var state = Crowded();
        var actor = state.Society.Society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).First();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "a-relocation-food", "food", Household, 6, storageBuildingId: HouseId);
        inventory = InventoryAuthority.AddLot(inventory, "relocation-personal-coat", "padded_coat", actor, 1, storageBuildingId: HouseId);
        inventory = InventoryAuthority.AddLot(inventory, "relocation-borrowed-axe", "wooden_axe", Household, 1);
        inventory = InventoryAuthority.Relocate(inventory, "relocation-borrow", "relocation-borrowed-axe", Household, 1, actor);
        var choices = new Choices();
        using var world = Restore(WithInventory(state, inventory), choices);
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        Assert.Equal(actor, noticed.AgentId);
        var deadline = noticed.Housing!.Relocation!.DeadlineTick;
        await AdvanceTo(world, deadline - 1);
        var beforePosition = world.Agents.Single(person => person.AgentId == actor).Position;
        var town = world.Towns.Single().ResidentIds.ToArray();
        await AdvanceTo(world, deadline);
        Assert.Equal(beforePosition, world.Agents.Single(person => person.AgentId == actor).Position);
        var departure = Assert.Single(world.Agents.Single(person => person.AgentId == actor).Departures!);
        Assert.Equal(2, departure.AllowancePortions);
        Assert.Equal(4, world.Society.Inventory.GetLot("a-relocation-food").Quantity);
        Assert.Equal(town, world.Towns.Single().ResidentIds);
        Assert.Equal(Household, world.WorldSimulation.Buildings.Single(building => building.InstanceId == HouseId).HouseholdId);
        Assert.Equal(actor, world.Society.Inventory.GetLot("relocation-personal-coat").OwnerId);
        Assert.Equal(HouseId, world.Society.Inventory.GetLot("relocation-personal-coat").StorageBuildingId);
        Assert.Equal(Household, world.Society.Inventory.GetLot("relocation-borrowed-axe").OwnerId);
        Assert.Equal(actor, world.Society.Inventory.GetLot("relocation-borrowed-axe").CarrierId);
        Assert.All(departure.AllowanceLotIds, id =>
        {
            var lot = world.Society.Inventory.GetLot(id);
            Assert.Equal(actor, lot.OwnerId);
            Assert.Equal(HouseId, lot.StorageBuildingId);
            Assert.False(PersonalEquipmentRules.IsCarried(lot, actor));
        });
        choices.Wanted[actor] = "household_collect:relocation-personal-coat";
        // Changing the stub's choice does not invalidate a cached idle decision.
        // Request a fresh decision after reload without changing custody or rights.
        var collecting = world.ExportState();
        collecting = collecting with
        {
            Agents = collecting.Agents.Select(person => person.AgentId == actor
                ? person with { LastDecisionContext = null } : person).ToArray(),
        };
        using var replay = Restore(collecting, choices);
        Assert.False(replay.DisplaceAdult(actor));
        for (var tick = 0; tick < 120 && !PersonalEquipmentRules.IsCarried(replay.Society.Inventory.GetLot("relocation-personal-coat"), actor); tick++)
            await replay.AdvanceOneTickAsync();
        Assert.True(PersonalEquipmentRules.IsCarried(replay.Society.Inventory.GetLot("relocation-personal-coat"), actor));
        var replayedDeparture = Assert.Single(replay.Agents.Single(person => person.AgentId == actor).Departures!);
        Assert.Equal(departure.Tick, replayedDeparture.Tick);
        Assert.Equal(departure.AllowancePortions, replayedDeparture.AllowancePortions);
        Assert.Equal(departure.AllowanceLotIds, replayedDeparture.AllowanceLotIds);
        Assert.Equal(4, replay.Society.Inventory.GetLot("a-relocation-food").Quantity);
        Assert.Single(replay.ExportState().Events, item => item.Kind == "household_left" &&
            item.Detail.StartsWith(actor + "|", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(12, false)]
    [InlineData(24, true)]
    public async Task OnlyCompletedExpansionAddsPlacesOrCancelsTheOriginalDeadline(int day, bool completesInTime)
    {
        var state = Crowded(day: day);
        var worker = state.Society.Society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).Last();
        using var world = Restore(WithExpandableHouse(state, worker));
        var expansion = world.StartBuildingExpansion(worker, HouseId);
        Assert.True(expansion.Applied, expansion.Failure);
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        var notice = noticed.Housing!.Relocation!;
        var job = Assert.Single(world.WorldSimulation.BuildingExpansions!);
        var view = new OwnerWorldObservationStore(world).GetSnapshot().PlacedBuildings.Single(building => building.InstanceId == HouseId);
        Assert.Equal(3, view.ResidentLimit);
        Assert.True(view.IsOvercrowded);
        using var replay = Restore(world.ExportState());
        await AdvanceTo(replay, Math.Min(job.CompletionTick, notice.DeadlineTick) - 1);
        Assert.Equal(notice, Assert.Single(Noticed(replay)).Housing!.Relocation);
        Assert.Null(replay.WorldSimulation.Buildings.Single(building => building.InstanceId == HouseId).Footprint);
        await AdvanceTo(replay, Math.Max(job.CompletionTick, notice.DeadlineTick) + 1);
        Assert.Equal(WorldProductionJobState.Completed, Assert.Single(replay.WorldSimulation.BuildingExpansions!).State);
        Assert.Empty(Noticed(replay));
        var resident = replay.Society.GetAgent(noticed.AgentId);
        Assert.Equal(completesInTime ? Household : null, resident.HouseholdId);
        Assert.Equal(completesInTime ? 4 : 3, replay.Society.GetHousehold(Household).MemberIds.Count);
        Assert.Equal(completesInTime ? 0 : 1, replay.Agents.Sum(person => person.Departures?.Count ?? 0));
        var expanded = new OwnerWorldObservationStore(replay).GetSnapshot().PlacedBuildings.Single(building => building.InstanceId == HouseId);
        Assert.Equal(6, expanded.ResidentLimit);
        Assert.False(expanded.IsOvercrowded);
        if (!completesInTime)
            Assert.Equal(notice.DeadlineTick, Assert.Single(replay.Agents.Single(person => person.AgentId == noticed.AgentId).Departures!).Tick);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllFamilyOrProtectedCareGroupsRemainOvercrowdedWithoutForcedDepartures(bool mixedCareGroups)
    {
        var state = Crowded(residents: 5);
        var ids = state.Society.Society.Agents.Select(person => person.Id).ToArray();
        if (mixedCareGroups)
        {
            state = WithDependent(WithDependent(WithDependent(state, ids[0], ids[1]), ids[0], ids[2]), ids[3], ids[4]);
        }
        else
        {
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person =>
                            person with { DomesticFamilyUnitId = "relocation-one-family" }).ToArray(),
                    },
                },
            };
        }
        using var world = Restore(state);
        await AdvanceTo(world, Day + 2);
        Assert.Empty(Noticed(world));
        Assert.Equal(5, world.Society.GetHousehold(Household).MemberIds.Count);
        Assert.All(world.Agents, person => Assert.Null(person.Departures));
        Assert.All(world.Agents.Where(person => world.Society.GetAgent(person.AgentId).AgeBand is
            SocietyAgeBand.Adult or SocietyAgeBand.Elder), person => Assert.Equal(HousingBlockers.Overcrowded, person.Housing?.Blocker));
        using var replay = Restore(world.ExportState());
        await AdvanceTo(replay, Day * 2 + 2);
        Assert.Empty(Noticed(replay));
        Assert.Equal(5, replay.Society.GetHousehold(Household).MemberIds.Count);
    }

    [Fact]
    public async Task VolunteeringReplacesTheSelectedAdultWithoutExtendingTheOriginalDeadline()
    {
        var choices = new Choices();
        using var world = Restore(Crowded(), choices);
        await AdvanceTo(world, 1);
        var original = Assert.Single(Noticed(world));
        var deadline = original.Housing!.Relocation!.DeadlineTick;
        await AdvanceTo(world, 4);
        var volunteer = world.Society.GetHousehold(Household).MemberIds.First(id => id != original.AgentId);
        choices.Wanted[volunteer] = "household_volunteer";
        var state = world.ExportState();
        // Request a fresh decision in this saved fixture instead of waiting for the idle polling interval.
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == volunteer
                ? person with { LastDecisionContext = null } : person).ToArray(),
        };
        using var choosing = Restore(state, choices);
        await AdvanceTo(choosing, 5);
        var replacement = Assert.Single(Noticed(choosing));
        Assert.Equal(volunteer, replacement.AgentId);
        Assert.Equal(HouseRelocationRules.Volunteer, replacement.Housing!.Relocation!.Reason);
        Assert.Equal(deadline, replacement.Housing.Relocation.DeadlineTick);
        using var replay = Restore(choosing.ExportState(), choices);
        await AdvanceTo(replay, deadline);
        Assert.Null(replay.Society.GetAgent(volunteer).HouseholdId);
        Assert.Equal(Household, replay.Society.GetAgent(original.AgentId).HouseholdId);
        Assert.Single(replay.Agents.SelectMany(person => person.Departures ?? []));
    }

    [Fact]
    public async Task SoleGuardianWhoseDependentLivesElsewhereNeverReceivesAForcedNotice()
    {
        var (state, child) = WithDestinationResident();
        var caregiver = state.Society.Society.Agents.Where(person => person.HouseholdId == Household)
            .Select(person => person.Id).Order(StringComparer.Ordinal).First();
        state = WithDependent(state, caregiver, child);
        using var world = Restore(state);
        Assert.Single(SocietyRules.MovingCareGroup(world.Society, caregiver));
        await AdvanceTo(world, 1);
        Assert.NotEmpty(Noticed(world));
        Assert.DoesNotContain(Noticed(world), person => person.AgentId == caregiver);
        Assert.False(world.DisplaceAdult(caregiver));
        await AdvanceTo(world, Day + 2);
        Assert.Equal(Household, world.Society.GetAgent(caregiver).HouseholdId);
        Assert.Equal("household:camp-beta", world.Society.GetAgent(child).HouseholdId);
        Assert.Equal(caregiver, world.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Null(world.Agents.Single(person => person.AgentId == caregiver).Departures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnApprovedHousingRequestCannotActOnANoticeInvalidatedByCareOrFamily(bool newCare)
    {
        var (state, destinationResident) = WithDestinationResident();
        using var world = Restore(state);
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        var actor = noticed.AgentId;
        state = world.ExportState();
        var otherResidents = state.Society.Society.Agents.Where(person => person.HouseholdId == Household && person.Id != actor)
            .Select(person => person.Id).ToArray();
        if (newCare)
            state = WithDependent(state, actor, otherResidents[0]);
        else
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person =>
                            person.Id == actor || otherResidents.Take(2).Contains(person.Id, StringComparer.Ordinal)
                                ? person with { DomesticFamilyUnitId = "relocation-new-majority" } : person).ToArray(),
                    },
                },
            };
        state = WithApprovedRequest(state, actor, destinationResident);
        using var changed = Restore(state);
        await AdvanceTo(changed, 2);
        Assert.Equal(Household, changed.Society.GetAgent(actor).HouseholdId);
        Assert.Null(changed.Agents.Single(person => person.AgentId == actor).Departures);
        Assert.Null(changed.Agents.Single(person => person.AgentId == actor).Housing?.Request);
        Assert.DoesNotContain(changed.ExportState().Events, item => item.Kind == "household_joined" &&
            item.Detail == $"{actor}:household:camp-beta");
        if (newCare)
        {
            Assert.Equal(Household, changed.Society.GetAgent(otherResidents[0]).HouseholdId);
            Assert.Equal(actor, changed.Society.GetAgent(otherResidents[0]).PrimaryCaregiverId);
        }
    }

    [Fact]
    public async Task AnApprovedSuitableHomeReceivesTheNoticedAdultThroughOneOrdinaryDeparture()
    {
        var (state, destinationResident) = WithDestinationResident();
        using var world = Restore(state);
        await AdvanceTo(world, 1);
        var noticed = Assert.Single(Noticed(world));
        var deadline = noticed.Housing!.Relocation!.DeadlineTick;
        var position = noticed.Position;
        using var accepted = Restore(WithApprovedRequest(world.ExportState(), noticed.AgentId, destinationResident));
        await AdvanceTo(accepted, 2);
        Assert.Equal("household:camp-beta", accepted.Society.GetAgent(noticed.AgentId).HouseholdId);
        var person = accepted.Agents.Single(person => person.AgentId == noticed.AgentId);
        Assert.Equal(position, person.Position);
        var departure = Assert.Single(person.Departures!);
        Assert.Equal("displaced", departure.Cause);
        Assert.Equal(2, departure.Tick);
        Assert.Null(person.Housing?.Request);
        Assert.Null(person.Housing?.Relocation);
        Assert.Equal(3, accepted.Society.GetHousehold(Household).MemberIds.Count);
        using var replay = Restore(accepted.ExportState());
        await AdvanceTo(replay, deadline + 1);
        Assert.Single(replay.Agents.Single(item => item.AgentId == noticed.AgentId).Departures!);
        Assert.Equal("household:camp-beta", replay.Society.GetAgent(noticed.AgentId).HouseholdId);
    }

    [Fact]
    public async Task ExpansionCompletionInvalidatesAnApprovedRequestBeforeItCanMoveTheNoticedAdult()
    {
        var (state, destinationResident) = WithDestinationResident(day: 24);
        var worker = state.Society.Society.Agents.Where(person => person.HouseholdId == Household)
            .Select(person => person.Id).Order(StringComparer.Ordinal).Last();
        using var world = Restore(WithExpandableHouse(state, worker));
        var started = world.StartBuildingExpansion(worker, HouseId);
        Assert.True(started.Applied, started.Failure);
        await AdvanceTo(world, 19);
        var noticed = Assert.Single(Noticed(world));
        Assert.Equal(20, Assert.Single(world.WorldSimulation.BuildingExpansions!).CompletionTick);
        using var finishing = Restore(WithApprovedRequest(world.ExportState(), noticed.AgentId, destinationResident));
        await AdvanceTo(finishing, 20);
        Assert.Equal(WorldProductionJobState.Completed, Assert.Single(finishing.WorldSimulation.BuildingExpansions!).State);
        Assert.Equal(Household, finishing.Society.GetAgent(noticed.AgentId).HouseholdId);
        Assert.Null(finishing.Agents.Single(person => person.AgentId == noticed.AgentId).Departures);
        Assert.Null(finishing.Agents.Single(person => person.AgentId == noticed.AgentId).Housing?.Request);
        Assert.Empty(Noticed(finishing));
    }

    [Fact]
    public async Task ModelHousingNoteExplainsThePendingAdmissionWithinTheBoundedContext()
    {
        var (state, destinationResident) = WithDestinationResident(day: 24);
        var worker = state.Society.Society.Agents.Where(person => person.HouseholdId == Household)
            .Select(person => person.Id).Order(StringComparer.Ordinal).Last();
        using var world = Restore(WithExpandableHouse(state, worker));
        var started = world.StartBuildingExpansion(worker, HouseId);
        Assert.True(started.Applied, started.Failure);
        await AdvanceTo(world, 1);
        var actor = Assert.Single(Noticed(world)).AgentId;
        state = WithApprovedRequest(world.ExportState(), actor, destinationResident);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                LastDecisionContext = null,
                Housing = person.Housing! with { Request = person.Housing.Request! with { Approvals = [] } },
            } : person).ToArray(),
        };
        var choices = new Choices();
        using var pending = Restore(state, choices);
        await AdvanceTo(pending, 2);
        Assert.True(choices.HousingNotes.TryGetValue(actor, out var note));
        Assert.InRange(note.Length, 1, 256);
        Assert.Contains("4/3", note, StringComparison.Ordinal);
        Assert.Contains("Waiting for a household's admission answers.", note, StringComparison.Ordinal);
        Assert.Contains("Expansion", note, StringComparison.Ordinal);
        Assert.DoesNotContain("Ask a household", note, StringComparison.Ordinal);
        Assert.NotNull(pending.Agents.Single(person => person.AgentId == actor).Housing?.Request);
    }

    private static (WorldRuntimeState State, string DestinationResident) WithDestinationResident(int day = Day)
    {
        var state = Crowded(residents: 5, day: day);
        var society = state.Society.Society;
        var destinationResident = society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).Last();
        society = SocietyRules.LeaveHousehold(society, destinationResident).Checkpoint;
        society = SocietyRules.JoinHouseholdCareGroup(society, destinationResident, "household:camp-beta").Checkpoint;
        return (state with { Society = state.Society with { Society = society } }, destinationResident);
    }

    private static WorldRuntimeState WithApprovedRequest(WorldRuntimeState state, string actor, string destinationResident) =>
        state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Housing = person.Housing! with
                {
                    Request = new("household:camp-beta", state.Society.Society.WorldTick,
                        state.Society.Society.WorldTick + WorldRuntime.HousingRequestTicks,
                        [destinationResident], [destinationResident], []),
                },
            } : person).ToArray(),
        };

    [Fact]
    public async Task AFamilyHouseIsToldToSplitOnlyWhenItCannotExpandEvenWithLandPermission()
    {
        // One family of five in a four-place House: nobody can be required to leave.
        var crowded = WithFamilies(Crowded(residents: 5), _ => "relocation-family:shared");
        var worker = crowded.Society.Society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).Last();
        var permitted = WithExpandableHouse(crowded, worker);
        var house = permitted.WorldSimulation!.Buildings.Single(building => building.InstanceId == HouseId);
        // The same site with Town title only: the household has not asked the Council for the extra land yet.
        var unpermitted = permitted with
        {
            HouseholdLandUseRights = permitted.HouseholdLandUseRights!
                .Where(right => right.GrantSource != "expansion_test_fixture").ToArray(),
        };
        // Another household holds every tile the House could grow onto.
        GridPoint[] around =
        [
            new(house.Position.X, house.Position.Y - 1), new(house.Position.X + 1, house.Position.Y),
            new(house.Position.X, house.Position.Y + 1), new(house.Position.X - 1, house.Position.Y),
        ];
        var enclosed = unpermitted with
        {
            HouseholdLandUseRights = unpermitted.HouseholdLandUseRights!.Concat(around.Select((tile, index) =>
                    new HouseholdLandUseRight("use:relocation-neighbour:" + index, house.TownId!, "household:camp-beta",
                        [tile], unpermitted.Society.Society.WorldTick, "test_grant")))
                .OrderBy(right => right.Id, StringComparer.Ordinal).ToArray(),
        };

        // A neighbour holds only the first tile the House would try; the other sides are free.
        var oneSideTaken = unpermitted with
        {
            HouseholdLandUseRights = enclosed.HouseholdLandUseRights!
                .Where(right => right.GrantSource != "test_grant" || right.Tiles.Contains(around[0])).ToArray(),
        };

        foreach (var (state, next, maySplit) in new[]
                 {
                     (permitted, "Next: expand the House.", false),
                     (unpermitted, "Next: get the Council's land permission and expand the House.", false),
                     (oneSideTaken, "Next: get the Council's land permission and expand the House.", false),
                     (enclosed, "Next: an adult may start a separate household with their dependents and build a House.", true),
                 })
        {
            var choices = new Choices();
            using var world = Restore(state, choices);
            await AdvanceTo(world, 2);
            Assert.Empty(Noticed(world));
            var member = world.Society.GetHousehold(Household).MemberIds.First(id => choices.HousingNotes.ContainsKey(id));
            Assert.EndsWith("Nobody can be required to leave. " + next, choices.HousingNotes[member], StringComparison.Ordinal);
            Assert.Equal(maySplit, choices.Offered[member].Contains("household_found", StringComparer.Ordinal));
            if (state != oneSideTaken) continue;
            // The land request offered must be one the Council can grant, not the neighbour's tile.
            var request = Assert.Single(choices.Offered.Values.SelectMany(ids => ids).Distinct(StringComparer.Ordinal),
                id => id.Contains("|request_expansion_land|", StringComparison.Ordinal));
            Assert.DoesNotContain($"({around[0].X}, {around[0].Y})", request, StringComparison.Ordinal);
        }

        // A House that is already being expanded is not told to split either.
        var building = new Choices();
        using var expanding = Restore(permitted, building);
        var started = expanding.StartBuildingExpansion(worker, HouseId);
        Assert.True(started.Applied, started.Failure);
        await AdvanceTo(expanding, 2);
        var resident = expanding.Society.GetHousehold(Household).MemberIds
            .First(id => id != worker && building.HousingNotes.ContainsKey(id));
        Assert.Contains("Next: finish the expansion.", building.HousingNotes[resident], StringComparison.Ordinal);
        Assert.DoesNotContain("household_found", building.Offered[resident]);
    }

    [Fact]
    public async Task BuiltInRulesKeepANoticedAdultHomeUntilTheDeadline()
    {
        // Nobody can be asked for a place, so only the deadline moves the adult out:
        // built-in rules must not found a household with no House as soon as notice arrives.
        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(Crowded(day: 120))));
        await AdvanceTo(world, 1);
        var actor = Assert.Single(Noticed(world)).AgentId;
        var notice = world.Agents.Single(person => person.AgentId == actor).Housing!.Relocation!;
        await AdvanceTo(world, notice.DeadlineTick - 1);
        Assert.Equal(Household, world.Society.GetAgent(actor).HouseholdId);
        Assert.Equal(notice, Assert.Single(Noticed(world)).Housing!.Relocation);
        await AdvanceTo(world, notice.DeadlineTick);
        Assert.NotEqual(Household, world.Society.GetAgent(actor).HouseholdId);
        var departure = Assert.Single(world.Agents.Single(person => person.AgentId == actor).Departures!);
        Assert.Equal("displaced", departure.Cause);
        Assert.Equal(notice.DeadlineTick, departure.Tick);
    }

    [Fact]
    public async Task DueNoticesLeaveInThePlannedOrderSoNobodyWithoutNoticeIsMovedOut()
    {
        // Run a quiet all-family House first, so arrival times can lie in the past.
        using var quiet = Restore(WithFamilies(Crowded(residents: 8), _ => "relocation-family:shared"));
        await AdvanceTo(quiet, 6);
        Assert.Empty(Noticed(quiet));
        var state = quiet.ExportState();
        var ids = state.Society.Society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        // Four unrelated adults and a family of four, with no majority. A family member arrived
        // last, then the unrelated adults in ID order; the fourth unrelated adult arrived before
        // them and is not needed to make the House fit.
        var family = ids.Skip(4).ToHashSet(StringComparer.Ordinal);
        state = WithFamilies(state, id => family.Contains(id) ? "relocation-family:shared" : "relocation-single:" + id);
        var arrivals = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [ids[7]] = 5,
            [ids[0]] = 4,
            [ids[1]] = 3,
            [ids[2]] = 2,
            [ids[3]] = 1,
        };
        var edges = state.Society.Society.Relationships.ToList();
        foreach (var id in ids)
        {
            var tick = arrivals.GetValueOrDefault(id, 0);
            var memberships = edges.Where(edge => edge.Type == SocietyRelationshipType.HouseholdMembership &&
                edge.TargetId == id && edge.HouseholdId == Household &&
                edge.State == SocietyRelationshipState.Accepted).ToArray();
            foreach (var edge in memberships)
                edges[edges.IndexOf(edge)] = edge with { ProposedTick = tick, EffectiveTick = tick };
            if (memberships.Length == 0 && tick > 0)
                edges.Add(new SocietyRelationship("relocation-member:" + id, 1, SocietyRelationshipType.HouseholdMembership,
                    id, id, SocietyRelationshipState.Accepted, SocietyConsentState.Accepted, tick, tick, "public",
                    Household, [id]));
        }
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Relationships = edges.OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(),
                },
            },
        };

        using var world = Restore(state);
        await AdvanceTo(world, 7);
        var noticed = Noticed(world);
        Assert.Equal([ids[0], ids[1], ids[2], ids[7]], noticed.Select(person => person.AgentId));
        var deadline = Assert.Single(noticed.Select(person => person.Housing!.Relocation!.DeadlineTick).Distinct());
        await AdvanceTo(world, deadline);

        // Exactly the four adults who had notice left. Leaving in ID order instead would give the
        // family the majority after the first exit and move out the adult who never had notice.
        Assert.Equal([ids[3], ids[4], ids[5], ids[6]],
            world.Society.GetHousehold(Household).MemberIds.Order(StringComparer.Ordinal));
        Assert.Empty(Noticed(world));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "relocation_notice" &&
            item.Detail.StartsWith(ids[3] + "|", StringComparison.Ordinal));
    }

    private static WorldRuntimeState WithFamilies(WorldRuntimeState state, Func<string, string> family) =>
        state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => person with
                    {
                        DomesticFamilyUnitId = family(person.Id),
                    }).ToArray(),
                },
            },
        };

    [Theory]
    [InlineData("deadline")]
    [InlineData("duration")]
    [InlineData("future_notice")]
    [InlineData("household")]
    [InlineData("reason")]
    public async Task InvalidSavedNoticesAreRejected(string corruption)
    {
        using var world = Restore(Crowded());
        await AdvanceTo(world, 1);
        var actor = Assert.Single(Noticed(world));
        var notice = actor.Housing!.Relocation!;
        var invalid = corruption switch
        {
            "deadline" => notice with { DeadlineTick = notice.NoticeTick },
            "duration" => notice with { DeadlineTick = notice.DeadlineTick + 1 },
            "future_notice" => notice with { NoticeTick = world.WorldTick + 1, DeadlineTick = world.WorldTick + 1 + Day },
            "household" => notice with { HouseholdId = "household:camp-beta" },
            _ => notice with { Reason = "not-a-relocation-reason" },
        };
        var state = world.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor.AgentId
                ? person with { Housing = person.Housing! with { Relocation = invalid } } : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state));
    }
}
