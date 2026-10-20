using ClankerWorld.Simulation.Cognition;
using System.Globalization;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static LandHearingPropertyActionPlan? PlanLandHearingProperty(ITownWorld world, string actor, LandHearingPropertyChoice choice,
        TownPropertyRequest? selectedRequest = null, TownPropertySnapshot? selectedSnapshot = null, IReadOnlyList<GridPoint>? selectedTiles = null)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null, Government: not null } town) return null;
        if (choice.Kind != LandHearingPropertyKind.Request)
        {
            if (town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.SubjectToken) is not { Property: { } property } item) return null;
            var revision = TownLandHearingRules.CurrentRevision(item);
            return new(actor, choice, property.Request, property.Snapshots.Single(snapshot => snapshot.Revision == revision.Number), revision.Tiles,
                null, false, null, town.Council.Revision, item.Id, revision,
                property.Consents.LastOrDefault(consent => consent.Revision == revision.Number && consent.AgentId == actor));
        }
        var building = world.WorldSimulation.Buildings.SingleOrDefault(building => CivicAgentToken(building.InstanceId) == choice.SubjectToken);
        if (building is null) return null;
        var target = choice.TargetToken == "town" ? null : world.Society.Households.SingleOrDefault(household => CivicAgentToken(household.Id) == choice.TargetToken)?.Id;
        var request = selectedRequest ?? new(building.InstanceId, building.HouseholdId, target);
        var snapshot = selectedSnapshot ?? world.TryCaptureProperty(town, request, 1, out _);
        if (snapshot is null) return null;
        var tiles = selectedTiles ?? TownLandRightsRules.OrderTiles(WorldContentSimulationRules.Footprint(
            world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building));
        var mayor = town.Government.Arrangement.Ordinary == TownArrangementRules.Mayor;
        var office = mayor ? town.Government.Offices.SingleOrDefault(office => office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
        return new(actor, choice, request, snapshot, tiles, CivicBoard(world, town), mayor, office, town.Council.Revision);
    }

    internal static LandHearingPropertyActionPlan BindLandHearingProperty(LandHearingPropertyActionPlan plan, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.Bound || plan.Choice.Kind != LandHearingPropertyKind.Request) return plan;
        var raw = terms?.Statement ?? proposal;
        return plan with { Bound = true, Statement = raw?.Trim(), InvalidStatement = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength) };
    }

    internal ActionCheck CheckLandHearingProperty(ITownWorld world, LandHearingPropertyActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? LandHearingCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null, Government: not null } town)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property hearing Town is unavailable");
        if (plan.Choice.Kind != LandHearingPropertyKind.Request)
        {
            if (town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { Status: "pending", Property: { Transfer: null } property } item ||
                plan.Revision is null || CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.SubjectToken ||
                TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
                property.Request != plan.Request || TownLandHearingRules.RecordVersion(property.Snapshots.Single(snapshot => snapshot.Revision == plan.Revision.Number)) !=
                    TownLandHearingRules.RecordVersion(plan.Snapshot) || !LandHearingReadCurrent(world, item, plan.Actor) ||
                property.Consents.LastOrDefault(consent => consent.Revision == plan.Revision.Number && consent.AgentId == plan.Actor) != plan.PreviousConsent ||
                plan.PreviousConsent?.Agreed == (plan.Choice.Kind == LandHearingPropertyKind.Accept))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected noticed property or personal consent has changed");
            var parties = LandHearingParties(world, town, plan.Tiles, item);
            return TownPropertyRules.ConsentRefusal(item, plan.Actor, world.WorldTick, town.Council.Knowledge, parties) is { } refusal
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal) : new();
        }
        if (CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town) || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            CivicAgentToken(plan.Request.BuildingId) != plan.Choice.SubjectToken ||
            (plan.Request.TargetHouseholdId is { } target ? CivicAgentToken(target) : "town") != plan.Choice.TargetToken ||
            world.TryCaptureProperty(town, plan.Request, 1, out _) is not { } current ||
            TownPropertyRules.MaterialVersion(current) != TownPropertyRules.MaterialVersion(plan.Snapshot))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property, ownership, goods, recipient or notice place has changed");
        if (plan.Mayor)
        {
            if (town.Government.Arrangement.Ordinary != TownArrangementRules.Mayor || plan.Office is null || plan.Office.HolderId != plan.Actor ||
                plan.Office.TermEndTick is not { } end || end <= world.WorldTick || !town.Government.Offices.Contains(plan.Office))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected governing office can no longer file this property request");
        }
        else if (town.Government.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
            !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) || town.Council.Revision != plan.CouncilRevision)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council property-filing authority has changed");
        if (TownPropertyRules.FilingRefusal(town.LandRecords, plan.Tiles, new(plan.Request, [plan.Snapshot], [])) is { } filingFailure)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, filingFailure);
        if (!plan.Bound) return new();
        if (plan.InvalidStatement || plan.Statement is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property filing statement is unavailable");
        var filing = new TownLandFilingRequest(plan.Tiles, TownPropertyRules.Outcome(plan.Request), plan.Statement) { Property = plan.Request };
        return !plan.Mayor && TownCouncilRules.ProposalRetryRefusal(town.Council, TownLandGovernmentFilingRules.RequestKey(filing),
            "council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry) : new();
    }
}
