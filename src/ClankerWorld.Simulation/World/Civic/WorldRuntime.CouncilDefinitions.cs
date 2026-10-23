namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static class CouncilActions
    {
        internal static readonly ActionDefinition<CouncilConsentChoice, CouncilConsentActionPlan> Consent = new(
            DecodeReferenceChoice(CouncilConsentChoice.Decode), static choice => choice.Encode(), TownSystem.GovernanceCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanCouncilConsent(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckCouncilConsent(world.civicWorld, plan, policy),
            ApplyCivicPlan);

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
