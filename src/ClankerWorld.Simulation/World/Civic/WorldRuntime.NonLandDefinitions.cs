namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private TPlan? PlanNonLandPhysicalRequest<TPlan>(string actor, NonLandPhysicalRequest request) where TPlan : ActionPlan
    {
        foreach (var town in towns)
            if (TownSystem.TryPlanNonLandPhysical(civicWorld, town, actor, request.CandidateId, null, out var selected))
                return selected as TPlan;
        return null;
    }

    private static class NonLandActions
    {
        internal static readonly ActionDefinition<NonLandFilingChoice, NonLandFilingActionPlan> Filing = CreateCivicDefinition<NonLandFilingChoice, NonLandFilingActionPlan>(
            NonLandFilingChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanNonLandFiling(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckNonLandFiling(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<NonLandEvidenceChoice, NonLandEvidenceActionPlan> Evidence = new(
            DecodeReferenceChoice(NonLandEvidenceChoice.Decode), static choice => choice.Encode(), TownSystem.GovernanceCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanNonLandEvidence(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckNonLandEvidence(world.civicWorld, plan, policy: policy),
            ApplyCivicPlan,
            WorldPlanCapabilities: static (world, plan) => TownSystem.NonLandEvidenceCapabilities(world.civicWorld, plan));

        internal static readonly ActionDefinition<NonLandJudgeChoice, NonLandJudgeActionPlan> Judge = CreateGovernanceDefinition<NonLandJudgeChoice, NonLandJudgeActionPlan>(
            NonLandJudgeChoice.Decode, static choice => choice.Encode(), TownSystem.PlanNonLandJudge,
            static (world, plan, policy) => TownSystem.CheckNonLandJudge(world, plan, policy: policy));

        internal static readonly ActionDefinition<NonLandAdjudicationChoice, NonLandAdjudicationActionPlan> Adjudication = new(
            DecodeReferenceChoice(NonLandAdjudicationChoice.Decode), static choice => choice.Encode(), TownSystem.GovernanceCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanNonLandAdjudication(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckNonLandAdjudication(world.civicWorld, plan, policy: policy),
            ApplyCivicPlan,
            WorldPlanCapabilities: static (world, plan) => TownSystem.NonLandAdjudicationCapabilities(world.civicWorld, plan));

        internal static readonly ActionDefinition<NonLandAgreementChoice, NonLandAgreementActionPlan> Agreement = CreateCivicDefinition<NonLandAgreementChoice, NonLandAgreementActionPlan>(
            NonLandAgreementChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanNonLandAgreement(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckNonLandAgreement(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<NonLandPhysicalRequest, NonLandWorkActionPlan> Work = new(
            DecodeReferenceChoice(candidate => NonLandPhysicalRequest.Decode(candidate) is { } request && request.Relay == false ? request : null), static choice => choice.Encode(), TownSystem.NonLandWorkCapabilities,
            static (world, actor, choice, _) => world.PlanNonLandPhysicalRequest<NonLandWorkActionPlan>(actor, choice),
            static (world, plan, policy) => TownSystem.CheckNonLandWork(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) => { world.townSystem.ApplyCheckedNonLandWork(world.civicWorld, plan); return true; });

        internal static readonly ActionDefinition<NonLandPhysicalRequest, NonLandReportRelayActionPlan> ReportRelay = new(
            DecodeReferenceChoice(candidate => NonLandPhysicalRequest.Decode(candidate) is { } request && request.Relay == true ? request : null), static choice => choice.Encode(), TownSystem.NonLandReportRelayCapabilities,
            static (world, actor, choice, _) => world.PlanNonLandPhysicalRequest<NonLandReportRelayActionPlan>(actor, choice),
            static (world, plan, policy) => TownSystem.CheckNonLandReportRelay(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) => { world.townSystem.ApplyCheckedNonLandReportRelay(world.civicWorld, plan); return true; });

    }
}
