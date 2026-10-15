namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static void ValidateLocalAdmissions(TownRuntimeState town)
    {
        var records = town.Admissions ?? [];
        if (records.Any(record => record is null) || records.Select(record => record.ProposalId).Distinct(StringComparer.Ordinal).Count() != records.Count)
            throw new InvalidDataException("A Town's saved admission records are invalid.");
        foreach (var record in records)
            if (record.DecidedTick < 0 || string.IsNullOrWhiteSpace(record.ProposalId) || string.IsNullOrWhiteSpace(record.SubjectId) ||
                record.PreviousTownId == town.Id || record.Status switch
                {
                    WorldRuntime.CivicSharedRules.AdmissionApproved => record.MemberIds is not null || record.Reason is not null,
                    WorldRuntime.CivicSharedRules.AdmissionAdmitted => record.Reason is not null || record.MemberIds is not { Count: > 0 } members ||
                        !members.Contains(record.SubjectId, StringComparer.Ordinal) || members.Any(string.IsNullOrWhiteSpace) ||
                        !members.SequenceEqual(members.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                    WorldRuntime.CivicSharedRules.AdmissionLapsed => record.PreviousTownId is not null || record.MemberIds is not null ||
                        !WorldRuntime.CivicSharedRules.AdmissionLapseReasons.Contains(record.Reason ?? "", StringComparer.Ordinal),
                    _ => true,
                })
                throw new InvalidDataException("A Town's saved admission record does not match a passed admission proposal.");
    }

    internal static void ValidateLocalLandRequest(HouseholdLandUseRequest request)
    {
        if (request.Status is not ("pending" or "granted" or "rejected" or "withdrawn" or "hearing_resolved") ||
            request.Status == "pending" && request.SettledTick is not null ||
            request.Status != "pending" && (request.SettledTick is null || request.SettledTick < request.RequestedTick) ||
            request.Consents is null || request.GrantAdults is null || request.HearingResolutions is null ||
            request.Consents.Any(c => c is null || string.IsNullOrWhiteSpace(c.AgentId) || c.Tick < request.RequestedTick ||
                request.SettledTick is { } settled && c.Tick > settled) ||
            request.Consents.Select(c => c.AgentId).Distinct(StringComparer.Ordinal).Count() != request.Consents.Count ||
            request.Status != "granted" && request.GrantAdults.Count != 0)
            throw new InvalidDataException("A saved household land request has invalid approval or consent history.");
        if (request.Status == "granted" && (request.GrantAdults.Count == 0 ||
            !request.GrantAdults.SequenceEqual(TownCouncilRules.Ordered(request.GrantAdults)) ||
            request.GrantAdults.Any(id => string.IsNullOrWhiteSpace(id) || !request.Consents.Any(c => c.AgentId == id && c.Accepted)) ||
            request.AgreedEndTick is { } end && end <= request.SettledTick))
            throw new InvalidDataException("A household land grant must retain its Council approval, adult acceptance and recorded terms.");
    }
}
