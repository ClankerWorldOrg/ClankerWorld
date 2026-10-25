using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CivicProcessInvariantTests
{
    private static TownRuntimeState Town() => new("town", "Town", "founded", 0, [], [], [new(0, 0)]);

    [Theory]
    [InlineData("status")]
    [InlineData("members")]
    [InlineData("reason")]
    public void MalformedAdmissionLeavesThePublishedTownUnchanged(string component)
    {
        var record = new TownAdmissionRecord("proposal", "resident", "admitted", 1, MemberIds: ["resident"]);
        record = component switch
        {
            "status" => record with { Status = "unsupported" },
            "members" => record with { MemberIds = ["resident", "resident"] },
            "reason" => record with { Reason = "unexpected" },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        TownPublicationAssertions.Reject(system => system.RestoreTowns([Town() with { Admissions = [record] }]));
    }

    [Theory]
    [InlineData("settlement")]
    [InlineData("consent")]
    [InlineData("adults")]
    public void MalformedLandRequestLeavesThePublishedLedgerUnchanged(string component)
    {
        var request = new HouseholdLandUseRequest("request", "town", "household", "resident", [new(0, 0)], 1);
        request = component switch
        {
            "settlement" => request with { SettledTick = 2 },
            "consent" => request with { Consents = [new("resident", true, 0)] },
            "adults" => request with { GrantAdults = ["resident"] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        TownPublicationAssertions.Reject(system => system.RestoreHouseholdLandUseRequests([request]));
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("delivery")]
    public void MalformedProjectLeavesThePublishedTownUnchanged(string component)
    {
        var definition = TownHallContent.Hall3x4();
        var site = new GridPoint(4, 4);
        var plan = new TownProjectPayload("Hall", definition.CanonicalId, site, TownHallContent.Entrance(site), definition.BuildCosts);
        var project = new TownProject("project", "proposal", plan, 0, "supplying", 0, 0, []);
        project = component switch
        {
            "stage" => project with { Stage = "unsupported" },
            "delivery" => project with { Deliveries = [new("incorrect", "resident", "source", "lot", "wood", 1, 0)] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        TownPublicationAssertions.Reject(system => system.RestoreTowns([Town() with { Projects = [project] }]));
    }

    [Fact]
    public void RequestPublicationDoesNotRequireTheSeparateCouncilAndPopulationLinks()
    {
        var request = new HouseholdLandUseRequest("request", "town", "household", "resident", [new(0, 0)], 1)
        { Status = "granted", SettledTick = 2, Consents = [new("resident", true, 1)], GrantAdults = ["resident"] };
        var system = new TownSystem(CivicState.Empty);

        system.RestoreHouseholdLandUseRequests([request]);

        Assert.Same(request, Assert.Single(system.State.HouseholdLandUseRequests));
    }
}
