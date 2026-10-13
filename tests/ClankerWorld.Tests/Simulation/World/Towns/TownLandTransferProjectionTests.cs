using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.Content;
using ClankerWorld.GodotClient.UI;
using GodotSnapshot = ClankerWorld.GodotClient.Protocol.OwnerWorldSnapshot;

namespace ClankerWorld.Tests;

public sealed class TownLandTransferProjectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(null, null)]
    [InlineData("tree_seed", "tree seeds")]
    [InlineData("medicinal_herbs", "medicinal herbs")]
    [InlineData("gold_ore", "gold ore")]
    [InlineData("named-content", "Moon pearls")]
    public void ExactPublishedTransferTermsReachClientWithCurrentAdultsAndNoConsentUntilPersonalAcceptance(string? kind, string? name)
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = generated.ExportState();
        if (kind == "named-content")
        {
            var digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var version = ContentVersion.Parse("1.0.0");
            var definition = new ContentDefinition("item", "moon-pearl", version, name!, digest);
            var registry = ContentPackageRegistry.Restore(initial.Content);
            registry.Propose(new("named-sale-goods", version, digest, [], [definition], []));
            initial = initial with { Content = registry.ExportState() };
            kind = definition.CanonicalId(digest);
        }
        var town = Assert.Single(initial.Towns!);
        // A bounded term exercises the full public permission contract; generated private property stays intact.
        var permission = initial.HouseholdLandUseRights![0] with { AgreedEndTick = 72 };
        var rights = initial.HouseholdLandUseRights!.Select(right => right.Id == permission.Id ? permission : right).ToArray();
        var people = initial.Society.Society.Agents;
        var adults = people.Where(person => person.Status == SocietyAgentStatus.Active &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            .ToDictionary(person => person.Id, person => person.HouseholdId, StringComparer.Ordinal);
        var households = initial.Society.Society.Households;
        var target = households.First(household => household.Id != permission.HouseholdId && adults.ContainsValue(household.Id));
        var parties = TownLandTransferRules.PartiesFor(rights, permission.Tiles, target.Id, adults);
        var filer = parties.Single(party => party.Kind == "source").AdultIds[0];
        var council = town.Council!;
        var noticeId = "notice:" + (council.Notices.Count + 1);
        var ledger = TownLandTransferRules.Propose(town.LandRecords, initial.Map, town.Id, filer, permission.Tiles,
            target.Id, rights, parties, [], 0, noticeId,
            price: kind is null ? null : new(permission.HouseholdId, kind, 2));
        var transfer = Assert.Single(ledger.Transfers);
        council = TownCouncilRules.PostNotice(council, "land_transfer", TownLandTransferRules.TermsToken(transfer),
            "The affected households may separately accept this existing permission transfer.", 0);
        council = TownCouncilRules.LearnNotice(council, filer, noticeId, 0);
        var publishedState = initial with
        {
            HouseholdLandUseRights = rights,
            Towns = [town with { Council = council, LandRecords = ledger }],
        };
        using var published = WorldRuntime.Restore(publishedState, _ => new ActionCoverageRecorder(chooseIdle: true));
        var before = WorldRuntimeCodec.Encode(published.ExportState());
        var snapshot = new OwnerWorldObservationStore(published).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        var clientTown = Assert.Single(client.Towns);
        var received = Assert.Single(clientTown.LandTransfers);
        if (kind is not null)
        {
            Assert.Equal((kind, 2), (received.Price!.ItemKind, received.Price.Quantity));
            Assert.Equal(name, received.Price.ItemName);
            Assert.Contains(LandTransferText.Details(received, tick => "time " + tick),
                line => line.StartsWith("Goods price: 2 " + name + " to ", StringComparison.Ordinal));
            Assert.Equal((kind, 2), (transfer.Price!.ItemKind, transfer.Price.Quantity));
        }
        else Assert.Null(received.Price);
        Assert.Equal(1, clientTown.LandTransferCount);
        Assert.Equal((transfer.Id, "pending", 0L, noticeId), (received.Id, received.Status, received.ProposedTick, received.NoticeId));
        Assert.Equal(people.Single(person => person.Id == filer).Name, received.FilerName);
        Assert.Equal((target.Id, target.Name), (received.TargetHouseholdId, received.TargetHouseholdName));
        Assert.Equal(permission.Tiles.Select(tile => (tile.X, tile.Y)), received.Tiles.Select(tile => (tile.X, tile.Y)));
        var version = Assert.Single(received.RightVersions);
        Assert.Equal((permission.Id, TownLandHearingRules.Version(permission)), (version.Id, version.Version));
        Assert.Equal((permission.TownId, permission.HouseholdId, permission.GrantSource, permission.GrantedTick, permission.AgreedEndTick),
            (version.Right.TownId, version.Right.HouseholdId, version.Right.GrantSource, version.Right.GrantedTick, version.Right.AgreedEndTick));
        Assert.Equal(permission.Tiles.Select(tile => (tile.X, tile.Y)), version.Right.Tiles.Select(tile => (tile.X, tile.Y)));
        foreach (var party in received.Parties)
        {
            var required = people.Where(person => adults.ContainsKey(person.Id) && person.HouseholdId == party.HouseholdId)
                .OrderBy(person => person.Id, StringComparer.Ordinal).ToArray();
            Assert.Equal("current", party.RosterKind);
            Assert.Equal(households.Single(household => household.Id == party.HouseholdId).Name, party.HouseholdName);
            Assert.Equal(required.Select(person => person.Id), party.AdultIds);
            Assert.Equal(required.Select(person => person.Name), party.AdultNames);
            Assert.Empty(party.AcceptedAdultIds);
        }
        Assert.Equal(filer, Assert.Single(received.Parties.SelectMany(party => party.NoticeAwareAdultIds)));
        Assert.Empty(received.Responses);
        Assert.Null(received.SettledTick);
        Assert.Null(received.ReceiptAdjustmentId);
        Assert.Equal(before, WorldRuntimeCodec.Encode(published.ExportState()));

        var source = parties.Single(party => party.Kind == "source");
        var answered = TownLandTransferRules.Respond(ledger, transfer.Id, TownLandTransferRules.TermsToken(transfer), filer,
            source.HouseholdId, "accept", council.Knowledge, rights, parties, [], 0);
        using var accepted = WorldRuntime.Restore(publishedState with
        {
            Towns = [town with { Council = council, LandRecords = answered }],
        }, _ => new ActionCoverageRecorder(chooseIdle: true));
        var acceptedSnapshot = new OwnerWorldObservationStore(accepted).GetSnapshot();
        var acceptedClient = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(acceptedSnapshot, JsonOptions), JsonOptions)!;
        var pending = Assert.Single(Assert.Single(acceptedClient.Towns).LandTransfers);
        Assert.Equal("pending", pending.Status);
        Assert.Equal(filer, Assert.Single(pending.Parties.SelectMany(party => party.AcceptedAdultIds)));
        Assert.True(pending.Parties.Sum(party => party.AdultIds.Count) > 1);
        Assert.Equal((filer, people.Single(person => person.Id == filer).Name, "accept", 0L),
            (pending.Responses[0].AgentId, pending.Responses[0].AgentName, pending.Responses[0].Kind, pending.Responses[0].Tick));
        Assert.Equal(JsonSerializer.Serialize(rights), JsonSerializer.Serialize(accepted.ExportState().HouseholdLandUseRights));
        var publicJson = JsonSerializer.Serialize(pending, JsonOptions);
        foreach (var privateField in new[] { "rawReply", "recentPrivateThoughts", "recentMemories", "providerMessages" })
            Assert.DoesNotContain("\"" + privateField + "\"", publicJson, StringComparison.Ordinal);
    }
}
