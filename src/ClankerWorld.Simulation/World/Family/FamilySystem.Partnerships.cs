using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static ActorCapabilityPolicy FamilyCareCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);

    internal static IEnumerable<SocietyRelationship> Partnerships(IFamilyPartnershipWorld world, string actor) => world.Society.Relationships.Where(item =>
        item.Type == SocietyRelationshipType.Partnership && (item.ProposerId == actor || item.TargetId == actor));

    internal static bool AvailablePartner(IFamilyPartnershipWorld world, string actor) => world.IsAdult(actor) &&
        !Partnerships(world, actor).Any(item => item.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted);

    internal static bool HasFamilyDecision(IFamilyPartnershipWorld world, string actor) => world.ReadyForBriefInteraction(actor) && Partnerships(world, actor).Any(item =>
        item.State == SocietyRelationshipState.Proposed && item.TargetId == actor);

    internal static bool CloseKin(IFamilyPartnershipWorld world, string actor, string other)
    {
        var parentage = world.Society.Relationships.Where(item =>
            item.Type == SocietyRelationshipType.BiologicalParentage &&
            item.State is not (SocietyRelationshipState.Proposed or SocietyRelationshipState.Rejected)).ToArray();
        static bool HasAncestor(IReadOnlyList<SocietyRelationship> edges, string descendant, string ancestor)
        {
            var pending = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Push(descendant);
            while (pending.TryPop(out var current))
            {
                if (!visited.Add(current)) continue;
                foreach (var edge in edges.Where(edge => edge.TargetId == current))
                {
                    if (edge.ProposerId == ancestor) return true;
                    pending.Push(edge.ProposerId);
                }
            }
            return false;
        }
        IEnumerable<string> ParentsOf(string child) => parentage.Where(item => item.TargetId == child).Select(item => item.ProposerId);
        bool Siblings(string first, string second) => ParentsOf(first).Intersect(ParentsOf(second), StringComparer.Ordinal).Any();
        bool AuntOrUncle(string elder, string younger) => ParentsOf(younger).Any(parent => parent != elder && Siblings(elder, parent));
        return HasAncestor(parentage, actor, other) || HasAncestor(parentage, other, actor) || Siblings(actor, other) ||
            AuntOrUncle(actor, other) || AuntOrUncle(other, actor);
    }

    internal static bool CanProposePartnership(IFamilyPartnershipWorld world, string actor, string other) => actor != other &&
        AvailablePartner(world, actor) && AvailablePartner(world, other) &&
        !CloseKin(world, actor, other) &&
        world.Society.GetAgent(actor).HouseholdId is not null &&
        world.Society.GetAgent(other).HouseholdId is not null &&
        !Partnerships(world, actor).Any(item => (item.ProposerId == other || item.TargetId == other) &&
            world.WorldTick - Math.Max(item.ProposedTick, item.EffectiveTick) < world.TicksPerDay) &&
        world.TrustScore(actor, other) > 0;

    internal static void MaintainPartnerships(IFamilyPartnershipWorld world)
    {
        foreach (var relationship in world.Society.Relationships.Where(item =>
                     item.Type == SocietyRelationshipType.Partnership && item.State == SocietyRelationshipState.Proposed).ToArray())
        {
            if (!world.IsAdult(relationship.ProposerId) || !world.IsAdult(relationship.TargetId) ||
                world.WorldTick - relationship.ProposedTick > 120)
            {
                world.RevokePartnership(relationship.Id, relationship.ProposerId);
                world.AppendEvent("partnership_expired", relationship.ProposerId);
            }
        }
    }

    internal static void AddFamilyCandidateOptions(IFamilyPartnershipWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (!world.HasSurvivalState || !world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor))
        {
            return;
        }
        foreach (var relationship in Partnerships(world, actor).Where(item => item.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted))
        {
            var other = relationship.ProposerId == actor ? relationship.TargetId : relationship.ProposerId;
            var name = world.Society.GetAgent(other).Name;
            if (relationship.State == SocietyRelationshipState.Proposed && relationship.TargetId == actor)
            {
                candidates.Add(new("partner_accept:" + relationship.Id, $"Accept {name}'s partnership proposal. This does not consent to parenthood.", 19));
                candidates.Add(new("partner_refuse:" + relationship.Id, $"Refuse {name}'s partnership proposal.", 70));
            }
            else
            {
                candidates.Add(new("partner_leave:" + relationship.Id, $"Withdraw from the partnership or proposal with {name}.", 110));
            }
        }
        foreach (var other in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).Where(other => CanProposePartnership(world, actor, other)))
        {
            candidates.Add(new("partner_propose:" + other,
                $"Propose a partnership with {world.Society.GetAgent(other).Name} after cooperating; they may refuse.", 65));
        }
    }

    internal static void CompletePartnershipAction(IFamilyPartnershipWorld world, PartnershipActionPlan plan)
    {
        var actor = plan.Actor;
        var target = plan.Choice.TargetId;
        switch (plan.Choice.Kind)
        {
            case PartnershipChoiceKind.Propose:
                var id = world.NewPartnershipId(actor, plan.PartnerId, world.WorldTick);
                world.ProposePartnership(new(id, 1, SocietyRelationshipType.Partnership, actor, plan.PartnerId, world.WorldTick, PrivacyClass: "public"));
                world.AppendEvent("partnership_proposed", actor);
                break;
            case PartnershipChoiceKind.Accept:
                world.AcceptPartnership(target, plan.Original!.Revision, actor);
                if (world.Society.GetRelationship(target).State == SocietyRelationshipState.Accepted)
                    world.AppendEvent("partnership_accepted", actor);
                break;
            case PartnershipChoiceKind.Refuse:
                world.RefusePartnership(target, plan.Original!.Revision, actor);
                world.AppendEvent("partnership_refused", actor);
                break;
            case PartnershipChoiceKind.Leave:
                world.RevokePartnership(target, actor);
                world.MaintainMarriages();
                world.AppendEvent("partnership_ended", actor);
                break;
        }
    }

    internal static PartnershipActionPlan? PlanPartnershipAction(IFamilyPartnershipWorld world, string actor, PartnershipChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        if (choice.Kind == PartnershipChoiceKind.Propose)
            return world.PhysicalAgents.Living.ContainsKey(choice.TargetId) ? new(actor, choice, choice.TargetId, null) : null;
        var relationship = Partnerships(world, actor).FirstOrDefault(item => item.Id == choice.TargetId);
        return relationship is null ? null : new(actor, choice,
            relationship.ProposerId == actor ? relationship.TargetId : relationship.ProposerId, relationship);
    }

    internal static ActionCheck CheckPartnershipActionPlan(IFamilyPartnershipWorld world, PartnershipActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? FamilyCareCapabilities);
        if (!capability.Allowed) return capability;
        if (plan.Choice.Kind == PartnershipChoiceKind.Propose)
            return world.PhysicalAgents.Living.ContainsKey(plan.PartnerId) && CanProposePartnership(world, plan.Actor, plan.PartnerId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that person is no longer available for a partnership proposal");
        if (plan.Original is not { } original ||
            !RelationshipPlan.Matches(Partnerships(world, plan.Actor).FirstOrDefault(item => item.Id == original.Id), original))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected partnership or consent has changed");
        var allowed = plan.Choice.Kind switch
        {
            PartnershipChoiceKind.Accept => original.State == SocietyRelationshipState.Proposed && original.TargetId == plan.Actor &&
                world.IsAdult(original.ProposerId) && !CloseKin(world, plan.Actor, original.ProposerId),
            PartnershipChoiceKind.Refuse => original.State == SocietyRelationshipState.Proposed && original.TargetId == plan.Actor,
            _ => original.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted &&
                !(original.State == SocietyRelationshipState.Proposed && original.TargetId == plan.Actor),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that partnership response is no longer available");
    }
}
