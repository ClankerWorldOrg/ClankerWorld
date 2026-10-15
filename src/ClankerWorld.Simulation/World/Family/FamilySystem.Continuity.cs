using System.Globalization;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    private static string ContinuityDetail(int eligibleCouples) =>
        "eligible_couples|" + eligibleCouples.ToString(CultureInfo.InvariantCulture) + "|threshold|" + ContinuityEligibleCoupleThreshold.ToString(CultureInfo.InvariantCulture);
    private static bool ActiveParenthood(ParenthoodPlan? plan) => plan?.Stage is "requested" or "preparing";
    internal void StartContinuityRule(IFamilyContinuityWorld world)
    {
        var couples = EligibleContinuityCouples(world).Count();
        if (couples >= ContinuityEligibleCoupleThreshold) return;
        CommitContinuity(new(true, []));
        world.AppendEvent("continuity_rule_on", ContinuityDetail(couples));
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
        .Where(pair => world.Society.GetAgent(pair.First).Status == SocietyAgentStatus.Active &&
            world.Society.GetAgent(pair.Second).Status == SocietyAgentStatus.Active && world.Partners(pair.First, pair.Second))
        .Distinct()
        .OrderBy(pair => pair.First, StringComparer.Ordinal)
        .ThenBy(pair => pair.Second, StringComparer.Ordinal);

    internal ContinuityCouple? ContinuityCoupleOf(IFamilyContinuityWorld world, string actor) => Continuity.Couples.FirstOrDefault(item =>
        item.FirstPartnerId == actor || item.SecondPartnerId == actor);

    internal ContinuityCouple? ContinuityCoupleFor(IFamilyContinuityWorld world, string owner, string partner) =>
        ContinuityCoupleOf(world, owner) is { } couple && (couple.FirstPartnerId == partner || couple.SecondPartnerId == partner)
            ? couple : null;

    internal static bool ContinuityAllowsPostponement(IFamilyContinuityWorld world, ContinuityCouple couple) => world.WorldTick < couple.DeadlineTick;

    internal bool ContinuityPlanDue(IFamilyContinuityWorld world, string owner, string partner) =>
        ContinuityCoupleFor(world, owner, partner) is { } couple && !ContinuityAllowsPostponement(world, couple);

    internal void MaintainContinuity(IFamilyContinuityWorld world)
    {
        var eligible = EligibleContinuityCouples(world).ToArray();
        var active = eligible.Length < ContinuityEligibleCoupleThreshold;
        if (active != Continuity.Active)
            world.AppendEvent(active ? "continuity_rule_on" : "continuity_rule_off", ContinuityDetail(eligible.Length));
        if (!active)
        {
            CommitContinuity(new(false, []));
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
        CommitContinuity(new(true, couples));
    }

    internal static void ProceedWithContinuityPlan(IFamilyContinuityWorld world, string first, string second)
    {
        var active = world.PhysicalAgents.Living.Values.Where(person => person.Parenthood is { } plan && ActiveParenthood(plan) &&
            (person.AgentId == first || person.AgentId == second || plan.PartnerId == first || plan.PartnerId == second)).ToArray();
        if (active.Length > 1) return;
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
        world.AppendEvent("continuity_plan_proceeded", owner);
    }
}
