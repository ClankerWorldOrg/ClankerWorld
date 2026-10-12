using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class TownGovernmentTestSetup
{
    internal static readonly TownArrangement NoOffice = new(TownArrangementRules.Council, TownArrangementRules.NoOffice);

    // Rule-specific fixtures start after residents remove the founding office, so
    // creating a new mandate still exercises the protected handover contract.
    // Their Council ledger is initialized independently by each fixture.
    internal static TownGovernmentState WithoutOffice(string townId, string[] adults, int day)
    {
        var council = TownCouncilState.Create(adults);
        var government = TownGovernmentState.Create();
        (council, government) = TownGovernmentRules.Propose(council, government, townId, adults[0],
            NoOffice, false, adults, 0, day);
        foreach (var adult in adults)
            government = TownGovernmentRules.Vote(government, government.Changes[0].Id, adult, true, 0);
        (_, government) = TownGovernmentRules.Advance(council, government, townId, "Test Town", "seed", adults, 0, day);
        Assert.Equal("completed", Assert.Single(government.Changes).Status);
        Assert.Empty(government.Offices);
        return government;
    }
}
