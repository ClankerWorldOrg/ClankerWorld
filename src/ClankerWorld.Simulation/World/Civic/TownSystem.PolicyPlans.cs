using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy FoodPolicyCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal FoodPolicyActionPlan? PlanFoodPolicy(string actor, string candidate)
    {
        if (State.FoodStewardship is not { } state) return null;
        var propose = candidate.StartsWith("food_policy_propose:", StringComparison.Ordinal);
        if (!propose && candidate is not ("food_policy_vote_yes" or "food_policy_vote_no")) return null;
        var policy = propose ? candidate["food_policy_propose:".Length..] : state.Ballot?.Policy;
        return policy is null ? null : new(actor, new(policy, propose ? null : candidate == "food_policy_vote_yes"),
            state.StewardId, state.FoodPolicy, state.LastResolutionTick, state.Ballot);
    }

    internal ActionCheck CheckFoodPolicy(ICivicWorld world, FoodPolicyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? FoodPolicyCapabilities);
        if (!capability.Allowed) return capability;
        if (State.FoodStewardship is not { } state || state.StewardId != plan.StewardId || state.FoodPolicy != plan.CurrentPolicy ||
            state.LastResolutionTick != plan.LastResolutionTick || !FoodPolicyVoters(world).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shared-food policy or voting authority has changed");
        if (plan.Choice.Approve is null)
            return state.Ballot is null && ProposedFoodPolicy(world, plan.Actor) == plan.Choice.Policy ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected policy proposal is no longer available");
        return state.Ballot is { } ballot && plan.Ballot is { } original && ballot.Policy == original.Policy &&
            ballot.ProposedTick == original.ProposedTick && ballot.ExpiryTick == original.ExpiryTick &&
            ballot.Electorate.SequenceEqual(original.Electorate) && ballot.Electorate.Contains(plan.Actor, StringComparer.Ordinal) &&
            !ballot.Approvals.Contains(plan.Actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(plan.Actor, StringComparer.Ordinal)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the original ballot or their personal response has changed");
    }
    internal static TownPolicyActionPlan? PlanTownPolicy(ITownWorld world, string actor, TownPolicyChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Government is not { } government) return null;
        if (choice.Kind == TownPolicyKind.BoatAccess)
            return new(actor, choice, null, choice.Target == "all" ? null : ResolveCivicAgentToken(world, choice.Target), null, null);
        if (!int.TryParse(choice.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var share)) return null;
        var law = government.Laws.SingleOrDefault(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).EstateDefault is not null);
        return new(actor, choice, share, null, law?.Id, law is null ? null : TownLawRules.Current(law));
    }

    internal static ActionCheck CheckTownPolicyPlan(ITownWorld world, TownPolicyActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
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
    internal static CivicNoticeActionPlan? PlanCivicNotice(ITownWorld world, string actor, CivicNoticeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: { } council } town) return null;
        var reading = choice.RecipientToken is null;
        var known = CivicHistory(town).Known(actor);
        return new(actor, choice, reading ? actor : ResolveCivicAgentToken(world, choice.RecipientToken!),
            reading ? CivicBoard(world, town) : null, council.Notices.Where(notice => reading || known.Contains(notice.Id)).ToArray());
    }

    internal static ActionCheck CheckCivicNoticePlan(ITownWorld world, CivicNoticeActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { Council: { } council } town ||
            !world.Agents.TryGetValue(plan.Recipient, out var recipient) ||
            !plan.ReadsCurrentBoard && plan.Notices.Any(notice => !council.Notices.Contains(notice)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected notices or recipient are no longer available");
        var history = CivicHistory(town);
        var reading = plan.Choice.RecipientToken is null;
        if (reading && (CivicBoard(world, town) != plan.NoticePlace || plan.NoticePlace is not { } board ||
                !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, board, WorldRuntime.CivicSharedRules.ResourceInteractionRange)) ||
            !reading && (plan.Recipient == plan.Actor ||
                !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, recipient.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
                plan.Notices.Any(notice => !history.Known(plan.Actor).Contains(notice.Id))))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected notices cannot be read or relayed here now");
        return (plan.ReadsCurrentBoard ? council.Notices : plan.Notices).Any(notice => !history.Known(plan.Recipient).Contains(notice.Id))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the recipient already knows those selected notices");
    }

    private static void OfferCivicNotice(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        string? recipient, string text, int priority)
    {
        var id = CivicAction(world, town.Id, recipient is null ? "read" : "relay", recipient ?? "");
        if (CivicNoticeChoice.Decode(id) is { } choice && PlanCivicNotice(world, actor, choice) is { } plan &&
            CheckCivicNoticePlan(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
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

    internal static ActionCheck CheckTownLawPlan(ITownWorld world, TownLawActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
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
