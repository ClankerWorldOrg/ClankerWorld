using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using GodotSnapshot = ClankerWorld.Contracts.OwnerWorldSnapshot;

namespace ClankerWorld.Tests;

public sealed class TownLandHearingProjectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ActualExpiryReachesOwnerClientJsonWithoutTurningPublicationOrInspectionIntoReceipt()
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = generated.ExportState();
        var permission = initial.HouseholdLandUseRights![0];
        // Start beside a real permission expiry; preserve generated titles, buildings, goods and people.
        using var world = WorldRuntime.Restore(initial with
        {
            HouseholdLandUseRights = initial.HouseholdLandUseRights!.Select(right => right.Id == permission.Id
                ? right with { AgreedEndTick = 1 } : right).ToArray(),
        }, _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var actual = Assert.Single(world.Towns[0].LandRecords.Cases);
        var notice = Assert.Single(world.Towns[0].Council!.Notices,
            item => item.Id == actual.Revisions[0].NoticeId);
        Assert.Equal("land_hearing", notice.Kind);
        var beforeObservation = WorldRuntimeCodec.Encode(world.ExportState());

        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        var town = Assert.Single(snapshot.Towns);
        var clientTown = Assert.Single(client.Towns);
        var projected = Assert.Single(town.LandHearings);
        var received = Assert.Single(clientTown.LandHearings);
        Assert.Equal(1, town.LandHearingCount);
        Assert.Equal(1, clientTown.LandHearingCount);
        Assert.Equal((actual.Id, "expiry", "pending"), (projected.Id, projected.Kind, projected.Status));
        Assert.Equal((actual.Id, "expiry", "pending"), (received.Id, received.Kind, received.Status));
        var plot = permission.Tiles.Select(tile => (tile.X, tile.Y)).ToArray();
        Assert.Equal(plot, projected.Tiles.Select(tile => (tile.X, tile.Y)));
        Assert.Equal(plot, received.Tiles.Select(tile => (tile.X, tile.Y)));
        var window = (Published: 1L, Deadline: 1L + initial.Society.Society.Config.TicksPerWorldDay);
        Assert.Equal(window, (projected.PublishedTick, projected.DeadlineTick));
        Assert.Equal(window, (received.PublishedTick, received.DeadlineTick));
        Assert.Equal(notice.Id, projected.NoticeId);
        Assert.Equal(notice.Id, received.NoticeId);

        var household = initial.Society.Society.Households.Single(item => item.Id == permission.HouseholdId);
        var adults = initial.Society.Society.Agents.Where(person => person.HouseholdId == household.Id &&
            person.Status == SocietyAgentStatus.Active && person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            .OrderBy(person => person.Id, StringComparer.Ordinal).ToArray();
        var party = Assert.Single(projected.Parties);
        var clientParty = Assert.Single(received.Parties);
        Assert.Equal(("household:" + household.Id, "household", household.Name), (party.Id, party.Kind, party.Name));
        Assert.Equal((party.Id, party.Kind, household.Name), (clientParty.Id, clientParty.Kind, clientParty.Name));
        Assert.Equal(adults.Select(person => person.Id), clientParty.AdultIds);
        Assert.Equal(adults.Select(person => person.Name), clientParty.AdultNames);
        Assert.Empty(party.NoticeAwareAdultIds);
        Assert.Empty(clientParty.NoticeAwareAdultIds);
        var current = Assert.Single(received.CurrentParties);
        Assert.Equal(clientParty.AdultIds, current.AdultIds);
        Assert.Equal(clientParty.AdultNames, current.AdultNames);
        Assert.Empty(projected.Reads);
        Assert.Empty(received.Reads);
        Assert.Empty(projected.Evidence);
        Assert.Empty(received.Evidence);
        Assert.Empty(received.Responses);
        Assert.Empty(received.Rulings);
        Assert.Null(received.Judge);
        var publicJson = JsonSerializer.Serialize(projected, JsonOptions);
        foreach (var privateField in new[] { "rawReply", "recentPrivateThoughts", "recentMemories", "providerMessages" })
            Assert.DoesNotContain("\"" + privateField + "\"", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain(world.Towns[0].Council!.Knowledge, receipt => receipt.NoticeId == notice.Id);
        Assert.Equal(beforeObservation, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public async Task ActualHouseholdDepartureChangesCurrentRespondersWithoutRewritingPublishedNoticeOrDeadline()
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = generated.ExportState();
        var permission = initial.HouseholdLandUseRights![0];
        using var world = WorldRuntime.Restore(initial with
        {
            HouseholdLandUseRights = initial.HouseholdLandUseRights!.Select(right => right.Id == permission.Id
                ? right with { AgreedEndTick = 1 } : right).ToArray(),
        }, _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var notice = Assert.Single(Assert.Single(world.Towns[0].LandRecords.Cases).Revisions);
        var originalParty = Assert.Single(notice.Parties);
        var departing = originalParty.AdultIds[0];
        world.Pause();
        Assert.True(world.DisplaceAdult(departing));
        Assert.Null(world.Society.GetAgent(departing).HouseholdId);
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var actual = Assert.Single(world.Towns[0].LandRecords.Cases);
        Assert.Equal(notice, Assert.Single(actual.Revisions));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        var hearing = Assert.Single(Assert.Single(client.Towns).LandHearings);
        Assert.Equal((notice.Number, notice.NoticeId, notice.PublishedTick, notice.DeadlineTick),
            (hearing.Revision, hearing.NoticeId, hearing.PublishedTick, hearing.DeadlineTick));
        var historicParty = Assert.Single(hearing.Parties);
        Assert.Equal(originalParty.AdultIds, historicParty.AdultIds);
        Assert.Contains(departing, historicParty.AdultIds);
        var current = Assert.Single(hearing.CurrentParties);
        Assert.Equal(originalParty.Id, current.Id);
        Assert.Equal(originalParty.AdultIds.Where(id => id != departing), current.AdultIds);
        Assert.DoesNotContain(departing, current.AdultIds);
        Assert.Equal(current.AdultIds.Select(id => world.Society.GetAgent(id).Name), current.AdultNames);
        Assert.Empty(current.NoticeAwareAdultIds);
        Assert.Empty(hearing.Responses);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void DepartedCouncilAuthorRemainsInNoticeHistoryWithoutBeingProjectedAsCurrentTownRepresentative()
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var town = Assert.Single(state.Towns!);
        var permission = state.HouseholdLandUseRights![0];
        var filer = town.Council!.Members[0];
        var day = state.Society.Society.Config.TicksPerWorldDay;
        var request = new TownLandFilingRequest(permission.Tiles, new("confirm"), "Review the Town's recorded use permission");
        var council = TownCouncilRules.SubmitProposal(town.Council!, town.Id, filer, "land_hearing", null,
            "Authorize this exact land hearing", "recorded permission", town.ResidentIds, 0, day, landHearingRequest: request);
        var proposal = Assert.Single(council.Proposals);
        foreach (var voter in proposal.Voters.Take(proposal.RequiredYes))
            council = TownCouncilRules.VoteProposal(council, proposal.Id, voter, true, 0);
        Assert.Equal("passed", Assert.Single(council.Proposals).Status);
        town = town with { Council = council };
        var parties = TownLandCasePartyRules.CurrentParties(town, permission.Tiles, state.HouseholdLandUseRights!, [],
            state.Society.Society.Agents, 0, townRepresentative: filer);
        var noticeId = "notice:" + (council.Notices.Count + 1);
        var ledger = TownLandHearingRules.File(town.LandRecords, town.Id,
            new(filer, "town", request.Statement, request.RequestedOutcome, 0, proposal.Id), permission.Tiles,
            state.HouseholdLandUseRights!, parties, 0, day, noticeId);
        var actual = Assert.Single(ledger.Cases);
        council = TownCouncilRules.PostNotice(council, "land_hearing", TownLandHearingRules.RevisionToken(actual),
            "Affected adults may respond to this formal land hearing.", 0);
        var remaining = town.ResidentIds.Where(id => id != filer).ToArray();
        // Capture the lawful all-adult Council after the author loses Town standing; keep the published case untouched.
        council = TownCouncilRules.Advance(council, town.Id, state.WorldSeed, remaining, 0, day);
        town = town with { Council = council, LandRecords = ledger, ResidentIds = remaining };
        using var world = WorldRuntime.Restore(state with { Towns = [town] }, _ => new ActionCoverageRecorder(chooseIdle: true));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        var hearing = Assert.Single(Assert.Single(client.Towns).LandHearings);
        Assert.Equal((actual.Revisions[0].Number, noticeId, 0L, (long)day),
            (hearing.Revision, hearing.NoticeId, hearing.PublishedTick, hearing.DeadlineTick));
        var published = Assert.Single(hearing.Parties, party => party.Kind == "town");
        Assert.Equal(filer, published.RepresentativeId);
        Assert.Equal(state.Society.Society.GetAgent(filer).Name, published.RepresentativeName);
        var current = Assert.Single(hearing.CurrentParties, party => party.Kind == "town");
        Assert.Null(current.RepresentativeId);
        Assert.Null(current.RepresentativeName);
        Assert.Empty(current.NoticeAwareAdultIds);
        Assert.Empty(hearing.Responses);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
    }
}
