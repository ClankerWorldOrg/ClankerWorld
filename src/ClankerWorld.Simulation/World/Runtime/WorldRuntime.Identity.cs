using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionParentIdentity[]? InitialChildFamilyBackground(string id, AgentState physical) => CognitionSystem.InitialChildFamilyBackground(CognitionPersonalIdentityWorld, id, physical);

    private void ApplyPersonalIdentityChoice(string id, CognitionDecisionRequest request, CognitionDecisionResponse? response, CognitionAdmissionResult admission) => CognitionSystem.ApplyPersonalIdentityChoice(CognitionPersonalIdentityWorld, id, request, response, admission);
}
