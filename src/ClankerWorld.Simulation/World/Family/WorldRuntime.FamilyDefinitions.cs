namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy FamilyCareCapabilities => FamilySystem.FamilyCareCapabilities;

    private static ActorCapabilityPolicy ParenthoodCapabilities(ParenthoodChoiceKind kind) => FamilySystem.ParenthoodCapabilities(kind);

    private static ActorCapabilityPolicy GuardianTravelCapabilities(GuardianTravelKind kind) => FamilySystem.GuardianTravelCapabilities(kind);

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
            static (world, actor, choice, _) => world.PlanPartnershipAction(actor, choice),
            static (world, plan, policy) => world.CheckPartnershipActionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompletePartnershipAction(plan); return true; });

        internal static readonly ActionDefinition<ParenthoodChoice, ParenthoodActionPlan> Parenthood = new(
            DecodeReferenceChoice(ParenthoodChoice.Decode), static choice => choice.Encode(), ParenthoodCapabilities(ParenthoodChoiceKind.Propose),
            static (world, actor, choice, _) => world.PlanParenthoodAction(actor, choice),
            static (world, plan, policy) => world.CheckParenthoodActionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteParenthoodAction(plan); return true; },
            PlanCapabilities: static plan => ParenthoodCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<GuardianTravelChoice, GuardianTravelActionPlan> GuardianTravel = new(
            DecodeReferenceChoice(GuardianTravelChoice.Decode), static choice => choice.Encode(), GuardianTravelCapabilities(GuardianTravelKind.Relocate),
            static (world, actor, choice, _) => world.PlanGuardianTravel(actor, choice),
            static (world, plan, policy) => world.CheckGuardianTravelPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedGuardianTravel(plan); return true; },
            PlanCapabilities: static plan => GuardianTravelCapabilities(plan.Choice.Kind),
            ApplyUnplanned: static (world, action) => world.TryApplyGuardianPlacementCandidate(action.AgentId, action.CandidateId));

        internal static readonly ActionDefinition<DependentCareChoice, DependentCareActionPlan> DependentCare = new(
            DecodeReferenceChoice(DependentCareChoice.Decode), static choice => choice.Encode(), FamilyCareCapabilities,
            static (world, actor, choice, _) => world.PlanDependentCare(actor, choice),
            static (world, plan, policy) => world.CheckDependentCarePlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteDependentCareChoice(plan); return true; },
            PlanCapabilities: DependentCareCapabilities,
            HandlesPhase: static (world, action, phase) => phase != ActionPhase.BeforeAge || world.HasGuardianPlacementTask(action.AgentId));

        internal static readonly ActionDefinition<PracticalCareChoice, PracticalCareActionPlan> PracticalCare = new(
            DecodeReferenceChoice(PracticalCareChoice.Decode), static choice => choice.Encode(), FamilyCareCapabilities,
            static (world, actor, choice, _) => world.PlanPracticalCare(actor, choice),
            static (world, plan, policy) => world.CheckPracticalCarePlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedPracticalCare(plan); return true; });
    }
}
