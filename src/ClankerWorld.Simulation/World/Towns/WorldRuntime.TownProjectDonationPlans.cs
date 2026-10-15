using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private TownProjectDonationActionPlan PlanTownProjectDonation(string actor, TownProjectChoice choice) =>
        new(actor, new(choice.Project.Id, choice.Lot!.Id, choice.Quantity, agents[actor].Position == TownProjectRules.WorkSite(choice.Project.Plan)),
            choice.Town.Id, choice.Project.ProposalId, choice.Project.Plan, choice.ItemKind, TownProjectRules.WorkSite(choice.Project.Plan),
            Reach(actor, TownProjectRules.WorkSite(choice.Project.Plan), ReachPurpose.TownProjectDonation, 0, 0, SwimmingPolicy.WhenCapable));

    private ActionCheck CheckTownProjectDonation(TownProjectDonationActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? TownProjectPhysicalCapabilities);
        if (!capability.Allowed) return capability;
        if (!ReadyForTownProject(plan.Actor) || TownForResident(plan.Actor) != plan.TownId || towns.SingleOrDefault(town => town.Id == plan.TownId) is not { Council: not null } town ||
            town.Projects.SingleOrDefault(project => project.Id == plan.Choice.ProjectId) is not { Stage: "supplying" or "working" } project ||
            project.ProposalId != plan.ProposalId || TownHearingProcedure.Digest(project.Plan) != TownHearingProcedure.Digest(plan.Project) ||
            !CivicHistory(town).Knows(plan.Actor, project.ProposalId) || TownProjectRules.WorkSite(project.Plan) != plan.Destination ||
            TownProjectSiteFailure(town, project.Plan, project.Id, plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project scope, knowledge or donation ability has changed");
        if (!project.Plan.Budget.Any(cost => cost.ResourceId == plan.ItemKind) || TownProjectMissing(town, project, plan.ItemKind) < plan.Choice.Quantity ||
            PersonalTownProjectDonationLots(plan.Actor, plan.ItemKind).SingleOrDefault(lot => lot.Id == plan.Choice.StockId) is not { } selected ||
            AvailableLotQuantity(selected) < plan.Choice.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected personal goods or exact required contribution are unavailable");
        if (plan.Choice.AtSite)
            return agents[plan.Actor].Position == plan.Destination ? new() :
                ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they must be at the selected project site to donate those goods");
        return agents[plan.Actor].Position != plan.Destination && Reach(plan.Actor, plan.Destination,
            ReachPurpose.TownProjectDonation, 0, 0, SwimmingPolicy.WhenCapable).Count > 0 ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected donation trip has ended or is out of reach");
    }

    private bool OfferTownProjectDonation(List<CognitionCandidate> candidates, string actor, TownProjectChoice choice)
    {
        var plan = PlanTownProjectDonation(actor, choice);
        if (!CheckActionPlan(plan).Allowed) return false;
        candidates.Add(new(plan.CandidateId, $"Choose to donate {choice.Quantity} of your own carried {choice.ItemKind} to {choice.Project.Plan.Name}. " +
            "The Town will own that exact contribution at the approved site.", 170, choice.Project.Id, choice.Project.Plan.Name)
        { ActionPlan = plan });
        return true;
    }
}
