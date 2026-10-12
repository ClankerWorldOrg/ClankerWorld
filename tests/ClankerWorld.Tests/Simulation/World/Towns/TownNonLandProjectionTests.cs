using System.Text.Json;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class TownNonLandProjectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task NativeReportReachesTownInspectionWithoutTurningPublicationIntoReceiptFindingOrRemedy()
    {
        var state = await NonLandRuntimeFixture.FiledAsync();
        using var world = NonLandRuntimeFixture.Create(state, new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == NonLandRuntimeFixture.Subject
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal)) : null,
        });
        var actual = Assert.Single(world.Towns[0].NonLandHearings.Cases);
        var revision = actual.Revisions[^1];
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = Client(snapshot);
        var town = Assert.Single(client.Towns);
        var hearing = Assert.Single(town.NonLandCases);
        Assert.Equal(1, town.NonLandCaseCount);
        Assert.Equal((actual.Id, actual.Allegation.SubjectId, actual.Allegation.ConductKind, actual.Allegation.ConductTick),
            (hearing.Id, hearing.SubjectId, hearing.ConductKind, hearing.ConductTick));
        Assert.Equal((actual.Allegation.Position.X, actual.Allegation.Position.Y), (hearing.Position.X, hearing.Position.Y));
        Assert.Equal(world.Society.GetAgent(actual.Allegation.SubjectId).Name, hearing.SubjectName);
        Assert.Equal(actual.Allegation.LawVersion, hearing.ApplicableLaw!.Version);
        Assert.Equal("resident_duty", hearing.ApplicableLaw.Scope);
        Assert.Equal((revision.NoticeId, revision.PublishedTick, revision.DeadlineTick),
            (hearing.Revisions[^1].NoticeId, hearing.Revisions[^1].PublishedTick, hearing.Revisions[^1].DeadlineTick));
        var subject = Assert.Single(hearing.Revisions[^1].Parties, party => party.SubjectId == NonLandRuntimeFixture.Subject);
        Assert.False(subject.NoticeAware);
        var current = Assert.Single(hearing.CurrentParties, party => party.SubjectId == NonLandRuntimeFixture.Subject);
        Assert.Equal(subject.RespondingAdultId, current.RespondingAdultId);
        Assert.False(current.NoticeAware);
        Assert.Empty(hearing.Findings);
        Assert.Empty(hearing.Offers);
        Assert.Empty(hearing.Agreements);
        Assert.Null(hearing.Judge);
        Assert.True(town.Government!.NonLandAuthorized);
        Assert.Null(town.Government.NonLandAuthority);
        Assert.Equal(actual.Evidence.Select(evidence => (evidence.Kind, evidence.Acquisition, evidence.SourceRecordId, evidence.SourceVersion)),
            hearing.Evidence.Select(evidence => (evidence.Kind, evidence.Acquisition, evidence.SourceRecordId, evidence.SourceVersion)));
        Assert.Equal(actual.Reads.Select(read => (read.AgentId, read.Revision, read.ReadTick)),
            hearing.Reads.Select(read => (read.AgentId, read.Revision, read.ReadTick)));
        var text = string.Join("\n", NonLandHearingText.Details(hearing, tick => "time " + tick));
        Assert.Contains("Allegation:", text, StringComparison.Ordinal);
        Assert.Contains("no eligible willing adjudicator", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Voluntary agreement", text, StringComparison.Ordinal);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));

        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].Council!.Knowledge.Any(receipt =>
            receipt.AgentId == NonLandRuntimeFixture.Subject && receipt.NoticeId == revision.NoticeId), 10);
        var learned = Assert.Single(Assert.Single(Client(new OwnerWorldObservationStore(world).GetSnapshot()).Towns).NonLandCases);
        Assert.True(Assert.Single(learned.Revisions[^1].Parties, party => party.SubjectId == NonLandRuntimeFixture.Subject).NoticeAware);
        Assert.Empty(learned.Responses);
        Assert.Empty(learned.Findings);
        Assert.Empty(learned.Agreements);
    }

    private static OwnerWorldSnapshot Client(WorldSnapshotView snapshot) =>
        JsonSerializer.Deserialize<OwnerWorldSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
}
