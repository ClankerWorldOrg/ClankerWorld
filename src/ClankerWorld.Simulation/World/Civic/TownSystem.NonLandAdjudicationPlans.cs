using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy NonLandAdjudicationCapabilities(ITownWorld world, NonLandAdjudicationActionPlan plan) =>
        new(plan.Choice.Verb == "law_case_reopen" ? ActionLifeStages.NonInfants : ActionLifeStages.Adults, AllowBoat: true,
            AllowUrgentFood: !world.IsChild(plan.Actor), AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static NonLandAdjudicationActionPlan? PlanNonLandAdjudication(ITownWorld world, string actor, NonLandAdjudicationChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => NonLandActionToken(item) == choice.CaseToken) is not { } item) return null;
        var request = choice.Verb == "law_case_assess_reopen" ? item.ReopenRequests.SingleOrDefault(request =>
            CivicAgentToken(request.Id + ":accept") == choice.OutcomeOrRequestToken || CivicAgentToken(request.Id + ":reject") == choice.OutcomeOrRequestToken) : null;
        return new(actor, choice, item, NonLandParties(world, town, item), item.Judge, request,
            request is not null && CivicAgentToken(request.Id + ":accept") == choice.OutcomeOrRequestToken);
    }

    internal static NonLandAdjudicationActionPlan BindNonLandAdjudication(NonLandAdjudicationActionPlan plan, CognitionNonLandChoice? terms)
    {
        if (plan.Bound) return plan;
        return plan with
        {
            Bound = true,
            EvidenceIds = (terms?.EvidenceIds ?? []).Select(reference => plan.Case.Evidence.FirstOrDefault(evidence =>
                evidence.Id == reference || CivicAgentToken(evidence.Id) == reference)?.Id ?? reference).ToArray(),
            Reasons = CognitionDecisionResponse.NormalizeIdentityText(plan.Choice.Verb == "law_case_reopen" ? terms?.Grounds ?? terms?.Statement : terms?.Statement),
            Uncertainty = CognitionDecisionResponse.NormalizeIdentityText(terms?.Uncertainty)
        };
    }

    internal static ActionCheck CheckNonLandAdjudication(ITownWorld world, NonLandAdjudicationActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var request = plan.Choice.Verb == "law_case_reopen";
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandAdjudicationCapabilities(world, plan));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.Case.Id) is not { } item ||
            NonLandActionToken(item) != plan.Choice.CaseToken || TownHearingProcedure.Digest(NonLandRevision(item)) != TownHearingProcedure.Digest(NonLandRevision(plan.Case)) ||
            !NonLandKnows(world, town, item, plan.Actor) || !NonLandReadCurrent(world, item, plan.Actor) ||
            TownHearingProcedure.Digest(NonLandParties(world, town, item)) != TownHearingProcedure.Digest(plan.Parties))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected finding material, noticed parties or informed knowledge has changed");
        string? failure;
        if (request)
        {
            if (!plan.Parties.Any(party => party.SubjectId == plan.Actor || party.RespondingAdultId == plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they are no longer a party to that selected finding");
            failure = TownNonLandRules.RequestReopenRefusal(item, plan.Choice.OutcomeOrRequestToken, plan.EvidenceIds ?? [], plan.Reasons ?? "", plan.Bound);
        }
        else
        {
            if (plan.Judge is not { } judge || judge.AgentId != plan.Actor || item.Judge != judge || !NonLandJudgeValid(world, town, item, judge))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected independent case authority has changed");
            if (plan.Choice.Verb == "law_case_find")
            {
                if (!NonLandFindingChoices.Contains(plan.Choice.OutcomeOrRequestToken, StringComparer.Ordinal))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected civil outcome is unavailable");
                failure = TownNonLandRules.FindingRefusal(item, judge, world.WorldTick,
                    plan.Choice.OutcomeOrRequestToken == "unsupported" ? "unsupported" : "supported",
                    plan.Choice.OutcomeOrRequestToken == "unsupported" ? "none" : plan.Choice.OutcomeOrRequestToken,
                    plan.EvidenceIds ?? [], plan.Reasons ?? "", plan.Uncertainty ?? "", plan.Parties, true,
                    HasPriorLawNotice(town, item.Allegation, item.Allegation.SubjectId), plan.Bound);
            }
            else
            {
                if (plan.Request is not { } original || item.ReopenRequests.SingleOrDefault(request => request.Id == original.Id) is not { Status: "pending" } current ||
                    TownHearingProcedure.Digest(current) != TownHearingProcedure.Digest(original))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected pending reopening request has changed");
                failure = TownNonLandRules.ReopeningRefusal(item, current, judge, plan.Accept,
                    plan.Bound ? plan.Reasons ?? "" : "Assess the selected grounds.", plan.Parties, world.WorldTick, CivicDay(world), "planned-notice", true);
            }
        }
        return failure is null ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, failure);
    }

    private static void OfferNonLandAdjudication(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownNonLandCase item, string verb, string terms, string text, int priority)
    {
        var choice = new NonLandAdjudicationChoice(town.Id, verb, NonLandActionToken(item), CivicAgentToken(terms));
        if (PlanNonLandAdjudication(world, actor, choice) is { } plan && CheckNonLandAdjudication(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
}
