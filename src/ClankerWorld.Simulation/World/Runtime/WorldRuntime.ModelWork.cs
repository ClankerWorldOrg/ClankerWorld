using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IModelWorkScheduler modelWorkScheduler = ThreadPoolModelWorkScheduler.Instance;
    private IModelWorkObserver? modelWorkObserver;
    private TimeProvider modelTimeProvider = TimeProvider.System;
    private readonly HashSet<string> admittedModelReplies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string ActorId, string RequestId), TrackedModelWork> modelWork = new();

    // Queue keys are saved actor, conversation or estate identities. Dictionary
    // slots (including reused slots) and transient request IDs are not replay order.
    private static T[] CompletedModelReplies<T>(IReadOnlyDictionary<string, T> pending, Func<T, Task> task) =>
        pending.Where(item => task(item.Value).IsCompleted)
            .OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Value).ToArray();

    private sealed class TrackedModelWork(ModelWorkTransition transition,
        CognitionDecisionRequest? decision, AgentConversationTurnRequest? conversation)
    {
        public readonly object Sync = new();
        public ModelWorkTransition Transition = transition;
        public Task Completion = Task.CompletedTask;
        public readonly CognitionDecisionRequest? Decision = decision;
        public readonly AgentConversationTurnRequest? Conversation = conversation;
        public ModelWorkSnapshot Snapshot()
        {
            lock (Sync) return new(Transition, Completion, Decision, Conversation);
        }
    }

    /// <summary>All four live queues, including finished replies awaiting a committed tick.</summary>
    public IReadOnlyList<ModelWorkSnapshot> PendingModelWork
    {
        get
        {
            gate.Wait();
            try
            {
                return modelWork.Values.Select(item => item.Snapshot()).OrderBy(item => item.Transition.RegisteredTick)
                    .ThenBy(item => item.Transition.Kind).ThenBy(item => item.Transition.ActorId, StringComparer.Ordinal).ToArray();
            }
            finally { gate.Release(); }
        }
    }

    // Test harnesses attach before starting any work, including to fixture-created runtimes.
    internal void ConfigureModelWork(IModelWorkScheduler scheduler, IModelWorkObserver? observer = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        gate.Wait();
        try
        {
            if (!modelWork.IsEmpty) throw new InvalidOperationException("Configure model work before starting calls.");
            modelWorkScheduler = scheduler;
            modelWorkObserver = observer;
            modelTimeProvider = timeProvider ?? TimeProvider.System;
        }
        finally { gate.Release(); }
    }

    private Task<T> ScheduleModelWork<T>(ModelWorkKind kind, string actorId, string requestId,
        Func<Task<T>> work, CognitionDecisionRequest? decision = null,
        AgentConversationTurnRequest? conversation = null, bool startInline = false)
    {
        var tracked = new TrackedModelWork(new(requestId, actorId, kind, ModelWorkStage.Registered, WorldTick),
            decision, conversation);
        if (!modelWork.TryAdd((actorId, requestId), tracked)) throw new InvalidOperationException("Duplicate model work request.");
        modelWorkObserver?.OnModelWork(tracked.Transition);
        var task = modelWorkScheduler.Schedule(async () =>
        {
            TransitionModelWork(tracked, ModelWorkStage.Started);
            try { return await work().ConfigureAwait(false); }
            finally { TransitionModelWork(tracked, ModelWorkStage.Finished); }
        }, startInline);
        lock (tracked.Sync) tracked.Completion = task;
        return task;
    }

    private void TransitionModelWork(TrackedModelWork tracked, ModelWorkStage stage, long? admittedTick = null)
    {
        lock (tracked.Sync)
        {
            if (tracked.Transition.Stage is ModelWorkStage.Admitted or ModelWorkStage.Discarded or ModelWorkStage.Cancelled) return;
            tracked.Transition = tracked.Transition with { Stage = stage, AdmittedTick = stage == ModelWorkStage.Admitted ? admittedTick : null };
            modelWorkObserver?.OnModelWork(tracked.Transition);
        }
    }

    private void FinishModelWork(string actorId, string requestId, ModelWorkStage stage, long? admittedTick = null)
    {
        if (!modelWork.TryRemove((actorId, requestId), out var tracked)) return;
        TransitionModelWork(tracked, stage, admittedTick);
    }
}
