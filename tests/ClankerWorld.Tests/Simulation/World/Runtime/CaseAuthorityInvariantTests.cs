using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CaseAuthorityInvariantTests
{
    private static TownRuntimeState Town() => new("town", "Town", "founded", 0, [], [], [new(0, 0)]);

    private static TownLandRecords LandCase() => TownLandHearingRules.File(TownLandRecords.Create(), "town",
        new("resident", "dispute", "Please review this plot.", new("confirm"), 1), [new(1, 0)], [],
        [new("household", "household", "household", "town", ["resident"])], 1, 10, "notice");

    private static TownLandRecords RuledLandCase()
    {
        var state = LandCase();
        var item = state.Cases[0];
        var judge = new TownLandCaseJudge("judge", "land_mayor", "authority", 2);
        state = TownLandHearingRules.AssignJudge(state, item.Id, judge);
        state = TownLandHearingRules.Inspect(state, item.Id, 1, "judge", 2);
        var map = new WorldMap(2, 2, 0, [new(new(0, 0), TerrainKind.Meadow), new(new(1, 0), TerrainKind.Meadow),
            new(new(0, 1), TerrainKind.Meadow), new(new(1, 1), TerrainKind.Meadow)], [], [], "local-ruling");
        return TownLandHearingRules.Rule(state, map, item.Id, 1, judge, 11, new("confirm"), [], [],
            "The noticed permission is confirmed.", [], item.Revisions[0].Parties, validAuthority: true).State;
    }

    [Theory]
    [InlineData("judge")]
    [InlineData("mandate")]
    [InlineData("stage")]
    [InlineData("ballot")]
    [InlineData("reopening")]
    public void InvalidLandAuthorityDoesNotReplaceThePublishedSnapshot(string component)
    {
        var state = LandCase();
        var item = state.Cases[0];
        var contest = new TownLandCaseJudgeContest("contest", "voting", 1, 1, 1, 11,
            ["resident"], ["judge"], [], [], 0, []);
        item = component switch
        {
            "judge" => item with { Judge = new("judge", "unsupported", "authority", 1) },
            "mandate" => item with { Judge = new("judge", "case_elected", "unrecorded-election", 1) },
            "stage" => item with { Contest = contest with { Stage = "unsupported" } },
            "ballot" => item with { Contest = contest with { Ballots = [new("not-a-voter", "judge")] } },
            "reopening" => item with { ReopenRequests = [new("land-reopen:town:1", "resident", 2, "unsupported", [], "Reasons.")] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => system.RestoreTowns([Town() with { LandRecords = state with { Cases = [item] } }]));

        Assert.Same(before, system.State);
    }

    [Theory]
    [InlineData("judge")]
    [InlineData("mandate")]
    [InlineData("stage")]
    [InlineData("ballot")]
    [InlineData("finding")]
    [InlineData("grounds")]
    [InlineData("reopening")]
    public void InvalidCivilAuthorityDoesNotReplaceThePublishedSnapshot(string component)
    {
        var state = TownNonLandRulesTests.Settled();
        var item = state.Cases[0];
        var contest = new TownCaseJudgeContest("contest", "voting", 1, 10, 10, 20,
            ["subject"], ["judge"], [], [], 0, []);
        item = component switch
        {
            "judge" => item with { Judge = new("judge", "unsupported", "authority", 10) },
            "mandate" => item with { Judge = new("judge", "case_elected", "unrecorded-election", 10) },
            "stage" => item with { Contest = contest with { Stage = "unsupported" } },
            "ballot" => item with { Contest = contest with { Ballots = [new("not-a-voter", "judge")] } },
            "finding" => item with { Findings = [item.Findings[0] with { Consequence = "unsupported" }] },
            "grounds" => item with { Findings = [item.Findings[0] with { EvidenceIds = ["unsubmitted-evidence"] }] },
            "reopening" => item with { ReopenRequests = [new("reopen", "subject", 22, "unsupported", [], "Reasons.")] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => system.RestoreTowns([Town() with { NonLandHearings = state with { Cases = [item] } }]));

        Assert.Same(before, system.State);
    }

    [Theory]
    [InlineData("reasons")]
    [InlineData("evidence")]
    [InlineData("outcome")]
    public void MalformedRulingDoesNotReplaceThePublishedSnapshot(string component)
    {
        var state = RuledLandCase();
        var item = state.Cases[0];
        var ruling = item.Rulings[0];
        ruling = component switch
        {
            "reasons" => ruling with { Reasons = "" },
            "evidence" => ruling with { EvidenceIds = ["unsubmitted-evidence"] },
            "outcome" => ruling with { Outcome = new("unsupported") },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => system.RestoreTowns([Town() with { LandRecords = state with { Cases = [item with { Rulings = [ruling] }] } }]));

        Assert.Same(before, system.State);
    }

    [Fact]
    public void NativeLandRulingPublishesBeforeItsExternalOfficeLinks()
    {
        var state = RuledLandCase();
        var system = new TownSystem(CivicState.Empty);

        system.RestoreTowns([Town() with { LandRecords = state }]);

        Assert.Same(state, Assert.Single(system.State.Towns).LandRecords);
    }

    [Fact]
    public void NativeFindingPublishesBeforeItsExternalPopulationAndOfficeLinks()
    {
        var state = TownNonLandRulesTests.Settled();
        var system = new TownSystem(CivicState.Empty);

        system.RestoreTowns([Town() with { NonLandHearings = state }]);

        Assert.Same(state, Assert.Single(system.State.Towns).NonLandHearings);
    }
}
