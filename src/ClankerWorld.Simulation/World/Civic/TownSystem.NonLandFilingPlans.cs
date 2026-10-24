using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static NonLandFilingActionPlan? PlanNonLandFiling(ITownWorld world, string actor, NonLandFilingChoice choice,
        (TownViolationAllegation Allegation, TownCaseEvidence Evidence)? selectedReport = null)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            CivicBoard(world, town) is not { } board) return null;
        var report = selectedReport ?? NonLandKnownReports(world, town, actor).FirstOrDefault(report => NonLandReportToken(report.Allegation) == choice.ReportToken);
        if (report.Allegation is null || town.NonLandHearings.ConductRecords.SingleOrDefault(record => record.Id == report.Allegation.IncidentId) is not { } conduct ||
            town.NonLandHearings.Acquisitions.SingleOrDefault(acquisition => acquisition.Id == report.Evidence.Id && acquisition.AgentId == actor) is not { } acquired) return null;
        var office = choice.Verb == "law_case_file_town" ? town.Government?.Offices.SingleOrDefault(office =>
            office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
        var filing = new TownViolationFiling(actor, NonLandFilingKind(choice), world.WorldTick, report.Allegation.Statement,
            report.Allegation.SourceEvidenceIds, office?.ElectionId);
        return new(actor, choice, board, report.Allegation, report.Evidence, conduct, acquired, office, town.Council.Revision,
            TownNonLandPartyRules.CurrentParties(town, report.Allegation, [filing], world.Society, world.WorldTick));
    }

    internal static string NonLandFilingKind(NonLandFilingChoice choice) => choice.Verb switch
    {
        "law_case_file_affected" => "affected",
        "law_case_file_town" => "town",
        _ => "witness"
    };

    internal static NonLandFilingActionPlan BindNonLandFiling(NonLandFilingActionPlan plan, CognitionNonLandChoice? terms) =>
        plan.Bound ? plan : plan with { Bound = true, Statement = CognitionDecisionResponse.NormalizeIdentityText(terms?.Statement) };

    internal static ActionCheck CheckNonLandFiling(ITownWorld world, NonLandFilingActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected report notice place is unavailable");
        var acquisition = town.NonLandHearings.Acquisitions.SingleOrDefault(item => item.Id == plan.Acquisition.Id && item.AgentId == plan.Actor);
        var conduct = town.NonLandHearings.ConductRecords.SingleOrDefault(item => item.Id == plan.Conduct.Id);
        if (acquisition is null || conduct is null || TownHearingProcedure.Digest(acquisition) != TownHearingProcedure.Digest(plan.Acquisition) ||
            TownHearingProcedure.Digest(conduct) != TownHearingProcedure.Digest(plan.Conduct) || NonLandReportToken(plan.Allegation) != plan.Choice.ReportToken ||
            !conduct.Laws.Any(law => law.LawId == plan.Allegation.LawId && law.Version == plan.Allegation.LawVersion &&
                (KnowsConductLaw(town, plan.Actor, law) || acquisition.Kind == "record_inspection" && town.NonLandHearings.Cases.Any(file =>
                    file.Id == acquisition.PublicCaseId && file.Allegation.LawId == law.LawId && file.Allegation.LawVersion == law.Version))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected known act, source or historical law is unavailable");
        var existing = town.NonLandHearings.Cases.SingleOrDefault(item => item.Key == TownNonLandRules.CaseKey(town.Id, plan.Allegation));
        if (existing is not null && (existing.Status != "pending" || existing.Filings.Any(filing => filing.AgentId == plan.Actor &&
                (filing.Kind == "affected" || plan.Actor == existing.Allegation.SubjectId)) || plan.Choice.Verb == "law_case_file" &&
                existing.Filings.Any(filing => filing.AgentId == plan.Actor)) ||
            plan.Choice.Verb == "law_case_file_affected" && plan.Actor == plan.Allegation.SubjectId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that exact report has already been filed or needs grounded reopening");
        if (plan.Choice.Verb == "law_case_file_town" && (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            town.Government?.Arrangement.Ordinary != TownArrangementRules.Mayor || plan.Office is null ||
            !town.Government.Offices.Contains(plan.Office) || plan.Office.TermEndTick <= world.WorldTick))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected ordinary Town office can no longer file that act");
        if (plan.Choice.Verb == "law_case_propose_town" && (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            town.Government?.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
            !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) || town.Council.Revision != plan.CouncilRevision))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council authorization has changed");
        var filing = new TownViolationFiling(plan.Actor, NonLandFilingKind(plan.Choice), world.WorldTick, plan.Statement ?? plan.Allegation.Statement,
            plan.Allegation.SourceEvidenceIds, plan.Office?.ElectionId);
        if (TownHearingProcedure.Digest(TownNonLandPartyRules.CurrentParties(town, plan.Allegation, [filing], world.Society, world.WorldTick)) !=
            TownHearingProcedure.Digest(plan.Parties))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected subject or legitimate response support has changed");
        if (!plan.Bound) return new();
        if (plan.Statement is null || !TownNonLandGovernmentFilingRules.IsValid(new(plan.Allegation with { Statement = plan.Statement }, plan.Statement), world.WorldTick))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected report needs a clear personal statement");
        if (plan.Choice.Verb == "law_case_propose_town" && TownCouncilRules.ProposalRetryRefusal(town.Council,
            TownNonLandGovernmentFilingRules.RequestKey(new(plan.Allegation, plan.Statement)),
            "council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry);
        if (TownNonLandRules.FilingRefusal(town.NonLandHearings, town.Id, plan.Allegation) is { } refusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
        return new();
    }

    private static void OfferNonLandFiling(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        (TownViolationAllegation Allegation, TownCaseEvidence Evidence) report, string verb, string text, int priority)
    {
        var choice = new NonLandFilingChoice(town.Id, verb, NonLandReportToken(report.Allegation));
        if (PlanNonLandFiling(world, actor, choice, report) is { } plan && CheckNonLandFiling(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
}
