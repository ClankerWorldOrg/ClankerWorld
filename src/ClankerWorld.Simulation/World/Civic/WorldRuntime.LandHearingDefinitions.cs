namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<LandHearingFilingChoice, LandHearingFilingActionPlan> LandHearingFilingDefinition => LandHearingActions.Filing;
    private static ActionDefinition<LandHearingEvidenceChoice, LandHearingEvidenceActionPlan> LandHearingEvidenceDefinition => LandHearingActions.Evidence;
    private static ActionDefinition<LandHearingJudgeChoice, LandHearingJudgeActionPlan> LandHearingJudgeDefinition => LandHearingActions.Judge;
    private static ActionDefinition<LandHearingRulingChoice, LandHearingRulingActionPlan> LandHearingRulingDefinition => LandHearingActions.Ruling;
    private static ActionDefinition<LandHearingReopeningChoice, LandHearingReopeningActionPlan> LandHearingReopeningDefinition => LandHearingActions.Reopening;
    private static ActionDefinition<LandHearingPropertyChoice, LandHearingPropertyActionPlan> LandHearingPropertyDefinition => LandHearingActions.Property;

    private static class LandHearingActions
    {
        internal static readonly ActionDefinition<LandHearingFilingChoice, LandHearingFilingActionPlan> Filing = new(
            DecodeReferenceChoice(LandHearingFilingChoice.Decode), static choice => choice.Encode(), TownSystem.LandHearingCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanLandHearingFiling(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingFiling(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandHearingEvidenceChoice, LandHearingEvidenceActionPlan> Evidence = new(
            DecodeReferenceChoice(LandHearingEvidenceChoice.Decode), static choice => choice.Encode(), TownSystem.LandHearingCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanLandHearingEvidence(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingEvidence(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandHearingJudgeChoice, LandHearingJudgeActionPlan> Judge = new(
            DecodeReferenceChoice(LandHearingJudgeChoice.Decode), static choice => choice.Encode(), TownSystem.LandHearingCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanLandHearingJudge(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingJudge(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandHearingRulingChoice, LandHearingRulingActionPlan> Ruling = new(
            DecodeReferenceChoice(LandHearingRulingChoice.Decode), static choice => choice.Encode(), TownSystem.LandHearingCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanLandHearingRuling(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingRuling(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandHearingReopeningChoice, LandHearingReopeningActionPlan> Reopening = new(
            DecodeReferenceChoice(LandHearingReopeningChoice.Decode), static choice => choice.Encode(), TownSystem.LandHearingCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanLandHearingReopening(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingReopening(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandHearingPropertyChoice, LandHearingPropertyActionPlan> Property = new(
            DecodeReferenceChoice(LandHearingPropertyChoice.Decode), static choice => choice.Encode(), TownSystem.LandHearingCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanLandHearingProperty(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandHearingProperty(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

    }
}
