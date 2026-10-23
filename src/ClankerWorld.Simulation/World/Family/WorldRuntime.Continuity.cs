using System.Globalization;
using System.Text.Json.Serialization;
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
    private const int ContinuityEligibleCoupleThreshold = FamilySystem.ContinuityEligibleCoupleThreshold;
    private const int ContinuityPostponeDays = FamilySystem.ContinuityPostponeDays;

    private FamilySystem familySystem = new(new(false, [], []));
    private PopulationContinuity continuity => familySystem.Continuity;

    private long ContinuityPostponeTicks => ContinuityPostponeDays * (long)worldSystems.Config.TicksPerDay;

    private static string ContinuityDetail(int eligibleCouples) =>
        "eligible_couples|" + eligibleCouples.ToString(CultureInfo.InvariantCulture) +
        "|threshold|" + ContinuityEligibleCoupleThreshold.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a partner's own model is told about the rule; null when it does not apply to them.</summary>
    private string? ContinuityNote(string actor)
    {
        if (continuity.Active && this.familySystem.ContinuitySingleOf(this.FamilyContinuityWorld, actor) is { } single)
            return BoundedText.Excerpt("Continuity rule: choose a single adult, not an elder or close kin. " +
                "You may say not yet for up to two world days but may not refuse. " +
                $"{Math.Max(0, (single.DeadlineTick - WorldTick) * 24 + worldSystems.Config.TicksPerDay - 1) / worldSystems.Config.TicksPerDay} hours left. Mutual choices pair first, else strongest relationship. " +
                (single.ChosenPartnerId is { } chosen ? $"Chosen: {society.Checkpoint.GetAgent(chosen).Name}."
                    : single.Postponed ? "You said not yet." : "No partner chosen."), 256);
        if (this.familySystem.ContinuityCoupleOf(this.FamilyContinuityWorld, actor) is not { } couple) return null;
        var preparing = agents.Values.FirstOrDefault(person => person.Parenthood is { Stage: "preparing" } plan &&
            (person.AgentId == actor || plan.PartnerId == actor));
        if (preparing?.Parenthood?.PrimaryCaregiverId is { } caregiver)
        {
            var deadline = !FamilySystem.ContinuityAllowsPostponement(this.FamilyContinuityWorld, couple) ? "Your two days are up. "
                : $"Your two days end in about {((couple.DeadlineTick - WorldTick) * 24 + worldSystems.Config.TicksPerDay - 1) / worldSystems.Config.TicksPerDay} hours. ";
            return "Continuity rule: you may put off a child for two days but may not refuse. " + deadline +
                "Preparing for parenthood. " + ParenthoodFoodNote(society.Checkpoint, caregiver, towns,
                    society.Checkpoint.GetAgent(actor).HouseholdId == society.Checkpoint.GetAgent(caregiver).HouseholdId);
        }
        var rule = $"Continuity rule: fewer than {ContinuityEligibleCoupleThreshold} adult couples who are not close relatives can have children. " +
            "You may put off having a child for up to two days but may not refuse.";
        if (!FamilySystem.ContinuityAllowsPostponement(this.FamilyContinuityWorld, couple))
            return rule + " Your two days are up, so your child plan is going ahead; the birth still needs food.";
        var ticksPerDay = worldSystems.Config.TicksPerDay;
        var hours = ((couple.DeadlineTick - WorldTick) * 24 + ticksPerDay - 1) / ticksPerDay;
        return rule + $" Your two days end in about {hours.ToString(CultureInfo.InvariantCulture)} hours; then your child plan goes ahead.";
    }

    private static void ValidateContinuity(PopulationContinuity? saved, SocietyCheckpoint society, int schemaVersion)
    {
        if (schemaVersion < ContinuitySchemaVersion || saved?.Couples is null || saved.Singles is null)
            throw new InvalidDataException($"World schema {ContinuitySchemaVersion} requires the continuity rule state.");
        FamilySystem.ValidateLocalContinuity(saved);
        var known = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var postponeTicks = ContinuityPostponeDays * (long)society.Config.TicksPerWorldDay;
        foreach (var single in saved.Singles)
            if (!known.Contains(single.AgentId) ||
                single.DeadlineTick < postponeTicks || single.DeadlineTick > society.WorldTick + postponeTicks ||
                single.ChosenPartnerId is { } chosen && !known.Contains(chosen))
                throw new InvalidDataException("The saved continuity partner choices are invalid.");
        foreach (var couple in saved.Couples)
            if (!known.Contains(couple.FirstPartnerId) || !known.Contains(couple.SecondPartnerId) ||
                couple.DeadlineTick < postponeTicks || couple.DeadlineTick > society.WorldTick + postponeTicks)
                throw new InvalidDataException("The saved continuity rule state is invalid.");
    }
}
