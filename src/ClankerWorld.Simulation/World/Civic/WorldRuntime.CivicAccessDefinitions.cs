namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<TownAdmissionChoice, TownAdmissionActionPlan> TownAdmissionDefinition => CivicAccessActions.TownAdmission;
    private static ActionDefinition<TownFoundingChoice, TownFoundingActionPlan> TownFoundingDefinition => CivicAccessActions.TownFounding;
    private static ActionDefinition<CivicNoticeChoice, CivicNoticeActionPlan> CivicNoticeDefinition => CivicAccessActions.CivicNotice;
    private static ActionDefinition<CivicVisitChoice, CivicVisitActionPlan> CivicVisitDefinition => CivicAccessActions.CivicVisit;

    private static class CivicAccessActions
    {
        internal static readonly ActionDefinition<TownAdmissionChoice, TownAdmissionActionPlan> TownAdmission = new(
            DecodeReferenceChoice(TownAdmissionChoice.Decode), static choice => choice.Encode(), TownSystem.CivicMembershipCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanTownAdmission(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckTownAdmissionPlan(world.civicWorld, plan, policy),
            ApplyCivicPlan);

        internal static readonly ActionDefinition<TownFoundingChoice, TownFoundingActionPlan> TownFounding = new(
            DecodeReferenceChoice(TownFoundingChoice.Decode), static choice => choice.Encode(), TownSystem.CivicMembershipCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanTownFounding(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckTownFoundingPlan(world.civicWorld, plan, policy),
            ApplyCivicPlan);

        internal static readonly ActionDefinition<CivicNoticeChoice, CivicNoticeActionPlan> CivicNotice = new(
            DecodeReferenceChoice(CivicNoticeChoice.Decode), static choice => choice.Encode(), TownSystem.CivicMembershipCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanCivicNotice(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckCivicNoticePlan(world.civicWorld, plan, policy),
            ApplyCivicPlan);

        internal static readonly ActionDefinition<CivicVisitChoice, CivicVisitActionPlan> CivicVisit = new(
            DecodeReferenceChoice(CivicVisitChoice.Decode), static choice => choice.Encode(), TownSystem.CivicVisitCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanCivicVisit(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckCivicVisit(world.civicWorld, plan, policy),
            ApplyCivicPlan);

    }
}
