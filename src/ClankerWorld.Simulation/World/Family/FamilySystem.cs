
namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem(PopulationContinuity continuity)
{
    internal PopulationContinuity Continuity { get; private set; } = continuity;
    // Provisional: four eligible couples retain the old eight-adult-partner threshold (#1266).
    internal const int ContinuityEligibleCoupleThreshold = 4;
    internal const int ContinuityPostponeDays = 2;

    private void CommitContinuity(PopulationContinuity next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalContinuity(next);
        Continuity = next;
    }

    internal static void ValidateLocalContinuity(PopulationContinuity saved)
    {
        string? previousSingle = null;
        foreach (var single in saved.Singles)
        {
            if (!saved.Active || single is null || string.IsNullOrWhiteSpace(single.AgentId) ||
                previousSingle is not null && string.CompareOrdinal(previousSingle, single.AgentId) >= 0 ||
                single.DeadlineTick < 0 ||
                single.ChosenPartnerId is { } chosen && (single.Postponed || chosen == single.AgentId || string.IsNullOrWhiteSpace(chosen)))
                throw new InvalidDataException("Family continuity has invalid local partner choices.");
            previousSingle = single.AgentId;
        }
        ContinuityCouple? previous = null;
        foreach (var couple in saved.Couples)
        {
            if (!saved.Active || couple is null || string.IsNullOrWhiteSpace(couple.FirstPartnerId) ||
                string.CompareOrdinal(couple.FirstPartnerId, couple.SecondPartnerId) >= 0 || couple.DeadlineTick < 0 ||
                previous is not null && (string.CompareOrdinal(previous.FirstPartnerId, couple.FirstPartnerId) > 0 ||
                    previous.FirstPartnerId == couple.FirstPartnerId && string.CompareOrdinal(previous.SecondPartnerId, couple.SecondPartnerId) >= 0))
                throw new InvalidDataException("Family continuity has invalid local state.");
            previous = couple;
        }
    }
}
