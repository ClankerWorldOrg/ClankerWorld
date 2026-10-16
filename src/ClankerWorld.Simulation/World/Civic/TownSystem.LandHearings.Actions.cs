using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal void AddTownLandHearingCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (!LandHearingAdult(world, actor) || town.Council is not { } council) return;
        var hearings = town.LandRecords;
        var atBoard = NearCivicBoard(world, actor, town);
        if (!atBoard && MayVisitLandHearing(world, actor, town) && !TownAdults(world, town).Contains(actor, StringComparer.Ordinal) && !MayVisitAsNewcomer(world, actor, town))
            candidates.Add(new(CivicAction(world, town.Id, "visit"), "Walk to the public notice place to inspect a land case you actually learned concerns you. Visiting changes no membership or property access.", 175));
        if (atBoard)
        {
            if (PlanLandHearingFiling(world, actor, new(town.Id, LandHearingFilingKind.Household, ""), bindAuthority: true) is { } householdFiling &&
                CheckLandHearingFiling(world, householdFiling).Allowed)
                candidates.Add(new(householdFiling.CandidateId,
                    "File a land disagreement affecting your household. State the disagreement and requested_outcome in civic_land_hearing, and exact connected civic_land_tiles. Filing supplies no agreement or permission. Nearby affected tiles: " +
                    TownLandClaimRules.DescribeTiles(householdFiling.NearbyAffectedTiles) + "." + (householdFiling.NearbyPending
                        ? " Land already under a pending hearing can only be filed again as exactly that hearing's plot, which joins it." : ""), 185)
                { ActionPlan = householdFiling });
            if (PlanLandHearingFiling(world, actor, new(town.Id, LandHearingFilingKind.TownOffice, ""), bindAuthority: true) is { } officeFiling &&
                CheckLandHearingFiling(world, officeFiling).Allowed)
                candidates.Add(new(officeFiling.CandidateId,
                    "File a land case on the Town's behalf under your recorded governing office. Name exact civic_land_tiles and civic_land_hearing statement/requested_outcome. You represent the Town and cannot judge this case.", 185)
                { ActionPlan = officeFiling });
            if (PlanLandHearingFiling(world, actor, new(town.Id, LandHearingFilingKind.CouncilProposal, ""), bindAuthority: true) is { } councilFiling &&
                CheckLandHearingFiling(world, councilFiling).Allowed)
                candidates.Add(new(councilFiling.CandidateId,
                    "Ask the current Council to authorize a specific Town land case. Name exact civic_land_tiles and civic_land_hearing statement/requested_outcome; actual Council approval is required before the Town files.", 185)
                { ActionPlan = councilFiling });
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
            if (atBoard)
                OfferLandHearingEvidence(world, candidates, actor, town, item, LandHearingEvidenceKind.Inspect, "",
                    "Read the actual public land-case file and formal records for " + TownLandClaimRules.DescribeTiles(revision.Tiles) +
                    ". Reading supplies evidence awareness and grants no private-building access.", 177);
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
            OfferLandHearingEvidence(world, candidates, actor, town, item, LandHearingEvidenceKind.Statement, "",
                "Submit your own statement to the current land-case file via civic_land_hearing.statement. It remains an allegation with your identity, not a verified fact.", 182);
            if (revision.Tiles.Any(tile => world.IsWithinInteractionRange(world.Agents[actor].Position, tile, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
                OfferLandHearingEvidence(world, candidates, actor, town, item, LandHearingEvidenceKind.Observe, "",
                    "Record what you can physically see on the nearby disputed plot. This supplies no unseen events, building access or permission.", 181);
            foreach (var party in parties)
            {
                OfferLandHearingEvidence(world, candidates, actor, town, item, LandHearingEvidenceKind.Answer, party.Id,
                    "Personally answer for " + party.Kind + " " + (party.HouseholdId ?? party.TownId) + " in this hearing via civic_land_hearing.statement. Your answer supplies neither another party's response nor permission to surrender rights.", 165);
                OfferLandHearingEvidence(world, candidates, actor, town, item, LandHearingEvidenceKind.Waive, party.Id,
                    "Explicitly waive your remaining response opportunity for " + party.Kind + " " + (party.HouseholdId ?? party.TownId) + ". This does not agree to a loss or transfer, or waive another party's answer.", 166);
            }
            if (TownAdults(world, town).Contains(actor, StringComparer.Ordinal) &&
                (item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            {
                OfferLandHearingJudge(world, candidates, actor, town, item, LandHearingJudgeKind.Register, "",
                    "Personally agree to stand as an independent adjudicator for this case only; no general Town office is granted.", 180);
                OfferLandHearingJudge(world, candidates, actor, town, item, LandHearingJudgeKind.Withdraw, "",
                    "Withdraw your willingness to hold this case mandate. The case file and completed response period remain.", 185);
                OfferLandHearingJudge(world, candidates, actor, town, item, LandHearingJudgeKind.Resign, "",
                    "Resign your mandate to adjudicate this case only.", 185);
            }
            if (item.Contest is { } contest)
                foreach (var candidate in contest.Candidates)
                    OfferLandHearingJudge(world, candidates, actor, town, item, LandHearingJudgeKind.Vote, candidate,
                        "Submit or revise your one-person ballot for " + world.Society.GetAgent(candidate).Name +
                        " as judge of this case only. Self-voting is allowed; general offices are unchanged.", 167);
            if (item.Judge is { } adjudicator && adjudicator.AgentId == actor && LandHearingJudgeValid(world, town, item, adjudicator) && LandHearingReadCurrent(world, item, actor))
            {
                if (item.Status == "pending" && TownLandHearingRules.CanCloseResponses(item, parties, world.WorldTick))
                {
                    var (kinds, lapsed) = item.Property is null ? TownLandHearingRules.RulingChoices(householdLandUseRights, revision.Tiles, parties, world.WorldTick) :
                        (world.PropertyCanTransfer(town, item) ? new[] { TownPropertyRules.Outcome(item.Property.Request).Kind, "reject" } : new[] { "reject" }, Array.Empty<string>());
                    foreach (var kind in kinds)
                    {
                        var id = CivicAction(world, town.Id, "hearing_rule", token, kind);
                        if (LandHearingRulingChoice.Decode(id) is { } rulingChoice && PlanLandHearingRuling(world, actor, rulingChoice) is { } ruling &&
                            CheckLandHearingRuling(world, ruling).Allowed)
                            candidates.Add(new(id,
                                "Personally rule to " + kind + (item.Property is null ? " only the recorded plot permission." :
                                    " the exact noticed property transfer; accepting transfers the building, recorded shared goods and its footprint permission together.") +
                                " Supply civic_land_hearing.statement reasons, actual evidence_ids and law_ids" +
                                (kind == "end" ? ", and household_id from affected households" : kind is "renew" or "amend" ? ", household_id from affected households and any agreed_end_tick" : "") + ". " +
                                (lapsed.Length == 0 ? "" : "The permission of household " + string.Join(", ", lapsed) +
                                    " on this plot is past its agreed end, so the ruling must renew, amend or end it. Renewing also renews every lapsed permission on this plot for its own household. ") +
                                LandHearingCaseChoices(world, town, item, actor), 165)
                            { ActionPlan = ruling });
                    }
                }
                foreach (var request in item.ReopenRequests)
                {
                    OfferLandHearingReopening(world, candidates, actor, town, item, true, request.Id + ":accept",
                        "Accept independently established grounds and open a fresh hearing; current rights remain until a valid correction. Give reasons in civic_land_hearing.statement. Filed " + request.Kind + " claim: " + request.Reasons, 165);
                    OfferLandHearingReopening(world, candidates, actor, town, item, true, request.Id + ":reject",
                        "Reject this reopening request with reasons in civic_land_hearing.statement; keep the old ruling and request in the case history. Filed " + request.Kind + " claim: " + request.Reasons, 166);
                }
            }
            foreach (var grounds in new[] { "material_evidence", "procedural_error" })
                OfferLandHearingReopening(world, candidates, actor, town, item, false, grounds,
                    "Request reopening for " + grounds.Replace('_', ' ') + "; identify actual inspected evidence_ids and state grounds in civic_land_hearing.grounds. A claim alone cannot reopen the case. " + LandHearingCaseChoices(world, town, item, actor), 184);
            foreach (var recipient in world.Agents.Values.Where(person => person.AgentId != actor &&
                         world.IsWithinInteractionRange(person.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
                OfferLandHearingEvidence(world, candidates, actor, town, item, LandHearingEvidenceKind.Relay, recipient.AgentId,
                    "Tell " + world.Society.GetAgent(recipient.AgentId).Name + " the case material you actually read; newly unseen file contents are excluded.", 181);
        }
    }

    internal string LandHearingCaseChoices(ITownWorld world, TownRuntimeState town, TownLandCase item, string actor)
    {
        var revision = TownLandHearingRules.CurrentRevision(item);
        var known = item.Reads.Where(read => read.AgentId == actor && read.Revision == revision.Number && read.ReadTick <= world.WorldTick)
            .SelectMany(read => read.EvidenceIds).ToHashSet(StringComparer.Ordinal);
        var propertyRecord = item.Property is null ? [] : item.Evidence.Where(evidence => known.Contains(evidence.Id) &&
            evidence.SourceRecordId == TownPropertyRules.RecordId(item, revision.Number)).Take(1).ToArray();
        var evidence = item.Evidence.Where(evidence => known.Contains(evidence.Id)).TakeLast(4).Concat(propertyRecord).DistinctBy(evidence => evidence.Id).Select(evidence =>
            evidence.Id + "=" + evidence.Kind + " from " + evidence.SourceAgentId + ": " + BoundedText.Excerpt(evidence.Text, 160));
        return "Requested result: " + revision.RequestedOutcome.Kind + " " + revision.RequestedOutcome.HouseholdId +
            ". Affected households: " + string.Join(", ", LandHearingParties(world, town, revision.Tiles, item)
                .Where(party => party.HouseholdId is not null).Select(party => party.HouseholdId)) +
            "; inspected evidence: " + string.Join("; ", evidence) +
            "; inspected law versions: " + string.Join(", ", LandHearingApplicableLaws(world, town, item)
                .Where(pair => item.Evidence.Any(e => known.Contains(e.Id) && e.SourceRecordId == LandHearingLawToken(pair.Law, pair.Version)))
                .Select(pair => LandHearingLawToken(pair.Law, pair.Version)).Take(16)) + ".";
    }

    internal (TownCouncilState Council, TownLandRecords LandRecords) ApplyTownLandHearingAction(ITownWorld world, TownRuntimeState town,
            string actor, string action, string subject, string choice, string? proposalText, IReadOnlyList<CognitionLandTile>? landTiles,
            CognitionLandHearingChoice? hearingChoice, TownCouncilState council, TownGovernmentState government, LandHearingFilingActionPlan? filingPlan = null, LandHearingEvidenceActionPlan? evidencePlan = null, LandHearingJudgeActionPlan? judgePlan = null, LandHearingRulingActionPlan? rulingPlan = null, LandHearingReopeningActionPlan? reopeningPlan = null)
    {
        var currentTown = town with { Council = council, Government = government };
        (council, var hearings) = AdvanceTownLandHearings(world, currentTown, council, government);
        currentTown = currentTown with { Council = council, LandRecords = hearings };
        if (LandHearingFilingChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } filingChoice)
        {
            SetTown(world, currentTown);
            filingPlan ??= PlanLandHearingFiling(world, actor, filingChoice);
            if (filingPlan is null) return (council, hearings);
            filingPlan = BindLandHearingFiling(filingPlan, landTiles, hearingChoice, proposalText);
            if (CheckLandHearingFiling(world, filingPlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(filingPlan, refusal); return (council, hearings); }
            var filing = filingPlan.Filing!;
            if (filingPlan.Choice.Kind == LandHearingFilingKind.CouncilProposal)
            {
                council = TownCouncilRules.SubmitProposal(council, town.Id, actor, "land_hearing", null,
                    "Authorize this Town land case.", "council:" + council.Revision, TownAdults(world, town), world.WorldTick, CivicDay(world),
                    noticeText: TownLandGovernmentFilingRules.Describe(filing), landHearingRequest: filing);
                return (council, hearings);
            }
            var office = filingPlan.Choice.Kind == LandHearingFilingKind.TownOffice ? filingPlan.Office : null;
            return OpenLandHearing(world, currentTown, council, hearings, new(actor, office is null ? "dispute" : "town", filing.Statement, filing.RequestedOutcome,
                world.WorldTick, office?.ElectionId), filing.Tiles, office is null ? filingPlan.Household : null, office is null ? null : actor);
        }
        TownLandCase item;
        if (LandHearingEvidenceChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } evidenceChoice)
        {
            SetTown(world, currentTown);
            evidencePlan ??= PlanLandHearingEvidence(world, actor, evidenceChoice);
            if (evidencePlan is null) return (council, hearings);
            evidencePlan = BindLandHearingEvidence(evidencePlan, hearingChoice, proposalText);
            if (CheckLandHearingEvidence(world, evidencePlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(evidencePlan, refusal); return (council, hearings); }
            item = hearings.Cases.Single(item => item.Id == evidencePlan.CaseId);
        }
        else if (LandHearingJudgeChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } judgeChoice)
        {
            SetTown(world, currentTown);
            judgePlan ??= PlanLandHearingJudge(world, actor, judgeChoice);
            if (judgePlan is null) return (council, hearings);
            if (CheckLandHearingJudge(world, judgePlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(judgePlan, refusal); return (council, hearings); }
            item = hearings.Cases.Single(item => item.Id == judgePlan.CaseId);
        }
        else if (LandHearingRulingChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } rulingChoice)
        {
            SetTown(world, currentTown);
            rulingPlan ??= PlanLandHearingRuling(world, actor, rulingChoice);
            if (rulingPlan is null) return (council, hearings);
            rulingPlan = BindLandHearingRuling(rulingPlan, hearingChoice, proposalText);
            if (CheckLandHearingRuling(world, rulingPlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(rulingPlan, refusal); return (council, hearings); }
            item = hearings.Cases.Single(item => item.Id == rulingPlan.CaseId);
        }
        else if (LandHearingReopeningChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } reopeningChoice)
        {
            SetTown(world, currentTown);
            reopeningPlan ??= PlanLandHearingReopening(world, actor, reopeningChoice);
            if (reopeningPlan is null) return (council, hearings);
            reopeningPlan = BindLandHearingReopening(reopeningPlan, hearingChoice);
            if (CheckLandHearingReopening(world, reopeningPlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(reopeningPlan, refusal); return (council, hearings); }
            item = hearings.Cases.Single(item => item.Id == reopeningPlan.CaseId);
        }
        else
        {
            var candidates = new List<CognitionCandidate>();
            AddTownLandHearingCandidates(world, candidates, actor, currentTown);
            if (!candidates.Any(candidate => candidate.Id == $"civic|{town.Id}|{action}|{subject}|{choice}"))
                throw new InvalidOperationException("The hearing choice no longer has current authority or informed eligibility.");
            if (action == "hearing_property_request")
                return world.RequestPropertyCase(currentTown, actor, subject, choice, LandHearingText(hearingChoice?.Statement ?? proposalText), council, government);
            item = hearings.Cases.Single(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == subject ||
                item.Contest is { } contest && CivicAgentToken(TownLandHearingRules.RevisionToken(item) + "#" + TownLandCaseJudgeRules.RoundToken(contest)) == subject);
        }
        var revision = TownLandHearingRules.CurrentRevision(item);
        var parties = LandHearingParties(world, currentTown, revision.Tiles, item);
        switch (action)
        {
            case "hearing_inspect":
                council = TownCouncilRules.LearnNotice(council, actor, revision.NoticeId, world.WorldTick);
                hearings = LandHearingInspectRecords(world, currentTown, hearings, item.Id, actor, council);
                break;
            case "hearing_observe":
                var observed = LandHearingObserve(world, hearings, item, actor, council, evidencePlan!.Observation);
                // Seeing the same thing again adds no evidence, so it is not logged as new evidence either.
                if (ReferenceEquals(observed, hearings)) return (council, hearings);
                hearings = observed;
                break;
            case "hearing_statement":
                var statement = evidencePlan!.Statement!;
                hearings = TownLandHearingRules.AddEvidence(hearings, item.Id, revision.Number,
                    new(LandHearingEvidenceId(item, actor, "allegation", statement), revision.Number, "allegation", "statement",
                        actor, null, null, world.WorldTick, actor, world.WorldTick, statement), council.Knowledge);
                break;
            case "hearing_answer":
            case "hearing_waive":
                hearings = TownLandHearingRules.Respond(hearings, item.Id, revision.Number, actor,
                    action == "hearing_answer" ? "answer" : "waive", action == "hearing_waive" ? LandHearingWaiverStatement : evidencePlan!.Statement!,
                    world.WorldTick, council.Knowledge, parties, partyId: evidencePlan!.Party!.Id);
                break;
            case "hearing_property_accept":
            case "hearing_property_refuse":
                hearings = TownPropertyRules.Consent(hearings, item.Id, actor, action == "hearing_property_accept", world.WorldTick, council.Knowledge, parties);
                break;
            case "hearing_judge_register": hearings = TownLandCaseJudgeRules.Register(hearings, item.Id, actor, TownAdults(world, town), LandHearingHouseholds(world), world.WorldTick, parties); break;
            case "hearing_judge_withdraw": hearings = TownLandCaseJudgeRules.Withdraw(hearings, item.Id, actor, world.WorldTick); break;
            case "hearing_judge_resign": hearings = TownLandCaseJudgeRules.Resign(hearings, item.Id, actor, world.WorldTick); break;
            case "hearing_judge_vote":
                hearings = TownLandCaseJudgeRules.Vote(hearings, item.Id, TownLandCaseJudgeRules.RoundToken(judgePlan!.Contest!), actor, judgePlan.Candidate!, world.WorldTick);
                break;
            case "hearing_rule":
                var rulingOutcome = rulingPlan!.Outcome!;
                var priorProperty = item.Property is not null && choice != "reject" ? rulingPlan.PropertySnapshot : null;
                if (priorProperty is not null)
                    priorProperty = priorProperty with
                    {
                        Tick = world.WorldTick,
                        SharedLots = priorProperty.SharedLots.Select(lot => world.Society.Inventory.GetLot(lot.Id)).ToArray()
                    };
                (hearings, var rights) = TownLandHearingRules.Rule(hearings, world.Map, item.Id, revision.Number, rulingPlan.Judge!, world.WorldTick,
                    rulingOutcome, rulingPlan.EvidenceIds ?? [], rulingPlan.LawIds ?? [], rulingPlan.Reasons!, householdLandUseRights, parties,
                    validAuthority: true, householdLandUseRequests);
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
                        council = TownCouncilRules.CancelLandUseProposal(council, proposalId, world.WorldTick);
                }
                council = TownCouncilRules.PostNotice(council, "result", settled.Rulings[^1].Id,
                    item.Property is not null ? priorProperty is null ? "Property hearing rejected; ownership remains unchanged." :
                        "Property hearing decided: " + priorProperty.Building.InstanceId + " and its recorded shared goods now belong to " +
                        (item.Property.Request.TargetHouseholdId is { } receiver ? "household " + receiver : "the Town") + ". Personal goods retain their owners." :
                        "Land hearing decided: " + rulingOutcome.Kind + ". Only recorded use permissions on " +
                        TownLandClaimRules.DescribeTiles(revision.Tiles) + " were considered; buildings, crops and goods retain their owners.", world.WorldTick);
                break;
            case "hearing_reopen":
                hearings = TownLandHearingRules.RequestReopen(hearings, item.Id, actor, reopeningPlan!.Choice.GroundsOrRequestToken, reopeningPlan.EvidenceIds ?? [],
                    reopeningPlan.Reasons!, world.WorldTick);
                break;
            case "hearing_assess_reopen":
                var request = reopeningPlan!.Request!;
                var accept = reopeningPlan.Accept;
                var reopenedProperty = reopeningPlan.PropertySnapshot;
                if (reopenedProperty is not null) reopenedProperty = reopenedProperty with
                {
                    Tick = world.WorldTick,
                    SharedLots = reopenedProperty.SharedLots.Select(lot => world.Society.Inventory.GetLot(lot.Id)).ToArray()
                };
                if (reopenedProperty is not null)
                    parties = TownPropertyRules.Parties(parties, item.Property! with
                    { Snapshots = item.Property.Snapshots.Append(reopenedProperty).ToArray() }, town.Id, world.Society.Agents);
                hearings = TownLandHearingRules.Reopen(hearings, item.Id, request.Id, reopeningPlan.Judge!, accept,
                    reopeningPlan.Reasons!, householdLandUseRights, parties, world.WorldTick, CivicDay(world),
                    TownCouncilRules.NextNoticeId(council), validAuthority: true);

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
                var recipient = evidencePlan!.Recipient!;
                council = TownCouncilRules.LearnNotice(council, recipient, revision.NoticeId, world.WorldTick, actor);
                hearings = TownLandHearingRules.RelayRead(hearings, item.Id, revision.Number, actor, recipient, world.WorldTick, evidencePlan.RelayedEvidence, evidencePlan.RelayedRequests);
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

    internal static TownLandRequestedOutcome LandHearingOutcome(ITownWorld world, string kind, CognitionLandHearingChoice? choice)
    {
        if (choice?.RequestedOutcome is { } requested && requested != kind)
            throw new InvalidOperationException("The requested outcome contradicts the chosen bounded action.");
        var outcome = new TownLandRequestedOutcome(kind, choice?.HouseholdId, choice?.AgreedEndTick);
        if (!TownLandHearingRules.IsValidOutcome(outcome, world.WorldTick) || outcome.HouseholdId is { } household &&
            !world.Society.Households.Any(item => item.Id == household))
            throw new InvalidOperationException("The land outcome is malformed or names an unknown household.");
        return outcome;
    }

    internal static string LandHearingText(string? text) => BoundedText.IsValid(text, TownLandGovernmentFilingRules.MaximumStatementLength) ? text!.Trim() :
            throw new InvalidOperationException("This hearing action requires a bounded personal statement.");

}
