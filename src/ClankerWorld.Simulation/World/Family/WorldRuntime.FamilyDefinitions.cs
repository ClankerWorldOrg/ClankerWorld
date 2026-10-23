namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy FamilyCareCapabilities => FamilySystem.FamilyCareCapabilities;

    private static ActorCapabilityPolicy DependentCareCapabilities(DependentCareActionPlan plan) => FamilySystem.DependentCareCapabilities(plan);

    private static ActionDefinition<PartnershipChoice, PartnershipActionPlan> PartnershipDefinition => FamilyActions.Partnership;
    private static ActionDefinition<ParenthoodChoice, ParenthoodActionPlan> ParenthoodDefinition => FamilyActions.Parenthood;
    private static ActionDefinition<GuardianTravelChoice, GuardianTravelActionPlan> GuardianTravelDefinition => FamilyActions.GuardianTravel;
    private static ActionDefinition<DependentCareChoice, DependentCareActionPlan> DependentCareDefinition => FamilyActions.DependentCare;
    private static ActionDefinition<PracticalCareChoice, PracticalCareActionPlan> PracticalCareDefinition => FamilyActions.PracticalCare;

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
}
