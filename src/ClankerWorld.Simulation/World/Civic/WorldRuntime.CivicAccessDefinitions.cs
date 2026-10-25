namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static class CivicAccessActions
    {
        internal static readonly ActionDefinition<TownAdmissionChoice, TownAdmissionActionPlan> TownAdmission = CreateCivicDefinition<TownAdmissionChoice, TownAdmissionActionPlan>(
            TownAdmissionChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanTownAdmission(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckTownAdmissionPlan(world.civicWorld, plan, policy));

        internal static readonly ActionDefinition<TownFoundingChoice, TownFoundingActionPlan> TownFounding = CreateCivicDefinition<TownFoundingChoice, TownFoundingActionPlan>(
            TownFoundingChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanTownFounding(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckTownFoundingPlan(world.civicWorld, plan, policy));

        internal static readonly ActionDefinition<CivicNoticeChoice, CivicNoticeActionPlan> CivicNotice = CreateGovernanceDefinition<CivicNoticeChoice, CivicNoticeActionPlan>(
            CivicNoticeChoice.Decode, static choice => choice.Encode(), TownSystem.PlanCivicNotice,
            static (world, plan, policy) => TownSystem.CheckCivicNoticePlan(world, plan, policy));

        internal static readonly ActionDefinition<CivicVisitChoice, CivicVisitActionPlan> CivicVisit = new(
            DecodeReferenceChoice(CivicVisitChoice.Decode), static choice => choice.Encode(), TownSystem.CivicVisitCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanCivicVisit(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckCivicVisit(world.civicWorld, plan, policy),
            ApplyCivicPlan);

    }
}
