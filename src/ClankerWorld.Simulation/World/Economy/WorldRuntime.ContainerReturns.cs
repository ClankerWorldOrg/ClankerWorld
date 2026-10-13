using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string ReturnEmptyVesselPrefix = VesselChoice.EmptyPrefix;

    private void AddEmptyVesselReturnCandidate(List<CognitionCandidate> candidates, string actor, AgentState person)
    {
        if (PlanVesselAction(actor, new(VesselActionKind.ReturnEmpty)) is not { } plan) return;
        AddVesselCandidate(candidates, plan, "Bring an empty household vessel back to the House for reuse.", 22, plan.SourceBuildingId);
    }
}
