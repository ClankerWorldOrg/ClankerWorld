using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Current standing and representation from one captured set of public world facts.</summary>
public static class TownLandCasePartyRules
{
    public static TownLandCaseParty[] CurrentParties(TownRuntimeState town, IReadOnlyList<GridPoint> tiles,
        IReadOnlyList<HouseholdLandUseRight> rights, IReadOnlyList<HouseholdLandUseRequest> requests,
        IReadOnlyList<SocietyAgent> agents, long tick, TownLandCase? item = null,
        string? filingHousehold = null, string? townRepresentative = null)
    {
        var householdIds = rights.Where(right => right.TownId == town.Id && right.Tiles.Any(tiles.Contains))
            .Select(right => right.HouseholdId)
            .Concat(requests.Where(request => request.TownId == town.Id && request.Status == "pending" &&
                TownLandRightsRules.UnresolvedRequestTiles(request).Any(tiles.Contains)).Select(request => request.HouseholdId))
            .Concat(item is null ? [] : TownLandHearingRules.CurrentRevision(item).Parties
                .Where(party => party.HouseholdId is not null).Select(party => party.HouseholdId!))
            .Concat(filingHousehold is null ? [] : new[] { filingHousehold });
        var parties = householdIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(id => new TownLandCaseParty(HouseholdPartyId(id), "household", id, town.Id,
                agents.Where(person => IsAdult(person) && person.HouseholdId == id)
                    .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray())).ToList();
        var townFiling = item?.Filings.LastOrDefault(filing => filing.AuthorityId is not null && filing.Kind != "expiry");
        if (townRepresentative is not null || townFiling is not null)
        {
            var representative = townRepresentative ?? (townFiling?.AgentId is { } filer &&
                TownFilingAuthority(town, filer, townFiling.AuthorityId, agents, tick) ? filer : null);
            if (representative is null && town.Government is { Arrangement.Ordinary: TownArrangementRules.Mayor } government)
                representative = government.Offices.SingleOrDefault(office => office.Mandates == "ordinary" &&
                    office.HolderId is not null && office.TermEndTick > tick)?.HolderId;
            parties.Add(new(TownRecordIds.TownParty(town.Id), "town", null, town.Id, [], representative));
        }
        return item?.Property is { } property ? TownPropertyRules.Parties(parties, property, town.Id, agents) :
            parties.OrderBy(party => party.Id, StringComparer.Ordinal).ToArray();
    }

    internal static string HouseholdPartyId(string household)
    {
        var id = "household:" + household;
        return DerivedId.Create(id, "household-sha256:");
    }

    public static bool TownFilingAuthority(TownRuntimeState town, string actor, string? authorityId,
        IReadOnlyList<SocietyAgent> agents, long tick)
    {
        if (!town.ResidentIds.Contains(actor, StringComparer.Ordinal) || !agents.Any(person => person.Id == actor && IsAdult(person)) ||
            town.Government is not { } government || authorityId is null) return false;
        if (government.Arrangement.Ordinary == TownArrangementRules.Mayor)
            return government.Offices.Any(office => office.Mandates == "ordinary" && office.HolderId == actor &&
                office.TermEndTick > tick && office.ElectionId == authorityId);
        return government.Arrangement.Ordinary is TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil &&
            town.Council is { } council && council.Members.Contains(actor, StringComparer.Ordinal) &&
            council.Proposals.Any(proposal => proposal.Id == authorityId && proposal.Kind == "land_hearing" &&
                proposal.Status == "passed" && proposal.AuthorId == actor);
    }

    private static bool IsAdult(SocietyAgent person) => person.Status == SocietyAgentStatus.Active &&
        LifeStageRules.IsAdult(person.AgeBand);
}
