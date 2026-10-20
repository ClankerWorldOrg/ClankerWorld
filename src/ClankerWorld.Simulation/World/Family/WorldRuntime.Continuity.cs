using System.Globalization;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// The low-population continuity rule. <paramref name="Active"/> records whether the rule was on at the
/// last check, so turning on or off is logged once; each eligible couple keeps the tick at which its
/// "not yet" ends and the child plan goes ahead.
/// </summary>
public sealed record PopulationContinuity(bool Active, IReadOnlyList<ContinuityCouple> Couples,
    [property: JsonRequired] IReadOnlyList<ContinuitySingle> Singles);

/// <summary>A single adult’s fixed deadline and optional partner preference.</summary>
public sealed record ContinuitySingle(string AgentId, long DeadlineTick, string? ChosenPartnerId = null, bool Postponed = false);

/// <summary>An eligible couple, with partner IDs in ordinal order, and when its child plan goes ahead.</summary>
public sealed record ContinuityCouple(string FirstPartnerId, string SecondPartnerId, long DeadlineTick);

public sealed partial class WorldRuntime
{
    // Provisional: four eligible couples retain the old eight-adult-partner threshold (#1266).
    private const int ContinuityEligibleCoupleThreshold = 4;
    private const int ContinuityPostponeDays = 2;

    private PopulationContinuity continuity = new(false, [], []);

    private long ContinuityPostponeTicks => ContinuityPostponeDays * (long)worldSystems.Config.TicksPerDay;

    private static string ContinuityDetail(int eligibleCouples) =>
        "eligible_couples|" + eligibleCouples.ToString(CultureInfo.InvariantCulture) +
        "|threshold|" + ContinuityEligibleCoupleThreshold.ToString(CultureInfo.InvariantCulture);

    /// <summary>Only accepted eligible partnerships count; unrelated single adults do not turn the rule off.</summary>
    private void StartContinuityRule()
    {
        var couples = EligibleContinuityCouples().Count();
        if (couples >= ContinuityEligibleCoupleThreshold) return;
        continuity = new(true, [], []);
        AppendEvent(new ContinuityRuleOnEventPayload(null, couples, ContinuityEligibleCoupleThreshold));
    }

    private bool HasInfant(string parent) => society.Checkpoint.Relationships.Any(item =>
        item.Type is SocietyRelationshipType.BiologicalParentage or SocietyRelationshipType.Caregiver &&
        item.State == SocietyRelationshipState.Accepted && item.ProposerId == parent &&
        agents.ContainsKey(item.TargetId) &&
        LifeStageRules.IsInfant(society.Checkpoint.GetAgent(item.TargetId).AgeBand));

    /// <summary>
    /// Living adult couples who could have children. Having an infant does not remove a partnership
    /// from the risk count; the no-infant check applies separately to holding a child plan.
    /// </summary>
    private IEnumerable<(string First, string Second)> EligibleContinuityCouples() => society.Checkpoint.Relationships
        .Where(item => item.Type == SocietyRelationshipType.Partnership && item.State == SocietyRelationshipState.Accepted)
        .Select(item => string.CompareOrdinal(item.ProposerId, item.TargetId) < 0
            ? (First: item.ProposerId, Second: item.TargetId)
            : (First: item.TargetId, Second: item.ProposerId))
        .Where(pair => Partners(pair.First, pair.Second))
        .Distinct()
        .OrderBy(pair => pair.First, StringComparer.Ordinal)
        .ThenBy(pair => pair.Second, StringComparer.Ordinal);

    private ContinuityCouple? ContinuityCoupleOf(string actor) => continuity.Couples.FirstOrDefault(item =>
        item.FirstPartnerId == actor || item.SecondPartnerId == actor);

    private ContinuitySingle? ContinuitySingleOf(string actor) => continuity.Singles.FirstOrDefault(single => single.AgentId == actor);

