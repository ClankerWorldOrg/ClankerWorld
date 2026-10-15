using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy NonLandEvidenceCapabilities(ITownWorld world, NonLandEvidenceActionPlan plan) =>
        new(ActionLifeStages.NonInfants, AllowBoat: true, AllowUrgentFood: !world.IsChild(plan.Actor), AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal const string NonLandWaiverStatement = "I choose not to respond. This admits nothing.";

    internal static NonLandEvidenceActionPlan? PlanNonLandEvidence(ITownWorld world, string actor, NonLandEvidenceChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => NonLandActionToken(item) == choice.CaseToken) is not { } item) return null;
        var revision = NonLandRevision(item);
        var party = choice.Verb is "law_case_answer" or "law_case_waive" ?
            NonLandParties(world, town, item).SingleOrDefault(party => CivicAgentToken(party.Id) == choice.PartyOrRecipientToken) : null;
        var reads = choice.Verb == "law_case_relay" ? item.Reads.Where(read => read.AgentId == actor &&
            read.Revision == revision.Number && read.ReadTick <= world.WorldTick).ToArray() : [];
        TownCaseEvidence? lawEvidence = null;
        if (choice.Verb == "law_case_inspect" && town.Government?.Laws.SingleOrDefault(law => law.Id == item.Allegation.LawId) is { } law &&
            law.Versions.SingleOrDefault(version => version.Version == item.Allegation.LawVersion) is { } version)
            lawEvidence = new(TownRecordIds.LawEvidence(law.Id, version.Version), revision.Number, "record", "record_inspection", actor,
                LandHearingLawToken(law, version), TownLandHearingRules.LawVersion(version), item.Allegation.ConductTick, actor, world.WorldTick,
                NonLandExcerpt("Applicable law at the alleged act: " + TownLawRules.Text(version.Subject, version.Rule), 256));
        return new(actor, choice, item.Id, revision, choice.Verb == "law_case_inspect" ? CivicBoard(world, town) : null,
            choice.Verb is "law_case_inspect" or "law_case_evidence" ? NonLandKnownEvidence(world, town, item, actor) : [], lawEvidence, party,
            choice.Verb == "law_case_relay" ? ResolveCivicAgentToken(world, choice.PartyOrRecipientToken) : null,
            reads.Length == 0 ? null : new(revision.Number, actor, world.WorldTick,
                TownHearingProcedure.Ordered(reads.SelectMany(read => read.EvidenceIds)), TownHearingProcedure.Ordered(reads.SelectMany(read => read.ReopenRequestIds)))
            {
                ResponseIds = TownHearingProcedure.Ordered(reads.SelectMany(read => read.ResponseIds)),
                FindingIds = TownHearingProcedure.Ordered(reads.SelectMany(read => read.FindingIds))
            });
    }

    internal static NonLandEvidenceActionPlan BindNonLandEvidence(NonLandEvidenceActionPlan plan, CognitionNonLandChoice? terms) =>
        plan.Bound || plan.Choice.Verb is not ("law_case_statement" or "law_case_advise" or "law_case_answer") ? plan :
            plan with { Bound = true, Statement = CognitionDecisionResponse.NormalizeIdentityText(terms?.Statement) };

    internal static ActionCheck CheckNonLandEvidence(ITownWorld world, NonLandEvidenceActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandEvidenceCapabilities(world, plan));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            NonLandActionToken(item) != plan.Choice.CaseToken || TownHearingProcedure.Digest(NonLandRevision(item)) != TownHearingProcedure.Digest(plan.Revision))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case material or noticed response parties have changed");
        if (plan.Choice.Verb == "law_case_inspect")
        {
            if (CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town) || !NonLandMayInspect(world, town, item, plan.Actor) ||
                NonLandReadCurrent(world, item, plan.Actor) && !item.ReopenRequests.Any(request => !item.Reads.Any(read =>
                    read.AgentId == plan.Actor && read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot inspect that selected public file now");
        }
        else if (!NonLandKnows(world, town, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they have not received that selected case notice");
        if (plan.Choice.Verb is "law_case_statement" or "law_case_evidence" && !NonLandMayInspect(world, town, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer have standing to submit to that case");
        if (plan.Choice.Verb == "law_case_advise" && (!LandHearingAdult(world, plan.Actor) ||
            !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) || town.Government?.Arrangement.NonLand == TownArrangementRules.Mayor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council mediation authority has changed");
        if (plan.Choice.Verb is "law_case_inspect" or "law_case_evidence")
        {
            var available = NonLandKnownEvidence(world, town, item, plan.Actor);
            if (plan.Evidence.Any(original => !available.Any(current => TownHearingProcedure.Digest(current with { SubmittedTick = original.SubmittedTick }) ==
                TownHearingProcedure.Digest(original))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "a selected lawful source is no longer available");
            if (plan.Choice.Verb == "law_case_evidence" && !plan.Evidence.Any(evidence => !item.Evidence.Any(old =>
                old.SourceRecordId == evidence.SourceRecordId && old.SourceVersion == evidence.SourceVersion && old.SubmittedByAgentId == plan.Actor)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "those selected sources have already been submitted");
            if (plan.Choice.Verb == "law_case_inspect" && (plan.LawEvidence is null || !town.Government!.Laws.Any(law => law.Id == item.Allegation.LawId &&
                law.Versions.Any(version => version.Version == item.Allegation.LawVersion && TownLandHearingRules.LawVersion(version) == plan.LawEvidence.SourceVersion))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected historical law record is unavailable");
        }
        if (plan.Choice.Verb is "law_case_answer" or "law_case_waive" && (item.Status != "pending" || plan.Party is not { } party ||
            party.RespondingAdultId != plan.Actor || !NonLandParties(world, town, item).Contains(party) || !plan.Revision.Parties.Contains(party) ||
            item.Responses.Any(response => response.Revision == plan.Revision.Number && response.PartyId == party.Id)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected person's response or legitimate caregiver support has changed");
        if (plan.Choice.Verb == "law_case_relay" && (plan.SourceRead is null || TownNonLandRules.RelayReadRefusal(item, plan.Actor, plan.SourceRead, world.WorldTick) is not null || !NonLandReadCurrent(world, item, plan.Actor) || plan.Recipient is not { } recipient ||
            recipient == plan.Actor || !world.Agents.TryGetValue(recipient, out var target) ||
            !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, target.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
            !NonLandMayInspect(world, town, item, recipient) || NonLandReadCurrent(world, item, recipient)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient or informed source is unavailable nearby");
        return plan.Bound && plan.Statement is null ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that submission needs a clear personal statement") : new();
    }

    private static void OfferNonLandEvidence(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownNonLandCase item, string verb, string target, string text, int priority)
    {
        var choice = new NonLandEvidenceChoice(town.Id, verb, NonLandActionToken(item), CivicAgentToken(target));
        if (PlanNonLandEvidence(world, actor, choice) is { } plan && CheckNonLandEvidence(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
}
