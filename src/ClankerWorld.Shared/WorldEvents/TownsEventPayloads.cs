using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

/// <summary>Land case events with the same revision and status fields.</summary>
public abstract record LandCaseWorldEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string CaseId, [property: JsonRequired] int Revision, [property: JsonRequired] string Status) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, CaseId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && CaseId is not null && Status is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseEvidenceEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseInspectedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseJudgeAssignedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseJudgeConsentEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseJudgeElectionEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseNoticeEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseOpenedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseRejectedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseRelayedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseReopenRequestedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseReopenedEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseResponseEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandCaseRulingEventPayload(string? ActorId, string TownId, string CaseId, int Revision, string Status) : LandCaseWorldEventPayload(ActorId, TownId, CaseId, Revision, Status);

/// <summary>Land transfer events with the same request and status fields.</summary>
public abstract record LandTransferWorldEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string RequestId, [property: JsonRequired] string Status) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{RequestId}|1|{ActorId}|{Status}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && RequestId is not null && Status is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandTransferBlockedEventPayload(string? ActorId, string TownId, string RequestId, string Status) : LandTransferWorldEventPayload(ActorId, TownId, RequestId, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandTransferConsentEventPayload(string? ActorId, string TownId, string RequestId, string Status) : LandTransferWorldEventPayload(ActorId, TownId, RequestId, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandTransferProposedEventPayload(string? ActorId, string TownId, string RequestId, string Status) : LandTransferWorldEventPayload(ActorId, TownId, RequestId, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandTransferReadEventPayload(string? ActorId, string TownId, string RequestId, string Status) : LandTransferWorldEventPayload(ActorId, TownId, RequestId, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandTransferSettledEventPayload(string? ActorId, string TownId, string RequestId, string Status) : LandTransferWorldEventPayload(ActorId, TownId, RequestId, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandTransferWithdrawnEventPayload(string? ActorId, string TownId, string RequestId, string Status) : LandTransferWorldEventPayload(ActorId, TownId, RequestId, Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandUseGrantedEventPayload(string? ActorId, [property: JsonRequired] string RequestId, [property: JsonRequired] string TownId, [property: JsonRequired] string HouseholdId, [property: JsonRequired] int TilesCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{RequestId}:{TownId}:{HouseholdId}:{TilesCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId, TownId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null && TownId is not null && HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LandUseRequestedEventPayload(string? ActorId, [property: JsonRequired] string RequestId, [property: JsonRequired] string TownId, [property: JsonRequired] string HouseholdId, [property: JsonRequired] int TilesLength) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{RequestId}:{TownId}:{HouseholdId}:{TilesLength}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId, TownId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null && TownId is not null && HouseholdId is not null;
}

/// <summary>Law case events with the same Town and subject references.</summary>
public abstract record LawCaseWorldEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string SubjectId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{SubjectId}|{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, SubjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && SubjectId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseEvidenceEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseFindingEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseInspectedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseJudgeAssignedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseJudgeConsentEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseJudgeElectionEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseOfferEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseOfferResponseEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseOpenedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseRejectedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseRelayedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseRemedyEffectEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string AgreementId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{AgreementId}|{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, AgreementId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && AgreementId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseReopenRequestedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseReopenedEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LawCaseResponseEventPayload(string? ActorId, string TownId, string SubjectId) : LawCaseWorldEventPayload(ActorId, TownId, SubjectId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketBuiltEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string PlacedInstanceId, [property: JsonRequired] int StallsLength) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{PlacedInstanceId}|{StallsLength}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, PlacedInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && PlacedInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketStallBorrowedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string OccupancyId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{BuildingId}|{ActorId}|{OccupancyId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, BuildingId, OccupancyId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && BuildingId is not null && OccupancyId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketStallBuiltEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string PlacedInstanceId, [property: JsonRequired] int Slot) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{PlacedInstanceId}|{Slot}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, PlacedInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && PlacedInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketStallLeftEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string OccupancyId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{BuildingId}|{ActorId}|{OccupancyId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, BuildingId, OccupancyId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && BuildingId is not null && OccupancyId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketStockCollectedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string StallId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{StallId}|{ActorId}|{LotId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, StallId, LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && StallId is not null && LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketStockDeliveredEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string ReceiptId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{BuildingId}|{ActorId}|{ReceiptId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, BuildingId, ReceiptId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && BuildingId is not null && ReceiptId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketStockLoadedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string StallId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{StallId}|{ActorId}|{LotId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, StallId, LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && StallId is not null && LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketTradeCancelledEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string StallBuildingId, [property: JsonRequired] string SellerId, [property: JsonRequired] string BuyerId, [property: JsonRequired] string OfferId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{StallBuildingId}|{SellerId}|{BuyerId}|{OfferId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, StallBuildingId, SellerId, BuyerId, OfferId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && StallBuildingId is not null && SellerId is not null && BuyerId is not null && OfferId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketTradeCompletedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string BuyerId, [property: JsonRequired] string OfferId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{BuildingId}|{ActorId}|{BuyerId}|{OfferId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, BuildingId, BuyerId, OfferId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && BuildingId is not null && BuyerId is not null && OfferId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketTradeOfferedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string MarketId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string Seller, [property: JsonRequired] string Buyer, [property: JsonRequired] string OfferId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{MarketId}|{BuildingId}|{Seller}|{Buyer}|{OfferId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, MarketId, BuildingId, Seller, Buyer, OfferId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && MarketId is not null && BuildingId is not null && Seller is not null && Buyer is not null && OfferId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownAbandonedEventPayload(string? ActorId, [property: JsonRequired] string TownId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownAdmissionAcceptedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string PreviousTownId, [property: JsonRequired] int MemberCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{SubjectId}|{PreviousTownId}|{MemberCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, SubjectId, PreviousTownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && SubjectId is not null && PreviousTownId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownAdmissionApprovedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string ProposalId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{SubjectId}|{ProposalId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, SubjectId, ProposalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && SubjectId is not null && ProposalId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownAdmissionLapsedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string ProposalId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{SubjectId}|{ProposalId}|{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, SubjectId, ProposalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && SubjectId is not null && ProposalId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownBorderExpandedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BuildingInstanceId, [property: JsonRequired] int BorderCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BuildingInstanceId}:tiles:{BorderCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownBuildingAssignedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BuildingInstanceId, [property: JsonRequired] int AssignedBuildingIdsCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BuildingInstanceId}:buildings:{AssignedBuildingIdsCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicActionEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string Action) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{ActorId}|{Action}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && Action is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicActionRejectedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string Action) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{ActorId}|{Action}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && Action is not null;
}

/// <summary>Civic notices that omit empty subject references.</summary>
public abstract record TownCivicNoticeWorldEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string NoticeText) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{SubjectId}|{NoticeText}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, SubjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && SubjectId is not null && NoticeText is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicCancelledEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicCouncilEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicElectionEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicGovernmentEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicLandHearingEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicLandTransferEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicLawEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicLawCaseEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicMayorEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicNonviolentHearingEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicProposalEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicRemedyEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicResultEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicRunoffEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownFoundedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string? PreviousTownId, [property: JsonRequired] int MemberCount, [property: JsonRequired] bool IsFirstTown) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => IsFirstTown ? $"{TownId}:residents:{MemberCount}" : $"{TownId}|{ActorId}|{PreviousTownId ?? "none"}|{MemberCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, PreviousTownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownFoundingStartedEventPayload(string? ActorId, [property: JsonRequired] string FirstTownId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{FirstTownId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FirstTownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FirstTownId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownLandClaimedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ProposalId, [property: JsonRequired] string Tiles) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{ProposalId}|{Tiles}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ProposalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ProposalId is not null && Tiles is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownLayoutSiteRejectedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string DefinitionId, [property: JsonRequired] int SelectedSiteX, [property: JsonRequired] int SelectedSiteY) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{ActorId}|{DefinitionId}|{SelectedSiteX}|{SelectedSiteY}|site_unavailable";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, DefinitionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && DefinitionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownMembershipEvaluatedEventPayload(string? ActorId) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:unaffiliated";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectApprovedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string ProjectName) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{ProjectId}:{ProjectName}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ProjectId is not null && ProjectName is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectBlockedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string Failure) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{ProjectId}:{Failure}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ProjectId is not null && Failure is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectCancelledEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{ProjectId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ProjectId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectCompletedEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string ProjectName) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{BuildingId}:{ProjectName}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId, BuildingId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null && BuildingId is not null && ProjectName is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectDonatedEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string LotId, [property: JsonRequired] int Quantity, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{LotId}:{Quantity}:{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId, LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null && LotId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectMaterialDeliveredEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string LotId, [property: JsonRequired] int Quantity, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{LotId}:{Quantity}:{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId, LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null && LotId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectMaterialPickedUpEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string MovedId, [property: JsonRequired] int Quantity, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{MovedId}:{Quantity}:{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId, MovedId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null && MovedId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectMaterialRecoveredEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string MovedId, [property: JsonRequired] int Quantity, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{MovedId}:{Quantity}:{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId, MovedId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null && MovedId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectMaterialReturnedEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] string LotId, [property: JsonRequired] int Quantity, [property: JsonRequired] string DestinationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{LotId}:{Quantity}:{DestinationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId, LotId, DestinationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null && LotId is not null && DestinationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectResumedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ProjectId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{ProjectId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ProjectId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownProjectWorkedEventPayload(string? ActorId, [property: JsonRequired] string ProjectId, [property: JsonRequired] int Done) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ProjectId}:{Done}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ProjectId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownResettledEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string PreviousTownId, [property: JsonRequired] int MemberCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{ActorId}|{PreviousTownId}|{MemberCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, PreviousTownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && PreviousTownId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownResidentJoinedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ResidentId, [property: JsonRequired] string Reason, [property: JsonRequired] int ResidentIdsCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{ResidentId}:{Reason}:residents:{ResidentIdsCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ResidentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ResidentId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownResidentLeftEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ResidentId, [property: JsonRequired] int ResidentsLength) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{ResidentId}:residents:{ResidentsLength}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, ResidentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ResidentId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownResourceCollectedEventPayload(string? ActorId, [property: JsonRequired] string CommunalId, [property: JsonRequired] int Quantity, [property: JsonRequired] string WarehouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CommunalId}:{Quantity}:{WarehouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CommunalId, WarehouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CommunalId is not null && WarehouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownResourcesStoredEventPayload(string? ActorId, [property: JsonRequired] string LotId, [property: JsonRequired] int Quantity, [property: JsonRequired] string WarehouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{LotId}:{Quantity}:{WarehouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { LotId, WarehouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null && WarehouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRevivedEventPayload(string? ActorId, [property: JsonRequired] string TownId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRoadExtendedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BuildingInstanceId, [property: JsonRequired] int ExtendedCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BuildingInstanceId}:tiles:{ExtendedCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRoadExtensionRefusedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BuildingInstanceId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BuildingInstanceId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BuildingInstanceId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRoadGeneratedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BuildingInstanceId, [property: JsonRequired] int LaidCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BuildingInstanceId}:tiles:{LaidCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRoadLinkUnconnectedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string NearestId, [property: JsonRequired] string Failure) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{NearestId}|{Failure}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, NearestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && NearestId is not null && Failure is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRoadLinkedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string NearestId, [property: JsonRequired] int Added) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{NearestId}|tiles:{Added}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, NearestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && NearestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownRoadUnconnectedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BuildingInstanceId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BuildingInstanceId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BuildingInstanceId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownStockSalvagedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{ActorId}|{ItemKind}|{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && ItemKind is not null;
}

/// <summary>Civic notices that retain both subject entries, including empty identities.</summary>
public abstract record TownCivicUnfilteredNoticeWorldEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string NoticeText) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}|{SubjectId}|{NoticeText}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [TownId, SubjectId];
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && SubjectId is not null && NoticeText is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicCandidateEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicUnfilteredNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicNominationEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicUnfilteredNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownCivicLandUseEventPayload(string? ActorId, string TownId, string SubjectId, string NoticeText) : TownCivicUnfilteredNoticeWorldEventPayload(ActorId, TownId, SubjectId, NoticeText);
