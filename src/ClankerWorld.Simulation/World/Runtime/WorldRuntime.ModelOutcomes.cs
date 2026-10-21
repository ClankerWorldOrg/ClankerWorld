using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null,
        string? setupBlocker = null) =>
        CognitionSystem.RecordModelAttempt(CognitionModelOutcomesWorld, id, status, acceptedCandidateId, setupBlocker);

    private void RecordModelCompletion(string id, CognitionAdmissionResult admission, string? failure) =>
        CognitionSystem.RecordModelCompletion(CognitionModelOutcomesWorld, id, admission, failure);
}
