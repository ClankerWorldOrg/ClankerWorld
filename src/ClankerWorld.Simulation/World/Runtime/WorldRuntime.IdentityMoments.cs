using PreparedIdentityMoment = ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment;
using PendingIdentityMoment = ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private readonly IReadOnlyDictionary<string, PendingIdentityMoment> pendingIdentityMoments;

    private void DiscoverIdentityMoments() =>
        CognitionSystem.DiscoverIdentityMoments(CognitionIdentityMomentsWorld);


    private void StartIdentityMoments() =>
        CognitionSystem.StartIdentityMoments(CognitionIdentityMomentsWorld);

    private void PrepareIdentityMoments(Action<PreparedIdentityMoment> prepare,
        IReadOnlySet<string>? activeModelCallIds = null, IReadOnlySet<string>? activeIdentityIds = null) =>
        CognitionSystem.PrepareIdentityMoments(CognitionIdentityMomentsWorld, prepare, activeModelCallIds, activeIdentityIds);

    private void StartPreparedIdentityMoment(PreparedIdentityMoment prepared) =>
        CognitionSystem.StartPreparedIdentityMoment(CognitionIdentityMomentsWorld, prepared);

    private void CompleteIdentityMoments(IReadOnlyList<PendingIdentityMoment> completed,
        Func<PendingIdentityMoment, bool> providerIsCurrent) =>
        CognitionSystem.CompleteIdentityMoments(CognitionIdentityMomentsWorld, completed, providerIsCurrent);

    private bool IsIdentityMomentProviderCurrent(PendingIdentityMoment pending) =>
        CognitionSystem.IsIdentityMomentProviderCurrent(CognitionIdentityMomentsWorld, pending);

    private void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome,
        string? personality = null, string? aspiration = null) =>
        CognitionSystem.FinishIdentityMoment(CognitionIdentityMomentsWorld, id, kind, outcome, personality, aspiration);

    private void CancelIdentityMoments(bool underRuntimeGate = true) =>
        CognitionSystem.CancelIdentityMoments(CognitionIdentityMomentsWorld, underRuntimeGate);

    private void ReconcileIdentityMoments() =>
        CognitionSystem.ReconcileIdentityMoments(CognitionIdentityMomentsWorld);
}
