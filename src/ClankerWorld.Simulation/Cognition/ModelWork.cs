namespace ClankerWorld.Simulation.Cognition;

public enum ModelWorkKind { Decision, IdentityMoment, Will, Conversation }
public enum ModelWorkStage { Registered, Started, Finished, Admitted, Discarded, Cancelled }

/// <summary>Schedules external work without changing when a tick accepts its reply.</summary>
public interface IModelWorkScheduler
{
    Task<T> Schedule<T>(Func<Task<T>> work, bool startInline = false);
}

/// <summary>Production preserves background decisions and inline conversation reservation.</summary>
public sealed class ThreadPoolModelWorkScheduler : IModelWorkScheduler
{
    public static ThreadPoolModelWorkScheduler Instance { get; } = new();
    public Task<T> Schedule<T>(Func<Task<T>> work, bool startInline = false) =>
        startInline ? work() : Task.Run(work);
}

public sealed record ModelWorkTransition(string RequestId, string ActorId, ModelWorkKind Kind,
    ModelWorkStage Stage, long RegisteredTick, long? AdmittedTick = null);

/// <summary>Callbacks may run on provider threads. Observers must be thread safe and must not call the runtime.</summary>
public interface IModelWorkObserver
{
    void OnModelWork(ModelWorkTransition transition);
}

/// <summary>A live queue entry. Completion is the runtime wrapper task, not a provider-side signal.</summary>
public sealed record ModelWorkSnapshot(ModelWorkTransition Transition, Task Completion,
    CognitionDecisionRequest? DecisionRequest = null, AgentConversationTurnRequest? ConversationRequest = null);
