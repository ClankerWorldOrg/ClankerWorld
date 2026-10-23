using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private long GuardianStageTicks => Math.Max(1, worldSystems.Config.TicksPerDay);

    private bool NeedsCaregiver(string child) =>
        FamilySystem.NeedsCaregiver(FamilyCareWorld, child);
    private bool CanAcceptGuardian(string adult, string child) =>
        FamilySystem.CanAcceptGuardian(FamilyCareWorld, adult, child);

    private IEnumerable<string> IllDependentsNeedingCare(string adult) =>
        FamilySystem.IllDependentsNeedingCare(FamilyCareWorld, adult);

    private bool HasDependentCareDecision(string actor) =>
        FamilySystem.HasDependentCareDecision(FamilyCareWorld, actor);

    private void MaintainDependentCare() =>
        FamilySystem.MaintainDependentCare(FamilyCareWorld);

    /// <summary>
    /// Decisions later in the tick can change who lives where, so open searches are brought up to date
    /// before the tick ends and the saved offers always match the world they were saved with.
    /// </summary>
    private void SettleGuardianSearches() =>
        FamilySystem.SettleGuardianSearches(FamilyCareWorld);

    private string[] GuardianAdultsForStage(string child, string stage)
    {
        return GuardianAdultsForStage(society.Checkpoint, towns, child, stage);
    }

    /// <summary>
    /// The search widens: each stage adds a new group and keeps asking the earlier ones, so a relative
    /// who missed their first day can still accept later.
    /// </summary>
    private static string[] GuardianAdultsForStage(
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        string child,
        string stage) =>
        FamilySystem.GuardianAdultsForStage(checkpoint, towns, child, stage);

    /// <summary>The adults a stage adds to the search; nobody belongs to more than one stage.</summary>
    private static string[] NewGuardianAdultsForStage(
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        string child,
        string stage) =>
        FamilySystem.NewGuardianAdultsForStage(checkpoint, towns, child, stage);

    private string[] TownAdultsFor(string child) => TownAdultsFor(society.Checkpoint, towns, child);

    private static string[] TownAdultsFor(SocietyCheckpoint checkpoint, IReadOnlyList<TownRuntimeState> towns, string child) =>
        FamilySystem.TownAdultsFor(checkpoint, towns, child);

    private string[] LivingAdultRelatives(string child) => LivingAdultRelatives(society.Checkpoint, child);

    private static string[] LivingAdultRelatives(SocietyCheckpoint checkpoint, string child) =>
        FamilySystem.LivingAdultRelatives(checkpoint, child);

    private void AddDependentCareOptions(List<CognitionCandidate> candidates, string actor) =>
        FamilySystem.AddDependentCareOptions(FamilyCareWorld, candidates, actor);

    private void ApplyDependentCareCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        if (candidate.StartsWith("guardian_tend:", StringComparison.Ordinal))
        { TendToIllDependent(actor, candidate["guardian_tend:".Length..], originalPlan); return; }
        var plan = originalPlan as DependentCareActionPlan ?? (DependentCareChoice.Decode(candidate) is { } choice ? PlanDependentCare(actor, choice) : null);
        if (plan is not null) ApplyDependentCarePlan(plan);
    }

    private void CompleteDependentCareChoice(DependentCareActionPlan plan) =>
        FamilySystem.CompleteDependentCareChoice(FamilyCareWorld, plan);

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
                !search.OfferedAdultIds.SequenceEqual(GuardianAdultsForStage(checkpoint, towns, child.Id, search.Stage), StringComparer.Ordinal) ||
                search.Stage != "town" && search.OfferedAdultIds.Count == 0)
                throw new InvalidDataException("The saved dependent guardian search is invalid.");
        }
    }

    private bool CanFitGuardianHousehold(string householdId, string adult, string child) =>
        FamilySystem.CanFitGuardianHousehold(FamilyCareWorld, householdId, adult, child);

    private void TendToIllDependent(string adult, string dependent, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as PracticalCareActionPlan ?? PlanPracticalCare(adult, new(PracticalCareKind.IllDependent, dependent));
        if (plan is not null) ApplyPracticalCarePlan(plan);
    }
}
