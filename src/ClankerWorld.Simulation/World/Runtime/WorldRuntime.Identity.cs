using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void ApplyPersonalIdentityChoice(string id, CognitionDecisionRequest request,
        CognitionDecisionResponse? response, CognitionAdmissionResult admission) =>
        CognitionSystem.ApplyPersonalIdentityChoice(CognitionPersonalIdentityWorld, id, request, response, admission);
}
