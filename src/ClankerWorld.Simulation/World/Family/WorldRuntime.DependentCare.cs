using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private long GuardianStageTicks => Math.Max(1, worldSystems.Config.TicksPerDay);

    private string[] GuardianAdultsForStage(string child, string stage)
    {
        return FamilySystem.GuardianAdultsForStage(society.Checkpoint, towns, child, stage);
    }

    private string[] TownAdultsFor(string child) => FamilySystem.TownAdultsFor(society.Checkpoint, towns, child);

    private string[] LivingAdultRelatives(string child) => FamilySystem.LivingAdultRelatives(society.Checkpoint, child);

    private void ApplyDependentCareCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        if (candidate.StartsWith("guardian_tend:", StringComparison.Ordinal))
        { TendToIllDependent(actor, candidate["guardian_tend:".Length..], originalPlan); return; }
        var plan = originalPlan as DependentCareActionPlan ?? (DependentCareChoice.Decode(candidate) is { } choice ? FamilySystem.PlanDependentCare(this.FamilyCareWorld, actor, choice) : null);
        if (plan is not null) ApplyDependentCarePlan(plan);
    }

    private static void ValidateDependentCare(
        IEnumerable<AgentState> physical,
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        int schemaVersion)
    {
        var people = checkpoint.Agents.ToDictionary(person => person.Id, StringComparer.Ordinal);
        foreach (var state in physical)
        {
            if (state.GuardianSearch is not { } search) continue;
            if (schemaVersion < DependentGuardianSearchSchemaVersion || !people.TryGetValue(state.AgentId, out var child) ||
                child.Status != SocietyAgentStatus.Active ||
                !LifeStageRules.IsDependent(child.AgeBand) ||
                SocietyRules.HasActivePrimaryCaregiver(checkpoint, child.Id) ||
                search.Stage is not ("relatives" or "household" or "town") ||
                search.StartedTick < 0 || search.StartedTick > checkpoint.WorldTick ||
                search.StageStartedTick < search.StartedTick || search.StageStartedTick > checkpoint.WorldTick ||
                search.OfferedAdultIds is null ||
                search.OfferedAdultIds.Any(id => string.IsNullOrWhiteSpace(id) || !people.TryGetValue(id, out var adult) ||
                    adult.Status != SocietyAgentStatus.Active || !LifeStageRules.IsAdult(adult.AgeBand)) ||
                search.OfferedAdultIds.Distinct(StringComparer.Ordinal).Count() != search.OfferedAdultIds.Count ||
                !search.OfferedAdultIds.SequenceEqual(search.OfferedAdultIds.Order(StringComparer.Ordinal)) ||
                !search.OfferedAdultIds.SequenceEqual(FamilySystem.GuardianAdultsForStage(checkpoint, towns, child.Id, search.Stage), StringComparer.Ordinal) ||
                search.Stage != "town" && search.OfferedAdultIds.Count == 0)
                throw new InvalidDataException("The saved dependent guardian search is invalid.");
        }
    }

    private void TendToIllDependent(string adult, string dependent, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as PracticalCareActionPlan ?? PlanPracticalCare(adult, new(PracticalCareKind.IllDependent, dependent));
        if (plan is not null) ApplyPracticalCarePlan(plan);
    }
}
