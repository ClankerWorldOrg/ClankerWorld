using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private bool IsChild(string actor) => AgentQueries.IsChild(agentWorld, actor);

    private bool ChildSocialAvailable(string actor, string target, string kind) =>
        FamilySystem.ChildSocialAvailable(FamilyChildWorld, actor, target, kind);

    private void AddChildCandidates(List<CognitionCandidate> candidates, string actor, AgentState state) =>
        FamilySystem.AddChildCandidates(FamilyChildWorld, candidates, actor, state);

    private void ApplyChildCandidate(string actor, AgentState state, string candidate) =>
        ApplyChildPlan(PlanChildCandidate(actor, candidate));

    private void ApplyChildSocial(ChildActionPlan plan) =>
        FamilySystem.ApplyChildSocial(FamilyChildWorld, plan);
}
