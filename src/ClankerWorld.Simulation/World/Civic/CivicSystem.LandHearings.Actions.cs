using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CivicSystem
{
    internal void AddTownLandHearingCandidates(ICivicTownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (!LandHearingAdult(world, actor) || town.Governance is not { } council) return;
        var hearings = town.LandHearings;
        var household = world.HouseholdFor(actor);
        var atBoard = NearCivicBoard(world, actor, town);
        if (!atBoard && MayVisitLandHearing(world, actor, town) && !TownAdults(world, town).Contains(actor, StringComparer.Ordinal) && !MayVisitAsNewcomer(world, actor, town))
            candidates.Add(new(CivicAction(world, town.Id, "visit"), "Walk to the public notice place to inspect a land case you actually learned concerns you. Visiting changes no membership or property access.", 175));
        if (atBoard && household is not null)
        {
            // Land a settled hearing covered, with permissions unchanged since, cannot start a new case, so it is not named.
            // Land under a pending hearing can only be filed again as that hearing's exact plot, which joins it.
            var pending = hearings.Cases.Where(item => item.Status == "pending").SelectMany(item => TownLandHearingRules.CurrentRevision(item).Tiles).ToHashSet();
            var affected = householdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household).SelectMany(right => right.Tiles)
                .Concat(householdLandUseRequests.Where(request => request.TownId == town.Id && request.HouseholdId == household && request.Status == "pending").SelectMany(TownLandRightsRules.UnresolvedRequestTiles))
                .Distinct().OrderBy(tile => world.Map.FootDistance(world.Inhabitants[actor].Position, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
                .Where(tile => pending.Contains(tile) || TownLandHearingRules.FilingRefusal(hearings, town.Id, [tile], householdLandUseRights) is null).Take(6).ToArray();
            if (affected.Length > 0)
                candidates.Add(new(CivicAction(world, town.Id, "hearing_file", choice: household),
                    "File a land disagreement affecting your household. State the disagreement and requested_outcome in civic_land_hearing, and exact connected civic_land_tiles. Filing supplies no agreement or permission. Nearby affected tiles: " +
                    TownLandClaimRules.DescribeTiles(affected) + "." + (affected.Any(pending.Contains)
                        ? " Land already under a pending hearing can only be filed again as exactly that hearing's plot, which joins it." : ""), 185));
        }
        if (atBoard && town.Government is { } government && TownAdults(world, town).Contains(actor, StringComparer.Ordinal))
        {
            if (government.Arrangement.Ordinary == TownArrangementRules.Mayor && government.Offices.Any(office =>
                    office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick))
                candidates.Add(new(CivicAction(world, town.Id, "hearing_file_town", choice: government.Offices.Single(office =>
                        office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick).ElectionId ?? ""),
                    "File a land case on the Town's behalf under your recorded governing office. Name exact civic_land_tiles and civic_land_hearing statement/requested_outcome. You represent the Town and cannot judge this case.", 185));
            else if (government.Arrangement.Ordinary is TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil &&
                     council.Members.Contains(actor, StringComparer.Ordinal))
                candidates.Add(new(CivicAction(world, town.Id, "hearing_propose_town", choice: "council:" + council.Revision),
                    "Ask the current Council to authorize a specific Town land case. Name exact civic_land_tiles and civic_land_hearing statement/requested_outcome; actual Council approval is required before the Town files.", 185));
        }
        world.AddTownPropertyCandidates(candidates, actor, town);
        foreach (var item in hearings.Cases)
        {
            var revision = TownLandHearingRules.CurrentRevision(item);
            var token = TownLandHearingRules.RevisionToken(item);
            var knows = TownLandHearingRules.HasNoticeReceipt(revision, actor, world.WorldTick, council.Knowledge);
            // A settled case with no rehearing request is closed. It asks nothing more of an adult who has read its
            // ruling, so closed cases neither lengthen every prompt nor keep adding reads and statements to the save.
            var open = item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending");
            if (!open && !atBoard && !knows) continue;
            var parties = LandHearingParties(world, town, revision.Tiles, item);
            var isParty = parties.Any(party => party.AdultIds.Contains(actor, StringComparer.Ordinal) || party.RepresentativeId == actor);
            // A changed right, title or law on the plot is new record evidence, so its file is offered again.
            if (atBoard && LandHearingMayInspect(world, town, item, actor) && (!LandHearingReadCurrent(world, item, actor) ||
                    item.Status == "settled" && (!LandHearingReadRuling(world, item, actor) || item.ReopenRequests.Any(request => request.Status == "pending" &&
                        !item.Reads.Any(read => read.AgentId == actor && read.Revision == revision.Number &&
                            read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))) || LandHearingUnreadRecords(world, town, item).Length > 0)))
                candidates.Add(new(CivicAction(world, town.Id, "hearing_inspect", token),
                    "Read the actual public land-case file and formal records for " + TownLandClaimRules.DescribeTiles(revision.Tiles) +
                    ". Reading supplies evidence awareness and grants no private-building access.", 177));
            if (!knows) continue;
            if (item.Status == "pending" && item.Property is { Transfer: null } property && LandHearingReadCurrent(world, item, actor) &&
                TownPropertyRules.RequiredConsent(property.Snapshots[^1]).Contains(actor, StringComparer.Ordinal) &&
                parties.Any(party => party.Kind == "household" && party.AdultIds.Contains(actor, StringComparer.Ordinal)))
                foreach (var agreement in new[] { "accept", "refuse" })
                    if (property.Consents.LastOrDefault(consent => consent.Revision == revision.Number && consent.AgentId == actor) is not { } last ||
                        last.Agreed != (agreement == "accept"))
                        candidates.Add(new(CivicAction(world, town.Id, "hearing_property_" + agreement, token),
                        "Personally " + (agreement == "accept" ? "agree to" : "refuse") + " the exact noticed transfer of " +
                        property.Request.BuildingId + " and its shared goods " +
                        (property.Request.TargetHouseholdId is { } recipient ? "to household " + recipient : "to the Town") +
                        ". This records only your own property consent; other living owners must choose for themselves.", 174));
            if (open && (isParty || TownAdults(world, town).Contains(actor, StringComparer.Ordinal)))
                candidates.Add(new(CivicAction(world, town.Id, "hearing_statement", token),
                    "Submit your own statement to the current land-case file via civic_land_hearing.statement. It remains an allegation with your identity, not a verified fact.", 182));
            // A party may still record a new physical fact on a settled plot: that is how material new evidence arises.
            // Seeing the same thing again adds nothing.
            if ((open && TownAdults(world, town).Contains(actor, StringComparer.Ordinal) || isParty) &&
                revision.Tiles.Any(tile => world.IsWithinInteractionRange(world.Inhabitants[actor].Position, tile, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
                candidates.Add(new(CivicAction(world, town.Id, "hearing_observe", token),
                    "Record what you can physically see on the nearby disputed plot. This supplies no unseen events, building access or permission.", 181));
            foreach (var party in parties.Where(party => item.Status == "pending" &&
                         (party.AdultIds.Contains(actor, StringComparer.Ordinal) || party.RepresentativeId == actor)))
            {
                candidates.Add(new(CivicAction(world, town.Id, "hearing_answer", token, party.Id),
                    "Personally answer for " + party.Kind + " " + (party.HouseholdId ?? party.TownId) + " in this hearing via civic_land_hearing.statement. Your answer supplies neither another party's response nor permission to surrender rights.", 165));
                candidates.Add(new(CivicAction(world, town.Id, "hearing_waive", token, party.Id),
                    "Explicitly waive your remaining response opportunity for " + party.Kind + " " + (party.HouseholdId ?? party.TownId) + ". This does not agree to a loss or transfer, or waive another party's answer.", 166));
            }
            if (TownAdults(world, town).Contains(actor, StringComparer.Ordinal) && (item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            {
                var willing = item.JudgeConsents.Any(consent => consent.AgentId == actor && consent.WithdrawnTick is null);
                if (!willing && !LandHearingJudgeConflict(world, town, item, actor))
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_judge_register", token), "Personally agree to stand as an independent adjudicator for this case only; no general Town office is granted.", 180));
                if (willing)
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_judge_withdraw", token), "Withdraw your willingness to hold this case mandate. The case file and completed response period remain.", 185));
                if (item.Judge is { Kind: "case_elected" } judge && judge.AgentId == actor)
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_judge_resign", token), "Resign your mandate to adjudicate this case only.", 185));
            }
            if (item.Contest is { Stage: "voting" } contest && contest.Voters.Contains(actor, StringComparer.Ordinal) &&
                council.Knowledge.Any(receipt => receipt.AgentId == actor && council.Notices.Any(notice => notice.Id == receipt.NoticeId &&
                    notice.Kind == "land_hearing" && notice.SubjectId == TownLandCaseJudgeRules.RoundToken(contest))))
                foreach (var candidate in contest.Candidates)
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_judge_vote", token + "#" + TownLandCaseJudgeRules.RoundToken(contest), candidate),
                        "Submit or revise your one-person ballot for " + world.Society.GetInhabitant(candidate).Name +
                        " as judge of this case only. Self-voting is allowed; general offices are unchanged.", 167));
            if (item.Judge is { } adjudicator && adjudicator.AgentId == actor && LandHearingJudgeValid(world, town, item, adjudicator) && LandHearingReadCurrent(world, item, actor))
            {
                if (item.Status == "pending" && TownLandHearingRules.CanCloseResponses(item, parties, world.WorldTick))
                {
                    // A ruling that would leave a lapsed permission as it is, or end one for an unrepresented household, is not offered.
                    var (kinds, lapsed) = item.Property is null ? TownLandHearingRules.RulingChoices(householdLandUseRights, revision.Tiles, parties, world.WorldTick) :
                        (world.PropertyCanTransfer(town, item) ? new[] { TownPropertyRules.Outcome(item.Property.Request).Kind, "reject" } : new[] { "reject" }, Array.Empty<string>());
                    foreach (var kind in kinds)
                        candidates.Add(new(CivicAction(world, town.Id, "hearing_rule", token, kind),
                            "Personally rule to " + kind + (item.Property is null ? " only the recorded plot permission." :
                                " the exact noticed property transfer; accepting transfers the building, recorded shared goods and its footprint permission together.") +
                            " Supply civic_land_hearing.statement reasons, actual evidence_ids and law_ids" +
                            (kind == "end" ? ", and household_id from affected households" : kind is "renew" or "amend" ? ", household_id from affected households and any agreed_end_tick" : "") + ". " +
                            (lapsed.Length == 0 ? "" : "The permission of household " + string.Join(", ", lapsed) +
                                " on this plot is past its agreed end, so the ruling must renew, amend or end it. Renewing also renews every lapsed permission on this plot for its own household. ") +
                            LandHearingCaseChoices(world, town, item, actor), 165));
                }
                // Grounds are assessed only on a settled case; a rehearing already under way decides before any other request.
                foreach (var request in item.ReopenRequests.Where(request => item.Status == "settled" && request.Status == "pending" &&
                             item.Reads.Any(read => read.AgentId == actor && read.Revision == revision.Number &&
                                 read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))))
                {
                    var established = request.Kind == "material_evidence" ? TownLandHearingRules.MaterialNewEvidence(item, request, hearings) : TownLandHearingRules.DemonstratedProceduralError(item, request);
                    if (established && TownLandHearingRules.ReopeningPlotIsAvailable(hearings, item) && world.PropertyMayReopen(town, item))
                        candidates.Add(new(CivicAction(world, town.Id, "hearing_assess_reopen", token, request.Id + ":accept"), "Accept independently established grounds and open a fresh hearing; current rights remain until a valid correction. Give reasons in civic_land_hearing.statement. Filed " + request.Kind + " claim: " + request.Reasons, 165));
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_assess_reopen", token, request.Id + ":reject"), "Reject this reopening request with reasons in civic_land_hearing.statement; keep the old ruling and request in the case history. Filed " + request.Kind + " claim: " + request.Reasons, 166));
                }
            }
            if (item.Status == "settled" && isParty && LandHearingReadCurrent(world, item, actor) && LandHearingReadRuling(world, item, actor))
                foreach (var grounds in new[] { "material_evidence", "procedural_error" })
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_reopen", token, grounds),
                        "Request reopening for " + grounds.Replace('_', ' ') + "; identify actual inspected evidence_ids and state grounds in civic_land_hearing.grounds. A claim alone cannot reopen the case. " + LandHearingCaseChoices(world, town, item, actor), 184));
            foreach (var recipient in world.Inhabitants.Values.Where(person => open && person.InhabitantId != actor &&
                         world.IsWithinInteractionRange(person.Position, world.Inhabitants[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
                if (LandHearingReadCurrent(world, item, actor) && LandHearingMayInspect(world, town, item, recipient.InhabitantId) && !LandHearingReadCurrent(world, item, recipient.InhabitantId))
                    candidates.Add(new(CivicAction(world, town.Id, "hearing_relay", token, recipient.InhabitantId),
                        "Tell " + world.Society.GetInhabitant(recipient.InhabitantId).Name + " the case material you actually read; newly unseen file contents are excluded.", 181));
        }
    }

    internal string LandHearingCaseChoices(ICivicTownWorld world, TownRuntimeState town, TownLandCase item, string actor)
    {
        var revision = TownLandHearingRules.CurrentRevision(item);
        var known = item.Reads.Where(read => read.AgentId == actor && read.Revision == revision.Number && read.ReadTick <= world.WorldTick)
            .SelectMany(read => read.EvidenceIds).ToHashSet(StringComparer.Ordinal);
        var propertyRecord = item.Property is null ? [] : item.Evidence.Where(evidence => known.Contains(evidence.Id) &&
            evidence.SourceRecordId == TownPropertyRules.RecordId(item, revision.Number)).Take(1).ToArray();
        var evidence = item.Evidence.Where(evidence => known.Contains(evidence.Id)).TakeLast(4).Concat(propertyRecord).DistinctBy(evidence => evidence.Id).Select(evidence =>
            evidence.Id + "=" + evidence.Kind + " from " + evidence.SourceAgentId + ": " + NonviolentExcerpt(evidence.Text, 160));
        return "Requested result: " + revision.RequestedOutcome.Kind + " " + revision.RequestedOutcome.HouseholdId +
            ". Affected households: " + string.Join(", ", LandHearingParties(world, town, revision.Tiles, item)
                .Where(party => party.HouseholdId is not null).Select(party => party.HouseholdId)) +
            "; inspected evidence: " + string.Join("; ", evidence) +
            "; inspected law versions: " + string.Join(", ", LandHearingApplicableLaws(world, town, item)
                .Where(pair => item.Evidence.Any(e => known.Contains(e.Id) && e.SourceRecordId == LandHearingLawToken(pair.Law, pair.Version)))
                .Select(pair => LandHearingLawToken(pair.Law, pair.Version)).Take(16)) + ".";
    }

    internal (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandHearingAction(ICivicTownWorld world, TownRuntimeState town,
            string actor, string action, string subject, string choice, string? proposalText, IReadOnlyList<CognitionLandTile>? landTiles,
            CognitionLandHearingChoice? hearingChoice, TownGovernanceState council, TownGovernmentState government)
    {
        var currentTown = town with { Governance = council, Government = government };
        (council, var hearings) = AdvanceTownLandHearings(world, currentTown, council, government);
        currentTown = currentTown with { Governance = council, LandHearings = hearings };
        var candidates = new List<CognitionCandidate>();
        AddTownLandHearingCandidates(world, candidates, actor, currentTown);
        if (!candidates.Any(candidate => candidate.Id == $"civic|{town.Id}|{action}|{subject}|{choice}"))
            throw new InvalidOperationException("The hearing choice no longer has current authority or informed eligibility.");
        if (action == "hearing_property_request")
            return world.RequestPropertyCase(currentTown, actor, subject, choice, LandHearingText(hearingChoice?.Statement ?? proposalText), council, government);
        if (action is "hearing_file" or "hearing_file_town" or "hearing_propose_town")
        {
            var tiles = TownLandRightsRules.OrderTiles(landTiles?.Select(tile => new GridPoint(tile.X, tile.Y)) ?? []);
            var outcome = LandHearingOutcome(world, hearingChoice?.RequestedOutcome ?? "", hearingChoice);
            var text = LandHearingText(hearingChoice?.Statement ?? proposalText);
            if (tiles.Length == 0 || !TownLandRightsRules.IsValidPlot(world.Map, tiles, world.WorldTick, world.WorldTick) ||
                tiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, townLandTitles)))
                throw new InvalidOperationException("A filing must name a connected plot under this Town's title.");
            if (action == "hearing_propose_town")
            {
                // A plot the Town could not file on now is refused before the Council spends a vote on it.
                if (TownLandHearingRules.FilingRefusal(hearings, town.Id, tiles, householdLandUseRights) is { } refusal)
                    throw new InvalidOperationException(refusal);
                var filing = new TownLandFilingRequest(tiles, outcome, text);
                council = TownGovernanceRules.SubmitProposal(council, town.Id, actor, "land_hearing", null,
                    "Authorize this Town land case.", "council:" + council.Revision, TownAdults(world, town), world.WorldTick, CivicDay(world),
                    noticeText: TownLandGovernmentFilingRules.Describe(filing), landHearingRequest: filing);
                return (council, hearings);
            }
            var household = action == "hearing_file" ? world.HouseholdFor(actor) : null;
            if (household is not null && !LandHearingParties(world, currentTown, tiles).Any(party => party.HouseholdId == household))
                throw new InvalidOperationException("A household filer must actually be affected by the named plot.");
            var office = action == "hearing_file_town" ? government.Offices.Single(office => office.Mandates == "ordinary" && office.HolderId == actor) : null;
            return OpenLandHearing(world, currentTown, council, hearings, new(actor, office is null ? "dispute" : "town", text, outcome,
                world.WorldTick, office?.ElectionId), tiles, household, office is null ? null : actor);
        }
        var item = hearings.Cases.Single(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == subject ||
            item.Contest is { } contest && CivicAgentToken(TownLandHearingRules.RevisionToken(item) + "#" + TownLandCaseJudgeRules.RoundToken(contest)) == subject);
        var revision = TownLandHearingRules.CurrentRevision(item);
        var parties = LandHearingParties(world, currentTown, revision.Tiles, item);
        switch (action)
        {
            case "hearing_inspect":
                council = TownGovernanceRules.LearnNotice(council, actor, revision.NoticeId, world.WorldTick);
                hearings = LandHearingInspectRecords(world, currentTown, hearings, item.Id, actor, council);
                break;
            case "hearing_observe":
                var observed = LandHearingObserve(world, hearings, item, actor, council);
                // Seeing the same thing again adds no evidence, so it is not logged as new evidence either.
                if (ReferenceEquals(observed, hearings)) return (council, hearings);
                hearings = observed;
                break;
            case "hearing_statement":
                var statement = LandHearingText(hearingChoice?.Statement ?? proposalText);
                hearings = TownLandHearingRules.AddEvidence(hearings, item.Id, revision.Number,
                    new(LandHearingEvidenceId(item, actor, "allegation", statement), revision.Number, "allegation", "statement",
                        actor, null, null, world.WorldTick, actor, world.WorldTick, statement), council.Knowledge);
                break;
            case "hearing_answer":
            case "hearing_waive":
                hearings = TownLandHearingRules.Respond(hearings, item.Id, revision.Number, actor,
                    action == "hearing_answer" ? "answer" : "waive", action == "hearing_waive" ? "I waive only my own response opportunity." : LandHearingText(hearingChoice?.Statement ?? proposalText),
                    world.WorldTick, council.Knowledge, parties, partyId: parties.Single(party => CivicAgentToken(party.Id) == choice).Id);
                break;
            case "hearing_property_accept":
            case "hearing_property_refuse":
                hearings = TownPropertyRules.Consent(hearings, item.Id, actor, action == "hearing_property_accept", world.WorldTick, council.Knowledge, parties);
                break;
            case "hearing_judge_register": hearings = TownLandCaseJudgeRules.Register(hearings, item.Id, actor, TownAdults(world, town), LandHearingHouseholds(world), world.WorldTick, parties); break;
            case "hearing_judge_withdraw": hearings = TownLandCaseJudgeRules.Withdraw(hearings, item.Id, actor, world.WorldTick); break;
            case "hearing_judge_resign": hearings = TownLandCaseJudgeRules.Resign(hearings, item.Id, actor, world.WorldTick); break;
            case "hearing_judge_vote":
                hearings = TownLandCaseJudgeRules.Vote(hearings, item.Id, TownLandCaseJudgeRules.RoundToken(item.Contest!), actor, ResolveCivicAgentToken(world, choice), world.WorldTick);
                break;
            case "hearing_rule":
                var rulingOutcome = item.Property is not null && choice != "reject" ? TownPropertyRules.Outcome(item.Property.Request) : LandHearingOutcome(world, choice, hearingChoice);
                TownPropertySnapshot? priorProperty = null;
                if (item.Property is not null && choice != "reject")
                {
                    if (rulingOutcome.Kind != choice || hearingChoice?.RequestedOutcome is { } requested && requested != choice ||
                        hearingChoice?.HouseholdId is { } supplied && supplied != rulingOutcome.HouseholdId ||
                        hearingChoice?.AgreedEndTick is not null || !world.PropertyCanTransfer(currentTown, item))
                        throw new InvalidOperationException("The property or a living owner's consent changed before this ruling.");
                    priorProperty = world.CaptureProperty(currentTown, item.Property.Request, revision.Number);
                }
                if (rulingOutcome.HouseholdId is { } target && !parties.Any(party => party.HouseholdId == target))
                    throw new InvalidOperationException("A ruling must concern an actual affected household.");
                var evidenceIds = hearingChoice?.EvidenceIds ?? [];
                var lawIds = hearingChoice?.LawIds ?? [];
                var applicable = LandHearingApplicableLaws(world, currentTown, item).Select(pair => LandHearingLawToken(pair.Law, pair.Version)).ToHashSet(StringComparer.Ordinal);
                if (lawIds.Any(id => !applicable.Contains(id) || !item.Evidence.Any(evidence => evidence.SourceRecordId == id && evidence.Kind == "record")))
                    throw new InvalidOperationException("A ruling can cite only an applicable law version actually inspected in this file.");
                (hearings, var rights) = TownLandHearingRules.Rule(hearings, world.Map, item.Id, revision.Number, item.Judge!, world.WorldTick,
                    rulingOutcome, evidenceIds, lawIds, LandHearingText(hearingChoice?.Statement ?? proposalText), householdLandUseRights, parties,
                    item.Judge is { } judge && LandHearingJudgeValid(world, currentTown, item, judge), householdLandUseRequests);
                householdLandUseRights = rights.ToList();
                if (priorProperty is not null) hearings = world.TransferCaseProperty(currentTown, hearings, item.Id, priorProperty);
                var settled = hearings.Cases.Single(c => c.Id == item.Id);
                foreach (var resolution in TownLandHearingRules.RequestResolutions(hearings, item.Id, settled.Rulings[^1].Id, householdLandUseRequests))
                {
                    var resolvedRequest = householdLandUseRequests.Single(candidate => candidate.Id == resolution.RequestId);
                    var resolutions = resolvedRequest.HearingResolutions.Append(resolution).ToArray();
                    var covered = resolvedRequest.Tiles.All(tile => resolutions.Any(receipt => receipt.Tiles.Contains(tile)));
                    resolvedRequest = resolvedRequest with
                    {
                        HearingResolutions = resolutions,
                        Status = covered ? "hearing_resolved" : "pending",
                        SettledTick = covered ? world.WorldTick : null
                    };
                    householdLandUseRequests = householdLandUseRequests.Select(current => current.Id == resolvedRequest.Id ? resolvedRequest : current).ToList();
                    if (resolvedRequest.CouncilProposalId is { } proposalId)
                        council = TownGovernanceRules.CancelLandUseProposal(council, proposalId, world.WorldTick);
                }
                council = TownGovernanceRules.PostNotice(council, "result", settled.Rulings[^1].Id,
                    item.Property is not null ? priorProperty is null ? "Property hearing rejected; ownership remains unchanged." :
                        "Property hearing decided: " + priorProperty.Building.InstanceId + " and its recorded shared goods now belong to " +
                        (item.Property.Request.TargetHouseholdId is { } receiver ? "household " + receiver : "the Town") + ". Personal goods retain their owners." :
                        "Land hearing decided: " + rulingOutcome.Kind + ". Only recorded use permissions on " +
                        TownLandClaimRules.DescribeTiles(revision.Tiles) + " were considered; buildings, crops and goods retain their owners.", world.WorldTick);
                break;
            case "hearing_reopen":
                hearings = TownLandHearingRules.RequestReopen(hearings, item.Id, actor, choice, hearingChoice?.EvidenceIds ?? [],
                    LandHearingText(hearingChoice?.Grounds ?? hearingChoice?.Statement), world.WorldTick);
                break;
            case "hearing_assess_reopen":
                var request = item.ReopenRequests.Single(request => request.Status == "pending" &&
                    (CivicAgentToken(request.Id + ":accept") == choice || CivicAgentToken(request.Id + ":reject") == choice));
                var accept = CivicAgentToken(request.Id + ":accept") == choice;
                var reopenedProperty = accept && item.Property is { Transfer: null } pendingProperty
                    ? world.CaptureProperty(currentTown, pendingProperty.Request, revision.Number + 1) : null;
                if (reopenedProperty is not null)
                    parties = TownPropertyRules.Parties(parties, item.Property! with
                    { Snapshots = item.Property.Snapshots.Append(reopenedProperty).ToArray() }, town.Id, world.Society.Inhabitants);
                hearings = TownLandHearingRules.Reopen(hearings, item.Id, request.Id, item.Judge!, accept,
                    LandHearingText(hearingChoice?.Statement), householdLandUseRights, parties, world.WorldTick, CivicDay(world),
                    "notice:" + (council.Notices.Count + 1), item.Judge is { } reopeningJudge && LandHearingJudgeValid(world, currentTown, item, reopeningJudge));
                if (accept)
                {
                    var reopened = hearings.Cases.Single(c => c.Id == item.Id);
                    if (reopenedProperty is not null)
                    {
                        reopened = reopened with { Property = reopened.Property! with { Snapshots = reopened.Property.Snapshots.Append(reopenedProperty).ToArray() } };
                        hearings = TownPropertyRules.Replace(hearings, reopened);
                    }
                    council = PostLandHearingNotice(council, reopened);
                }
                break;
            case "hearing_relay":
                var recipient = ResolveCivicAgentToken(world, choice);
                council = TownGovernanceRules.LearnNotice(council, recipient, revision.NoticeId, world.WorldTick, actor);
                hearings = TownLandHearingRules.RelayRead(hearings, item.Id, revision.Number, actor, recipient, world.WorldTick);
                break;
            default: throw new InvalidOperationException("Unsupported land-hearing action.");
        }
        var eventKind = action switch
        {
            "hearing_rule" => "ruling",
            "hearing_reopen" => "reopen_requested",
            "hearing_assess_reopen" => hearings.Cases.Single(c => c.Id == item.Id).Status == "pending" ? "reopened" : "rejected",
            "hearing_answer" or "hearing_waive" or "hearing_property_accept" or "hearing_property_refuse" => "response",
            "hearing_judge_register" or "hearing_judge_withdraw" or "hearing_judge_resign" => "judge_consent",
            "hearing_judge_vote" => "judge_election",
            "hearing_inspect" => "inspected",
            "hearing_relay" => "relayed",
            _ => "evidence",
        };
        LandHearingEvent(world, eventKind, town, hearings.Cases.Single(c => c.Id == item.Id), actor,
            action == "hearing_rule" ? choice : action["hearing_".Length..]);
        return (council, hearings);
    }

    internal static TownLandRequestedOutcome LandHearingOutcome(ICivicTownWorld world, string kind, CognitionLandHearingChoice? choice)
    {
        if (choice?.RequestedOutcome is { } requested && requested != kind)
            throw new InvalidOperationException("The requested outcome contradicts the chosen bounded action.");
        var outcome = new TownLandRequestedOutcome(kind, choice?.HouseholdId, choice?.AgreedEndTick);
        if (!TownLandHearingRules.IsValidOutcome(outcome, world.WorldTick) || outcome.HouseholdId is { } household &&
            !world.Society.Households.Any(item => item.Id == household))
            throw new InvalidOperationException("The land outcome is malformed or names an unknown household.");
        return outcome;
    }

    internal static string LandHearingText(string? text) => TownLandHearingRules.ValidText(text, 256) ? text!.Trim() :
            throw new InvalidOperationException("This hearing action requires a bounded personal statement.");

}
