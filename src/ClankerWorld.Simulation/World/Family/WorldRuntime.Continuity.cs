using System.Globalization;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// The low-population continuity rule. <paramref name="Active"/> records whether the rule was on at the
/// last check, so turning on or off is logged once; each eligible couple keeps the tick at which its
/// "not yet" ends and the child plan goes ahead.
/// </summary>
public sealed record PopulationContinuity(bool Active, IReadOnlyList<ContinuityCouple> Couples);

/// <summary>An eligible couple, with partner IDs in ordinal order, and when its child plan goes ahead.</summary>
public sealed record ContinuityCouple(string FirstPartnerId, string SecondPartnerId, long DeadlineTick);

public sealed partial class WorldRuntime
{
    // Provisional: four eligible couples retain the old eight-adult-partner threshold (#1266).
    private const int ContinuityEligibleCoupleThreshold = FamilySystem.ContinuityEligibleCoupleThreshold;
    private const int ContinuityPostponeDays = FamilySystem.ContinuityPostponeDays;

    private FamilySystem familySystem = new(new(false, []));
    private PopulationContinuity continuity => familySystem.Continuity;

    private long ContinuityPostponeTicks => ContinuityPostponeDays * (long)worldSystems.Config.TicksPerDay;

    private static string ContinuityDetail(int eligibleCouples) => FamilySystem.ContinuityDetail(eligibleCouples);

    /// <summary>Only accepted eligible partnerships count; unrelated single adults do not turn the rule off.</summary>
    private void StartContinuityRule() =>
        familySystem.StartContinuityRule(FamilyContinuityWorld);

    private bool HasInfant(string parent) =>
        FamilySystem.HasInfant(FamilyContinuityWorld, parent);

    /// <summary>
    /// Living adult couples who could have children. Having an infant does not remove a partnership
    /// from the risk count; the no-infant check applies separately to holding a child plan.
    /// The rule only reads accepted partnerships;
    /// it never proposes or accepts one.
    /// </summary>
    private IEnumerable<(string First, string Second)> EligibleContinuityCouples() =>
        FamilySystem.EligibleContinuityCouples(FamilyContinuityWorld);

    private ContinuityCouple? ContinuityCoupleOf(string actor) =>
        familySystem.ContinuityCoupleOf(FamilyContinuityWorld, actor);

    private ContinuityCouple? ContinuityCoupleFor(string owner, string partner) =>
        familySystem.ContinuityCoupleFor(FamilyContinuityWorld, owner, partner);

    /// <summary>The couple may still say "not yet" to a plan; refusal is never offered while the rule holds them.</summary>
    private bool ContinuityAllowsPostponement(ContinuityCouple couple) =>
        FamilySystem.ContinuityAllowsPostponement(FamilyContinuityWorld, couple);

    /// <summary>The couple's two days are up, so their plan goes ahead rather than being put off or expiring.</summary>
    private bool ContinuityPlanDue(string owner, string partner) =>
        familySystem.ContinuityPlanDue(FamilyContinuityWorld, owner, partner);

    private void MaintainContinuity() =>
        familySystem.MaintainContinuity(FamilyContinuityWorld);

    /// <summary>Once "not yet" has run out, the couple's plan goes ahead as if both had agreed.</summary>
    private void ProceedWithContinuityPlan(string first, string second) =>
        FamilySystem.ProceedWithContinuityPlan(FamilyContinuityWorld, first, second);

    /// <summary>What a partner's own model is told about the rule; null when it does not apply to them.</summary>
    private string? ContinuityNote(string actor)
    {
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
        if (schemaVersion < ContinuitySchemaVersion || saved?.Couples is null)
            throw new InvalidDataException($"World schema {ContinuitySchemaVersion} requires the continuity rule state.");
        FamilySystem.ValidateLocalContinuity(saved);
        var known = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var postponeTicks = ContinuityPostponeDays * (long)society.Config.TicksPerWorldDay;
        foreach (var couple in saved.Couples)
        {
            if (!known.Contains(couple.FirstPartnerId) || !known.Contains(couple.SecondPartnerId) ||
                couple.DeadlineTick < postponeTicks || couple.DeadlineTick > society.WorldTick + postponeTicks)
                throw new InvalidDataException("The saved continuity rule state is invalid.");
        }
    }
}
