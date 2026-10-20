using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy GovernanceCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static GovernmentOfficeActionPlan? PlanGovernmentOffice(ITownWorld world, string actor, GovernmentOfficeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Government: { } government } town) return null;
        var extending = choice.Kind == GovernmentOfficeKind.ExtendNonLand;
        var office = extending ? government.Offices.SingleOrDefault(office => CivicAgentToken(NonLandExtensionToken(office)) == choice.Value) :
            government.Offices.SingleOrDefault(office => office.Mandates == choice.Subject && office.HolderId == actor);
        var change = choice.Kind == GovernmentOfficeKind.AcceptNonLand ? government.Changes.SingleOrDefault(change => change.Id == choice.Subject) : null;
        return new(actor, choice, government.Consents.SingleOrDefault(consent => consent.AgentId == actor && consent.Mandates == choice.Subject),
            office, change, extending ? TownArrangementRules.Parse(choice.Subject) : null, extending ? CivicBoard(world, town) : null);
    }

    internal static ActionCheck CheckGovernmentOffice(ITownWorld world, GovernmentOfficeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not
            { Council: not null, Government: { } government } town || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected personal office choice is no longer available");
        var consent = government.Consents.SingleOrDefault(consent => consent.AgentId == plan.Actor && consent.Mandates == plan.Choice.Subject);
        if (plan.Choice.Kind is GovernmentOfficeKind.Register or GovernmentOfficeKind.Withdraw)
        {
            var officeInPlay = TownArrangementRules.HasOffice(government.Arrangement) || government.Contest is not null ||
                government.Changes.Any(change => change.Status is "queued" or "voting" or "handover" && TownArrangementRules.HasOffice(change.Target));
            var firstVacancy = government.Offices.Count > 0 && government.Offices.All(office => office.HolderId is null) &&
                !government.Changes.Any(change => change.Status is "queued" or "voting" or "handover");
            return TownArrangementRules.IsMandates(plan.Choice.Subject) && consent == plan.Consent &&
                (plan.Choice.Kind == GovernmentOfficeKind.Withdraw ? consent is not null : consent is null && officeInPlay &&
                    (!firstVacancy || plan.Choice.Subject == TownArrangementRules.Mandates(government.Arrangement)))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected office consent or vacancy has changed");
        }
        if (plan.Choice.Kind == GovernmentOfficeKind.Resign)
            return plan.Office is { } held && government.Offices.Contains(held) && held.HolderId == plan.Actor
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected office term has ended");
        if (plan.Choice.Kind == GovernmentOfficeKind.ExtendNonLand)
            return plan.Office is { HolderId: not null, Mandates: "land" or "ordinary" } original &&
                government.Offices.Contains(original) && original.TermEndTick > world.WorldTick && government.Arrangement.NonLand == "none" &&
                plan.Arrangement == government.Arrangement with { NonLand = TownArrangementRules.Mayor } &&
                CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected incumbent term or additional duties have changed");
        var change = government.Changes.SingleOrDefault(change => change.Id == plan.Choice.Subject);
        return plan.Change?.NonLandExtension is { ConsentTick: null } selected && change?.NonLandExtension == selected &&
            change.Target == plan.Change.Target && change.RequestKey == plan.Change.RequestKey &&
            TownGovernmentRules.CanAcceptNonLandDuties(town.Council, government, change.Id, plan.Actor, TownAdults(world, town), world.WorldTick)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected additional duties or current term are no longer available");
    }

    private static void OfferGovernmentOffice(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, GovernmentOfficeKind kind, string subject, string value, string text, int priority)
    {
        var id = CivicAction(world, town.Id, GovernmentOfficeChoice.Verb(kind), subject, value);
        if (GovernmentOfficeChoice.Decode(id) is { } choice && PlanGovernmentOffice(world, actor, choice) is { } plan && CheckGovernmentOffice(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
