using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool IsAdult(string actor) => AgentQueries.IsAdult(agentWorld, actor);

    private IEnumerable<SocietyRelationship> Partnerships(string actor) => society.Checkpoint.Relationships.Where(item =>
        item.Type == SocietyRelationshipType.Partnership && (item.ProposerId == actor || item.TargetId == actor));

    private bool AvailablePartner(string actor) => IsAdult(actor) &&
        !Partnerships(actor).Any(item => item.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted);

    private bool HasFamilyDecision(string actor) => ReadyForBriefInteraction(actor) && Partnerships(actor).Any(item =>
        item.State == SocietyRelationshipState.Proposed && item.TargetId == actor);

    // Close relatives cannot become partners or plan a child: parents and children, grandparents and grandchildren
    // at any depth, full and half siblings, and aunts or uncles with their nieces or nephews. Only biological
    // parentage counts, so first cousins, shared households and caregivers do not.
    private bool CloseKin(string actor, string other)
    {
        var parentage = society.Checkpoint.Relationships.Where(item =>
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

    private bool CanProposePartnership(string actor, string other) => actor != other &&
        AvailablePartner(actor) && AvailablePartner(other) &&
        !CloseKin(actor, other) &&
        society.Checkpoint.GetAgent(actor).HouseholdId is not null &&
        society.Checkpoint.GetAgent(other).HouseholdId is not null &&
        !Partnerships(actor).Any(item => (item.ProposerId == other || item.TargetId == other) &&
            WorldTick - Math.Max(item.ProposedTick, item.EffectiveTick) < worldSystems.Config.TicksPerDay) &&
        TrustScore(actor, other) > 0;

    private void MaintainPartnerships()
    {
        foreach (var relationship in society.Checkpoint.Relationships.Where(item =>
                     item.Type == SocietyRelationshipType.Partnership && item.State == SocietyRelationshipState.Proposed).ToArray())
        {
            if (!IsAdult(relationship.ProposerId) || !IsAdult(relationship.TargetId) ||
                WorldTick - relationship.ProposedTick > 120)
            {
                society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, relationship.Id, relationship.ProposerId));
                AppendEvent(new PartnershipExpiredEventPayload(relationship.ProposerId));
            }
        }
    }

    private void AddFamilyCandidateOptions(List<CognitionCandidate> candidates, string actor)
    {
        if (survivalState is null || !IsAdult(actor) || !ReadyForBriefInteraction(actor))
        {
            return;
        }
        AddContinuityPartnerCandidates(candidates, actor);
        foreach (var relationship in Partnerships(actor).Where(item => item.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted))
        {
            var other = relationship.ProposerId == actor ? relationship.TargetId : relationship.ProposerId;
            var name = society.Checkpoint.GetAgent(other).Name;
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
        foreach (var other in agents.Keys.Order(StringComparer.Ordinal).Where(other => CanProposePartnership(actor, other)))
        {
            candidates.Add(new("partner_propose:" + other,
                $"Propose a partnership with {society.Checkpoint.GetAgent(other).Name} after cooperating; they may refuse.", 65));
        }
    }

    private void ApplyFamilyCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as PartnershipActionPlan ?? (PartnershipChoice.Decode(candidate) is { } choice ? PlanPartnershipAction(actor, choice) : null);
        if (plan is not null) ApplyPartnershipActionPlan(plan);
    }

    private void CompletePartnershipAction(PartnershipActionPlan plan)
    {
        var actor = plan.Actor;
        var target = plan.Choice.TargetId;
        switch (plan.Choice.Kind)
        {
            case PartnershipChoiceKind.Choose:
            case PartnershipChoiceKind.Postpone:
                continuity = continuity with
                {
                    Singles = continuity.Singles.Select(single => single.AgentId == actor
                    ? single with
                    {
                        ChosenPartnerId = plan.Choice.Kind == PartnershipChoiceKind.Choose ? plan.PartnerId : null,
                        Postponed = plan.Choice.Kind == PartnershipChoiceKind.Postpone
                    }
                    : single).ToArray()
                };
                break;
            case PartnershipChoiceKind.Propose:
                ProposePartnership(actor, plan.PartnerId);
                break;
            case PartnershipChoiceKind.Accept:
                AcceptPartnership(plan.Original!);
                break;
            case PartnershipChoiceKind.Refuse:
                society.Apply(checkpoint => SocietyRules.RefuseRelationship(checkpoint, target, plan.Original!.Revision, actor));
                AppendEvent(new PartnershipRefusedEventPayload(actor));
                break;
            case PartnershipChoiceKind.Leave:
                society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, target, actor));
                MaintainMarriages();
                AppendEvent(new PartnershipEndedEventPayload(actor));
                break;
        }
    }

    private SocietyRelationship ProposePartnership(string actor, string partner)
    {
        var id = NewPartnershipId(actor, partner, WorldTick);
        society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
            new(id, 1, SocietyRelationshipType.Partnership, actor, partner, WorldTick, PrivacyClass: "public")));
        AppendEvent(new PartnershipProposedEventPayload(actor));
        return society.Checkpoint.GetRelationship(id);
    }

    private void AcceptPartnership(SocietyRelationship proposal)
    {
        society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, proposal.Id, proposal.Revision, proposal.TargetId));
        if (society.Checkpoint.GetRelationship(proposal.Id).State == SocietyRelationshipState.Accepted)
            AppendEvent(new PartnershipAcceptedEventPayload(proposal.TargetId));
    }
}
