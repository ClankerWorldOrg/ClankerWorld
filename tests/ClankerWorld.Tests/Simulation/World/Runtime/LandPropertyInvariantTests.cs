using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class LandPropertyInvariantTests
{
    private static TownRuntimeState Town(TownLandRecords state) =>
        new("town", "Town", "founded", 0, [], [], [new(0, 0)]) { LandRecords = state };

    private static WorldMap Map() => new(3, 1, 0,
        [new(new(0, 0), TerrainKind.Meadow), new(new(1, 0), TerrainKind.Meadow), new(new(2, 0), TerrainKind.Meadow)], [], [], "local-property");

    private static readonly TownLandTransferParty[] Parties = [new("alpha", "source", ["owner"]), new("beta", "beneficiary", ["recipient"])];
    private static readonly HouseholdLandUseRight[] Rights = [new("right", "town", "alpha", [new(1, 0)], 0, "grant", 100)];
    private static readonly TownCivicReceipt[] Receipts = [new("owner", "notice", 1), new("recipient", "notice", 1)];

    private static TownLandRecords Proposed() => TownLandTransferRules.Propose(TownLandRecords.Create(), Map(), "town", "owner",
        [new(1, 0)], "beta", Rights, Parties, [], 0, "notice", new("alpha", "wood", 2));

    private static TownLandRecords Transferred()
    {
        var state = Proposed();
        var request = state.Transfers[0];
        state = TownLandTransferRules.Respond(state, request.Id, TownLandTransferRules.TermsToken(request), "owner", "alpha", "accept", Receipts, Rights, Parties, [], 2);
        state = TownLandTransferRules.Respond(state, request.Id, TownLandTransferRules.TermsToken(request), "recipient", "beta", "accept", Receipts, Rights, Parties, [], 2);
        var payment = new TownLandSalePayment("recipient", "owner", new(1, 0), [new("paid-lot", 2, 1)]);
        return TownLandTransferRules.Advance(state, Map(), Rights, new Dictionary<string, IReadOnlyList<TownLandTransferParty>> { [request.Id] = Parties },
            [], Receipts, 3, new Dictionary<string, TownLandSalePayment> { [request.Id] = payment }).State;
    }

    private static TownLandRecords PropertyCase()
    {
        var request = new TownPropertyRequest("building", "alpha", null);
        var building = new PlacedBuilding("building", "house", new(1, 0), 0, "town", "alpha");
        var property = new TownPropertyCase(request, [new(1, 1, building, [], ["owner"], [])], []);
        TownLandCaseParty[] parties = [new("alpha", "household", "alpha", "town", ["owner"]), new("town", "town", null, "town", [], "filer")];
        return TownLandHearingRules.File(TownLandRecords.Create(), "town",
            new("filer", "town", "Review this exact building.", TownPropertyRules.Outcome(request), 1, "authority"),
            [new(1, 0)], [], parties, 1, 10, "notice", property);
    }

    private static void Reject(TownLandRecords state)
    {
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => system.RestoreTowns([Town(state)]));

        Assert.Same(before, system.State);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("owner")]
    [InlineData("consent")]
    public void InvalidPropertyFileDoesNotPublish(string component)
    {
        var state = PropertyCase();
        var item = state.Cases[0];
        var property = item.Property!;
        property = component switch
        {
            "snapshot" => property with { Snapshots = [property.Snapshots[0] with { Revision = 2 }] },
            "owner" => property with { Snapshots = [property.Snapshots[0] with { Building = property.Snapshots[0].Building with { HouseholdId = "beta" } }] },
            "consent" => property with { Consents = [new(1, "owner", true, 2)] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Reject(state with { Cases = [item with { Property = property }] });
    }

    [Theory]
    [InlineData("status")]
    [InlineData("source")]
    [InlineData("party")]
    [InlineData("response")]
    public void InvalidTransferDoesNotPublish(string component)
    {
        var state = Proposed();
        var request = state.Transfers[0];
        request = component switch
        {
            "status" => request with { Status = "unsupported" },
            "source" => request with { RightVersions = [request.RightVersions[0] with { Version = "changed" }] },
            "party" => request with { Parties = [request.Parties[0] with { AdultIds = ["owner", "owner"] }, request.Parties[1]] },
            "response" => request with { Responses = [new("alpha", "owner", "unsupported", 2, ["owner"])] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Reject(state with { Transfers = [request] });
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("event")]
    [InlineData("payer")]
    public void InvalidPaymentDoesNotPublish(string component)
    {
        var state = Transferred();
        var request = state.Transfers[0];
        var payment = request.Receipt!.Payment!;
        payment = component switch
        {
            "quantity" => payment with { Lots = [payment.Lots[0] with { Quantity = 1 }] },
            "event" => payment with { Lots = [payment.Lots[0] with { TransferEventId = 0 }] },
            "payer" => payment with { BuyerAgentId = "unconsenting-person" },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Reject(state with { Transfers = [request with { Receipt = request.Receipt with { Payment = payment } }] });
    }

    [Fact]
    public void UnrecordedAdjustmentSourceDoesNotPublish()
    {
        var state = Proposed() with { Sequence = 2, Adjustments = [new("land-adjustment:town:2", "unsupported", 0, [], [], [new(1, 0)])] };

        Reject(state);
    }

    [Fact]
    public void NativePropertyAndTransferFilesPublishBeforeTheirExternalLinks()
    {
        var system = new TownSystem(CivicState.Empty);
        var property = PropertyCase();
        system.RestoreTowns([Town(property)]);
        Assert.Same(property, Assert.Single(system.State.Towns).LandRecords);
        var transferred = Transferred();

        system.RestoreTowns([Town(transferred)]);

        Assert.Same(transferred, Assert.Single(system.State.Towns).LandRecords);
        Assert.Equal("transferred", Assert.Single(transferred.Transfers).Status);
    }
}
