using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public static partial class TownLandTransferValidation
{
    internal static void ValidateLocal(string townId, TownLandRecords state)
    {
        Check(state.Transfers is not null && state.Transfers.All(request => request is not null), "Saved voluntary transfer records must be present.");
        Check(state.Transfers.Select(request => request.Id).Distinct(StringComparer.Ordinal).Count() == state.Transfers.Count,
            "Saved voluntary transfer identities must be unique.");
        if (state.Transfers.Count == 0) return;
        Check(state.OriginalRights.All(version => version.Right is not null) && state.Adjustments.All(adjustment =>
            adjustment.PriorRights is not null && adjustment.ResultRights is not null &&
            adjustment.PriorRights.All(version => version is not null && version.Right is not null) && adjustment.ResultRights.All(right => right is not null)),
            "Saved transfer provenance cannot contain incomplete permission history.");
        foreach (var request in state.Transfers)
        {
            Check(TownGovernmentValidation.ValidId(request.Id, "land-transfer:" + townId + ":", state.Sequence) && request.TownId == townId && TownHearingProcedure.Id(request.FilerId) &&
                TownHearingProcedure.Id(request.TargetHouseholdId) && request.ProposedTick >= 0 &&
                request.Status is "pending" or "transferred" or "rejected" or "withdrawn" or "invalidated" &&
                request.RightVersions is { Count: > 0 } && request.RightVersions.All(version => version is not null && version.Right is not null) &&
                request.Parties is { Count: > 0 } && request.Parties.All(party => party is not null) && request.Responses is not null &&
                request.Responses.All(response => response is not null) && TownSystem.IsLocallyValidPlot(request.Tiles, request.ProposedTick),
                "A saved transfer must retain its immutable known parties, connected titled plot and exact prior permissions.");
            Check(request.RightVersions.Select(version => version.Id).SequenceEqual(request.RightVersions.Select(version => version.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                "Transfer permission snapshots must be unique and canonical.");
            foreach (var version in request.RightVersions)
            {
                var right = version.Right;
                Check(version.Id == right.Id && Id(right.Id) && right.TownId == townId &&
                    right.HouseholdId != request.TargetHouseholdId && right.Tiles.Any(request.Tiles.Contains) &&
                    version.Version == TownLandHearingRules.Version(right) &&
                    TownSystem.IsLocallyValidPlot(right.Tiles, right.GrantedTick, right.AgreedEndTick) && right.GrantedTick <= request.ProposedTick &&
                    (right.AgreedEndTick is null || right.AgreedEndTick > request.ProposedTick),
                    "A transfer cannot invent a prior permission, expiry term or source household.");
            }
            Check(request.Tiles.All(tile => request.RightVersions.Count(version => version.Right.Tiles.Contains(tile)) == 1),
                "A transfer plot must consist entirely of unambiguous existing permissions.");
            if (request.Price is { } price)
                Check(price.Quantity > 0 && TownLandHearingRules.ValidText(price.ItemKind, ContentPackageRules.MaximumContentIdLength) && !price.ItemKind.Any(char.IsWhiteSpace) &&
                    !InventoryContainerRules.IsContainer(price.ItemKind) && price.ItemKind != "handcart" &&
                    request.RightVersions.All(version => version.Right.HouseholdId == price.SellerHouseholdId) &&
                    request.Parties.Any(party => party.HouseholdId == price.SellerHouseholdId && party.AdultIds.Contains(request.FilerId)),
                    "A goods sale must retain its exact positive price and actual selling household.");
            ValidateLocalParties(request.Parties, request, requireAdults: true);
            Check(request.Parties.Any(party => party.AdultIds.Contains(request.FilerId, StringComparer.Ordinal)), "A transfer filer must be an actual affected adult.");
            foreach (var response in request.Responses)
                Check(request.Parties.Any(party => party.HouseholdId == response.HouseholdId) &&
                    response.Kind is "accept" or "decline" && response.Tick >= request.ProposedTick &&
                    (request.SettledTick is null || response.Tick <= request.SettledTick) && CanonicalPeople(response.PartyAdults) &&
                    response.PartyAdults.Contains(response.AgentId, StringComparer.Ordinal),
                    "Each transfer response needs the household adult's own actual notice receipt and contemporaneous roster.");
            Check(request.Responses.Select(response => response.Tick).SequenceEqual(request.Responses.Select(response => response.Tick).Order()),
                "Transfer responses must retain their actual chronological order.");
            Check(request.Status == "pending" ? request.SettledTick is null && request.Receipt is null && request.Reason is null :
                request.SettledTick >= request.ProposedTick, "A transfer closure must retain its actual settlement time.");
            if (request.Status == "transferred")
            {
                var receipt = request.Receipt;
                Check(receipt is not null && receipt.Tick == request.SettledTick && Id(receipt.AdjustmentId) && request.Reason is null,
                    "A completed transfer needs its exact durable permission-adjustment receipt.");
                Check(request.Price is null ? receipt.Payment is null : receipt.Payment is not null,
                    "A paid permission transfer needs its actual goods payment; a free transfer cannot invent one.");
                ValidateLocalParties(receipt.Parties, request, requireAdults: true);
                Check(TownLandTransferRules.HasAllRecordedConsents(request, receipt.Parties, receipt.Tick) &&
                    request.RightVersions.All(version => version.Right.AgreedEndTick is null || version.Right.AgreedEndTick > receipt.Tick) &&
                    request.Responses.All(response => response.Kind == "accept") &&
                    state.Adjustments.Count(adjustment => adjustment.Id == receipt.AdjustmentId && adjustment.TransferId == request.Id && adjustment.Kind == "voluntary_transfer") == 1,
                    "A completed transfer needs every current source and beneficiary adult's separate acceptance.");
            }
            else
            {
                Check(request.Receipt is null && !state.Adjustments.Any(adjustment => adjustment.TransferId == request.Id), "An unfinished or refused transfer cannot alter permissions.");
                Check(request.Status switch
                {
                    "pending" => request.Responses.All(response => response.Kind == "accept"),
                    "rejected" => request.Reason == "adult_declined" && request.Responses.Count > 0 &&
                        request.Responses[^1].Kind == "decline" && request.Responses[^1].Tick == request.SettledTick,
                    "withdrawn" => request.Reason == "filer_withdrew",
                    "invalidated" => request.Reason is "rights_changed" or "permission_expired" or "plot_disputed" or "hearing_open",
                    _ => false
                }, "An invalidated, declined or withdrawn transfer must retain its actual closure reason.");
            }
        }
        ValidateLocalPayments(state);
    }

    private static void ValidateLocalParties(IReadOnlyList<TownLandTransferParty> parties, TownLandTransferRequest request, bool requireAdults) =>
        Check(parties is not null && parties.All(party => party is not null &&
            CanonicalPeople(party.AdultIds) && (!requireAdults || party.AdultIds.Count > 0)) &&
            TownLandTransferRules.ValidParties(request.RightVersions.Select(version => version.Right), request.TargetHouseholdId, parties),
            "A transfer must retain precisely its source and beneficiary households and actual adult signatories.");
    internal static void ValidateLocalAdjustment(TownLandRecords state, TownLandRightAdjustment adjustment)
    {
        var request = state.Transfers.SingleOrDefault(item => item.Id == adjustment.TransferId);
        Check(request is { Status: "transferred", Receipt: { } } && request.Receipt.AdjustmentId == adjustment.Id &&
            request.SettledTick == adjustment.Tick && adjustment.CaseId is null && adjustment.RulingId is null && adjustment.BuildingId is null &&
            adjustment.TargetHouseholdId == request.TargetHouseholdId && adjustment.Tiles.SequenceEqual(request.Tiles) &&
            TownLandTransferRules.SameSources(request.RightVersions, adjustment.PriorRights), "A voluntary adjustment needs its exact completed transfer receipt and prior permission versions.");
    }
    private static void ValidateLocalPayments(TownLandRecords state)
    {
        foreach (var request in state.Transfers.Where(item => item.Price is not null && item.Status == "transferred"))
        {
            var price = request.Price!;
            var receipt = request.Receipt!;
            var payment = receipt.Payment;
            Check(payment is not null &&
                receipt.Parties.Any(party => party.HouseholdId == request.TargetHouseholdId && party.AdultIds.Contains(payment.BuyerAgentId)) &&
                receipt.Parties.Any(party => party.HouseholdId == price.SellerHouseholdId && party.AdultIds.Contains(payment.SellerAgentId)) &&
                payment.Lots is { Count: > 0 } && payment.Lots.All(lot => lot is not null && !string.IsNullOrWhiteSpace(lot.SourceLotId) && !lot.SourceLotId.Any(char.IsControl) && lot.Quantity > 0 && lot.TransferEventId > 0) &&
                payment.Lots.Sum(lot => (long)lot.Quantity) == price.Quantity &&
                payment.Lots.Select(lot => lot.SourceLotId).Distinct(StringComparer.Ordinal).Count() == payment.Lots.Count &&
                payment.Lots.Select(lot => lot.TransferEventId).SequenceEqual(payment.Lots.Select(lot => lot.TransferEventId).Distinct().Order()),
                "A sale receipt needs actual accepted household adults and the exact paid physical quantities.");
        }
    }
}
