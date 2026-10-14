namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static TownProjectProposalActionPlan? PlanTownProjectProposal(ITownWorld world, string actor,
        TownProjectProposalChoice choice, TownProjectPayload? selected = null)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town) return null;
        var project = selected ?? (choice.Boat ? world.Port(choice.DefinitionOrPort) is { } port ?
            WorldRuntime.CivicSharedRules.BoatProjectPlan(port, "Communal boat") : null :
            world.OfferedTownProjectPlan(town, actor, choice.DefinitionOrPort, choice.Coordinates, null));
        return project is null ? null : new(actor, choice, project, town.Council.Revision);
    }

    internal static TownProjectProposalActionPlan BindTownProjectName(TownProjectProposalActionPlan plan, string? name) =>
        plan.Bound ? plan : plan with { Bound = true, Project = plan.Project with { Name = string.IsNullOrWhiteSpace(name) ? plan.Project.Name : name.Trim() } };

    internal static ActionCheck CheckTownProjectProposal(ITownWorld world, TownProjectProposalActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) || town.Council.Revision != plan.CouncilRevision)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Town project proposal authority has changed");
        if (TownProjectRules.PayloadRefusal(plan.Project) is { } payload)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, payload);
        if (world.TownProjectSiteFailure(town, plan.Project, actor: plan.Actor) is { } site)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, site);
        if (TownCouncilRules.ProposalRetryRefusal(town.Council, TownProjectRules.RequestKey(plan.Project),
            "council:" + plan.CouncilRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry);
        return new();
    }
}
