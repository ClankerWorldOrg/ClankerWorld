namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy HouseholdCommunityCapabilities => CommunitySystem.HouseholdCommunityCapabilities;
    private static ActorCapabilityPolicy LearningCapabilities => CommunitySystem.LearningCapabilities;

    private static ActionDefinition<HousingChoice, HousingActionPlan> HousingDefinition => CommunityActions.Housing;
    private static ActionDefinition<LearningChoice, LearningActionPlan> LearningDefinition => CommunityActions.Learning;
    private static ActionDefinition<DepartureChoice, DepartureActionPlan> DepartureDefinition => CommunityActions.Departure;
    private static ActionDefinition<ReplacementCareChoice, ReplacementCareActionPlan> ReplacementCareDefinition => CommunityActions.ReplacementCare;
    private static ActionDefinition<RelocationChoice, RelocationActionPlan> RelocationDefinition => CommunityActions.Relocation;
    private static ActionDefinition<HouseholdResumeChoice, HouseholdResumeActionPlan> HouseholdResumeDefinition => CommunityActions.HouseholdResume;

    private static class CommunityActions
    {
        internal static readonly ActionDefinition<HousingChoice, HousingActionPlan> Housing = new(
            DecodeReferenceChoice(HousingChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => world.PlanHousingAction(actor, choice),
            static (world, plan, policy) => world.CheckHousingActionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteHousingAction(plan); return true; });

        internal static readonly ActionDefinition<LearningChoice, LearningActionPlan> Learning = new(
            DecodeReferenceChoice(LearningChoice.Decode), static choice => choice.Encode(), LearningCapabilities,
            static (world, actor, choice, _) => world.PlanLearningAction(actor, choice),
            static (world, plan, policy) => world.CheckLearningActionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteLearningAction(plan); return true; });

        internal static readonly ActionDefinition<DepartureChoice, DepartureActionPlan> Departure = new(
            DecodeReferenceChoice(DepartureChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => world.PlanDepartureAction(actor, choice),
            static (world, plan, policy) => world.CheckDepartureActionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteDepartureAction(plan); return true; });

        internal static readonly ActionDefinition<ReplacementCareChoice, ReplacementCareActionPlan> ReplacementCare = new(
            DecodeReferenceChoice(ReplacementCareChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => world.PlanReplacementCare(actor, choice),
            static (world, plan, policy) => world.CheckReplacementCarePlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteReplacementCare(plan); return true; });

        internal static readonly ActionDefinition<RelocationChoice, RelocationActionPlan> Relocation = new(
            DecodeReferenceChoice(RelocationChoice.Decode), static _ => RelocationChoice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, _, _) => world.PlanRelocationAction(actor),
            static (world, plan, policy) => world.CheckRelocationActionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteRelocationAction(plan); return true; });

        internal static readonly ActionDefinition<HouseholdResumeChoice, HouseholdResumeActionPlan> HouseholdResume = new(
            DecodeReferenceChoice(HouseholdResumeChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => world.PlanHouseholdResume(actor, choice),
            static (world, plan, policy) => world.CheckHouseholdResumePlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteHouseholdResume(plan); return true; });
    }
}
