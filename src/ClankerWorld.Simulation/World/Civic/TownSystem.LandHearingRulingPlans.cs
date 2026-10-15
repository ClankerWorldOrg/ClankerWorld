using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal LandHearingRulingActionPlan? PlanLandHearingRuling(ITownWorld world, string actor, LandHearingRulingChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.RevisionToken) is not { } item) return null;
        var revision = TownLandHearingRules.CurrentRevision(item);
        var parties = LandHearingParties(world, town, revision.Tiles, item);
        var lapsed = TownLandHearingRules.RulingChoices(householdLandUseRights, revision.Tiles, parties, world.WorldTick).Lapsed;
        return new(actor, choice, item.Id, revision, item.Judge, lapsed,
            householdLandUseRights.Where(right => right.Tiles.Any(revision.Tiles.Contains)).ToArray(),
            householdLandUseRequests.Where(request => request.TownId == town.Id && request.Status == "pending" &&
                TownLandRightsRules.UnresolvedRequestTiles(request).Any(revision.Tiles.Contains)).ToArray(),
            item.Property?.Request, item.Property is not null && choice.OutcomeKind != "reject" && world.PropertyCanTransfer(town, item)
                ? world.CaptureProperty(town, item.Property.Request, revision.Number) : null);
    }

    internal static LandHearingRulingActionPlan BindLandHearingRuling(LandHearingRulingActionPlan plan, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.Bound) return plan;
        var raw = terms?.Statement ?? proposal;
        var property = plan.PropertyRequest is not null && plan.Choice.OutcomeKind != "reject";
        var outcome = property ? TownPropertyRules.Outcome(plan.PropertyRequest!)
            : new TownLandRequestedOutcome(plan.Choice.OutcomeKind, terms?.HouseholdId, terms?.AgreedEndTick);
        return plan with
        {
            Bound = true,
            Outcome = outcome,
            EvidenceIds = terms?.EvidenceIds?.ToArray() ?? [],
            LawIds = terms?.LawIds?.ToArray() ?? [],
            Reasons = raw?.Trim(),
            InvalidTerms = !TownLandHearingRules.ValidText(raw, TownLandGovernmentFilingRules.MaximumStatementLength) ||
                terms?.RequestedOutcome is { } supplied && supplied != plan.Choice.OutcomeKind || property &&
                (outcome.Kind != plan.Choice.OutcomeKind || terms?.HouseholdId is { } household && household != outcome.HouseholdId || terms?.AgreedEndTick is not null)
        };
    }

    internal ActionCheck CheckLandHearingRuling(ITownWorld world, LandHearingRulingActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? LandHearingCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { Status: "pending" } item ||
            CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.RevisionToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
            plan.Judge is not { } judge || judge.AgentId != plan.Actor || item.Judge != judge || !LandHearingJudgeValid(world, town, item, judge) ||
            !TownLandHearingRules.HasNoticeReceipt(plan.Revision, plan.Actor, world.WorldTick, town.Council.Knowledge) || !LandHearingReadCurrent(world, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected noticed case, informed judge or mandate has changed");
        var parties = LandHearingParties(world, town, plan.Revision.Tiles, item);
        var choices = item.Property is null ? TownLandHearingRules.RulingChoices(householdLandUseRights, plan.Revision.Tiles, parties, world.WorldTick).Kinds
            : world.PropertyCanTransfer(town, item) ? new[] { TownPropertyRules.Outcome(item.Property.Request).Kind, "reject" } : new[] { "reject" };
        if (!TownLandHearingRules.CanCloseResponses(item, parties, world.WorldTick) || !choices.Contains(plan.Choice.OutcomeKind, StringComparer.Ordinal) ||
            TownLandHearingRules.RecordVersion(householdLandUseRights.Where(right => right.Tiles.Any(plan.Revision.Tiles.Contains)).ToArray()) !=
                TownLandHearingRules.RecordVersion(plan.AffectedRights) ||
            TownLandHearingRules.RecordVersion(householdLandUseRequests.Where(request => request.TownId == town.Id && request.Status == "pending" &&
                TownLandRightsRules.UnresolvedRequestTiles(request).Any(plan.Revision.Tiles.Contains)).ToArray()) != TownLandHearingRules.RecordVersion(plan.PendingRequests) ||
            item.Property?.Request != plan.PropertyRequest)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ruling opportunity, permissions or affected applications have changed");
        if (!plan.Bound) return new();
        if (plan.InvalidTerms || plan.Outcome is not { } outcome || plan.Reasons is null ||
            !TownLandHearingRules.IsValidOutcome(outcome, world.WorldTick) || outcome.HouseholdId is { } target &&
                (!world.Society.Households.Any(household => household.Id == target) || !parties.Any(party => party.HouseholdId == target)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ruling terms or affected household are unavailable");
        if (plan.PropertyRequest is not null && plan.Choice.OutcomeKind != "reject" &&
            (plan.PropertySnapshot is not { } snapshot || item.Property is null || !world.PropertyCanTransfer(town, item) || TownPropertyRules.MaterialVersion(snapshot) !=
                TownPropertyRules.MaterialVersion(item.Property.Snapshots.Single(current => current.Revision == plan.Revision.Number))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected noticed property or personal consent has changed");
        var evidence = plan.EvidenceIds ?? [];
        var laws = plan.LawIds ?? [];
        var applicable = LandHearingApplicableLaws(world, town, item).Select(pair => LandHearingLawToken(pair.Law, pair.Version)).ToHashSet(StringComparer.Ordinal);
        if (laws.Any(id => !applicable.Contains(id) || !item.Evidence.Any(record => record.SourceRecordId == id && record.Kind == "record")))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cited law version was not actually inspected or is no longer applicable");
        return TownLandHearingRules.RulingRefusal(item, world.Map, judge, world.WorldTick, outcome, evidence, laws, plan.Reasons,
            householdLandUseRights, parties, validAuthority: true, householdLandUseRequests) is { } refusal
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal) : new();
    }
}
