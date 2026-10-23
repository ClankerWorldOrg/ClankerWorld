namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static class CouncilActions
    {
        internal static readonly ActionDefinition<CouncilConsentChoice, CouncilConsentActionPlan> Consent = CreateGovernanceDefinition<CouncilConsentChoice, CouncilConsentActionPlan>(
            CouncilConsentChoice.Decode, static choice => choice.Encode(), TownSystem.PlanCouncilConsent,
            static (world, plan, policy) => TownSystem.CheckCouncilConsent(world, plan, policy));

        internal static readonly ActionDefinition<CouncilBallotChoice, CouncilBallotActionPlan> Ballot = new(
            DecodeReferenceChoice(CouncilBallotChoice.Decode), static choice => choice.Encode(), TownSystem.GovernanceCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanCouncilBallot(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckCouncilBallot(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, ballot: plan.Selected, originalPlan: plan);
                return true;
            });
    }
}
