using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static TownLawActionPlan? PlanTownLaw(ITownWorld world, string actor, TownLawChoice choice)
    {
        var town = world.Towns.SingleOrDefault(town => town.Id == choice.TownId);
        if (town?.Council is null || town.Government is null) return null;
        if (choice.Kind == TownLawChoiceKind.Adopt)
        {
            var scope = choice.Target.StartsWith("site:", StringComparison.Ordinal) ? TownLawRules.Site : choice.Target;
            if (!TownLawRules.IsScope(scope)) return null;
            var site = scope == TownLawRules.Site ? TownLawRules.SiteAround(world.Map, world.Agents[actor].Position, town.Id, world.TownLandTitles) : [];
            return new(actor, choice, scope, site, null);
        }
        var law = town.Government.Laws.SingleOrDefault(law => law.Id == choice.Target);
        return law is null ? null : new(actor, choice, TownLawRules.Current(law).Scope,
            TownLawRules.Current(law).SiteTiles.ToArray(), TownLawRules.Current(law));
    }

    internal static TownLawActionPlan BindTownLawText(TownLawActionPlan plan, string? text) =>
        plan.Text is not null || plan.Choice.Kind == TownLawChoiceKind.Repeal ? plan : plan with { Text = text };

    internal static ActionCheck CheckTownLawPlan(ITownWorld world, TownLawActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null, Government: { } government } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town law proposal is no longer available to them");
        if (plan.Choice.Kind == TownLawChoiceKind.Adopt)
        {
            if (!TownLawRules.IsScope(plan.Scope) || plan.Scope == TownLawRules.Site &&
                (plan.Choice.Target != SiteToken(world.Agents[plan.Actor].Position) || plan.SiteTiles.Count == 0 ||
                 !TownLawRules.SiteAround(world.Map, world.Agents[plan.Actor].Position, town.Id, world.TownLandTitles).SequenceEqual(plan.SiteTiles)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected law site or scope has changed");
        }
        else
        {
            var law = government.Laws.SingleOrDefault(law => law.Id == plan.Choice.Target);
            if (law is null || !TownLawRules.IsInForce(law) || plan.Version is not { } version ||
                TownLawRules.Current(law) with { SiteTiles = version.SiteTiles } != version ||
                !TownLawRules.Current(law).SiteTiles.SequenceEqual(version.SiteTiles) ||
                plan.Choice.VersionToken != CivicAgentToken(version.Version.ToString(CultureInfo.InvariantCulture)) ||
                plan.Choice.Kind == TownLawChoiceKind.Amend && (version.BoatAccess is not null || version.EstateDefault is not null) ||
                town.Council!.Notices.LastOrDefault(notice => notice.SubjectId == law.Id) is not { } latest ||
                !CivicHistory(town).Known(plan.Actor).Contains(latest.Id))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected law version is no longer available");
        }
        if (plan.Text is { } text && (!TownLawRules.TryParse(text, out var subject, out var rule) ||
                plan.Choice.Kind == TownLawChoiceKind.Amend && plan.Version is { } original && original.Subject == subject && original.Rule == rule))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected law wording is unavailable or unchanged");
        return new();
    }

    private static void OfferTownLaw(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLawChoiceKind kind, string target, string version, string text, int priority)
    {
        var id = CivicAction(world, town.Id, TownLawChoice.Verb(kind), target, version);
        if (TownLawChoice.Decode(id) is { } choice && PlanTownLaw(world, actor, choice) is { } plan && CheckTownLawPlan(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
