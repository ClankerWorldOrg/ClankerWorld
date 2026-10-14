using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static TownPolicyActionPlan? PlanTownPolicy(ITownWorld world, string actor, TownPolicyChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Government is not { } government) return null;
        if (choice.Kind == TownPolicyKind.BoatAccess)
            return new(actor, choice, null, choice.Target == "all" ? null : ResolveCivicAgentToken(world, choice.Target), null, null);
        if (!int.TryParse(choice.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var share)) return null;
        var law = government.Laws.SingleOrDefault(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).EstateDefault is not null);
        return new(actor, choice, share, null, law?.Id, law is null ? null : TownLawRules.Current(law));
    }

    internal static ActionCheck CheckTownPolicyPlan(ITownWorld world, TownPolicyActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not
            { Council: not null, Government: { } government } town || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town policy is no longer available to them");
        if (plan.Choice.Kind == TownPolicyKind.BoatAccess)
        {
            if (!world.HasCommunalBoatPort(town.Id) ||
                (plan.Visitor is null ? government.Laws.Any(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).BoatAccess is { VisitorId: null }) :
                    !world.Agents.TryGetValue(plan.Visitor, out var visitor) || town.ResidentIds.Contains(plan.Visitor, StringComparer.Ordinal) ||
                    !world.IsWithinInteractionRange(visitor.Position, world.Agents[plan.Actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
                    TownBoatAccessRules.Allows(town, plan.Visitor, world.WorldTick)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected visitor or communal boat permission has changed");
            return new();
        }
        var law = government.Laws.SingleOrDefault(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).EstateDefault is not null);
        if (plan.TownShare is not (0 or 25 or 50 or 75 or 100) || TownEstateDefaultRules.HasPending(government) || law?.Id != plan.LawId ||
            (law is null ? plan.Choice.Target != "adopt" || plan.Version is not null :
                plan.Version is not { } version || TownLawRules.Current(law) with { SiteTiles = version.SiteTiles } != version ||
                !TownLawRules.Current(law).SiteTiles.SequenceEqual(version.SiteTiles) ||
                plan.Choice.Target != law.Id + ":" + version.Version.ToString(CultureInfo.InvariantCulture) ||
                version.EstateDefault!.TownSharePercent == plan.TownShare ||
                town.Council.Notices.LastOrDefault(notice => notice.SubjectId == law.Id) is not { } latest ||
                !CivicHistory(town).Known(plan.Actor).Contains(latest.Id)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected default estate law or share has changed");
        return new();
    }

    internal static void OfferTownPolicy(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownPolicyChoice choice, string text, int priority)
    {
        if (PlanTownPolicy(world, actor, choice) is { } plan && CheckTownPolicyPlan(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
}
