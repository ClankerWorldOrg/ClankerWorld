namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<TownLandClaimChoice, TownLandClaimActionPlan> TownLandClaimDefinition => LandRightsActions.TownLandClaim;
    private static ActionDefinition<HouseholdLandFilingChoice, HouseholdLandFilingActionPlan> HouseholdLandFilingDefinition => LandRightsActions.HouseholdLandFiling;
    private static ActionDefinition<HouseholdLandResponseChoice, HouseholdLandResponseActionPlan> HouseholdLandResponseDefinition => LandRightsActions.HouseholdLandResponse;
    private static ActionDefinition<LandTransferProposalChoice, LandTransferProposalActionPlan> LandTransferProposalDefinition => LandRightsActions.LandTransferProposal;
    private static ActionDefinition<LandTransferResponseChoice, LandTransferResponseActionPlan> LandTransferResponseDefinition => LandRightsActions.LandTransferResponse;
    private static ActionDefinition<LandSaleCollectionChoice, LandSaleCollectionActionPlan> LandSaleCollectionDefinition => LandRightsActions.LandSaleCollection;
    private static ActionDefinition<LandSaleExchangeChoice, LandSaleExchangeActionPlan> LandSaleExchangeDefinition => LandRightsActions.LandSaleExchange;

    private static class LandRightsActions
    {
        internal static readonly ActionDefinition<TownLandClaimChoice, TownLandClaimActionPlan> TownLandClaim = new(
            DecodeReferenceChoice(TownLandClaimChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanTownLandClaim(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckTownLandClaim(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<HouseholdLandFilingChoice, HouseholdLandFilingActionPlan> HouseholdLandFiling = new(
            DecodeReferenceChoice(HouseholdLandFilingChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanHouseholdLandFiling(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckHouseholdLandFiling(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<HouseholdLandResponseChoice, HouseholdLandResponseActionPlan> HouseholdLandResponse = new(
            DecodeReferenceChoice(HouseholdLandResponseChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanHouseholdLandResponse(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckHouseholdLandResponse(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandTransferProposalChoice, LandTransferProposalActionPlan> LandTransferProposal = new(
            DecodeReferenceChoice(LandTransferProposalChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanLandTransferProposal(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandTransferProposal(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandTransferResponseChoice, LandTransferResponseActionPlan> LandTransferResponse = new(
            DecodeReferenceChoice(LandTransferResponseChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanLandTransferResponse(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandTransferResponse(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandSaleCollectionChoice, LandSaleCollectionActionPlan> LandSaleCollection = new(
            DecodeReferenceChoice(LandSaleCollectionChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanLandSaleCollection(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandSaleCollection(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

        internal static readonly ActionDefinition<LandSaleExchangeChoice, LandSaleExchangeActionPlan> LandSaleExchange = new(
            DecodeReferenceChoice(LandSaleExchangeChoice.Decode), static choice => choice.Encode(), TownSystem.LandRightsCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanLandSaleExchange(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandSaleExchange(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });

    }
}
