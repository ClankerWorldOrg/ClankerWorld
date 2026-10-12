using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CivicSystem
{
    internal static bool LandHearingAdult(ICivicTownWorld world, string actor) => world.Society.Inhabitants.Any(person =>
            person.Id == actor && person.Status == SocietyInhabitantStatus.Active &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder);

    internal TownLandCaseParty[] LandHearingParties(ICivicTownWorld world, TownRuntimeState town,
            IReadOnlyList<GridPoint> tiles, TownLandCase? item = null, string? filingHousehold = null,
            string? townRepresentative = null) =>
            TownLandCasePartyRules.CurrentParties(town, tiles, householdLandUseRights, householdLandUseRequests,
                world.Society.Inhabitants, world.WorldTick, item, filingHousehold, townRepresentative);

    internal static bool LandHearingTownFilingAuthority(ICivicTownWorld world, TownRuntimeState town, string actor, string? authorityId)
            => TownLandCasePartyRules.TownFilingAuthority(town, actor, authorityId, world.Society.Inhabitants, world.WorldTick);

    internal static HashSet<string> LandHearingDirectStakes(ICivicTownWorld world, TownLandCase item)
    {
        var tiles = TownLandHearingRules.CurrentRevision(item).Tiles;
        var agents = world.Society.Inhabitants.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var buildings = world.WorldSimulation.Buildings.Where(building => WorldContentSimulationRules.Footprint(
            world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building).Any(tiles.Contains)).ToArray();
        var buildingIds = buildings.Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
        var lots = world.Society.Inventory.Lots;
        var onsite = lots.Where(lot => lot.GroundPosition is { } ground && tiles.Contains(new GridPoint(ground.X, ground.Y)) ||
            lot.StorageBuildingId is { } storage && buildingIds.Contains(storage)).Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        while (lots.Any(lot => lot.ContainerLotId is { } parent && onsite.Contains(parent) && !onsite.Contains(lot.Id)))
            foreach (var lot in lots.Where(lot => lot.ContainerLotId is { } parent && onsite.Contains(parent))) onsite.Add(lot.Id);
        return item.Filings.Where(filing => filing.AgentId is not null).Select(filing => filing.AgentId!)
            .Concat(item.Property is { } property ? TownPropertyRules.RequiredConsent(property.Snapshots[^1]) : [])
            .Concat(buildings.Where(building => building.HouseholdId is not null).SelectMany(building => world.HouseholdAdults(building.HouseholdId!)))
            .Concat(world.Fields.Where(field => tiles.Contains(field.Position)).SelectMany(field => world.HouseholdAdults(field.HouseholdId)))
            .Concat(lots.Where(lot => onsite.Contains(lot.Id)).SelectMany(lot => agents.Contains(lot.OwnerId) ? new[] { lot.OwnerId } : world.HouseholdAdults(lot.OwnerId)))
            .ToHashSet(StringComparer.Ordinal);
    }

    internal bool LandHearingJudgeConflict(ICivicTownWorld world, TownRuntimeState town, TownLandCase item, string actor) =>
            TownLandHearingRules.JudgeConflict(actor, world.HouseholdFor(actor),
                LandHearingParties(world, town, TownLandHearingRules.CurrentRevision(item).Tiles, item), LandHearingDirectStakes(world, item));

    internal bool LandHearingJudgeValid(ICivicTownWorld world, TownRuntimeState town, TownLandCase item, TownLandCaseJudge judge)
    {
        if (!TownAdults(world, town).Contains(judge.AgentId, StringComparer.Ordinal) || LandHearingJudgeConflict(world, town, item, judge.AgentId) ||
            town.Government is not { } government || government.Arrangement.Land != TownArrangementRules.Mayor)
            return false;
        if (judge.Kind == "case_elected")
            return item.JudgeConsents.Any(consent => consent.AgentId == judge.AgentId && consent.WithdrawnTick is null) &&
                item.ContestHistory.Any(contest => contest.Id == judge.AuthorityId && contest.Stage == "completed" && contest.WinnerId == judge.AgentId);
        return judge.Kind == "land_mayor" && government.Offices.Any(office => office.Mandates == "land" &&
            office.HolderId == judge.AgentId && office.TermEndTick > world.WorldTick && office.ElectionId == judge.AuthorityId);
    }

    internal bool LandHearingMayInspect(ICivicTownWorld world, TownRuntimeState town, TownLandCase item, string actor) =>
            LandHearingAdult(world, actor) && (TownAdults(world, town).Contains(actor, StringComparer.Ordinal) || item.Judge?.AgentId == actor ||
                LandHearingParties(world, town, TownLandHearingRules.CurrentRevision(item).Tiles, item)
                    .Any(party => party.AdultIds.Contains(actor, StringComparer.Ordinal) || party.RepresentativeId == actor));

    internal static bool LandHearingReadCurrent(ICivicTownWorld world, TownLandCase item, string actor) => item.Reads.Any(read =>
            read.AgentId == actor && read.Revision == TownLandHearingRules.CurrentRevision(item).Number && read.ReadTick <= world.WorldTick &&
            item.Evidence.All(evidence => evidence.SubmittedTick <= read.ReadTick && read.EvidenceIds.Contains(evidence.Id, StringComparer.Ordinal)));

    internal static bool LandHearingReadRuling(ICivicTownWorld world, TownLandCase item, string actor)
    {
        if (item.Rulings.Count == 0) return false;
        var ruling = item.Rulings[^1];
        return item.Reads.Any(read => read.AgentId == actor && read.Revision == TownLandHearingRules.CurrentRevision(item).Number &&
            read.ReadTick >= ruling.Tick && read.ReadTick <= world.WorldTick && item.Evidence.Any(evidence => evidence.Kind == "record" &&
                evidence.SourceRecordId == ruling.Id && read.EvidenceIds.Contains(evidence.Id, StringComparer.Ordinal)));
    }

    internal bool MayVisitLandHearing(ICivicTownWorld world, string actor, TownRuntimeState town) => LandHearingAdult(world, actor) &&
            town.Governance is { } council && CanWalkToCivicBoard(world, actor, town) && town.LandHearings.Cases.Any(item =>
                LandHearingMayInspect(world, town, item, actor) && TownLandHearingRules.HasNoticeReceipt(
                    TownLandHearingRules.CurrentRevision(item), actor, world.WorldTick, council.Knowledge));

    internal bool HasOpenLandHearingPlot(ICivicTownWorld world, string townId, IEnumerable<GridPoint> tiles)
    {
        var plot = tiles.ToHashSet();
        return towns.SingleOrDefault(town => town.Id == townId)?.LandHearings?.Cases.Any(item =>
            item.Status == "pending" && TownLandHearingRules.CurrentRevision(item).Tiles.Any(plot.Contains)) == true;
    }

    internal static bool TownLandHearingElectionBusy(TownRuntimeState town) =>
            town.LandHearings?.Cases.Any(item => item.Contest is { Stage: "voting" }) == true;

}
