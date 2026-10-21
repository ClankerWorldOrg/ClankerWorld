using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyWillWorld
{
    AgentStore PhysicalAgents { get; }
    TimeProvider ModelTimeProvider { get; }
    Task<T> ScheduleWillWork<T>(CognitionDecisionRequest request, Func<Task<T>> work);
    void RecordWillReplyAdmission(string requestId);
    void CancelWillWork(CognitionDecisionRequest request);
    SocietyCheckpoint Society { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    DeclarativeWorldContentState Content { get; }
    long WorldTick { get; }
    IDecisionProvider? CreateProvider(string actor);
    string? CognitionHouseholdId(string? household);
    int DestinationRoom(string warehouse);
    void MarkWillStarted(string estate);
    SocietyOperationResult ResolveWill(string estate, SocietyWillDirective? directive, string reason, string? finalWords, IReadOnlySet<string>? towns);
    void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate);
    void AppendEvent(WorldEventPayload payload);
}
