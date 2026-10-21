using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CivicInvariantTests
{
    [Theory]
    [InlineData("town")]
    [InlineData("founder")]
    [InlineData("policy")]
    [InlineData("ballot")]
    [InlineData("title")]
    [InlineData("right")]
    [InlineData("request")]
    public void InvalidCivicRecordFailsBeforePublication(string component)
    {
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() =>
        {
            switch (component)
            {
                case "town": system.RestoreTowns([new("town", "Town", "founded", 0, [], [], [])]); break;
                case "founder": system.RestoreFounderSetup(new(["founder", "founder"], false)); break;
                case "policy": system.RestoreFoodStewardship(new(null, "unknown", 0)); break;
                case "ballot": system.RestoreFoodStewardship(new(null, "open", 0, new("essential_first", 0, 120, ["voter"], ["voter"], ["voter"]))); break;
                case "title": system.RestoreTownLandTitles([new("title", "town", [], 0)]); break;
                case "right": system.RestoreHouseholdLandUseRights([new("right", "town", "home", [new(0, 0)], 5, "grant", 4)]); break;
                case "request": system.RestoreHouseholdLandUseRequests([new("request", "town", "home", "actor", [new(1, 0), new(0, 0)], 0)]); break;
                default: throw new ArgumentOutOfRangeException(nameof(component));
            }
        });

        Assert.Same(before, system.State);
    }

    [Fact]
    public void RecordPublicationKeepsExternalLandAuthorityForTheLinkCheck()
    {
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;
        var title = new TownLandTitleRecord("title", "town", [new(0, 0)], 0);

        system.RestoreTownLandTitles([title]);

        Assert.Same(title, Assert.Single(system.State.TownLandTitles));
        Assert.Empty(before.TownLandTitles);
    }
}
