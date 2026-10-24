namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy HouseholdCommunityCapabilities => CommunitySystem.HouseholdCommunityCapabilities;
    private static ActorCapabilityPolicy LearningCapabilities => CommunitySystem.LearningCapabilities;

    private static class CommunityActions
    {
        internal static readonly ActionDefinition<HousingChoice, HousingActionPlan> Housing = new(
            DecodeReferenceChoice(HousingChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanHousingAction(world.CommunityHousingWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckHousingActionPlan(world.CommunityHousingWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteHousingAction(world.CommunityHousingWorld, plan); return true; });

        internal static readonly ActionDefinition<LearningChoice, LearningActionPlan> Learning = new(
            DecodeReferenceChoice(LearningChoice.Decode), static choice => choice.Encode(), LearningCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanLearningAction(world.CommunityLearningWorld, actor, choice, false),
            static (world, plan, policy) => CommunitySystem.CheckLearningActionPlan(world.CommunityLearningWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteLearningAction(world.CommunityLearningWorld, plan); return true; });

        internal static readonly ActionDefinition<DepartureChoice, DepartureActionPlan> Departure = new(
            DecodeReferenceChoice(DepartureChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanDepartureAction(world.CommunityDepartureWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckDepartureActionPlan(world.CommunityDepartureWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteDepartureAction(world.CommunityDepartureWorld, plan); return true; });

        internal static readonly ActionDefinition<ReplacementCareChoice, ReplacementCareActionPlan> ReplacementCare = new(
            DecodeReferenceChoice(ReplacementCareChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanReplacementCare(world.CommunityDepartureWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckReplacementCarePlan(world.CommunityDepartureWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteReplacementCare(world.CommunityDepartureWorld, plan); return true; });

        internal static readonly ActionDefinition<RelocationChoice, RelocationActionPlan> Relocation = new(
            DecodeReferenceChoice(RelocationChoice.Decode), static _ => RelocationChoice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, _, _) => CommunitySystem.PlanRelocationAction(world.CommunityRelocationWorld, actor),
            static (world, plan, policy) => CommunitySystem.CheckRelocationActionPlan(world.CommunityRelocationWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteRelocationAction(world.CommunityRelocationWorld, plan); return true; });

        internal static readonly ActionDefinition<HouseholdResumeChoice, HouseholdResumeActionPlan> HouseholdResume = new(
            DecodeReferenceChoice(HouseholdResumeChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanHouseholdResume(world.CommunityWorkWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckHouseholdResumePlan(world.CommunityWorkWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteHouseholdResume(world.CommunityWorkWorld, plan); return true; });
    }
}
