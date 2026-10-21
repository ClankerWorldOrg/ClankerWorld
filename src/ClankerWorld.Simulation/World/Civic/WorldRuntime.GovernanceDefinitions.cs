namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<TChoice, TPlan> CreateGovernanceDefinition<TChoice, TPlan>(
        Func<string, TChoice?> decode, Func<TChoice, string> encode,
        Func<ITownWorld, string, TChoice, TPlan?> plan,
        Func<ITownWorld, TPlan, ActorCapabilityPolicy, ActionCheck> check)
        where TChoice : class where TPlan : ActionPlan => new(
            DecodeReferenceChoice(decode), encode, TownSystem.GovernanceCapabilities,
            (world, actor, choice, _) => plan(world.civicWorld, actor, choice),
            (world, original, policy) => check(world.civicWorld, original, policy),
            static (world, original, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, original.Actor, original.CandidateId, originalPlan: original);
                return true;
            });


    private static class GovernanceActions
    {
        internal static readonly ActionDefinition<MayoralBallotChoice, MayoralBallotActionPlan> MayoralBallot = CreateGovernanceDefinition<MayoralBallotChoice, MayoralBallotActionPlan>(
            MayoralBallotChoice.Decode, static choice => choice.Encode(), TownSystem.PlanMayoralBallot,
            static (world, plan, policy) => TownSystem.CheckMayoralBallot(world, plan, policy: policy));

        internal static readonly ActionDefinition<GovernmentOfficeChoice, GovernmentOfficeActionPlan> GovernmentOffice = CreateGovernanceDefinition<GovernmentOfficeChoice, GovernmentOfficeActionPlan>(
            GovernmentOfficeChoice.Decode, static choice => choice.Encode(), TownSystem.PlanGovernmentOffice,
            static (world, plan, policy) => TownSystem.CheckGovernmentOffice(world, plan, policy: policy));

        internal static readonly ActionDefinition<GovernmentProposalChoice, GovernmentProposalActionPlan> GovernmentProposal = CreateGovernanceDefinition<GovernmentProposalChoice, GovernmentProposalActionPlan>(
            GovernmentProposalChoice.Decode, static choice => choice.Encode(), TownSystem.PlanGovernmentProposal,
            static (world, plan, policy) => TownSystem.CheckGovernmentProposal(world, plan, policy: policy));

        internal static readonly ActionDefinition<TownPolicyChoice, TownPolicyActionPlan> TownPolicy = CreateGovernanceDefinition<TownPolicyChoice, TownPolicyActionPlan>(
            TownPolicyChoice.Decode, static choice => choice.Encode(), TownSystem.PlanTownPolicy,
            static (world, plan, policy) => TownSystem.CheckTownPolicyPlan(world, plan, policy: policy));

        internal static readonly ActionDefinition<TownLawChoice, TownLawActionPlan> TownLaw = CreateGovernanceDefinition<TownLawChoice, TownLawActionPlan>(
            TownLawChoice.Decode, static choice => choice.Encode(), TownSystem.PlanTownLaw,
            static (world, plan, policy) => TownSystem.CheckTownLawPlan(world, plan, policy: policy));

        internal static readonly ActionDefinition<CivicProposalVoteChoice, CivicProposalVoteActionPlan> CivicProposalVote = CreateGovernanceDefinition<CivicProposalVoteChoice, CivicProposalVoteActionPlan>(
            CivicProposalVoteChoice.Decode, static choice => choice.Encode(), TownSystem.PlanCivicProposalVote,
            static (world, plan, policy) => TownSystem.CheckCivicProposalVote(world, plan, policy: policy));

    }
}
