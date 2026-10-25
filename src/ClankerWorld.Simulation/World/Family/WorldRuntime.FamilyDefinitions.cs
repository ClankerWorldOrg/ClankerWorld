using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy FamilyCareCapabilities => FamilySystem.FamilyCareCapabilities;

    private static ActorCapabilityPolicy DependentCareCapabilities(DependentCareActionPlan plan) => FamilySystem.DependentCareCapabilities(plan);

    private static class FamilyActions
    {
        internal static readonly ActionDefinition<PartnershipChoice, PartnershipActionPlan> Partnership = new(
            DecodeReferenceChoice(PartnershipChoice.Decode), static choice => choice.Encode(), FamilyCareCapabilities,
            static (world, actor, choice, _) => world.familySystem.PlanPartnershipAction(world.FamilyPartnershipWorld, actor, choice),
            static (world, plan, policy) => world.familySystem.CheckPartnershipActionPlan(world.FamilyPartnershipWorld, plan, policy),
            static (world, plan, _, _) => { world.familySystem.CompletePartnershipAction(world.FamilyPartnershipWorld, plan); return true; });

        internal static readonly ActionDefinition<ParenthoodChoice, ParenthoodActionPlan> Parenthood = new(
            DecodeReferenceChoice(ParenthoodChoice.Decode), static choice => choice.Encode(), FamilySystem.ParenthoodCapabilities(ParenthoodChoiceKind.Propose),
            static (world, actor, choice, _) => FamilySystem.PlanParenthoodAction(world.FamilyParenthoodWorld, actor, choice),
            static (world, plan, policy) => FamilySystem.CheckParenthoodActionPlan(world.FamilyParenthoodWorld, plan, policy),
            static (world, plan, _, _) => { FamilySystem.CompleteParenthoodAction(world.FamilyParenthoodWorld, plan); return true; },
            PlanCapabilities: static plan => FamilySystem.ParenthoodCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<GuardianTravelChoice, GuardianTravelActionPlan> GuardianTravel = new(
            DecodeReferenceChoice(GuardianTravelChoice.Decode), static choice => choice.Encode(), FamilySystem.GuardianTravelCapabilities(GuardianTravelKind.Relocate),
            static (world, actor, choice, _) => FamilySystem.PlanGuardianTravel(world.FamilyGuardianWorld, actor, choice),
            static (world, plan, policy) => FamilySystem.CheckGuardianTravelPlan(world.FamilyGuardianWorld, plan, policy),
            static (world, plan, _, _) => { FamilySystem.ApplyCheckedGuardianTravel(world.FamilyGuardianWorld, plan); return true; },
            PlanCapabilities: static plan => FamilySystem.GuardianTravelCapabilities(plan.Choice.Kind),
            ApplyUnplanned: static (world, action) => world.TryApplyGuardianPlacementCandidate(action.AgentId, action.CandidateId));

        internal static readonly ActionDefinition<DependentCareChoice, DependentCareActionPlan> DependentCare = new(
            DecodeReferenceChoice(DependentCareChoice.Decode), static choice => choice.Encode(), FamilyCareCapabilities,
            static (world, actor, choice, _) => FamilySystem.PlanDependentCare(world.FamilyCareWorld, actor, choice),
            static (world, plan, policy) => FamilySystem.CheckDependentCarePlan(world.FamilyCareWorld, plan, policy),
            static (world, plan, _, _) => { FamilySystem.CompleteDependentCareChoice(world.FamilyCareWorld, plan); return true; },
            PlanCapabilities: DependentCareCapabilities,
            HandlesPhase: static (world, action, phase) => phase != ActionPhase.BeforeAge || FamilySystem.HasGuardianPlacementTask(world.FamilyGuardianWorld, action.AgentId));

        internal static readonly ActionDefinition<PracticalCareChoice, PracticalCareActionPlan> PracticalCare = new(
            DecodeReferenceChoice(PracticalCareChoice.Decode), static choice => choice.Encode(), FamilyCareCapabilities,
            static (world, actor, choice, _) => world.PlanPracticalCare(actor, choice),
            static (world, plan, policy) => FamilySystem.CheckPracticalCarePlan(world.FamilyPracticalCareWorld, plan, policy),
            static (world, plan, _, _) => { FamilySystem.ApplyCheckedPracticalCare(world.FamilyPracticalCareWorld, plan); return true; });
    }

    private static class ChildActions
    {
        internal static readonly ActionDefinition<ChildChoice, ChildActionPlan> Definition = new(
            static (string candidate, out ChildChoice choice) =>
            {
                var decoded = ChildChoice.Parse(candidate);
                choice = decoded.GetValueOrDefault();
                return decoded.HasValue;
            },
            static choice => choice.Id,
            new(ActionLifeStages.Children),
            static (world, actor, choice, _) => FamilySystem.PlanChildChoice(world.FamilyChildWorld, actor, choice),
            static (world, plan, policy) => FamilySystem.CheckChildPlan(world.FamilyChildWorld, plan, policy),
            static (world, plan, _, _) =>
            {
                FamilySystem.ApplyCheckedChildPlan(world.FamilyChildWorld, plan);
                return true;
            });
    }

    private void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice,
        string description, int priority, string? destinationId = null)
    {
        if (global::ClankerWorld.Simulation.World.WorldRuntime.ChildActions.Definition.Offer(this, actor, choice) is { } plan)
            candidates.Add(new(global::ClankerWorld.Simulation.World.WorldRuntime.ChildActions.Definition.Encode(choice), description, priority, destinationId) { ActionPlan = plan });
    }

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

    private void AddDependentCareCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        FamilySystem.AddDependentCareOptions(this.FamilyCareWorld, options, actor);
        foreach (var candidate in options)
        {
            if (DependentCareChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (global::ClankerWorld.Simulation.World.WorldRuntime.FamilyActions.DependentCare.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void ApplyDependentCarePlan(DependentCareActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        FamilySystem.CompleteDependentCareChoice(this.FamilyCareWorld, plan);
    }

    private bool IsAdult(string actor) => AgentQueries.IsAdult(agentWorld, actor);

    private void ApplyGuardianTravelPlan(GuardianTravelActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        FamilySystem.ApplyCheckedGuardianTravel(this.FamilyGuardianWorld, plan);
    }

    private void AddParenthoodCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var start = candidates.Count;
        AddParenthoodCandidateOptions(candidates, actor);
        for (var index = start; index < candidates.Count;)
        {
            var candidate = candidates[index];
            if (ParenthoodChoice.Decode(candidate.Id) is not { } choice) { index++; continue; }
            if (global::ClankerWorld.Simulation.World.WorldRuntime.FamilyActions.Parenthood.Offer(this, actor, choice) is { } plan)
            {
                candidates[index] = candidate with { ActionPlan = plan };
                index++;
            }
            else candidates.RemoveAt(index);
        }
    }

    private void AddFamilyCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        this.familySystem.AddFamilyCandidateOptions(this.FamilyPartnershipWorld, options, actor);
        foreach (var candidate in options)
            if (PartnershipChoice.Decode(candidate.Id) is { } choice && global::ClankerWorld.Simulation.World.WorldRuntime.FamilyActions.Partnership.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

    private PracticalCareActionPlan? PlanPracticalCare(string actor, PracticalCareChoice choice) =>
        FamilySystem.PlanPracticalCare(FamilyPracticalCareWorld, actor, choice);

    private void ApplyPracticalCarePlan(PracticalCareActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        FamilySystem.ApplyCheckedPracticalCare(this.FamilyPracticalCareWorld, plan);
    }
}
