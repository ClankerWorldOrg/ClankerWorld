using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CouncilInvariantTests
{
    [Theory]
    [InlineData("form")]
    [InlineData("members")]
    [InlineData("sequence")]
    [InlineData("proposal")]
    [InlineData("election")]
    [InlineData("notice")]
    [InlineData("relay")]
    public void InvalidCouncilFailsBeforeItsTownPublishes(string component)
    {
        var council = TownCouncilState.Create(["resident"]);
        var invalid = component switch
        {
            "form" => council with { Form = "unsupported" },
            "members" => council with { Members = ["resident", "resident"] },
            "sequence" => council with { Sequence = -1 },
            "proposal" => council with { Proposals = [new("forged", "request", "law", "resident", null, "Text", "", 0, 0, 120, ["resident"], 1, [])] },
            "election" => council with { Sequence = 1, Election = new("town:election:1", "initial", "unsupported", 0, 120, 240, 3, ["resident"], ["resident"], [], [], [], []) },
            "notice" => council with { Notices = [new("forged", "result", "subject", "Result", 0)] },
            "relay" => council with
            {
                Notices = [new("notice:1", "result", "subject", "Result", 0)],
                Knowledge = [new("resident", "notice:1", 1, "other"), new("other", "notice:1", 1, "resident")],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["resident"], [], [new(0, 0)]) { Council = invalid };

        Assert.Throws<InvalidDataException>(() => system.RestoreTowns([town]));

        Assert.Same(before, system.State);
    }

    [Fact]
    public void LocalCouncilChecksLeavePopulationEligibilityForTheLinkBoundary()
    {
        var system = new TownSystem(CivicState.Empty);
        var council = TownCouncilState.Create(["external-person"]);
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["resident"], [], [new(0, 0)]) { Council = council };

        system.RestoreTowns([town]);

        Assert.Same(council, Assert.Single(system.State.Towns).Council);
    }
}
