using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal const string LandHearingWaiverStatement = "I choose not to respond. This speaks only for me.";

    internal LandHearingEvidenceActionPlan? PlanLandHearingEvidence(ITownWorld world, string actor, LandHearingEvidenceChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.RevisionToken) is not { } item) return null;
        var revision = TownLandHearingRules.CurrentRevision(item);
        var party = choice.Kind is LandHearingEvidenceKind.Answer or LandHearingEvidenceKind.Waive
            ? LandHearingParties(world, town, revision.Tiles, item).SingleOrDefault(party => CivicAgentToken(party.Id) == choice.PartyOrRecipientToken) : null;
        var reads = choice.Kind == LandHearingEvidenceKind.Relay
            ? item.Reads.Where(read => read.AgentId == actor && read.Revision == revision.Number && read.ReadTick <= world.WorldTick).ToArray() : [];
        return new(actor, choice, item.Id, revision, choice.Kind == LandHearingEvidenceKind.Inspect ? CivicBoard(world, town) : null,
            party, choice.Kind == LandHearingEvidenceKind.Relay ? ResolveCivicAgentToken(world, choice.PartyOrRecipientToken) : null,
            TownLandHearingRules.Ordered(reads.SelectMany(read => read.EvidenceIds)), TownLandHearingRules.Ordered(reads.SelectMany(read => read.ReopenRequestIds)),
            choice.Kind == LandHearingEvidenceKind.Observe ? PlanLandPlotObservation(world, item, actor) : null);
    }

    internal static LandHearingEvidenceActionPlan BindLandHearingEvidence(LandHearingEvidenceActionPlan plan, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.StatementBound || plan.Choice.Kind is not (LandHearingEvidenceKind.Statement or LandHearingEvidenceKind.Answer)) return plan;
        var raw = terms?.Statement ?? proposal;
        return plan with
        {
            StatementBound = true,
            Statement = raw?.Trim(),
            InvalidStatement = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength)
        };
    }

    internal ActionCheck CheckLandHearingEvidence(ITownWorld world, LandHearingEvidenceActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.RevisionToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected land case or noticed revision has changed");
        var revision = TownLandHearingRules.CurrentRevision(item);
        var parties = LandHearingParties(world, town, revision.Tiles, item);
        var isParty = parties.Any(party => party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal) || party.RepresentativeId == plan.Actor);
        var resident = TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal);
        var open = item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending");
        if (plan.Choice.Kind == LandHearingEvidenceKind.Inspect)
            return CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town) && LandHearingMayInspect(world, town, item, plan.Actor) &&
                (!LandHearingReadCurrent(world, item, plan.Actor) || item.Status == "settled" &&
                    (!LandHearingReadRuling(world, item, plan.Actor) || item.ReopenRequests.Any(request => request.Status == "pending" &&
                        !item.Reads.Any(read => read.AgentId == plan.Actor && read.Revision == revision.Number && read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))) ||
                        LandHearingUnreadRecords(world, town, item) is [_, ..])) ? new()
                : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected public case file is unavailable or already read");
        if (!TownLandHearingRules.HasNoticeReceipt(revision, plan.Actor, world.WorldTick, town.Council.Knowledge))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they have not learned that selected case notice");
        if (plan.StatementBound && (plan.InvalidStatement || plan.Statement is null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected personal statement is unavailable");
        switch (plan.Choice.Kind)
        {
            case LandHearingEvidenceKind.Statement:
                return open && (isParty || resident) ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot submit that selected case statement now");
            case LandHearingEvidenceKind.Answer:
            case LandHearingEvidenceKind.Waive:
                return item.Status == "pending" && plan.Party is { } selected && CivicAgentToken(selected.Id) == plan.Choice.PartyOrRecipientToken &&
                    parties.Any(party => party.Id == selected.Id &&
                        (party.Kind == "household" ? party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal) : party.RepresentativeId == plan.Actor)) &&
                    revision.Parties.Any(party => party.Id == selected.Id) ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household or Town response party has changed");
            case LandHearingEvidenceKind.Observe:
                if (!(open && resident || isParty) || plan.Observation is not { VisibleTiles.Count: > 0 } observation)
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot observe that selected case plot now");
                var currentObservation = PlanLandPlotObservation(world, item, plan.Actor);
                return currentObservation.VisibleTiles.SequenceEqual(observation.VisibleTiles) && currentObservation.Description == observation.Description ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected nearby plot view or its physical facts have changed");
            case LandHearingEvidenceKind.Relay:
                if (!open || plan.Recipient is not { } recipient || recipient == plan.Actor || !world.Agents.ContainsKey(recipient) ||
                    CivicAgentToken(recipient) != plan.Choice.PartyOrRecipientToken ||
                    !world.IsWithinInteractionRange(world.Agents[recipient].Position, world.Agents[plan.Actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
                    !LandHearingReadCurrent(world, item, plan.Actor) || !LandHearingMayInspect(world, town, item, recipient) || LandHearingReadCurrent(world, item, recipient))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case relay source or recipient is no longer eligible");
                var reads = item.Reads.Where(read => read.AgentId == plan.Actor && read.Revision == revision.Number && read.ReadTick <= world.WorldTick).ToArray();
                return plan.RelayedEvidence.All(id => reads.Any(read => read.EvidenceIds.Contains(id, StringComparer.Ordinal))) &&
                    plan.RelayedRequests.All(id => reads.Any(read => read.ReopenRequestIds.Contains(id, StringComparer.Ordinal))) ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case material was not actually read by the relay source");
        }
        return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case evidence action is unavailable");
    }

    private void OfferLandHearingEvidence(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandCase item, LandHearingEvidenceKind kind, string target, string text, int priority)
    {
        var id = CivicAction(world, town.Id, LandHearingEvidenceChoice.Verb(kind), TownLandHearingRules.RevisionToken(item), target);
        if (LandHearingEvidenceChoice.Decode(id) is { } choice && PlanLandHearingEvidence(world, actor, choice) is { } plan && CheckLandHearingEvidence(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
