using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RemedyInvariantTests
{
    private static TownRuntimeState Town(TownNonLandHearingState state) =>
        new("town", "Town", "founded", 0, [], [], [new(0, 0)]) { NonLandHearings = state };

    private static TownNonLandHearingState Offered()
    {
        var state = TownNonLandRulesTests.Settled();
        return TownRemedyRules.Offer(state, state.Cases[0].Id, state.Cases[0].Findings[0].Id, "judge",
            [new("goods", "return_goods", "subject", "witness", "wood", 2, null)], 20,
            "A voluntary contribution.", 21, 10, "notice:offer", feasible: true);
    }

    private static TownNonLandHearingState Accepted()
    {
        var state = Offered();
        var offer = state.Offers[0];
        return TownRemedyRules.Respond(state, offer.Id, offer.Revision, "subject", "accept", null, 22, 10,
            [new("subject", "notice:offer", 22)], feasible: true);
    }

    private static TownNonLandHearingState Performed()
    {
        var state = Accepted();
        return TownRemedyRules.RecordEffect(state, new("effect", state.Agreements[0].Id, "goods", "subject", 23,
            "return_goods", "witness", "wood", 1, null, Identity.Parse("native-receipt"), "version"));
    }

    private static void Reject(TownNonLandHearingState state) =>
        TownPublicationAssertions.Reject(system => system.RestoreTowns([Town(state)]));

    [Theory]
    [InlineData("terms")]
    [InlineData("finding")]
    [InlineData("response")]
    public void InvalidOfferDoesNotPublish(string component)
    {
        var state = Offered();
        var offer = state.Offers[0];
        offer = component switch
        {
            "terms" => offer with { TermsHash = "changed" },
            "finding" => offer with { FindingId = "unrecorded-finding" },
            "response" => offer with { Responses = [new("other-person", 1, offer.TermsHash, "accept", 22, offer.NoticeId)] },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Reject(state with { Offers = [offer] });
    }

    [Theory]
    [InlineData("offer")]
    [InlineData("consent")]
    [InlineData("deadline")]
    [InlineData("completion")]
    [InlineData("predecessor")]
    public void InvalidAgreementDoesNotPublish(string component)
    {
        var state = Accepted();
        var agreement = state.Agreements[0];
        agreement = component switch
        {
            "offer" => agreement with { OfferRevision = 2 },
            "consent" => agreement with { Consents = [] },
            "deadline" => agreement with { DeadlineTick = agreement.AcceptedTick - 1 },
            "completion" => agreement with { Status = "completed" },
            "predecessor" => agreement with { ReplacesAgreementId = "unrecorded-agreement" },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Reject(state with { Agreements = [agreement] });
    }

    [Theory]
    [InlineData("contributor")]
    [InlineData("quantity")]
    [InlineData("term")]
    public void InvalidPhysicalCreditDoesNotPublish(string component)
    {
        var state = Performed();
        var effect = state.Effects[0];
        effect = component switch
        {
            "contributor" => effect with { ActorId = "other-person" },
            "quantity" => effect with { Quantity = 3 },
            "term" => effect with { TermId = "unagreed-term" },
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };

        Reject(state with { Effects = [effect] });
    }

    [Fact]
    public void NativePartialPerformancePublishesBeforeExternalReceiptAndPopulationLinks()
    {
        var state = Performed();
        var system = new TownSystem(CivicState.Empty);

        system.RestoreTowns([Town(state)]);

        Assert.Same(state, Assert.Single(system.State.Towns).NonLandHearings);
        Assert.Equal("pending", state.Agreements[0].Status);
    }
}
