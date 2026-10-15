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
    internal static bool HasExplicitConversationProvider(ICognitionConversationTasksWorld world, string agentId)
    {
        if (world.Host.ProviderFactory is null)
            return false;
        try
        {
            return world.Host.ProviderFactory(agentId) is IAgentConversationProvider provider && provider.CanSpeakAs(agentId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    internal static void ApplyConversationTurnOutcome(ICognitionConversationTasksWorld world, PendingConversationTurn pending, ConversationTurnOutcome outcome, long worldTick, Func<PendingConversationTurn, bool> providerRouteIsCurrent) => world.ApplyConversationTurnOutcome(pending.Request, outcome.Response, outcome.Failure, worldTick, () => providerRouteIsCurrent(pending));

    internal static void CompleteConversationTurns(ICognitionConversationTasksWorld world, IReadOnlyList<PendingConversationTurn> completed, long worldTick, Func<PendingConversationTurn, bool> providerRouteIsCurrent)
    {
        foreach (var pending in completed.OrderBy(item => item.Request.ConversationId, StringComparer.Ordinal))
        {
            if (!pending.Task.IsCompleted)
                continue;
            world.ApplyConversationTurnOutcome(pending, pending.Task.GetAwaiter().GetResult(), worldTick, providerRouteIsCurrent);
        }
    }

    internal static void StartConversationTurns(ICognitionConversationTasksWorld world)
    {
        if (world.Host.ProviderFactory is null)
            return;
        var capacity = Math.Max(0, world.MaximumConversationProviderCallsPerTick - world.Host.ConversationTurns.Count);
        foreach (var conversation in world.Conversations.Where(item => item.Status == AgentConversationStatus.Ready || item.Status == AgentConversationStatus.WrapUp && !item.Turns.Any(turn => turn.IsWrapUp)).OrderBy(item => item.CreatedTick).ThenBy(item => item.Id, StringComparer.Ordinal).Take(capacity).ToArray())
        {
            if (!world.CanContinueConversation(conversation))
            {
                world.SuspendConversation(conversation.Id, AgentConversationInterruption.UrgentNeed);
                continue;
            }

            IAgentConversationProvider provider;
            try
            {
                provider = world.Host.ProviderFactory(conversation.CurrentSpeakerId ?? conversation.InitiatorId) as IAgentConversationProvider ?? throw new ProviderConversationUnavailableException("The personal conversation provider is unavailable.");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                world.SuspendConversation(conversation.Id, AgentConversationFailureClassifier.Classify(exception));
                continue;
            }

            var speakerId = conversation.Status == AgentConversationStatus.WrapUp ? conversation.InitiatorId : conversation.CurrentSpeakerId!;
            long providerEpoch;
            try
            {
                if (!provider.CanSpeakAs(speakerId))
                {
                    world.SuspendConversation(conversation.Id, AgentConversationInterruption.ProviderUnavailable);
                    continue;
                }

                providerEpoch = provider.ProviderEpoch;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                world.SuspendConversation(conversation.Id, AgentConversationFailureClassifier.Classify(exception));
                continue;
            }

            if (!AgentConversationRules.TryBeginTurn(conversation, world.WorldTick, world.Society.RunEpoch, out var started))
                continue;
            AgentConversationTurnRequest request;
            try
            {
                request = world.CreateConversationRequest(started, speakerId) with
                {
                    ExpectedProviderEpoch = providerEpoch
                };
                request.Validate();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                world.SuspendConversation(conversation.Id, AgentConversationFailureClassifier.Classify(exception));
                continue;
            }

            var cancellation = new CancellationTokenSource();
            // The provider runs on this gate-holding thread until its first
            // await, which comes after it reserves a paid model call.
            var task = world.RunConversationProviderUnderGate(provider, request, cancellation.Token);
            world.RecordStartedConversation(started);
            world.Host.QueueConversationTurn(started.Id, new PendingConversationTurn(request, task, cancellation, providerEpoch));
            world.AppendEvent("conversation_turn_started", $"{started.Id}:{speakerId}");
        }
    }

    internal static AgentConversationTurnRequest CreateConversationRequest(ICognitionConversationTasksWorld world, AgentConversation conversation, string speakerId)
    {
        var otherId = speakerId == conversation.InitiatorId ? conversation.InviteeId : conversation.InitiatorId;
        var speaker = world.Society.GetAgent(speakerId);
        var other = world.Society.GetAgent(otherId);
        var physical = world.Agents[speakerId];
        var purpose = conversation.Kind == AgentConversationKind.MarriageSurname ? AgentConversationPurpose.SurnameChoice : conversation.AwaitingWrapUp ? AgentConversationPurpose.WrapUp : AgentConversationPurpose.PublicTurn;
        return new AgentConversationTurnRequest(Guid.NewGuid().ToString("N"), conversation.Id, conversation.Revision, world.Society.RunEpoch, world.WorldTick, purpose, speakerId, speaker.Name, otherId, other.Name, physical.Personality, physical.Aspiration, conversation.Turns, purpose == AgentConversationPurpose.WrapUp ? world.CanProposeMarriage(conversation) ? [AgentConversationEffect.None, AgentConversationEffect.MutualTrust, AgentConversationEffect.Marriage] : [AgentConversationEffect.None, AgentConversationEffect.MutualTrust] : [AgentConversationEffect.None])
        {
            RequestedActivity = world.RequestedConversationActivity(conversation, speakerId),
            AllowedSurnames = purpose == AgentConversationPurpose.SurnameChoice ? AgentMarriageRules.AllowedSurnames(world.Marriages.Single(item => item.SurnameConversationId == conversation.Id)) : [],
        };
    }

    internal static void CancelPendingConversationTurn(ICognitionConversationTasksWorld world, string conversationId, AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true)
    {
        if (!world.Host.ForgetConversationTurn(conversationId, out var pending))
            return;
        world.CancelProviderCall(pending.Cancellation, underRuntimeGate);
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
        if (suspendCurrent && world.Conversations.FirstOrDefault(item => item.Id == conversationId) is { Status: AgentConversationStatus.AwaitingSpeaker } current && current.Revision == pending.Request.Revision)
            world.SuspendConversation(conversationId, interruption);
    }

    internal static void ReconcilePendingConversationTurns(ICognitionConversationTasksWorld world)
    {
        foreach (var (id, pending) in world.Host.ConversationTurns.ToArray())
        {
            var conversation = world.Conversations.FirstOrDefault(item => item.Id == id);
            if (conversation is null || conversation.Status != AgentConversationStatus.AwaitingSpeaker || conversation.Revision != pending.Request.Revision || world.Society.RunEpoch != pending.Request.RunEpoch)
            {
                world.CancelPendingConversationTurn(id, AgentConversationInterruption.Disconnected, suspendCurrent: false);
                continue;
            }

            try
            {
                var provider = world.Host.ProviderFactory?.Invoke(pending.Request.SpeakerId) as IAgentConversationProvider;
                if (provider is null || provider.ProviderEpoch != pending.ProviderEpoch || !provider.CanSpeakAs(pending.Request.SpeakerId))
                    world.CancelPendingConversationTurn(id, AgentConversationInterruption.ProviderUnavailable);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                world.CancelPendingConversationTurn(id, AgentConversationInterruption.ProviderUnavailable);
            }
        }
    }

    internal static bool IsConversationTurnProviderCurrent(ICognitionConversationTasksWorld world, PendingConversationTurn pending)
    {
        try
        {
            var provider = world.Host.ProviderFactory?.Invoke(pending.Request.SpeakerId) as IAgentConversationProvider;
            return provider is not null && provider.ProviderEpoch == pending.ProviderEpoch && provider.CanSpeakAs(pending.Request.SpeakerId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    internal static void CancelNoLongerAwaitingConversationTurns(ICognitionConversationTasksWorld world)
    {
        foreach (var (id, pending) in world.Host.ConversationTurns.ToArray())
        {
            var conversation = world.Conversations.FirstOrDefault(item => item.Id == id);
            if (conversation is null || conversation.Status != AgentConversationStatus.AwaitingSpeaker || conversation.Revision != pending.Request.Revision)
                world.CancelPendingConversationTurn(id, AgentConversationInterruption.UrgentNeed, suspendCurrent: false);
        }
    }
}
