namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static class LandRightsActions
    {
        internal static readonly ActionDefinition<TownLandClaimChoice, TownLandClaimActionPlan> TownLandClaim = CreateGovernanceDefinition<TownLandClaimChoice, TownLandClaimActionPlan>(
            TownLandClaimChoice.Decode, static choice => choice.Encode(), TownSystem.PlanTownLandClaim,
            static (world, plan, policy) => TownSystem.CheckTownLandClaim(world, plan, policy: policy));

        internal static readonly ActionDefinition<HouseholdLandFilingChoice, HouseholdLandFilingActionPlan> HouseholdLandFiling = CreateCivicDefinition<HouseholdLandFilingChoice, HouseholdLandFilingActionPlan>(
            HouseholdLandFilingChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanHouseholdLandFiling(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckHouseholdLandFiling(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<HouseholdLandResponseChoice, HouseholdLandResponseActionPlan> HouseholdLandResponse = CreateGovernanceDefinition<HouseholdLandResponseChoice, HouseholdLandResponseActionPlan>(
            HouseholdLandResponseChoice.Decode, static choice => choice.Encode(), TownSystem.PlanHouseholdLandResponse,
            static (world, plan, policy) => TownSystem.CheckHouseholdLandResponse(world, plan, policy: policy));

        internal static readonly ActionDefinition<LandTransferProposalChoice, LandTransferProposalActionPlan> LandTransferProposal = CreateCivicDefinition<LandTransferProposalChoice, LandTransferProposalActionPlan>(
            LandTransferProposalChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanLandTransferProposal(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandTransferProposal(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandTransferResponseChoice, LandTransferResponseActionPlan> LandTransferResponse = CreateCivicDefinition<LandTransferResponseChoice, LandTransferResponseActionPlan>(
            LandTransferResponseChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanLandTransferResponse(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandTransferResponse(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandSaleCollectionChoice, LandSaleCollectionActionPlan> LandSaleCollection = CreateCivicDefinition<LandSaleCollectionChoice, LandSaleCollectionActionPlan>(
            LandSaleCollectionChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => world.townSystem.PlanLandSaleCollection(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandSaleCollection(world.civicWorld, plan, policy: policy));

        internal static readonly ActionDefinition<LandSaleExchangeChoice, LandSaleExchangeActionPlan> LandSaleExchange = CreateCivicDefinition<LandSaleExchangeChoice, LandSaleExchangeActionPlan>(
            LandSaleExchangeChoice.Decode, static choice => choice.Encode(), static (world, actor, choice) => TownSystem.PlanLandSaleExchange(world.civicWorld, actor, choice),
            static (world, plan, policy) => world.townSystem.CheckLandSaleExchange(world.civicWorld, plan, policy: policy));

    }
}
