namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal (TownCouncilState Council, TownLandRecords LandRecords) AdvanceTownLandHearings(
    ITownWorld world, TownRuntimeState town, TownCouncilState council, TownGovernmentState government)
    {
        var hearings = town.LandRecords ?? TownLandRecords.Create();
        foreach (var right in householdLandUseRights.Where(right => right.TownId == town.Id && right.AgreedEndTick <= world.WorldTick).ToArray())
        {
            if (hearings.Cases.Any(item => item.Filings.Any(filing => filing.Kind == "expiry" && filing.AuthorityId == right.Id))) continue;
            // A different pending case over these tiles must rule on the lapsed permission first; only a
            // ruling made at or after the agreed end, with the terms unchanged since, has already reviewed it.
            if (TownLandHearingRules.FilingRefusal(hearings, town.Id, right.Tiles, householdLandUseRights, right.Id) is not null) continue;
            var filing = new TownLandCaseFiling(null, "expiry", "Review a use right that reached its agreed end; it stays in place until the ruling.",
                new("confirm"), world.WorldTick, right.Id);
            (council, hearings) = OpenLandHearing(world, town, council, hearings, filing, right.Tiles);
        }
        foreach (var proposal in council.Proposals.Where(proposal => proposal.Kind == "land_hearing" && proposal.Status == "passed"))
        {
            var unopened = TownRecordIds.UnopenedHearing(proposal.Id);
            if (proposal.LandHearingRequest is not { } request ||
                hearings.Cases.Any(item => item.Filings.Any(filing => filing.AuthorityId == proposal.Id)) ||
                council.Notices.Any(notice => notice.Kind == "result" && notice.SubjectId == unopened) ||
                !LandHearingTownFilingAuthority(world, town with { Council = council, Government = government }, proposal.AuthorId, proposal.Id)) continue;
            var filing = new TownLandCaseFiling(proposal.AuthorId, "town", request.Statement, request.RequestedOutcome, world.WorldTick, proposal.Id);
            // The Council's vote stands when its filing can no longer open: this runs inside the deciding
            // vote, so a refusal is published once as a result instead of undoing that vote.
            var refusal = TownLandHearingRules.IsValidOutcome(request.RequestedOutcome, world.WorldTick) ? null : "Its requested end date has passed.";
            try
            {
                if (refusal is null)
                    (council, hearings) = OpenLandHearing(world, town, council, hearings, filing, request.Tiles, townRepresentative: proposal.AuthorId,
                        propertyRequest: request.Property);
            }
            catch (InvalidOperationException exception) { refusal = exception.Message; }
            if (refusal is not null)
                council = TownCouncilRules.PostNotice(council, "result", unopened,
                    "The Town land hearing that the Council authorized could not open. " + refusal, world.WorldTick);
        }
        foreach (var item in hearings.Cases.Where(item => item.Status == "pending").ToArray())
        {
            var revision = TownLandHearingRules.CurrentRevision(item);
            var noticeId = TownCouncilRules.NextNoticeId(council);
            TownPropertySnapshot? snapshot = null;
            var partiesItem = item;
            if (item.Property is { Transfer: null } property)
            {
                try
                {
                    snapshot = world.CaptureProperty(town, property.Request, revision.Number + 1);
                    partiesItem = item with { Property = property with { Snapshots = property.Snapshots.Append(snapshot).ToArray() } };
                }
                catch (InvalidOperationException)
                {
                    // Keep the last valid notice when its asset can no longer be captured.
                    // Current parties and conflicts still govern the judge below; a missing
                    // asset must never produce a notice without its matching property file.
                    continue;
                }
            }
            var revised = TownLandHearingRules.Revise(hearings, item.Id, householdLandUseRights,
                LandHearingParties(world, town with { Council = council, Government = government }, revision.Tiles, partiesItem),
                revision.Tiles, world.WorldTick, CivicDay(world), noticeId, assetsChanged: snapshot is not null &&
                    TownPropertyRules.MaterialVersion(snapshot) != TownPropertyRules.MaterialVersion(item.Property!.Snapshots[^1]));
            if (TownLandHearingRules.CurrentRevision(revised.Cases.Single(c => c.Id == item.Id)).Number != revision.Number)
            {
                hearings = revised;
                var current = hearings.Cases.Single(c => c.Id == item.Id);
                if (snapshot is not null)
                {
                    current = current with { Property = current.Property! with { Snapshots = current.Property.Snapshots.Append(snapshot).ToArray() } };
                    hearings = TownPropertyRules.Replace(hearings, current);
                }
                council = PostLandHearingNotice(council, current);
                LandHearingEvent(world, "notice", town, current, null, "revised");
            }
        }
        return AdvanceLandHearingJudges(world, town with { Council = council, Government = government, LandRecords = hearings }, council, government, hearings);
    }

    internal (TownCouncilState Council, TownLandRecords LandRecords) OpenLandHearing(ITownWorld world, TownRuntimeState town,
            TownCouncilState council, TownLandRecords hearings, TownLandCaseFiling filing, IReadOnlyList<GridPoint> tiles,
            string? filingHousehold = null, string? townRepresentative = null, TownPropertyRequest? propertyRequest = null)
    {
        if (!TownLandRightsRules.IsValidPlot(world.Map, tiles, world.WorldTick, world.WorldTick) ||
            tiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, townLandTitles)))
            throw new InvalidOperationException("A land case needs one connected plot under this Town's existing title.");
        var before = hearings.Cases.Count;
        var property = propertyRequest is null ? null : new TownPropertyCase(propertyRequest, [world.CaptureProperty(town, propertyRequest, 1)], []);
        var parties = LandHearingParties(world, town, tiles, filingHousehold: filingHousehold, townRepresentative: townRepresentative);
        if (property is not null) parties = TownPropertyRules.Parties(parties, property, town.Id, world.Society.Agents);
        hearings = TownLandHearingRules.File(hearings, town.Id, filing, tiles, householdLandUseRights,
            parties, world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council), property);
        var item = hearings.Cases.Single(item => item.Status == "pending" && item.Filings.Contains(filing) &&
            TownLandHearingRules.CurrentRevision(item).Tiles.SequenceEqual(tiles));
        if (hearings.Cases.Count > before)
        {
            council = PostLandHearingNotice(council, item);
            LandHearingEvent(world, "opened", town, item, filing.AgentId, filing.Kind);
        }
        return (council, hearings);
    }

    internal static TownCouncilState PostLandHearingNotice(TownCouncilState council, TownLandCase item)
    {
        var revision = TownLandHearingRules.CurrentRevision(item);
        return TownCouncilRules.PostNotice(council, "land_hearing", TownLandHearingRules.RevisionToken(item),
            "Land hearing opened for " + TownLandClaimRules.DescribeTiles(revision.Tiles) +
            (item.Property is { } property ? ". Property requested: " + property.Request.BuildingId +
                " and its recorded shared goods. Living former members keep ownership unless each personally agrees. Waiving a response gives no property consent." : "") +
            ". Affected adults may answer or explicitly waive their own response. Existing permissions and property remain while the case is pending.", revision.PublishedTick);
    }

    internal static void LandHearingEvent(ITownWorld world, string kind, TownRuntimeState town, TownLandCase item, string? actor, string status) =>
            world.AppendEvent("land_case_" + kind,
                $"{town.Id}|{item.Id}|{TownLandHearingRules.CurrentRevision(item).Number}|{actor ?? ""}|{status}", CivicBoard(world, town));

}
