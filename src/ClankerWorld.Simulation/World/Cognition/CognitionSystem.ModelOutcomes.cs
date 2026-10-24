using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void RecordModelAttempt(ICognitionModelOutcomesWorld world, string id, string status, string? acceptedCandidateId = null,
        string? setupBlocker = null)
    {
        if (!world.Agents.TryGetValue(id, out var physical)) return;
        var previous = physical.LastModelAttempt;
        var next = new ModelAttempt(status, world.WorldTick,
            acceptedCandidateId ?? previous?.LastAcceptedCandidateId,
            acceptedCandidateId is not null ? world.WorldTick : previous?.LastAcceptedTick, setupBlocker);
        if (next == previous) return;
        world.PhysicalAgents.RecordModelAttempt(id, physical, next);
        world.MarkCheckpointCurrent();
        world.AppendEvent(new ModelAttemptStatusEventPayload(id, status));
    }


    internal static void RecordModelCompletion(ICognitionModelOutcomesWorld world, string id, CognitionAdmissionResult admission, string? failure)
    {
        if (admission.Accepted && !admission.FellBack)
            world.RecordModelAttempt(id, "ready", admission.Intention?.CandidateId);
        else
        {
            var outcome = failure ?? admission.Outcome;
            world.RecordModelAttempt(id, CognitionProviderFailures.Status(outcome),
                setupBlocker: outcome == "unsupported_request" ? "unsupported_request" : null);
        }
    }

}
