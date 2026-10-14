using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;

namespace ClankerWorld.Tests;

public sealed class WorldEventFormatterTransferredTests
{
    private const string Actor = "world:agent:birth:founder:0001:agent:0002:1";
    private const string Recipient = Actor + ":birth:agent:0002:2";

    [Fact]
    public void OrnamentLinesResolveDescendantsBeforeTheGiftLotSuffix()
    {
        var snapshot = Snapshot(Person("founder:0001", "Rowan"), Person(Actor, "Mira"), Person(Recipient, "Orin"));
        Assert.Equal("Mira put on an ornament.", Line("ornament_worn", Actor + ":ornament:smith:output:ornament", snapshot));
        Assert.Equal("Mira took off an ornament.", Line("ornament_removed", Actor + ":ornament:smith:output:ornament", snapshot));
        Assert.Equal("Mira gave an ornament to Orin.", Line("ornament_given", Actor + ":ornament_gift:" + Recipient + ":smith:output:ornament:gift:actual-lot", snapshot));
    }

    [Fact]
    public void ToolRequestsNameThePeopleAndDistinguishMakingFromPurchase()
    {
        var snapshot = Snapshot(Person(Actor, "Mira"), Person(Recipient, "Orin"));
        const string request = "tool-request:river:smith:001";
        Assert.Equal("Mira asked the Blacksmith for a tool.", Line("tool_request_placed", Actor + ":" + request, snapshot));
        Assert.Equal("Orin agreed to make the tool.", Line("tool_request_accepted", Recipient + ":" + request, snapshot));
        Assert.Equal("A requested tool is ready. It's handed over once it's paid for.", Line("tool_request_ready", Recipient + ":" + request, snapshot));
        Assert.Equal("A requested tool was paid for and handed over.", Line("tool_request_completed", Actor + ":" + request, snapshot));
    }

    [Fact]
    public void WrittenWorksNameTheHistoricalAuthorAndActualReader()
    {
        var snapshot = Snapshot(Person(Actor, "Mira", "deceased"), Person(Recipient, "Orin"));
        Assert.Equal("Mira finished writing a book.", Line("agent_knowledge_artifact_created", Actor + "|knowledge-artifact-000002|book|1", snapshot));
        Assert.Equal("Orin learned from a written work.", Line("agent_knowledge_artifact_read", Actor + "|" + Recipient + "|knowledge-artifact-000003|1", snapshot));
    }

    [Theory]
    [InlineData("council")]
    [InlineData("election")]
    [InlineData("runoff")]
    [InlineData("proposal")]
    [InlineData("result")]
    [InlineData("cancelled")]
    public void CivicNoticesNameEachTownAndOnlyTheKnownOutcome(string suffix)
    {
        var snapshot = Snapshot() with { Towns = [Town("town:first", "First Town"), Town("town:second", "Second Town")] };
        foreach (var town in snapshot.Towns)
        {
            var text = Line("town_civic_" + suffix, town.Id + "|subject-id|notice", snapshot);
            Assert.Contains(town.Name, text, StringComparison.Ordinal);
            Assert.DoesNotContain(town.Id, text, StringComparison.Ordinal);
            if (suffix == "council") Assert.DoesNotContain("proposals", text, StringComparison.Ordinal);
            if (suffix == "result") Assert.Contains("Towns page", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("law", "wording and scope")]
    [InlineData("government", "vote or handover")]
    [InlineData("mayor", "result")]
    public void GovernmentNoticesGiveTheCorrectTownsPageGuidance(string suffix, string guidance)
    {
        var snapshot = Snapshot() with { Towns = [Town("town:first", "First Town")] };
        var text = Line("town_civic_" + suffix, "town:first|record-id|notice", snapshot);
        Assert.Contains("First Town", text, StringComparison.Ordinal);
        Assert.Contains("Towns page", text, StringComparison.Ordinal);
        Assert.Contains(guidance, text, StringComparison.Ordinal);
        Assert.DoesNotContain("town:first", text, StringComparison.Ordinal);
        Assert.True(GameUiText.IsPlayerFacingEvent("town_civic_" + suffix));
    }

    [Theory]
    [InlineData("property", "property ruling", "ownership outcome")]
    [InlineData("dispute", "land ruling", "use right change")]
    [InlineData("expiry", "land ruling", "use right change")]
    public void RulingsDescribeTheRecordedCaseKind(string kind, string subject, string result)
    {
        var judge = new OwnerLandHearingJudge(Actor, "Mira", "case", "authority", 10);
        var outcome = new OwnerLandHearingOutcome("reclaim", null, null, null);
        var ruling = new OwnerLandHearingRuling("land-ruling:town:first:2", 1, judge, 25, outcome, [new(2, 2)], [], [], "Recorded reasons.", []);
        var hearing = new OwnerTownLandHearing("land-hearing:town:first:2", kind, "settled", 10, 25,
            1, [new(2, 2)], [], "notice", 10, 24, outcome, [], [], [], [], [ruling], judge, [], null, null, []);
        var snapshot = Snapshot() with { Towns = [Town("town:first", "First Town") with { LandHearings = [hearing] }] };
        var text = Line("town_civic_result", "town:first|" + ruling.Id + "|Recorded outcome.", snapshot);
        Assert.Equal($"First Town posted the result of a {subject}. See the Towns page for its {result} and reasons.", text);
        if (kind == "property") Assert.DoesNotContain("use right change", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("town_civic_nonviolent_hearing")]
    [InlineData("town_civic_law_case")]
    [InlineData("town_civic_remedy")]
    [InlineData("law_case_offer_response")]
    [InlineData("law_case_remedy_effect")]
    public void IndividualHearingNoticesNeverClaimTheWholeRemedyFinished(string kind)
    {
        var snapshot = Snapshot() with { Towns = [Town("town:first", "First Town")] };
        var text = Line(kind, "town:first|record-ui|actor-ui", snapshot);
        Assert.True(GameUiText.IsPlayerFacingEvent(kind));
        Assert.DoesNotContain("record-ui", text, StringComparison.Ordinal);
        Assert.DoesNotContain("actor-ui", text, StringComparison.Ordinal);
        Assert.DoesNotContain("agreement completed", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remedy completed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimalCareNamesTheRecordedAnimal()
    {
        var snapshot = Snapshot() with
        {
            Animals = [new("smoke-horse", "Moss", "horse", "female", 300, "adult", new(0, 0), "household:first", "Mira's household", "cared", null, 0, null, null, null, null, false, [], [])],
        };
        Assert.Equal("Moss got a day's feed and water.", EventTextFixture.Describe(new(1, 25, "animal_cared", "smoke-horse")
        { Payload = new ClankerWorld.Simulation.World.AnimalCaredEventPayload(null, "smoke-horse") }, snapshot));
    }

    private static string Line(string kind, string detail, OwnerWorldSnapshot snapshot) => EventTextFixture.Describe(new(1, 25, kind, detail), snapshot);
    private static OwnerWorldTown Town(string id, string name) => new(id, name, "founded", 0, [], [], []);
    private static OwnerWorldSnapshot Snapshot(params OwnerWorldAgent[] people) => new("event-goldens", 25, "map", [], [], [], null, 1) { Agents = people };
    private static OwnerWorldAgent Person(string id, string name, string lifecycle = "active") => new(id, name, lifecycle, new(0, 0), 8000, [], [], new("idle", null, null, [], ""), new(new(0, 0), [], []), false);
}
