using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static string ContinuityDetail(int eligibleCouples) =>
        "eligible_couples|" + eligibleCouples.ToString(CultureInfo.InvariantCulture) + "|threshold|" + ContinuityEligibleCoupleThreshold.ToString(CultureInfo.InvariantCulture);
    internal static bool ActiveParenthood(ParenthoodPlan? plan) => plan?.Stage is "requested" or "preparing";

    internal void StartContinuityRule(IFamilyContinuityWorld world)
    {
        var couples = EligibleContinuityCouples(world).Count();
        if (couples >= ContinuityEligibleCoupleThreshold) return;
        CommitContinuity(new(true, [], []));
        world.AppendEvent(new ContinuityRuleOnEventPayload(null, couples, ContinuityEligibleCoupleThreshold));
    }

    internal static bool HasInfant(IFamilyContinuityWorld world, string parent) => world.Society.Relationships.Any(item =>
        item.Type is SocietyRelationshipType.BiologicalParentage or SocietyRelationshipType.Caregiver &&
        item.State == SocietyRelationshipState.Accepted && item.ProposerId == parent &&
        world.PhysicalAgents.Living.ContainsKey(item.TargetId) &&
        LifeStageRules.IsInfant(world.Society.GetAgent(item.TargetId).AgeBand));

    internal static IEnumerable<(string First, string Second)> EligibleContinuityCouples(IFamilyContinuityWorld world) => world.Society.Relationships
        .Where(item => item.Type == SocietyRelationshipType.Partnership && item.State == SocietyRelationshipState.Accepted)
        .Select(item => string.CompareOrdinal(item.ProposerId, item.TargetId) < 0
            ? (First: item.ProposerId, Second: item.TargetId)
            : (First: item.TargetId, Second: item.ProposerId))
        .Where(pair => world.Partners(pair.First, pair.Second))
        .Distinct()
        .OrderBy(pair => pair.First, StringComparer.Ordinal)
        .ThenBy(pair => pair.Second, StringComparer.Ordinal);

    internal ContinuityCouple? ContinuityCoupleOf(IFamilyContinuityWorld world, string actor) => Continuity.Couples.FirstOrDefault(item =>
        item.FirstPartnerId == actor || item.SecondPartnerId == actor);

    internal ContinuitySingle? ContinuitySingleOf(IFamilyContinuityWorld world, string actor) => Continuity.Singles.FirstOrDefault(single => single.AgentId == actor);

    internal static bool IsContinuitySingle(IFamilyContinuityWorld world, string actor) => world.CanHaveChildren(actor) &&
        world.Society.GetAgent(actor) is { Status: SocietyAgentStatus.Active, HouseholdId: not null } &&
        !world.Partnerships(actor).Any(item => item.State == SocietyRelationshipState.Accepted);

    internal static bool CanContinuityPair(IFamilyContinuityWorld world, string first, string second) =>
        IsContinuitySingle(world, first) && IsContinuitySingle(world, second) && world.EligibleContinuityParents(first, second);

    internal void AddContinuityPartnerCandidates(IFamilyContinuityWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (!Continuity.Active || ContinuitySingleOf(world, actor) is not { } single || world.WorldTick >= single.DeadlineTick) return;
        foreach (var other in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).Where(other => other != single.ChosenPartnerId && CanContinuityPair(world, actor, other)))
            candidates.Add(new(new PartnershipChoice(PartnershipChoiceKind.Choose, other).Encode(),
                $"Choose {world.Society.GetAgent(other).Name} as your partner under the Continuity rule. Mutual choices pair at the deadline.", 95));
        if (!single.Postponed)
            candidates.Add(new(new PartnershipChoice(PartnershipChoiceKind.Postpone, actor).Encode(),
                "Not yet: put off choosing a partner until your two days end; the Continuity rule still pairs eligible adults then, so you may not refuse.", 99));
    }

    internal void PairContinuitySingles(IFamilyContinuityWorld world)
    {
        while (EligibleContinuityCouples(world).Count() < ContinuityEligibleCoupleThreshold)
        {
            var due = Continuity.Singles.Where(single => world.WorldTick >= single.DeadlineTick && IsContinuitySingle(world, single.AgentId)).ToArray();
            var pairs = due.SelectMany(first => due.Where(second => string.CompareOrdinal(first.AgentId, second.AgentId) < 0 &&
                    CanContinuityPair(world, first.AgentId, second.AgentId)), (first, second) => (First: first, Second: second))
                .OrderByDescending(pair => pair.First.ChosenPartnerId == pair.Second.AgentId && pair.Second.ChosenPartnerId == pair.First.AgentId)
                .ThenByDescending(pair => world.TrustScore(pair.First.AgentId, pair.Second.AgentId) + world.TrustScore(pair.Second.AgentId, pair.First.AgentId))
                .ThenBy(pair => pair.First.AgentId, StringComparer.Ordinal).ThenBy(pair => pair.Second.AgentId, StringComparer.Ordinal);
            if (pairs.FirstOrDefault() is not { First: { } first, Second: { } second }) return;
            // Use native proposal/acceptance, including its exclusive partnership and family-unit rules.
            // The Continuity deadline replaces voluntary trust and proposal retry requirements.
            var proposal = world.Partnerships(first.AgentId).FirstOrDefault(item => item.State == SocietyRelationshipState.Proposed &&
                (item.ProposerId == second.AgentId || item.TargetId == second.AgentId))
                ?? world.ProposePartnership(first.AgentId, second.AgentId);
            world.AcceptPartnership(proposal);
            if (world.Society.GetRelationship(proposal.Id).State != SocietyRelationshipState.Accepted) return;
            foreach (var pending in world.Society.Relationships.Where(item => item.Type == SocietyRelationshipType.Partnership &&
                         item.State == SocietyRelationshipState.Proposed &&
                         (item.ProposerId == first.AgentId || item.TargetId == first.AgentId ||
                          item.ProposerId == second.AgentId || item.TargetId == second.AgentId)).ToArray())
                world.RevokePartnership(pending.Id, pending.ProposerId);
            world.AppendEvent(new ContinuityPartnershipFormedEventPayload(first.AgentId, second.AgentId));
        }
    }

    internal ContinuityCouple? ContinuityCoupleFor(IFamilyContinuityWorld world, string owner, string partner) =>
        ContinuityCoupleOf(world, owner) is { } couple && (couple.FirstPartnerId == partner || couple.SecondPartnerId == partner)
            ? couple : null;

    internal static bool ContinuityAllowsPostponement(IFamilyContinuityWorld world, ContinuityCouple couple) => world.WorldTick < couple.DeadlineTick;

    internal bool ContinuityPlanDue(IFamilyContinuityWorld world, string owner, string partner) =>
        ContinuityCoupleFor(world, owner, partner) is { } couple && !ContinuityAllowsPostponement(world, couple);

    internal void MaintainContinuity(IFamilyContinuityWorld world)
    {
        var eligible = EligibleContinuityCouples(world).ToArray();
        if (eligible.Length < ContinuityEligibleCoupleThreshold)
        {
            // Keep the original transient singles projection; final state is checked below.
            Continuity = Continuity with
            {
                Singles = world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).Where(id => IsContinuitySingle(world, id))
                .Select(id => ContinuitySingleOf(world, id) ?? new(id, world.WorldTick + world.ContinuityPostponeTicks)).ToArray()
            };
            PairContinuitySingles(world);
            eligible = EligibleContinuityCouples(world).ToArray();
        }
        var active = eligible.Length < ContinuityEligibleCoupleThreshold;
        if (active != Continuity.Active)
            world.AppendEvent(active ? new ContinuityRuleOnEventPayload(null, eligible.Length, ContinuityEligibleCoupleThreshold) : new ContinuityRuleOffEventPayload(null, eligible.Length, ContinuityEligibleCoupleThreshold));
        if (!active)
        {
            CommitContinuity(new(false, [], []));
            return;
        }
        var couples = new List<ContinuityCouple>();
        foreach (var (first, second) in eligible.Where(pair => !HasInfant(world, pair.First) && !HasInfant(world, pair.Second)))
        {
            var deadline = Continuity.Couples.FirstOrDefault(item =>
                item.FirstPartnerId == first && item.SecondPartnerId == second)?.DeadlineTick ?? world.WorldTick + world.ContinuityPostponeTicks;
            couples.Add(new(first, second, deadline));
            if (world.WorldTick >= deadline) ProceedWithContinuityPlan(world, first, second);
        }
        CommitContinuity(new(true, couples, Continuity.Singles.Where(single => IsContinuitySingle(world, single.AgentId)).ToArray()));
    }

    internal static void ProceedWithContinuityPlan(IFamilyContinuityWorld world, string first, string second)
    {
        var active = world.PhysicalAgents.Living.Values.Where(person => person.Parenthood is { } plan && ActiveParenthood(plan) &&
            (person.AgentId == first || person.AgentId == second || plan.PartnerId == first || plan.PartnerId == second)).ToArray();
        if (active is [_, _, ..]) return;
        string owner;
        if (active.Length == 1)
        {
            var plan = active[0].Parenthood!;
            owner = active[0].AgentId;
            if (plan.Stage != "requested" || plan.PartnerId != (owner == first ? second : first)) return;
            world.SetParenthood(owner, plan with { Stage = "preparing" });
        }
        else
        {
            // The partner who asked most recently initiates; otherwise the first by ID, as a stable tie-break.
            owner = new[] { first, second }
                .OrderByDescending(id => world.PhysicalAgents.Living[id].Parenthood is { } previous &&
                    previous.PartnerId == (id == first ? second : first) ? previous.RequestedTick : -1)
                .ThenBy(id => id, StringComparer.Ordinal).First();
            var partner = owner == first ? second : first;
            var previous = world.PhysicalAgents.Living[owner].Parenthood;
            world.SetParenthood(owner, previous is { Stage: "postponed" } && previous.PartnerId == partner
                ? previous with { Stage = "preparing" }
                : new(partner, "preparing", world.WorldTick, world.WorldTick,
                    PrimaryCaregiverId: owner, IntendedHouseholdId: world.HouseholdFor(owner)));
            world.MarkCurrentSchema();
        }
        world.AppendEvent(new ContinuityPlanProceededEventPayload(owner));
    }
}
