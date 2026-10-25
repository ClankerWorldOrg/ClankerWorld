using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void StartModelDecisions(ICognitionModelTasksWorld world, IEnumerable<string> completedIds)
    {
        var capacity = Math.Max(0, world.Host.MaximumDispatch - world.Host.ModelCalls.Count);
        if (capacity == 0) return;
        // Applying a reply can change its actor's choices. Refresh any retained
        // work in the next prepared tick before spending another model call.
        var excluded = world.Host.ModelCalls.Keys.Concat(world.Host.IdentityMoments.Keys).Concat(completedIds)
            .Concat(world.Agents.Keys.Where(id => world.IsConversationBusy(id) && !world.ShouldDispatchConversationChoice(id)))
            .ToHashSet(StringComparer.Ordinal);
        // Plan checks may read other agents' intentions. Capture and filter
        // outside the society scheduler's gate so those reads can take it too.
        var observations = world.Capture().Cognition.Queue
            .Where(entry => !excluded.Contains(entry.AgentId) && world.IsQueuedObservationReady(entry.Observation))
            .ToDictionary(entry => entry.AgentId, entry => world.RefreshOfferedPlans(entry.Observation), StringComparer.Ordinal);
        // A turn or another participant's decision can change the conversation
        // after this tick's observation was queued. Defer it before batching.
        foreach (var preview in world.PreviewModelRequests(excluded, world.IsQueuedObservationReady,
                     observation => observations[observation.AgentId]).Take(capacity))
        {
            var cancellation = new CancellationTokenSource();
            var task = world.ScheduleModelWork(ModelWorkKind.Decision, preview.AgentId, preview.Request.RequestId, async () =>
            {
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    return new ModelDecisionOutcome(
                        await preview.DecideAsync(cancellation.Token).ConfigureAwait(false), null);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    return new ModelDecisionOutcome(null,
                        CognitionProviderFailures.FromException(exception, cancellation.Token));
                }
            }, decision: preview.Request);
            world.Host.QueueModelDecision(preview.AgentId, new PendingModelDecision(preview.Request, task, cancellation,
                            world.Agents[preview.AgentId].LastDecisionContext));
            world.RecordModelAttempt(preview.AgentId, "waiting");
            world.AppendEvent(new HostedDecisionStartedEventPayload(preview.AgentId));
        }
    }


    internal static bool IsQueuedObservationReady(ICognitionModelTasksWorld world, AgentObservation observation) => world.IsUndeliveredFinishedOrderDecision(observation) ||
        observation.WorldTick == world.WorldTick &&
        string.Equals(observation.ConversationChoiceContext,
            world.ConversationChoiceContextFor(observation.AgentId), StringComparison.Ordinal);

    internal static void CancelPendingModelCall(ICognitionModelTasksWorld world, string agentId, bool underRuntimeGate = true)
    {
        if (!world.Host.ForgetModelDecision(agentId, out var pending)) return;
        world.FinishModelWork(pending.Request.Observation.AgentId, pending.Request.RequestId, ModelWorkStage.Cancelled);
        world.RecordModelAttempt(agentId, "canceled");
        world.CancelProviderCall(pending.Cancellation, underRuntimeGate);
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
    }
    internal static bool HasExplicitConversationProvider(ICognitionConversationTasksWorld world, string agentId)
    {
        if (world.Host.ProviderFactory is null) return false;
        try
        {
            return world.Host.ProviderFactory(agentId) is IAgentConversationProvider provider &&
                provider.CanSpeakAs(agentId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }


    internal static void ApplyConversationTurnOutcome(ICognitionConversationTasksWorld world, PendingConversationTurn pending,
        ConversationTurnOutcome outcome, long worldTick, Func<PendingConversationTurn, bool> providerRouteIsCurrent)
    {
        if (world.ApplyConversationTurnOutcome(pending.Request, outcome.Response, outcome.Failure,
            worldTick, () => providerRouteIsCurrent(pending)))
            world.MarkModelReplyAdmitted(pending.Request.RequestId);
    }


    internal static void CompleteConversationTurns(ICognitionConversationTasksWorld world,
        IReadOnlyList<PendingConversationTurn> completed,
        long worldTick,
        Func<PendingConversationTurn, bool> providerRouteIsCurrent)
    {
        foreach (var pending in completed)
        {
            if (!pending.Task.IsCompleted) continue;
            world.ApplyConversationTurnOutcome(
                pending,
                pending.Task.GetAwaiter().GetResult(),
                worldTick,
                providerRouteIsCurrent);
        }
    }


    internal static void StartConversationTurns(ICognitionConversationTasksWorld world)
    {
        if (world.Host.ProviderFactory is null) return;
        var capacity = Math.Max(0, world.MaximumConversationProviderCallsPerTick - world.Host.ConversationTurns.Count);
        foreach (var conversation in world.Conversations
                     .Where(item => item.Status == AgentConversationStatus.Ready ||
                         item.Status == AgentConversationStatus.WrapUp && !item.Turns.Any(turn => turn.IsWrapUp))
                     .OrderBy(item => item.CreatedTick)
                     .ThenBy(item => item.Id, StringComparer.Ordinal)
                     .Take(capacity).ToArray())
        {
            if (!world.CanContinueConversation(conversation))
            {
                world.SuspendConversation(conversation.Id, AgentConversationInterruption.UrgentNeed);
                continue;
            }

            IAgentConversationProvider provider;
            try
            {
                provider = world.Host.ProviderFactory(conversation.CurrentSpeakerId ?? conversation.InitiatorId) as
                    IAgentConversationProvider ?? throw new ProviderConversationUnavailableException("The personal conversation provider is unavailable.");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                world.SuspendConversation(conversation.Id, AgentConversationFailureClassifier.Classify(exception));
                continue;
            }

            var speakerId = conversation.Status == AgentConversationStatus.WrapUp
                ? conversation.InitiatorId
                : conversation.CurrentSpeakerId!;
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
                request = world.CreateConversationRequest(started, speakerId) with { ExpectedProviderEpoch = providerEpoch };
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
            world.Host.QueueConversationTurn(started.Id, new PendingConversationTurn(
                request, task, cancellation, providerEpoch));
            world.AppendEvent(new ConversationTurnStartedEventPayload(speakerId, started.Id, speakerId));
        }
    }


    internal static AgentConversationTurnRequest CreateConversationRequest(ICognitionConversationTasksWorld world, AgentConversation conversation, string speakerId)
    {
        var otherId = speakerId == conversation.InitiatorId ? conversation.InviteeId : conversation.InitiatorId;
        var speaker = world.Society.GetAgent(speakerId);
        var other = world.Society.GetAgent(otherId);
        var physical = world.Agents[speakerId];
        var purpose = conversation.Kind == AgentConversationKind.MarriageSurname
            ? AgentConversationPurpose.SurnameChoice
            : conversation.AwaitingWrapUp
            ? AgentConversationPurpose.WrapUp
            : AgentConversationPurpose.PublicTurn;
        return new AgentConversationTurnRequest(
            Guid.NewGuid().ToString("N"),
            conversation.Id,
            conversation.Revision,
            world.Society.RunEpoch,
            world.WorldTick,
            purpose,
            speakerId,
            speaker.Name,
            otherId,
            other.Name,
            physical.Personality,
            physical.Aspiration,
            conversation.Turns,
            purpose == AgentConversationPurpose.WrapUp
                ? world.CanProposeMarriage(conversation)
                    ? [AgentConversationEffect.None, AgentConversationEffect.MutualTrust, AgentConversationEffect.Marriage]
                    : [AgentConversationEffect.None, AgentConversationEffect.MutualTrust]
                : [AgentConversationEffect.None])
        {
            RequestedActivity = world.RequestedConversationActivity(conversation, speakerId),
            AllowedSurnames = purpose == AgentConversationPurpose.SurnameChoice
                ? AgentMarriageRules.AllowedSurnames(world.Marriages.Single(item => item.SurnameConversationId == conversation.Id))
                : [],
        };
    }


    internal static void CancelPendingConversationTurn(ICognitionConversationTasksWorld world,
        string conversationId,
        AgentConversationInterruption interruption,
        bool suspendCurrent = true,
        bool underRuntimeGate = true)
    {
        if (!world.Host.ForgetConversationTurn(conversationId, out var pending)) return;
        world.FinishModelWork(pending.Request.SpeakerId, pending.Request.RequestId, ModelWorkStage.Cancelled);
        world.CancelProviderCall(pending.Cancellation, underRuntimeGate);
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
        if (suspendCurrent && world.Conversations.FirstOrDefault(item => item.Id == conversationId) is
            { Status: AgentConversationStatus.AwaitingSpeaker } current &&
            current.Revision == pending.Request.Revision)
            world.SuspendConversation(conversationId, interruption);
    }


    internal static void ReconcilePendingConversationTurns(ICognitionConversationTasksWorld world)
    {
        foreach (var (id, pending) in world.Host.ConversationTurns.ToArray())
        {
            var conversation = world.Conversations.FirstOrDefault(item => item.Id == id);
            if (conversation is null || conversation.Status != AgentConversationStatus.AwaitingSpeaker ||
                conversation.Revision != pending.Request.Revision ||
                world.Society.RunEpoch != pending.Request.RunEpoch)
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
            return provider is not null &&
                provider.ProviderEpoch == pending.ProviderEpoch &&
                provider.CanSpeakAs(pending.Request.SpeakerId);
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
            if (conversation is null || conversation.Status != AgentConversationStatus.AwaitingSpeaker ||
                conversation.Revision != pending.Request.Revision)
                world.CancelPendingConversationTurn(id, AgentConversationInterruption.UrgentNeed, suspendCurrent: false);
        }
    }

}
