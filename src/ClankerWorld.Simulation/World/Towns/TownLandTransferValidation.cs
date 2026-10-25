using System.Diagnostics.CodeAnalysis;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public static partial class TownLandTransferValidation
{
    public static void Validate(WorldMap map, long tick, string townId, TownLandRecords state,
        IReadOnlyList<HouseholdLandUseRight> currentRights, IReadOnlyList<TownLandTitleRecord> titles,
        IReadOnlySet<string> agents, IReadOnlySet<string> households, TownCouncilState? council)
    {
        ValidateLocal(townId, state);
        var recordedRights = currentRights.Concat(state.OriginalRights.Select(version => version.Right))
            .Concat(state.Adjustments.SelectMany(adjustment => adjustment.PriorRights.Select(version => version.Right).Concat(adjustment.ResultRights))).ToArray();
        foreach (var request in state.Transfers)
        {
            Check(agents.Contains(request.FilerId) && households.Contains(request.TargetHouseholdId) &&
                TownLandRightsRules.IsValidPlot(map, request.Tiles, tick, request.ProposedTick) &&
                request.Tiles.All(tile => TownLandRightsRules.IsCoveredByTownTitle(tile, townId, titles)),
                "A saved transfer must retain its immutable known parties, connected titled plot and exact prior permissions.");
            foreach (var version in request.RightVersions)
                Check(households.Contains(version.Right.HouseholdId) &&
                    TownLandRightsRules.IsValidPlot(map, version.Right.Tiles, request.ProposedTick, version.Right.GrantedTick, version.Right.AgreedEndTick) &&
                    recordedRights.Any(recorded => recorded.Id == version.Id && TownLandHearingRules.Version(recorded) == version.Version),
                    "A transfer cannot invent a prior permission, expiry term or source household.");
            ValidateParties(request.Parties, agents, households);
            Check(council?.Notices.Any(notice => notice.Id == request.NoticeId && notice.Kind == "land_transfer" &&
                notice.SubjectId == TownLandTransferRules.TermsToken(request) && notice.PostedTick == request.ProposedTick) == true,
                "A transfer needs its actual public notice containing the exact immutable terms.");
            foreach (var response in request.Responses)
                Check(agents.Contains(response.AgentId) && response.Tick <= tick && response.PartyAdults.All(agents.Contains) &&
                    TownLandTransferRules.HasNotice(request, response.AgentId, response.Tick, council!.Knowledge),
                    "Each transfer response needs the household adult's own actual notice receipt and contemporaneous roster.");
            Check(request.SettledTick is null || request.SettledTick <= tick, "A transfer closure must retain its actual settlement time.");
            if (request.Status == "transferred")
            {
                var receipt = request.Receipt!;
                ValidateParties(receipt.Parties, agents, households);
                Check(TownLandTransferRules.HasAllConsents(request, receipt.Parties, council!.Knowledge, receipt.Tick),
                    "A completed transfer needs every current source and beneficiary adult's separate acceptance.");
            }
            else if (request.Status == "pending")
                Check(TownLandTransferRules.PendingFailure(state, request, currentRights, [], tick) is null,
                    "An invalidated, declined or withdrawn transfer must retain its actual closure reason.");
        }
    }

    public static void ValidateSalePayments(WorldMap map, TownLandRecords state, InventoryCheckpoint inventory)
    {
        ValidateLocalPayments(state);
        foreach (var request in state.Transfers.Where(item => item.Price is not null && item.Status == "transferred"))
        {
            var price = request.Price!;
            var receipt = request.Receipt!;
            var payment = receipt.Payment;
            Check(payment is not null && map.Contains(payment.Position), "A sale receipt needs actual accepted household adults and the exact paid physical quantities.");
            for (var index = 0; index < payment.Lots.Count; index++)
            {
                var lot = payment.Lots[index];
                var transferId = TownLandTransferRules.PaymentTransferId(request, payment.BuyerAgentId, index);
                var retained = inventory.Events.SingleOrDefault(item => item.EventId == lot.TransferEventId);
                Check(retained is null ? lot.TransferEventId <= inventory.EventHistoryFloor :
                    retained.Kind == "inventory_transferred" && retained.WorldTick == receipt.Tick &&
                    retained.Detail == $"{transferId}:{payment.BuyerAgentId}:{price.SellerHouseholdId}:{lot.SourceLotId}:{lot.Quantity}:land_use_right_payment",
                    "A sale payment receipt must match its actual committed inventory transfer.");
                var destinationId = DerivedId.Create(lot.SourceLotId + "#transfer:" + transferId);
                Check(inventory.Lots.Where(item => item.Id == destinationId || item.Id == lot.SourceLotId)
                    .All(item => item.ItemKind == price.ItemKind), "A retained paid lot must have the agreed goods kind.");
            }
        }
    }

    public static void ValidateAdjustment(WorldMap map, TownLandRecords state, TownLandRightAdjustment adjustment)
    {
        var request = state.Transfers.SingleOrDefault(item => item.Id == adjustment.TransferId);
        ValidateLocalAdjustment(state, adjustment);
        var expected = TownLandRightsRules.ReassignFootprintRights(map, adjustment.PriorRights.Select(version => version.Right).ToArray(),
            adjustment.Tiles.ToHashSet(), request!.TargetHouseholdId, adjustment.Tick);
        Check(TownLandHearingValidation.TileTerms(expected).SequenceEqual(TownLandHearingValidation.TileTerms(adjustment.ResultRights)),
            "A voluntary transfer may reassign only the noticed existing permissions, preserving all original terms and outside pieces.");
    }

    private static void ValidateParties(IReadOnlyList<TownLandTransferParty> parties,
        IReadOnlySet<string> agents, IReadOnlySet<string> households) =>
        Check(parties.All(party => households.Contains(party.HouseholdId) && party.AdultIds.All(agents.Contains)),
            "A transfer must retain precisely its source and beneficiary households and actual adult signatories.");
    private static bool CanonicalPeople(IReadOnlyList<string>? values) => values is not null && values.All(value => IdentityRules.Reference(value, int.MaxValue, requireTrimmed: true)) && values.SequenceEqual(TownLandHearingRules.Ordered(values));
    private static void Check([DoesNotReturnIf(false)] bool valid, string reason) { if (!valid) throw new InvalidDataException(reason); }
}
