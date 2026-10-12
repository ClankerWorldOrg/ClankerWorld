using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static readonly string[] NonLandFindingChoices = ["unsupported", "explanation", "warning", "censure"];
    internal void AddChildNonLandCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (!world.IsChild(actor) || WorldRuntime.CivicSharedRules.NeedsUrgentFood(world.Agents[actor]) || world.NeedsUrgentWarmth(world.Agents[actor])) return;
        foreach (var town in world.Towns.Where(town => town.FoundingState == "founded" && town.Council is not null))
            AddTownNonLandCandidates(world, candidates, actor, town);
    }

    internal void AddTownNonLandCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (town.Council is not { } council || !world.Agents.TryGetValue(actor, out var actorState) || world.Society.GetAgent(actor).AgeBand == SocietyAgeBand.Infant) return;
        var adult = LandHearingAdult(world, actor);
        var atBoard = NearCivicBoard(world, actor, town);
        var history = CivicHistory(town);
        if (!atBoard && MayVisitNonLandCase(world, actor, town) && !candidates.Any(candidate => candidate.Id == CivicAction(world, town.Id, "visit")))
            candidates.Add(new(CivicAction(world, town.Id, "visit"), "Visit the public notice place for a reported act you actually know about. Visiting grants no membership or private access.", 175));
        if (adult && atBoard)
            foreach (var report in NonLandKnownReports(world, town, actor).Where(report => !town.NonLandHearings.Cases.Any(item =>
                         item.Key == TownNonLandRules.CaseKey(town.Id, report.Allegation) &&
                         (item.Status != "pending" || item.Filings.Any(filing => filing.AgentId == actor &&
                             (filing.Kind == "affected" || actor == item.Allegation.SubjectId))))).Take(6))
            {
                var token = NonLandReportToken(report.Allegation);
                var label = "Report known conduct as an allegation: " + NonLandKnownAllegationText(world, town, report.Allegation) +
                    ". Supply civic_nonviolent.statement explaining the alleged breach; the engine does not decide guilt. Source: " + report.Evidence.Acquisition + ".";
                if (!town.NonLandHearings.Cases.Any(item => item.Key == TownNonLandRules.CaseKey(town.Id, report.Allegation) && item.Filings.Any(filing => filing.AgentId == actor)))
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_file", token), label, 185));
                if (actor != report.Allegation.SubjectId)
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_file_affected", token),
                        "Report this known act as personally affecting you, explaining your claimed harm in civic_nonviolent.statement. Your claimed stake grants a response opportunity, not proof that the allegation or harm is true. " + label, 185));
                if (town.Government is { } government && TownAdults(world, town).Contains(actor, StringComparer.Ordinal))
                {
                    if (government.Arrangement.Ordinary == TownArrangementRules.Mayor && government.Offices.Any(office =>
                            office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick))
                        candidates.Add(new(CivicAction(world, town.Id, "law_case_file_town", token), "File this known allegation under your current ordinary Town office. " + label, 186));
                    else if (government.Arrangement.Ordinary is TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil &&
                        council.Members.Contains(actor, StringComparer.Ordinal))
                        candidates.Add(new(CivicAction(world, town.Id, "law_case_propose_town", token), "Ask the Council to authorize this known Town allegation; approval is required. " + label, 186));
                }
            }
        foreach (var item in town.NonLandHearings.Cases)
        {
            var revision = NonLandRevision(item);
            var token = NonLandActionToken(item);
            var parties = NonLandParties(world, town, item);
            var isParty = parties.Any(party => party.SubjectId == actor || party.RespondingAdultId == actor);
            if (atBoard && NonLandMayInspect(world, town, item, actor) && (!NonLandReadCurrent(world, item, actor) ||
                item.ReopenRequests.Any(request => !item.Reads.Any(read => read.AgentId == actor && read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal)))))
                candidates.Add(new(CivicAction(world, town.Id, "law_case_inspect", token),
                    "Read this public case, its alleged act, historical law and sourced evidence. It remains an allegation until a reasoned finding; reading grants no private access.", 177));
            if (!NonLandKnows(world, town, item, actor)) continue;
            if (NonLandMayInspect(world, town, item, actor))
            {
                candidates.Add(new(CivicAction(world, town.Id, "law_case_statement", token),
                    "Submit your own statement via civic_nonviolent.statement. A child may speak with caregiver support; testimony is not a verified fact or another person's consent.", 182));
                if (NonLandKnownEvidence(world, town, item, actor).Any(evidence => !item.Evidence.Any(old => old.SourceRecordId == evidence.SourceRecordId &&
                        old.SourceVersion == evidence.SourceVersion && old.SubmittedByAgentId == actor)))
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_evidence", token), "Submit only evidence you actually observed or lawfully learned. Private information stays private.", 181));
            }
            if (adult && council.Members.Contains(actor, StringComparer.Ordinal) && town.Government?.Arrangement.NonLand != TownArrangementRules.Mayor)
                candidates.Add(new(CivicAction(world, town.Id, "law_case_advise", token), "Offer Council mediation or advice via civic_nonviolent.statement while the formal non-land mandate is absent. Advice is a sourced statement, not a finding or enforceable consequence.", 183));
            foreach (var party in parties.Where(party => item.Status == "pending" && party.RespondingAdultId == actor &&
                         !item.Responses.Any(response => response.Revision == revision.Number && response.PartyId == party.Id)))
            {
                candidates.Add(new(CivicAction(world, town.Id, "law_case_answer", token, party.Id),
                    "Answer this allegation for " + world.Society.GetAgent(party.SubjectId).Name +
                    (party.SubjectId == actor ? "." : " with your actual caregiver support.") + " Supply civic_nonviolent.statement. This promises no goods or work." + NonLandEvidenceChoices(world, town, item, actor), 165));
                candidates.Add(new(CivicAction(world, town.Id, "law_case_waive", token, party.Id), "Explicitly waive only this person's current response opportunity; this admits nothing and supplies no restorative consent.", 166));
            }
            if (adult && TownAdults(world, town).Contains(actor, StringComparer.Ordinal) && (item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            {
                var willing = item.JudgeConsents.Any(consent => consent.AgentId == actor && consent.WithdrawnTick is null);
                if (!willing && !NonLandConflict(world, town, item, actor)) candidates.Add(new(CivicAction(world, town.Id, "law_case_judge_register", token), "Personally agree to stand as an independent judge of this case only, subject to actual resident election.", 180));
                if (willing) candidates.Add(new(CivicAction(world, town.Id, "law_case_judge_withdraw", token), "Withdraw your willingness to adjudicate this case; its records remain.", 185));
                if (item.Judge is { Kind: "case_elected" } held && held.AgentId == actor)
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_judge_resign", token), "Resign your authority over this case only.", 185));
            }
            if (item.Contest is { Stage: "voting" } contest && contest.Voters.Contains(actor, StringComparer.Ordinal) && history.Knows(actor, TownCaseJudgeRules.RoundToken(contest)))
                foreach (var candidate in contest.Candidates)
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_judge_vote", token + "#" + TownCaseJudgeRules.RoundToken(contest), candidate),
                        "Vote for " + world.Society.GetAgent(candidate).Name + " as independent judge of this case only.", 167));
            if (item.Judge is { } judge && judge.AgentId == actor && NonLandJudgeValid(world, town, item, judge) && NonLandReadCurrent(world, item, actor))
            {
                if (item.Status == "pending" && TownNonLandRules.CanCloseResponses(item, parties, world.WorldTick))
                    foreach (var outcome in NonLandFindingChoices.Where(outcome => outcome != "censure" ||
                                 HasPriorLawNotice(town, item.Allegation, item.Allegation.SubjectId)))
                        candidates.Add(new(CivicAction(world, town.Id, "law_case_find", token, outcome),
                            "Record " + outcome + ". An adverse finding requires more-likely-than-not evidence, reasons and uncertainty in civic_nonviolent; rumor or silence alone is insufficient. A first incident without credible prior law notice favors explanation or warning. No property or membership changes." + NonLandEvidenceChoices(world, town, item, actor), 165));
                foreach (var request in item.ReopenRequests.Where(request => request.Status == "pending" && item.Reads.Any(read => read.AgentId == actor &&
                             read.Revision == revision.Number && read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))))
                {
                    if (request.Kind == "material_evidence" ? TownNonLandRules.MaterialNewEvidence(item, request) : TownNonLandRules.DemonstratedProceduralError(item, request))
                        candidates.Add(new(CivicAction(world, town.Id, "law_case_assess_reopen", token, request.Id + ":accept"), "Reopen on established material evidence or procedure error, with reasons in civic_nonviolent.statement. Prior findings and physical effects remain recorded.", 165));
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_assess_reopen", token, request.Id + ":reject"), "Explain why these reopening grounds are not established; retain the request and prior result.", 166));
                }
            }
            if (item.Status == "settled" && isParty && NonLandReadCurrent(world, item, actor))
                foreach (var grounds in new[] { "material_evidence", "procedural_error" })
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_reopen", token, grounds), "Request reopening for " + grounds.Replace('_', ' ') + ". Supply actual evidence_ids and grounds; a claim alone changes no finding." + NonLandEvidenceChoices(world, town, item, actor), 184));
            if (adult && item.Findings.Count > 0 && item.Findings[^1] is { Result: "supported" } finding && NonLandReadCurrent(world, item, actor) &&
                (isParty || finding.Judge.AgentId == actor && NonLandJudgeValid(world, town, item, finding.Judge)))
                candidates.Add(new(CivicAction(world, town.Id, "remedy_offer", token, finding.Id),
                    "Offer feasible named goods, repair or Town resource delivery in civic_nonviolent.terms, with reasons and optional completion_ticks (default three days after consent). Each contributing adult must personally agree. No stock is reserved and no work forced." + NonLandRemedyReferences(world, town, actor), 183));
            foreach (var recipient in world.Agents.Values.Where(person => person.AgentId != actor && world.IsWithinInteractionRange(person.Position, actorState.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
                if (NonLandReadCurrent(world, item, actor) && NonLandMayInspect(world, town, item, recipient.AgentId) && !NonLandReadCurrent(world, item, recipient.AgentId))
                    candidates.Add(new(CivicAction(world, town.Id, "law_case_relay", token, recipient.AgentId), "Relay the case material you actually read to " + world.Society.GetAgent(recipient.AgentId).Name + " nearby.", 181));
        }
        foreach (var agreement in town.NonLandHearings.Agreements.Where(agreement => adult && agreement.Status != "completed" &&
                     !TownRemedyRules.IsSuperseded(town.NonLandHearings, agreement.Id) && agreement.Terms.Any(term => term.ContributorId == actor)))
        {
            var original = town.NonLandHearings.Offers.Single(offer => offer.Id == agreement.OfferId);
            var file = town.NonLandHearings.Cases.Single(item => item.Id == original.CaseId);
            if (NonLandReadCurrent(world, file, actor) && !town.NonLandHearings.Offers.Any(offer => offer.CaseId == file.Id && offer.Status == "pending"))
                candidates.Add(new(CivicAction(world, town.Id, "remedy_renegotiate", agreement.Id, agreement.TermsHash),
                    "Request a feasible change to your unfinished voluntary agreement via civic_nonviolent.terms. Prior work remains recorded; replacement terms require fresh consent. Inability is no new offense." + NonLandRemedyReferences(world, town, actor), 183));
        }
        foreach (var offer in town.NonLandHearings.Offers.Where(offer => offer.Status == "pending" && world.WorldTick < offer.ResponseDeadlineTick &&
                     offer.Terms.Any(term => term.ContributorId == actor)))
        {
            if (atBoard && !history.Known(actor).Contains(offer.NoticeId))
                candidates.Add(new(CivicAction(world, town.Id, "remedy_read", NonLandOfferToken(offer)), "Read the actual voluntary offer: " + NonLandTermsText(offer.Terms) + ". " + NonLandOfferTiming(world, offer) + " Publication alone is not your awareness or consent.", 177));
            if (!adult || !history.Known(actor).Contains(offer.NoticeId) || offer.Responses.Any(response => response.AgentId == actor && response.Kind == "accept")) continue;
            if (NonLandRemedyCapacityFeasible(world, offer.Terms) &&
                NonLandRemedyDurationFeasible(world, town, offer.Terms.Where(term => term.ContributorId == actor).ToArray(), offer.CompletionTicks))
                candidates.Add(new(CivicAction(world, town.Id, "remedy_accept", NonLandOfferToken(offer)), "Personally accept your exact contribution: " + NonLandTermsText(offer.Terms.Where(term => term.ContributorId == actor)) + ". " + NonLandOfferTiming(world, offer) + " This does not start work or reserve goods.", 165));
            candidates.Add(new(CivicAction(world, town.Id, "remedy_decline", NonLandOfferToken(offer)), "Decline this voluntary offer. Declining creates no offense or automatic penalty.", 166));
            candidates.Add(new(CivicAction(world, town.Id, "remedy_counter", NonLandOfferToken(offer)), "Counter with feasible named terms in civic_nonviolent.terms; everyone must read and consent to the new offer." + NonLandRemedyReferences(world, town, actor), 183));
        }
    }

    internal static string NonLandTermsText(IEnumerable<TownRemedyTerm> terms) => string.Join("; ", terms.Select(term =>
        term.Quantity + " " + (term.ItemKind ?? term.Kind).Replace('_', ' ') + " by " + CivicAgentToken(term.ContributorId) +
        (term.BeneficiaryId is { } beneficiary ? " for " + CivicAgentToken(beneficiary) : "") + (term.TargetId is { } target ? " at " + CivicAgentToken(target) : "")));
    internal static string NonLandDaysText(ITownWorld world, long ticks) =>
        ((decimal)ticks / CivicDay(world)).ToString("0.################", System.Globalization.CultureInfo.InvariantCulture) +
        (ticks == CivicDay(world) ? " world day" : " world days");
    internal static string NonLandOfferTiming(ITownWorld world, TownRemedyOffer offer) =>
        "Complete within " + NonLandDaysText(world, offer.CompletionTicks) + " after everyone accepts. Answer within " +
        NonLandDaysText(world, offer.ResponseDeadlineTick - offer.PublishedTick) + " of publication.";
    internal string NonLandRemedyReferences(ITownWorld world, TownRuntimeState town, string actor)
    {
        var people = world.Agents.Values.Where(person => person.AgentId == actor || town.ResidentIds.Contains(person.AgentId) || world.IsWithinInteractionRange(person.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)).ToArray();
        var lots = world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor || lot.OwnerId == world.HouseholdFor(actor) || world.Agents.TryGetValue(lot.OwnerId, out var holder) && ToolProgressionRules.IsTopLevelCarriedLot(lot, lot.OwnerId) && world.IsWithinInteractionRange(holder.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
            CanObserveNonLandAct(world, actor, lot.OwnerId, holder.Position));
        var warehouses = world.WorldSimulation.Buildings.Where(building => building.TownId == town.Id &&
            (TownForResident(world, actor) == town.Id || world.IsWithinInteractionRange(building.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)));
        return " Use these references in terms. People: " + string.Join("; ", people.Select(person => world.Society.GetAgent(person.AgentId).Name + " (" + CivicAgentToken(person.AgentId) + ")")) +
            ". Town: " + town.Name + " (" + CivicAgentToken(town.Id) + "). Known goods: " + string.Join("; ", lots.Select(lot =>
                lot.ItemKind.Replace('_', ' ') + " (" + CivicAgentToken(lot.Id) + ") held by " + CivicAgentToken(lot.OwnerId))) +
            ". Known Town buildings: " + string.Join("; ", warehouses.Select(building => CivicAgentToken(building.InstanceId))) + ".";
    }
}
