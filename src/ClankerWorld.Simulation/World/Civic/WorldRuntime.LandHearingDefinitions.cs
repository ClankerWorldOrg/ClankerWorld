namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static class LandHearingActions
    {
        internal static readonly ActionDefinition<LandHearingFilingChoice, LandHearingFilingActionPlan> Filing = CreateCivicDefinition<LandHearingFilingChoice, LandHearingFilingActionPlan>(
            LandHearingFilingChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanLandHearingFiling(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingFiling(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandHearingEvidenceChoice, LandHearingEvidenceActionPlan> Evidence = CreateCivicDefinition<LandHearingEvidenceChoice, LandHearingEvidenceActionPlan>(
            LandHearingEvidenceChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanLandHearingEvidence(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingEvidence(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandHearingJudgeChoice, LandHearingJudgeActionPlan> Judge = CreateCivicDefinition<LandHearingJudgeChoice, LandHearingJudgeActionPlan>(
            LandHearingJudgeChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanLandHearingJudge(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingJudge(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandHearingRulingChoice, LandHearingRulingActionPlan> Ruling = CreateCivicDefinition<LandHearingRulingChoice, LandHearingRulingActionPlan>(
            LandHearingRulingChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanLandHearingRuling(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingRuling(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandHearingReopeningChoice, LandHearingReopeningActionPlan> Reopening = CreateCivicDefinition<LandHearingReopeningChoice, LandHearingReopeningActionPlan>(
            LandHearingReopeningChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanLandHearingReopening(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingReopening(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandHearingPropertyChoice, LandHearingPropertyActionPlan> Property = CreateCivicDefinition<LandHearingPropertyChoice, LandHearingPropertyActionPlan>(
            LandHearingPropertyChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanLandHearingProperty(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingProperty(world.civicWorld, plan, policy: policy));

    }
}