    private bool IsContinuitySingle(string actor) => CanHaveChildren(actor) &&
        society.Checkpoint.GetAgent(actor) is { Status: SocietyAgentStatus.Active, HouseholdId: not null } &&
        !Partnerships(actor).Any(item => item.State == SocietyRelationshipState.Accepted);

    private bool CanContinuityPair(string first, string second) =>
        IsContinuitySingle(first) && IsContinuitySingle(second) && EligibleContinuityParents(first, second);

    private void AddContinuityPartnerCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!continuity.Active || ContinuitySingleOf(actor) is not { } single || WorldTick >= single.DeadlineTick) return;
        foreach (var other in agents.Keys.Order(StringComparer.Ordinal).Where(other => other != single.ChosenPartnerId && CanContinuityPair(actor, other)))
            candidates.Add(new(new PartnershipChoice(PartnershipChoiceKind.Choose, other).Encode(),
                $"Choose {society.Checkpoint.GetAgent(other).Name} as your partner under the continuity rule. Mutual choices pair at the deadline.", 95));
        if (!single.Postponed)
            candidates.Add(new(new PartnershipChoice(PartnershipChoiceKind.Postpone, actor).Encode(),
                "Not yet: put off choosing a partner until your two days end; the continuity rule still pairs eligible adults then, so you may not refuse.", 99));
    }

    private void PairContinuitySingles()
    {
        while (EligibleContinuityCouples().Count() < ContinuityEligibleCoupleThreshold)
        {
            var due = continuity.Singles.Where(single => WorldTick >= single.DeadlineTick && IsContinuitySingle(single.AgentId)).ToArray();
            var pairs = due.SelectMany(first => due.Where(second => string.CompareOrdinal(first.AgentId, second.AgentId) < 0 &&
                    CanContinuityPair(first.AgentId, second.AgentId)), (first, second) => (First: first, Second: second))
                .OrderByDescending(pair => pair.First.ChosenPartnerId == pair.Second.AgentId && pair.Second.ChosenPartnerId == pair.First.AgentId)
                .ThenByDescending(pair => TrustScore(pair.First.AgentId, pair.Second.AgentId) + TrustScore(pair.Second.AgentId, pair.First.AgentId))
                .ThenBy(pair => pair.First.AgentId, StringComparer.Ordinal).ThenBy(pair => pair.Second.AgentId, StringComparer.Ordinal);
            if (pairs.FirstOrDefault() is not { First: { } first, Second: { } second }) return;
            // Use native proposal/acceptance, including its exclusive partnership and family-unit rules.
            // The continuity deadline replaces voluntary trust and proposal retry requirements.
            var proposal = Partnerships(first.AgentId).FirstOrDefault(item => item.State == SocietyRelationshipState.Proposed &&
                (item.ProposerId == second.AgentId || item.TargetId == second.AgentId))
                ?? ProposePartnership(first.AgentId, second.AgentId);
            AcceptPartnership(proposal);
            if (society.Checkpoint.GetRelationship(proposal.Id).State != SocietyRelationshipState.Accepted) return;
            foreach (var pending in society.Checkpoint.Relationships.Where(item => item.Type == SocietyRelationshipType.Partnership &&
                         item.State == SocietyRelationshipState.Proposed &&
                         (item.ProposerId == first.AgentId || item.TargetId == first.AgentId ||
                          item.ProposerId == second.AgentId || item.TargetId == second.AgentId)).ToArray())
                society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, pending.Id, pending.ProposerId));
            AppendEvent(new ContinuityPartnershipFormedEventPayload(first.AgentId, second.AgentId));
        }
    }

    private ContinuityCouple? ContinuityCoupleFor(string owner, string partner) =>
        ContinuityCoupleOf(owner) is { } couple && (couple.FirstPartnerId == partner || couple.SecondPartnerId == partner)
            ? couple : null;

    /// <summary>The couple may still say "not yet" to a plan; refusal is never offered while the rule holds them.</summary>
    private bool ContinuityAllowsPostponement(ContinuityCouple couple) => WorldTick < couple.DeadlineTick;

    /// <summary>The couple's two days are up, so their plan goes ahead rather than being put off or expiring.</summary>
    private bool ContinuityPlanDue(string owner, string partner) =>
        ContinuityCoupleFor(owner, partner) is { } couple && !ContinuityAllowsPostponement(couple);

    private void MaintainContinuity()
    {
        var eligible = EligibleContinuityCouples().ToArray();
        if (eligible.Length < ContinuityEligibleCoupleThreshold)
        {
            continuity = continuity with
            {
                Singles = agents.Keys.Order(StringComparer.Ordinal).Where(IsContinuitySingle)
                .Select(id => ContinuitySingleOf(id) ?? new(id, WorldTick + ContinuityPostponeTicks)).ToArray()
            };
            PairContinuitySingles();
            eligible = EligibleContinuityCouples().ToArray();
        }
        var active = eligible.Length < ContinuityEligibleCoupleThreshold;
        if (active != continuity.Active)
            AppendEvent(active ? new ContinuityRuleOnEventPayload(null, eligible.Length, ContinuityEligibleCoupleThreshold) : new ContinuityRuleOffEventPayload(null, eligible.Length, ContinuityEligibleCoupleThreshold));
        if (!active)
        {
            continuity = new(false, [], []);
            return;
        }
        var couples = new List<ContinuityCouple>();
        foreach (var (first, second) in eligible.Where(pair => !HasInfant(pair.First) && !HasInfant(pair.Second)))
        {
            var deadline = continuity.Couples.FirstOrDefault(item =>
                item.FirstPartnerId == first && item.SecondPartnerId == second)?.DeadlineTick ?? WorldTick + ContinuityPostponeTicks;
            couples.Add(new(first, second, deadline));
            if (WorldTick >= deadline) ProceedWithContinuityPlan(first, second);
        }
        continuity = new(true, couples, continuity.Singles.Where(single => IsContinuitySingle(single.AgentId)).ToArray());
    }

    /// <summary>Once "not yet" has run out, the couple's plan goes ahead as if both had agreed.</summary>
    private void ProceedWithContinuityPlan(string first, string second)
    {
        var active = agents.Values.Where(person => person.Parenthood is { } plan && ActiveParenthood(plan) &&
            (person.AgentId == first || person.AgentId == second || plan.PartnerId == first || plan.PartnerId == second)).ToArray();
        if (active.Length > 1) return;
        string owner;
        if (active.Length == 1)
        {
            var plan = active[0].Parenthood!;
            owner = active[0].AgentId;
            if (plan.Stage != "requested" || plan.PartnerId != (owner == first ? second : first)) return;
            SetParenthood(owner, plan with { Stage = "preparing" });
        }
        else
        {
            // The partner who asked most recently initiates; otherwise the first by ID, as a stable tie-break.
            owner = new[] { first, second }
                .OrderByDescending(id => agents[id].Parenthood is { } previous &&
                    previous.PartnerId == (id == first ? second : first) ? previous.RequestedTick : -1)
                .ThenBy(id => id, StringComparer.Ordinal).First();
            var partner = owner == first ? second : first;
            var previous = agents[owner].Parenthood;
            SetParenthood(owner, previous is { Stage: "postponed" } && previous.PartnerId == partner
                ? previous with { Stage = "preparing" }
                : new(partner, "preparing", WorldTick, WorldTick,
                    PrimaryCaregiverId: owner, IntendedHouseholdId: HouseholdFor(owner)));
            checkpointSchemaVersion = StateSchemaVersion;
        }
        AppendEvent(new ContinuityPlanProceededEventPayload(owner));
    }

    /// <summary>What a partner's own model is told about the rule; null when it does not apply to them.</summary>
    private string? ContinuityNote(string actor)
    {
        if (continuity.Active && ContinuitySingleOf(actor) is { } single)
            return BoundedText.Excerpt("Continuity rule: choose a single adult, not an elder or close kin. " +
                "You may say not yet for up to two world days but may not refuse. " +
                $"{Math.Max(0, (single.DeadlineTick - WorldTick) * 24 + worldSystems.Config.TicksPerDay - 1) / worldSystems.Config.TicksPerDay} hours left. Mutual choices pair first, else strongest relationship. " +
                (single.ChosenPartnerId is { } chosen ? $"Chosen: {society.Checkpoint.GetAgent(chosen).Name}."
                    : single.Postponed ? "You said not yet." : "No partner chosen."), 256);
        if (ContinuityCoupleOf(actor) is not { } couple) return null;
        var preparing = agents.Values.FirstOrDefault(person => person.Parenthood is { Stage: "preparing" } plan &&
            (person.AgentId == actor || plan.PartnerId == actor));
        if (preparing?.Parenthood?.PrimaryCaregiverId is { } caregiver)
        {
            var deadline = !ContinuityAllowsPostponement(couple) ? "Your two days are up. "
                : $"Your two days end in about {((couple.DeadlineTick - WorldTick) * 24 + worldSystems.Config.TicksPerDay - 1) / worldSystems.Config.TicksPerDay} hours. ";
            return "Continuity rule: you may put off a child for two days but may not refuse. " + deadline +
                "Preparing for parenthood. " + ParenthoodFoodNote(society.Checkpoint, caregiver, towns,
                    society.Checkpoint.GetAgent(actor).HouseholdId == society.Checkpoint.GetAgent(caregiver).HouseholdId);
        }
        var rule = $"Continuity rule: fewer than {ContinuityEligibleCoupleThreshold} adult couples who are not close relatives can have children. " +
            "You may put off having a child for up to two days but may not refuse.";
        if (!ContinuityAllowsPostponement(couple))
            return rule + " Your two days are up, so your child plan is going ahead; the birth still needs food.";
        var ticksPerDay = worldSystems.Config.TicksPerDay;
        var hours = ((couple.DeadlineTick - WorldTick) * 24 + ticksPerDay - 1) / ticksPerDay;
        return rule + $" Your two days end in about {hours.ToString(CultureInfo.InvariantCulture)} hours; then your child plan goes ahead.";
    }

    private static void ValidateContinuity(PopulationContinuity? saved, SocietyCheckpoint society, int schemaVersion)
    {
        if (schemaVersion < ContinuitySchemaVersion || saved?.Couples is null || saved.Singles is null)
            throw new InvalidDataException($"World schema {ContinuitySchemaVersion} requires the continuity rule state.");
        var known = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var postponeTicks = ContinuityPostponeDays * (long)society.Config.TicksPerWorldDay;
        string? previousSingle = null;
        foreach (var single in saved.Singles)
        {
            if (!saved.Active || single is null || !known.Contains(single.AgentId) ||
                previousSingle is not null && string.CompareOrdinal(previousSingle, single.AgentId) >= 0 ||
                single.DeadlineTick < postponeTicks || single.DeadlineTick > society.WorldTick + postponeTicks ||
                single.ChosenPartnerId is { } chosen && (single.Postponed || chosen == single.AgentId || !known.Contains(chosen)))
                throw new InvalidDataException("The saved continuity partner choices are invalid.");
            previousSingle = single.AgentId;
        }
        ContinuityCouple? previous = null;
        foreach (var couple in saved.Couples)
        {
            if (!saved.Active || couple is null || !known.Contains(couple.FirstPartnerId) || !known.Contains(couple.SecondPartnerId) ||
                string.CompareOrdinal(couple.FirstPartnerId, couple.SecondPartnerId) >= 0 ||
                previous is not null && (string.CompareOrdinal(previous.FirstPartnerId, couple.FirstPartnerId) > 0 ||
                    previous.FirstPartnerId == couple.FirstPartnerId &&
                    string.CompareOrdinal(previous.SecondPartnerId, couple.SecondPartnerId) >= 0) ||
                couple.DeadlineTick < postponeTicks || couple.DeadlineTick > society.WorldTick + postponeTicks)
                throw new InvalidDataException("The saved continuity rule state is invalid.");
            previous = couple;
        }
    }
}
