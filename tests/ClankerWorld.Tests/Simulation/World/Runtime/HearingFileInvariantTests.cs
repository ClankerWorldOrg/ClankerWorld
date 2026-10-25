using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HearingFileInvariantTests
{
    private static TownRuntimeState Town() => new("town", "Town", "founded", 0, [], [], [new(0, 0)]);

    private static TownLandRecords LandCase() => TownLandHearingRules.File(TownLandRecords.Create(), "town",
        new("resident", "dispute", "Please review this plot.", new("confirm"), 1), [new(1, 0)], [],
        [new("household", "household", "household", "town", ["resident"])], 1, 10, "notice");

    [Theory]
    [InlineData("sequence")]
    [InlineData("revisions")]
    [InlineData("party")]
    [InlineData("read")]
    [InlineData("consent")]
    public void MalformedLandCaseFailsBeforePublication(string component)
    {
        var state = LandCase();
        var item = state.Cases[0];
        state = component switch
        {
            "sequence" => state with { Sequence = -1 },
            "revisions" => state with { Cases = [item with { Revisions = [item.Revisions[0] with { Number = 2 }] }] },
            "party" => state with { Cases = [item with { Revisions = [item.Revisions[0] with { Parties = [item.Revisions[0].Parties[0] with { Kind = "unsupported" }] }] }] },
            "read" => state with { Cases = [item with { Reads = [new(1, "resident", 2, ["unsubmitted-evidence"])] }] },
            "consent" => state with { Cases = [item with { JudgeConsents = [new("resident", 0)] }] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        TownPublicationAssertions.Reject(system => system.RestoreTowns([Town() with { LandRecords = state }]));
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("revisions")]
    [InlineData("filing")]
    [InlineData("read")]
    [InlineData("consent")]
    [InlineData("duplicate_revision")]
    public void MalformedCivilCaseFailsBeforePublication(string component)
    {
        var state = TownNonLandRulesTests.Pending();
        var item = state.Cases[0];
        state = component switch
        {
            "sequence" => state with { Sequence = -1 },
            "revisions" => state with { Cases = [item with { Revisions = [item.Revisions[0] with { Number = 2 }] }] },
            "duplicate_revision" => state with
            {
                Cases = [item with
            {
                Revisions = [item.Revisions[0], item.Revisions[0]],
                Responses = [new(1, "subject", "subject", "subject", "answer", "An answer.", 12)],
            }]
            },
            "filing" => state with { Cases = [item with { Filings = [item.Filings[0] with { Kind = "unsupported" }] }] },
            "read" => state with { Cases = [item with { Reads = [new(1, "resident", 20, ["unsubmitted-evidence"], [])] }] },
            "consent" => state with { Cases = [item with { JudgeConsents = [new("resident", 0)] }] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        TownPublicationAssertions.Reject(system => system.RestoreTowns([Town() with { NonLandHearings = state }]));
    }

    [Fact]
    public void ValidNativeFilesCanPublishBeforeTheirExternalNoticeAndPopulationLinks()
    {
        var land = LandCase();
        var civil = TownNonLandRulesTests.Pending();
        var system = new TownSystem(CivicState.Empty);

        system.RestoreTowns([Town() with { LandRecords = land, NonLandHearings = civil }]);

        var town = Assert.Single(system.State.Towns);
        Assert.Same(land, town.LandRecords);
        Assert.Same(civil, town.NonLandHearings);
    }
}
