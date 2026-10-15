using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GovernmentInvariantTests
{
    private static TownGovernmentState VotingGovernment()
    {
        var target = TownArrangementRules.Supported.First(item => item != TownArrangementRules.Initial);
        var change = new TownGovernmentChange("town:government:1", TownGovernmentRules.ChangeRequestKey("arrangement", target),
            "arrangement", target, "resident", new string('a', 64), 0, "voting", 0, 120, ["resident"], [new("resident", true)])
        { OpeningVoters = ["resident"] };
        return TownGovernmentState.Create() with { Sequence = 1, Changes = [change] };
    }

    private static TownRuntimeState Town(TownGovernmentState government) =>
        new("town", "Town", "founded", 0, ["resident"], [], [new(0, 0)])
        { Council = TownCouncilState.Create(["resident"]), Government = government };

    [Theory]
    [InlineData("arrangement")]
    [InlineData("sequence")]
    [InlineData("change")]
    [InlineData("contest")]
    [InlineData("office")]
    [InlineData("law")]
    [InlineData("draft")]
    [InlineData("enacted")]
    [InlineData("consent")]
    public void InvalidGovernmentFailsBeforeItsTownPublishes(string component)
    {
        var government = TownGovernmentState.Create();
        var invalid = component switch
        {
            "arrangement" => government with { Arrangement = government.Arrangement with { Ordinary = "unsupported" } },
            "sequence" => government with { Sequence = -1 },
            "change" => VotingGovernment() with { Changes = [VotingGovernment().Changes[0] with { Status = "unsupported" }] },
            "contest" => government with { Sequence = 1, Contest = new("town:mayor:1", "vacancy", "land", null, "unsupported", 0, 0, null, null, [], [], [], [], 0) },
            "office" => government with { Offices = [government.Offices[0] with { HolderId = "resident", TermStartTick = 0 }, government.Offices[1]] },
            "law" => government with { Sequence = 1, Laws = [new("town:law:1", [])] },
            "draft" => government with { LawDrafts = [new("proposal", "adopt", null, null, "Subject", "Rule", "unsupported", [])] },
            "enacted" => government with { LawDrafts = [new("proposal", "adopt", "town:law:1", null, "Subject", "Rule", TownLawRules.Jurisdiction, [], "enacted")] },
            "consent" => government with { Consents = [new("resident", "unsupported", 0)] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => system.RestoreTowns([Town(invalid)]));

        Assert.Same(before, system.State);
    }

    [Fact]
    public void DecidingVoteIsAllowedUntilTheExistingAdvanceCompletesItsTransition()
    {
        var government = VotingGovernment();
        var town = Town(government);
        var system = new TownSystem(CivicState.Empty);

        system.RestoreTowns([town]);
        Assert.Same(government, Assert.Single(system.State.Towns).Government);
        Assert.Throws<InvalidDataException>(() => TownGovernmentStateValidation.ValidateLocal("town", government, allowIntermediateVotes: false));

        var result = TownGovernmentRules.Advance(town.Council!, government, "town", "Town", "government-local", ["resident"], 1, 120);
        TownGovernmentStateValidation.ValidateLocal("town", result.Government, allowIntermediateVotes: false);
        TownSystem.ValidateCompletedGovernmentAuthority(result.Council, result.Government);

        Assert.Equal("completed", Assert.Single(result.Government.Changes).Status);
        Assert.Equal("voting", Assert.Single(government.Changes).Status);
    }

    [Fact]
    public void LocalGovernmentPublicationLeavesPopulationAuthorityForItsLinkCheck()
    {
        var government = TownGovernmentState.Create();
        var system = new TownSystem(CivicState.Empty);

        system.RestoreTowns([Town(government)]);

        Assert.Same(government, Assert.Single(system.State.Towns).Government);
    }
}
