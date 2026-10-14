using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal LandHearingFilingActionPlan? PlanLandHearingFiling(ITownWorld world, string actor, LandHearingFilingChoice choice, bool bindAuthority = false)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            CivicBoard(world, town) is not { } board) return null;
        var household = world.HouseholdFor(actor);
        var pending = town.LandRecords.Cases.Where(item => item.Status == "pending")
            .SelectMany(item => TownLandHearingRules.CurrentRevision(item).Tiles).ToHashSet();
        var affected = choice.Kind == LandHearingFilingKind.Household
            ? householdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household).SelectMany(right => right.Tiles)
                .Concat(householdLandUseRequests.Where(request => request.TownId == town.Id && request.HouseholdId == household && request.Status == "pending")
                    .SelectMany(TownLandRightsRules.UnresolvedRequestTiles))
                .Distinct().OrderBy(tile => world.Map.FootDistance(world.Agents[actor].Position, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
                .Where(tile => pending.Contains(tile) || TownLandHearingRules.FilingRefusal(town.LandRecords, town.Id, [tile], householdLandUseRights) is null)
                .Take(6).ToArray() : [];
        var office = choice.Kind == LandHearingFilingKind.TownOffice ? town.Government?.Offices.SingleOrDefault(office =>
            office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
        if (bindAuthority) choice = choice with
        {
            AuthorityToken = CivicAgentToken(choice.Kind switch
            {
                LandHearingFilingKind.Household => household ?? "",
                LandHearingFilingKind.TownOffice => office?.ElectionId ?? "",
                _ => DerivedId.Create("council:" + town.Council.Revision.ToString(CultureInfo.InvariantCulture))
            })
        };
        return new(actor, choice, board, household, office, town.Council.Revision, affected, affected.Any(pending.Contains));
    }

    internal static LandHearingFilingActionPlan BindLandHearingFiling(LandHearingFilingActionPlan plan,
        IReadOnlyList<CognitionLandTile>? tiles, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.Bound) return plan;
        var raw = terms?.Statement ?? proposal;
        return plan with
        {
            Bound = true,
            InvalidTerms = !TownLandHearingRules.ValidText(raw, TownLandGovernmentFilingRules.MaximumStatementLength),
            Filing = new(TownLandRightsRules.OrderTiles(tiles?.Select(tile => new GridPoint(tile.X, tile.Y)) ?? []),
                new(terms?.RequestedOutcome ?? "", terms?.HouseholdId, terms?.AgreedEndTick), raw?.Trim() ?? "")
        };
    }

    internal ActionCheck CheckLandHearingFiling(ITownWorld world, LandHearingFilingActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected filing notice place is unavailable");
        switch (plan.Choice.Kind)
        {
            case LandHearingFilingKind.Household:
                if (plan.Household is null || world.HouseholdFor(plan.Actor) != plan.Household || plan.Choice.AuthorityToken != CivicAgentToken(plan.Household))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected household no longer has an affected filing plot");
                if (!plan.Bound && !plan.NearbyAffectedTiles.Any(tile =>
                    (householdLandUseRights.Any(right => right.TownId == town.Id && right.HouseholdId == plan.Household && right.Tiles.Contains(tile)) ||
                        householdLandUseRequests.Any(request => request.TownId == town.Id && request.HouseholdId == plan.Household && request.Status == "pending" &&
                            TownLandRightsRules.UnresolvedRequestTiles(request).Contains(tile))) &&
                    (town.LandRecords.Cases.Any(item => item.Status == "pending" && TownLandHearingRules.CurrentRevision(item).Tiles.Contains(tile)) ||
                        TownLandHearingRules.FilingRefusal(town.LandRecords, town.Id, [tile], householdLandUseRights) is null)))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "none of the selected affected filing tiles remains available");
                break;
            case LandHearingFilingKind.TownOffice:
                if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) || town.Government?.Arrangement.Ordinary != TownArrangementRules.Mayor ||
                    plan.Office is null || plan.Office.HolderId != plan.Actor || plan.Office.TermEndTick is not { } end || end <= world.WorldTick ||
                    !town.Government.Offices.Contains(plan.Office) || plan.Choice.AuthorityToken != CivicAgentToken(plan.Office.ElectionId ?? ""))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected governing office can no longer file this case");
                break;
            case LandHearingFilingKind.CouncilProposal:
                if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
                    town.Government?.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
                    town.Council.Revision != plan.CouncilRevision || !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) ||
                    plan.Choice.AuthorityToken != CivicAgentToken("council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture)))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council filing authority has changed");
                break;
        }
        if (!plan.Bound) return new();
        if (plan.InvalidTerms || plan.Filing is not { } filing || !TownLandHearingRules.IsValidOutcome(filing.RequestedOutcome, world.WorldTick) ||
            filing.RequestedOutcome.Kind is "reclaim" or "grant" || filing.RequestedOutcome.HouseholdId is { } target && !world.Society.Households.Any(household => household.Id == target) ||
            !TownLandRightsRules.IsValidPlot(world.Map, filing.Tiles, world.WorldTick, world.WorldTick) ||
            filing.Tiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, townLandTitles)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected filing statement, plot or requested outcome is unavailable");
        if (TownLandHearingRules.FilingRefusal(town.LandRecords, town.Id, filing.Tiles, householdLandUseRights) is { } refusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
        if (plan.Choice.Kind == LandHearingFilingKind.Household && !LandHearingParties(world, town, filing.Tiles).Any(party => party.HouseholdId == plan.Household))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected household is no longer affected by that exact plot");
        if (plan.Choice.Kind == LandHearingFilingKind.CouncilProposal)
        {
            if (!TownLandGovernmentFilingRules.IsValid(filing, world.WorldTick))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Council filing request is malformed");
            if (TownCouncilRules.ProposalRetryRefusal(town.Council, TownLandGovernmentFilingRules.RequestKey(filing),
                "council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry);
        }
        else if (town.LandRecords.Cases.Any(item => item.Status == "pending" && item.Property is not null && TownLandHearingRules.CurrentRevision(item).Tiles.SequenceEqual(filing.Tiles)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected plot already has a different property hearing");
        return new();
    }
}
