using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void StartModelDecisions(ICognitionModelTasksWorld world, IEnumerable<string> completedIds)
    {
        var capacity = Math.Max(0, world.Host.MaximumDispatch - world.Host.ModelCalls.Count);
        if (capacity == 0)
            return;
        // Applying a reply can change its actor's choices. Refresh any retained
        // work in the next prepared tick before spending another model call.
        var excluded = world.Host.ModelCalls.Keys.Concat(world.Host.IdentityMoments.Keys).Concat(completedIds).Concat(world.Agents.Keys.Where(id => world.IsConversationBusy(id) && !world.ShouldDispatchConversationChoice(id))).ToHashSet(StringComparer.Ordinal);
        // A turn or another participant's decision can change the conversation
        // after this tick's observation was queued. Defer it before batching.
        foreach (var preview in world.PreviewModelRequests(excluded, world.IsQueuedObservationReady).Take(capacity))
        {
            var cancellation = new CancellationTokenSource();
            var task = Task.Run(async () =>
            {
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    return new ModelDecisionOutcome(await preview.DecideAsync(cancellation.Token).ConfigureAwait(false), null);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    return new ModelDecisionOutcome(null, CognitionProviderFailures.FromException(exception, cancellation.Token));
                }
            });
            world.Host.QueueModelDecision(preview.AgentId, new PendingModelDecision(preview.Request, task, cancellation, world.Agents[preview.AgentId].LastDecisionContext));
            world.RecordModelAttempt(preview.AgentId, "waiting");
            world.AppendEvent("hosted_decision_started", preview.AgentId);
        }
    }

    internal static bool IsQueuedObservationReady(ICognitionModelTasksWorld world, AgentObservation observation) => world.IsUndeliveredFinishedOrderDecision(observation) || observation.WorldTick == world.WorldTick && string.Equals(observation.ConversationChoiceContext, world.ConversationChoiceContextFor(observation.AgentId), StringComparison.Ordinal);

    internal static void CancelPendingModelCall(ICognitionModelTasksWorld world, string agentId, bool underRuntimeGate = true)
    {
        if (!world.Host.ForgetModelDecision(agentId, out var pending))
            return;
        world.RecordModelAttempt(agentId, "canceled");
        world.CancelProviderCall(pending.Cancellation, underRuntimeGate);
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
    }
}
