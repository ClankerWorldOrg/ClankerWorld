using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static LandHearingReopeningActionPlan? PlanLandHearingReopening(ITownWorld world, string actor, LandHearingReopeningChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.RevisionToken) is not { Rulings.Count: > 0 } item) return null;
        var request = choice.Assessment ? item.ReopenRequests.SingleOrDefault(request => DerivedId.Create(request.Id + ":accept", "agent-sha256:") == choice.GroundsOrRequestToken ||
            DerivedId.Create(request.Id + ":reject", "agent-sha256:") == choice.GroundsOrRequestToken) : null;
        var accept = request is not null && DerivedId.Create(request.Id + ":accept", "agent-sha256:") == choice.GroundsOrRequestToken;
        return new(actor, choice, item.Id, TownLandHearingRules.CurrentRevision(item), item.Rulings[^1], request, accept, item.Judge,
            accept && item.Property is { Transfer: null } property && world.PropertyMayReopen(town, item)
                ? world.CaptureProperty(town, property.Request, TownLandHearingRules.CurrentRevision(item).Number + 1) : null);
    }

    internal static LandHearingReopeningActionPlan BindLandHearingReopening(LandHearingReopeningActionPlan plan, CognitionLandHearingChoice? terms)
    {
        if (plan.Bound) return plan;
        var raw = plan.Choice.Assessment ? terms?.Statement : terms?.Grounds ?? terms?.Statement;
        return plan with
        {
            Bound = true,
            EvidenceIds = terms?.EvidenceIds?.ToArray() ?? [],
            Reasons = raw?.Trim(),
            InvalidTerms = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength)
        };
    }

    internal ActionCheck CheckLandHearingReopening(ITownWorld world, LandHearingReopeningActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { Status: "settled", Rulings.Count: > 0 } item ||
            CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.RevisionToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
            TownLandHearingRules.RecordVersion(item.Rulings[^1]) != TownLandHearingRules.RecordVersion(plan.Ruling) ||
            !TownLandHearingRules.HasNoticeReceipt(plan.Revision, plan.Actor, world.WorldTick, town.Council.Knowledge) ||
            !LandHearingReadCurrent(world, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected settled ruling, noticed file or personal knowledge has changed");
        if (!plan.Choice.Assessment)
        {
            if (!LandHearingReadRuling(world, item, plan.Actor) || !LandHearingParties(world, town, plan.Revision.Tiles, item)
                    .Any(party => party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal) || party.RepresentativeId == plan.Actor) ||
                plan.Choice.GroundsOrRequestToken is not ("material_evidence" or "procedural_error"))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot submit those selected reopening grounds now");
            if (!plan.Bound) return new();
            return plan.InvalidTerms || plan.Reasons is null || TownLandHearingRules.RequestReopenRefusal(item,
                plan.Choice.GroundsOrRequestToken, plan.EvidenceIds ?? [], plan.Reasons) is not null ?
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected reopening grounds or cited evidence are unavailable") : new();
        }
        if (plan.Judge is not { } judge || judge.AgentId != plan.Actor || item.Judge != judge || !LandHearingJudgeValid(world, town, item, judge) ||
            plan.Request is not { } original || item.ReopenRequests.SingleOrDefault(request => request.Id == original.Id) is not { Status: "pending" } current ||
            TownLandHearingRules.RecordVersion(current) != TownLandHearingRules.RecordVersion(original) ||
            DerivedId.Create(current.Id + (plan.Accept ? ":accept" : ":reject"), "agent-sha256:") != plan.Choice.GroundsOrRequestToken ||
            !item.Reads.Any(read => read.AgentId == plan.Actor && read.Revision == plan.Revision.Number && read.ReadTick >= current.Tick && read.ReadTick <= world.WorldTick &&
                read.ReopenRequestIds.Contains(current.Id, StringComparer.Ordinal)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected pending request or informed assessor has changed");
        if (plan.Accept && (!world.PropertyMayReopen(town, item) || item.Property is { Transfer: null } &&
                (plan.PropertySnapshot is not { } snapshot || TownPropertyRules.MaterialVersion(snapshot) !=
                    TownPropertyRules.MaterialVersion(world.CaptureProperty(town, item.Property.Request, plan.Revision.Number + 1)))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property can no longer receive a fresh hearing notice");
        if (TownLandHearingRules.ReopeningRefusal(town.LandRecords, item, current, judge, plan.Accept,
            plan.Bound ? plan.Reasons ?? "" : "Assess the selected request", world.WorldTick, validAuthority: true) is { } refusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
        return !plan.Bound || !plan.InvalidTerms ? new()
            : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected assessment reasons are unavailable");
    }

    private void OfferLandHearingReopening(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandCase item, bool assessment, string terms, string text, int priority)
    {
        var id = CivicAction(world, town.Id, assessment ? "hearing_assess_reopen" : "hearing_reopen", TownLandHearingRules.RevisionToken(item), terms);
        if (LandHearingReopeningChoice.Decode(id) is { } choice && PlanLandHearingReopening(world, actor, choice) is { } plan && CheckLandHearingReopening(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
