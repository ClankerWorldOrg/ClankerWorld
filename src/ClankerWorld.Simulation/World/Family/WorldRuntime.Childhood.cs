using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddChildCandidates(List<CognitionCandidate> candidates, string actor, AgentState state) =>
        FamilySystem.AddChildCandidates(FamilyChildWorld, candidates, actor, state);
}
