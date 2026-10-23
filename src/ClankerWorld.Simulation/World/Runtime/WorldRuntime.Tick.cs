using PreparedIdentityMoment = ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment;
using PendingIdentityMoment = ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment;
using PendingConversationTurn = ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn;
using PendingModelDecision = ClankerWorld.Simulation.World.CognitionSystem.PendingModelDecision;
using PendingWillDecision = ClankerWorld.Simulation.World.FamilyWillService.PendingWillDecision;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public ValueTask<WorldStepResult> AdvanceOneTickAsync(CancellationToken cancellationToken = default) =>
        AdvanceOneTickAsync(null, cancellationToken);

    public async ValueTask<WorldStepResult> AdvanceOneTickAsync(
        Func<bool>? commitPermitted, CancellationToken cancellationToken = default) =>
        await AdvanceOneTickCoreAsync(false, commitPermitted, null, cancellationToken).ConfigureAwait(false);

    /// <summary>Playable-host path: model decisions run between ticks, never inside a tick transaction.</summary>
    public ValueTask<WorldStepResult> AdvanceOneTickNonBlockingAsync(
        Func<bool>? commitPermitted = null, CancellationToken cancellationToken = default) =>
        AdvanceOneTickCoreAsync(true, commitPermitted, null, cancellationToken);

    /// <summary>Playable-host path that binds birth choices into the admitted tick transaction.</summary>
    public ValueTask<WorldStepResult> AdvanceOneTickNonBlockingAsync(
        Func<bool>? commitPermitted,
        Func<WorldRuntime, IReadOnlyList<WorldEventEntry>, IReadOnlyList<PreparedChildModelSelection>> prepareChildModelSelections,
        CancellationToken cancellationToken = default) =>
        AdvanceOneTickCoreAsync(true, commitPermitted, prepareChildModelSelections, cancellationToken);

    private async ValueTask<WorldStepResult> AdvanceOneTickCoreAsync(
        bool deferModelCalls, Func<bool>? commitPermitted,
        Func<WorldRuntime, IReadOnlyList<WorldEventEntry>, IReadOnlyList<PreparedChildModelSelection>>? prepareChildModelSelections,
        CancellationToken cancellationToken,
        Func<WorldRuntime, Action>? prepareCheckpoint = null,
        WorldTickFailurePolicy failurePolicy = WorldTickFailurePolicy.Strict,
        WorldActionRefusal? replayRefusal = null)
    {
        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorldRuntimeState baseline;
            ConversationState baselineConversations;
            FarmingSystem baselineFarming;
            CivicState baselineCivic;
            long baselineEventId;
            int baselineOrderCancellationCount;
            IReadOnlyDictionary<string, PlannedRoute> routesBefore;
            PendingModelDecision[] completed = [];
            IReadOnlySet<string> activeModelCallIds = new HashSet<string>(StringComparer.Ordinal);
            PendingWillDecision[] completedWills = [];
            PendingConversationTurn[] completedConversationTurns = [];
            PendingIdentityMoment[] completedIdentityMoments = [];
            IReadOnlySet<string> retainedIdentityIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlySet<string> retainedModelCallIds = new HashSet<string>(StringComparer.Ordinal);
            string[] activeWillIds = [];
            IReadOnlyDictionary<string, string> inactiveWillReasons = new Dictionary<string, string>();
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (society.Checkpoint.IsPaused)
                {
                    return new WorldStepResult(false, "paused", WorldTick, [], []);
                }
                if (deferModelCalls)
                {
                    foreach (var (id, pending) in pendingModelCalls.ToArray())
                    {
                        if (!society.Checkpoint.Agents.Any(person => person.Id == id && person.Status == SocietyAgentStatus.Active) ||
                            society.CurrentProviderEpoch(id) != pending.Request.ProviderEpoch ||
                            !IsOrderDecisionObservationCurrent(pending.Request.Observation))
                        {
                            CancelPendingModelCall(id);
                        }
                    }
                    foreach (var (id, pending) in pendingWills.ToArray())
                    {
                        var estate = society.Checkpoint.Estates.FirstOrDefault(item => item.Id == id);
                        if (estate is null || estate.WillStatus != "pending")
                        {
                            CancelPendingWill(id);
                            continue;
                        }
                        if (society.Checkpoint.RunEpoch != pending.Request.Observation.RunEpoch)
                        {
                            CancelPendingWill(id, "run_epoch_changed");
                            continue;
                        }
                        try
                        {
                            var currentProvider = providerFactory?.Invoke(estate.DeceasedId) ?? new DeterministicDecisionProvider();
                            if (currentProvider.KindFor(pending.Request.Observation) != DecisionProviderKind.LargeLanguageModel ||
                                currentProvider.ProviderEpoch != pending.Request.ProviderEpoch)
                                CancelPendingWill(id, "provider_changed");
                        }
                        catch (Exception exception) when (exception is not OutOfMemoryException)
                        {
                            CancelPendingWill(id, "provider_unavailable");
                        }
                    }
                    completed = CompletedModelReplies(pendingModelCalls, item => item.Task);
                    activeModelCallIds = pendingModelCalls.Keys.ToHashSet(StringComparer.Ordinal);
                    completedWills = CompletedModelReplies(pendingWills, item => item.Task);
                    activeWillIds = pendingWills.Keys.ToArray();
                    inactiveWillReasons = new Dictionary<string, string>(pendingWillCancellationReasons, StringComparer.Ordinal);
                }
                ReconcilePendingConversationTurns();
                ReconcileIdentityMoments();
                completedIdentityMoments = CompletedModelReplies(pendingIdentityMoments, item => item.Task);
                retainedIdentityIds = pendingIdentityMoments.Keys.Except(completedIdentityMoments.Select(item => item.Request.Observation.AgentId),
                    StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
                retainedModelCallIds = pendingModelCalls.Keys.Except(completed.Select(item => item.Request.Observation.AgentId),
                    StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
                completedConversationTurns = CompletedModelReplies(pendingConversationTurns, item => item.Task);
                baseline = CaptureState();
                baselineConversations = conversationSystem.CheckpointState;
                baselineFarming = farmingSystem.Fork();
                baselineCivic = townSystem.CheckpointState;
                baselineEventId = nextEventId;
                baselineOrderCancellationCount = orderCancellations.Count;
                routesBefore = plannedRoutes;
            }
            finally
            {
                gate.Release();
            }

            var tickTimer = System.Diagnostics.Stopwatch.StartNew();
            if (replayRefusal is not null && (replayRefusal.RefusedTick != checked(baseline.Society.Society.WorldTick + 1) ||
                replayRefusal.RetryAfterTick - replayRefusal.RefusedTick != baseline.WorldSystems!.Config.TicksPerDay ||
                !baseline.Society.Society.Agents.Any(person => person.Id == replayRefusal.AgentId &&
                    person.Status == SocietyAgentStatus.Active)))
                throw new InvalidDataException("The replayed refusal does not belong to this tick.");
            var recorder = failurePolicy == WorldTickFailurePolicy.RefuseSingleAction
                ? new TickInputRecorder(providerFactory) : null;
            Func<string, IDecisionProvider>? attemptProviders = recorder is null ? providerFactory : recorder.ProviderFor;
            Dictionary<string, string>? originalActions = null;
            bool BaselineIsCurrent() => nextEventId == baselineEventId && WorldTick == baseline.Society.Society.WorldTick &&
                historyArchiveHead == baseline.HistoryArchiveHead && orderCancellations.Count == baselineOrderCancellationCount &&
                (recorder is null || recorder.IsCurrent);
            WorldStepResult? Interrupted() => commitPermitted is not null && !commitPermitted()
                ? new(false, "waiting_for_client", WorldTick, [], [])
                : !BaselineIsCurrent() ? new(false, "tick_superseded_by_owner_change", WorldTick, [], []) : null;

            async Task<PreparedTick> PrepareAttemptAsync(IReadOnlySet<string>? withheld, WorldActionRefusal? refusal = null)
            {
                var proposed = RestoreCore(baseline, attemptProviders, maxCognitionDispatchPerCycle,
                    trustedPreparedState: true, preparedConversations: baselineConversations,
                    preparedCivic: baselineCivic, preparedFarming: baselineFarming);
                proposed.previousPlannedRoutes = routesBefore;
                proposed.CopyRecipeProjectPlansFrom(this);
                proposed.CopyConstructionProjectPlansFrom(this);
                proposed.withheldActions = withheld;
                proposed.attemptedActions = recorder is null && replayRefusal is null ? null : new(StringComparer.Ordinal);
                if (withheld is null) originalActions = proposed.attemptedActions;
                try
                {
                    var result = await proposed.AdvancePreparedTickAsync(deferModelCalls, completed, activeModelCallIds, completedWills,
                        activeWillIds, inactiveWillReasons, completedConversationTurns, cancellationToken).ConfigureAwait(false);
                    tickTimer.Stop();
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    tickTimer.Start();
                    var gateHeld = true;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (Interrupted() is { } interrupted) return new(proposed, interrupted, null);
                        if (!result.Advanced) return new(proposed, result, null);
                        if (prepareChildModelSelections is not null)
                        {
                            gate.Release();
                            gateHeld = false;
                            var births = result.Events.Where(item => item.Kind == "child_born")
                                .Select(item => item.Detail).ToHashSet(StringComparer.Ordinal);
                            foreach (var prepared in prepareChildModelSelections(proposed, result.Events))
                            {
                                if (!births.Contains(prepared.ChildId))
                                    throw new InvalidOperationException("A child model selection must belong to a child born in the committed tick.");
                                proposed.ApplyChildModelSelection(prepared.ChildId, prepared.Selection);
                            }
                            tickTimer.Stop();
                            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                            tickTimer.Start();
                            gateHeld = true;
                            cancellationToken.ThrowIfCancellationRequested();
                            if (Interrupted() is { } changed) return new(proposed, changed, null);
                        }
                        proposed.CompleteConversationTurns(completedConversationTurns, proposed.WorldTick, IsConversationTurnProviderCurrent);
                        proposed.CompleteClosedTalkOrders();
                        proposed.CompleteIdentityMoments(completedIdentityMoments, IsIdentityMomentProviderCurrent);
                        if (deferModelCalls)
                            proposed.ProcessWillDecisions(completedWills, activeWillIds, inactiveWillReasons, IsWillDecisionProviderCurrent);
                        if (refusal is not null)
                        {
                            if (replayRefusal is not null &&
                                proposed.attemptedActions?.GetValueOrDefault(refusal.AgentId) != refusal.CandidateId &&
                                !proposed.IsReplayChoice(refusal.AgentId, refusal.CandidateId))
                                throw new InvalidDataException("The refused choice does not match this actor's action.");
                            proposed.RecordActionRefusal(refusal);
                        }
                        List<PreparedIdentityMoment>? identityMoments = null;
                        if (prepareCheckpoint is not null)
                        {
                            // Consume each opportunity in the durable proposal before any
                            // potentially paid request starts. Trials only stage requests.
                            identityMoments = [];
                            proposed.PrepareIdentityMoments(identityMoments.Add, retainedModelCallIds, retainedIdentityIds);
                        }
                        gate.Release();
                        gateHeld = false;
                        return new(proposed, result, prepareCheckpoint?.Invoke(proposed), identityMoments);
                    }
                    finally { if (gateHeld) gate.Release(); }
                }
                catch { proposed.Dispose(); throw; }
            }

            PreparedTick preparedTick;
            try
            {
                preparedTick = await PrepareAttemptAsync(replayRefusal is null ? null :
                    new HashSet<string>([replayRefusal.AgentId], StringComparer.Ordinal), replayRefusal).ConfigureAwait(false);
            }
            catch (Exception exception) when (recorder is not null && exception is not (OutOfMemoryException or OperationCanceledException))
            {
                var check = ActionFailureCheck(exception);
                var actors = baseline.Society.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active)
                    .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
                WorldStepResult? interruption = null;
                var differentFault = false;
                async Task<bool> SucceedsWithoutAsync(IEnumerable<string> withheld)
                {
                    try
                    {
                        using var trial = await PrepareAttemptAsync(withheld.ToHashSet(StringComparer.Ordinal)).ConfigureAwait(false);
                        if (!trial.Result.Advanced) interruption = trial.Result;
                        return trial.Result.Advanced;
                    }
                    catch (Exception trialFault) when (trialFault is not (OutOfMemoryException or OperationCanceledException))
                    {
                        differentFault |= ActionFailureCheck(trialFault) != check;
                        return false;
                    }
                }
                if (!await SucceedsWithoutAsync(actors).ConfigureAwait(false))
                {
                    if (interruption is not null) return interruption;
                    throw;
                }
                var candidates = actors;
                while (candidates.Length > 1)
                {
                    var half = candidates.Take(candidates.Length / 2).ToArray();
                    candidates = await SucceedsWithoutAsync(half).ConfigureAwait(false)
                        ? half : candidates.Skip(half.Length).ToArray();
                    if (interruption is not null) return interruption;
                    if (differentFault) throw;
                }
                if (candidates.Length == 0 || !await SucceedsWithoutAsync(candidates).ConfigureAwait(false) ||
                    originalActions is null || !originalActions.TryGetValue(candidates[0], out var candidate) || candidate == "safe_idle")
                {
                    if (interruption is not null) return interruption;
                    throw;
                }
                var refusedTick = checked(baseline.Society.Society.WorldTick + 1);
                var refusal = new WorldActionRefusal(candidates[0], candidate, refusedTick,
                    checked(refusedTick + baseline.WorldSystems!.Config.TicksPerDay), check);
                preparedTick = await PrepareAttemptAsync(candidates.ToHashSet(StringComparer.Ordinal), refusal).ConfigureAwait(false);
            }
            using (preparedTick)
            {
                var proposed = preparedTick.Runtime;
                var result = preparedTick.Result;
                tickTimer.Stop();
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                tickTimer.Start();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Interrupted() is { } interrupted) return interrupted;
                    if (!result.Advanced) return result;
                    recorder?.StopRecording();
                    tickTimer.Stop();
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo? writeFailure = null;
                    try { preparedTick.WriteCheckpoint?.Invoke(); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        // The valid advanced world is retained for the existing paused write retry.
                        writeFailure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                    }
                    tickTimer.Start();
                    CommitPreparedTick(proposed);
                    plannedRoutes = proposed.plannedRoutes;
                    tickTimer.Stop();
                    lastTickMilliseconds = Math.Round(tickTimer.Elapsed.TotalMilliseconds, 1);
                    foreach (var item in completedIdentityMoments)
                    {
                        FinishModelWork(item.Request.Observation.AgentId, item.Request.RequestId, proposed.admittedModelReplies.Contains(item.Request.RequestId)
                            ? ModelWorkStage.Admitted : ModelWorkStage.Discarded, WorldTick);
                        cognitionSystem.ForgetCompletedIdentityMoment(item.Request.Observation.AgentId);
                        item.Cancellation.Dispose();
                    }
                    if (writeFailure is null && (commitPermitted is null || commitPermitted()))
                        foreach (var identityMoment in preparedTick.IdentityMoments ?? [])
                            StartPreparedIdentityMoment(identityMoment);
                    if (deferModelCalls)
                    {
                        foreach (var item in completedWills)
                            FinishModelWork(item.Request.Observation.AgentId, item.Request.RequestId,
                                proposed.admittedModelReplies.Contains(item.Request.RequestId)
                                    ? ModelWorkStage.Admitted : ModelWorkStage.Discarded, WorldTick);
                        foreach (var id in pendingWills.Keys.Where(id =>
                                     society.Checkpoint.Estates.All(estate => estate.Id != id || estate.WillStatus != "pending"))
                                 .ToArray())
                            CancelPendingWill(id);
                        foreach (var item in completed)
                        {
                            var decision = result.Decisions.FirstOrDefault(decision => decision.AgentId == item.Request.Observation.AgentId);
                            FinishModelWork(item.Request.Observation.AgentId, item.Request.RequestId, decision?.Admission is { Accepted: true, FellBack: false }
                                ? ModelWorkStage.Admitted : ModelWorkStage.Discarded, WorldTick);
                            cognitionSystem.ForgetCompletedModelDecision(item.Request.Observation.AgentId);
                            item.Cancellation.Dispose();
                        }
                        foreach (var item in completedWills)
                        {
                            familyWills.ForgetCompleted(item.EstateId);
                            item.Cancellation.Dispose();
                        }
                        foreach (var item in completedConversationTurns)
                        {
                            FinishModelWork(item.Request.SpeakerId, item.Request.RequestId, proposed.admittedModelReplies.Contains(item.Request.RequestId)
                                ? ModelWorkStage.Admitted : ModelWorkStage.Discarded, WorldTick);
                            cognitionSystem.ForgetCompletedConversationTurn(item.Request.ConversationId);
                            item.Cancellation.Dispose();
                        }
                        CancelNoLongerAwaitingConversationTurns();
                        foreach (var id in inactiveWillReasons.Keys)
                            familyWills.ForgetCancellation(id);
                        if (writeFailure is null && (commitPermitted is null || commitPermitted())) StartWillDecisions();
                        if (writeFailure is null && (commitPermitted is null || commitPermitted()))
                            StartModelDecisions(completed.Select(item => item.Request.Observation.AgentId));
                    }
                    else
                    {
                        foreach (var item in completedConversationTurns)
                        {
                            FinishModelWork(item.Request.SpeakerId, item.Request.RequestId, proposed.admittedModelReplies.Contains(item.Request.RequestId)
                                ? ModelWorkStage.Admitted : ModelWorkStage.Discarded, WorldTick);
                            cognitionSystem.ForgetCompletedConversationTurn(item.Request.ConversationId);
                            item.Cancellation.Dispose();
                        }
                        CancelNoLongerAwaitingConversationTurns();
                    }
                    if (writeFailure is null && (commitPermitted is null || commitPermitted())) StartConversationTurns();
                    if (prepareCheckpoint is null && writeFailure is null && (commitPermitted is null || commitPermitted())) StartIdentityMoments();
                    writeFailure?.Throw();
                    return result with { Events = events.Where(item => item.EventId >= baselineEventId).ToArray() };
                }
                finally { gate.Release(); }
            }
        }
        finally { tickGate.Release(); }
    }

    private void StartModelDecisions(IEnumerable<string> completedIds) =>
        CognitionSystem.StartModelDecisions(CognitionModelTasksWorld, completedIds);

    // Queued work with no call in flight is rebuilt before dispatch, except an
    // order that finished before the model ever saw it: its exact message is still sent.
    private bool IsQueuedObservationReady(AgentObservation observation) =>
        CognitionSystem.IsQueuedObservationReady(CognitionModelTasksWorld, observation);

    private AgentObservation RefreshOfferedPlans(AgentObservation observation)
    {
        if (IsUndeliveredFinishedOrderDecision(observation)) return observation;
        // Earlier actions can spend queued supplies before this model call starts.
        var candidates = observation.Candidates.Where(candidate =>
            candidate.ActionPlan is not { } plan || CheckActionPlan(plan).Allowed).ToArray();
        if (candidates.Length == observation.Candidates.Count) return observation;
        return observation with
        {
            Candidates = candidates,
            ObservationDigest = ObservationDigest(observation.AgentId, observation.WorldId!, agents[observation.AgentId],
                candidates, observation.RetrievedMemories ?? [], observation.MemoryCompactionCandidates ?? [],
                observation.KnownMapFacts ?? [], observation.Self!, observation.ObserverGuidance ?? [], observation.MemorySummaryOptions),
        };
    }

    private void CancelPendingModelCall(string agentId, bool underRuntimeGate = true) =>
        CognitionSystem.CancelPendingModelCall(CognitionModelTasksWorld, agentId, underRuntimeGate);

    public void CancelPendingModelCalls()
    {
        gate.Wait();
        try
        {
            foreach (var id in pendingModelCalls.Keys.ToArray()) CancelPendingModelCall(id);
            foreach (var id in pendingWills.Keys.ToArray()) CancelPendingWill(id);
            CancelIdentityMoments();
            foreach (var id in pendingConversationTurns.Keys.ToArray())
                CancelPendingConversationTurn(id, AgentConversationInterruption.Disconnected);
            SuspendAllConversations(AgentConversationInterruption.Disconnected);
        }
        finally { gate.Release(); }
    }

    private void CommitPreparedTick(WorldRuntime proposed)
    {
        // Transfer the committed society; disposing the proposal retires the old one.
        (society, proposed.society) = (proposed.society, society);
        worldSeed = proposed.worldSeed;
        map = proposed.map;
        mapAuthoring = proposed.mapAuthoring;
        farmingSystem = proposed.farmingSystem;
        transportSystem = proposed.transportSystem;
        animalSystem = proposed.animalSystem;
        geographyOptions = proposed.geographyOptions;
        contentRegistry = proposed.contentRegistry;
        environmentSystem = proposed.environmentSystem;
        survivalState = proposed.survivalState;
        townSystem = proposed.townSystem;
        familySystem = proposed.familySystem;
        worldContent = proposed.worldContent;
        constructionSystem = proposed.constructionSystem;
        productionSystem = proposed.productionSystem;
        assetReservations = proposed.assetReservations;
        agentStore = proposed.agentStore;
        resources = proposed.resources;
        communitySystem = proposed.communitySystem;
        orderSystem = proposed.orderSystem;
        worldEventLog = proposed.worldEventLog;
        checkpointSchemaVersion = proposed.checkpointSchemaVersion;
        jevEnabled = proposed.jevEnabled;
        routineHelper = proposed.routineHelper;
        jevPolicyRevision = proposed.jevPolicyRevision;
        conversationSystem = proposed.conversationSystem;
        actionRefusals = proposed.actionRefusals;
        refusedActionCount = proposed.refusedActionCount;
        CopyRecipeProjectPlansFrom(proposed);
        CopyConstructionProjectPlansFrom(proposed);
    }

    /// <summary>Rewind this paused world to a validated checkpoint of the same world.</summary>
    public void LoadPausedCheckpoint(WorldRuntimeState checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        tickGate.Wait();
        try
        {
            gate.Wait();
            try
            {
                if (!string.Equals(checkpoint.WorldSeed, worldSeed, StringComparison.Ordinal) ||
                    !string.Equals(checkpoint.Society.Society.WorldId, society.Checkpoint.WorldId, StringComparison.Ordinal))
                    throw new InvalidDataException("A checkpoint belongs to a different world.");
                if (!society.Checkpoint.IsPaused)
                    throw new InvalidOperationException("Pause the world before loading a checkpoint.");
                using var restored = Restore(checkpoint, providerFactory,
                    maxCognitionDispatchPerCycle);
                // Loading never resumes a world implicitly, even if the saved
                // checkpoint was taken while it was running.
                restored.Pause();
                var nextObserverGeneration = checked(observerGeneration + 1);
                foreach (var id in pendingModelCalls.Keys.ToArray()) CancelPendingModelCall(id);
                foreach (var id in pendingWills.Keys.ToArray()) CancelPendingWill(id);
                CancelIdentityMoments();
                foreach (var id in pendingConversationTurns.Keys.ToArray())
                    CancelPendingConversationTurn(id, AgentConversationInterruption.OwnerPaused, suspendCurrent: false);
                CommitPreparedTick(restored);
                observerGeneration = nextObserverGeneration;
                // Routes and timing described the world as it was; the next tick measures again.
                plannedRoutes = new(StringComparer.Ordinal);
                lastTickMilliseconds = null;
            }
            finally { gate.Release(); }
        }
        finally { tickGate.Release(); }
    }

    /// <summary>Replace the selected world while the current world is paused.</summary>
    public void SwitchPausedWorld(WorldRuntimeState checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        tickGate.Wait();
        try
        {
            gate.Wait();
            try
            {
                if (!society.Checkpoint.IsPaused)
                    throw new InvalidOperationException("Pause the world before selecting another world.");
                using var restored = Restore(checkpoint, providerFactory,
                    maxCognitionDispatchPerCycle);
                restored.Pause();
                var nextObserverGeneration = checked(observerGeneration + 1);
                foreach (var id in pendingModelCalls.Keys.ToArray()) CancelPendingModelCall(id);
                foreach (var id in pendingWills.Keys.ToArray()) CancelPendingWill(id);
                CancelIdentityMoments();
                foreach (var id in pendingConversationTurns.Keys.ToArray())
                    CancelPendingConversationTurn(id, AgentConversationInterruption.OwnerPaused, suspendCurrent: false);
                CommitPreparedTick(restored);
                observerGeneration = nextObserverGeneration;
                // Routes and timing described the world as it was; the next tick measures again.
                plannedRoutes = new(StringComparer.Ordinal);
                lastTickMilliseconds = null;
            }
            finally { gate.Release(); }
        }
        finally { tickGate.Release(); }
    }

    private async ValueTask<WorldStepResult> AdvancePreparedTickAsync(
        bool deferModelCalls, IReadOnlyList<PendingModelDecision> completed, IReadOnlySet<string> activeModelCallIds,
        IReadOnlyList<PendingWillDecision> completedWills, IReadOnlyList<string> activeWillIds,
        IReadOnlyDictionary<string, string> inactiveWillReasons,
        IReadOnlyList<PendingConversationTurn> completedConversationTurns,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (society.Checkpoint.IsPaused)
            {
                return new WorldStepResult(false, "paused", WorldTick, [], []);
            }

            guardianPlacementActions.Reset();
            var startingEvent = events.Count;
            var targetTick = checked(WorldTick + 1);
            actionRefusals.RemoveAll(refusal => refusal.RetryAfterTick <= targetTick);
            StageHearthContent();
            StageHouseContent();
            StageBuiltInContent(HouseToolsContent.PackageId, HouseContent.PackageId, HouseToolsContent.Create, "house_tools_content_staged");
            StageWarehouseContent();
            StageFarmContent();
            StageBlacksmithContent();
            StageOrnamentContent();
            StageHouseCookingContent();
            StageRestaurantContent();
            StagePotteryContent();
            StageSiloContent();
            StageTailorContent();
            StageCareContent();
            StageBuiltInContent(TownHallContent.PackageId, HouseContent.PackageId, TownHallContent.Create, "town_hall_content_staged");
            StageBuiltInContent(KnowledgeContent.PackageId, HouseContent.PackageId, KnowledgeContent.Create, "knowledge_content_staged");
            StageBuiltInContent(MarketContent.PackageId, TownHallContent.PackageId, MarketContent.Create, "market_content_staged");
            StageBuiltInContent(PortContent.PackageId, TownHallContent.PackageId, PortContent.Create, "port_content_staged");
            StageBuiltInContent(StreetLanternContent.PackageId, HouseContent.PackageId, StreetLanternContent.Create, "street_lantern_content_staged");
            StageBuiltInContent(StoreContent.PackageId, HouseContent.PackageId, StoreContent.Create, "store_content_staged");
            StageBuildingVariantContent();
            StageAnimalContent();
            var readyPackages = contentRegistry.GetActivationCandidates(targetTick);
            var reservationPreview = WorldAssetReservationLedger.Restore(
                assetReservations.ExportState(),
                assetReservations.Policy);
            foreach (var package in readyPackages)
            {
                var reservation = reservationPreview.TryReservePackage(
                    package.Manifest.PackageId,
                    package.Manifest.AssetReservations ?? [],
                    targetTick);
                if (!reservation.IsSuccess)
                {
                    return new WorldStepResult(
                        false,
                        $"asset_reservation_rejected:{reservation.FailureCode ?? "invalid"}",
                        WorldTick,
                        [],
                        []);
                }
            }

            assetReservations = reservationPreview;
            MaintainProductionOrdersBeforeTick();
            OrderSystem.MaintainConstructionOrdersBeforeTick(ConstructionOrdersWorld);
            OrderSystem.MaintainExpansionOrdersBeforeTick(ExpansionOrdersWorld);
            society.AdvanceTo(targetTick, TownStoresForDueEstates(targetTick), DefaultEstateDivisionsForDueEstates(targetTick));
            foreach (var boat in boatTransport.Boats.Where(boat => boat.GroundCargoLotIds is { Count: > 0 }).ToArray())
                MoveBoatGroundCargo(boat, preserveCustody: true);
            AdvanceEnvironmentWeather();

            var activatedWorldContent = worldContent;
            foreach (var package in readyPackages)
            {
                activatedWorldContent = ContentDefinitionPayloadCodec.ApplyPackage(
                    activatedWorldContent,
                    package.Manifest);
            }

            foreach (var activated in contentRegistry.ActivateReady(targetTick))
            {
                AppendEvent(new ContentActivatedEventPayload(null, activated.Manifest.PackageId));
                if (activated.Manifest.PackageId == HearthContent.PackageId)
                {
                    AddStartingResources();
                }
                if (activated.Manifest.Definitions.Any(definition => !string.IsNullOrWhiteSpace(definition.PayloadJson)))
                {
                    AppendEvent(new ContentDefinitionsActivatedEventPayload(null, activated.Manifest.PackageId, activatedWorldContent.Buildings.Count, activatedWorldContent.Recipes.Count));
                }
            }
            worldContent = activatedWorldContent;
            if (targetTick == 1 && contentRegistry.ExportState().Packages.Any(package =>
                    package.Manifest.PackageId == HearthContent.PackageId &&
                    package.Lifecycle == ContentPackageLifecycle.Active && package.ActivationTick == 0))
                AddStartingResources();
            CancelUnavailableWorkers();
            ReconcilePausedHouseholdWork();
            ProcessBuildingExpansions(targetTick);
            ProcessProduction(targetTick);
            MaintainFarmFields();

            WearEquippedClothing();
            AdvanceSurvival();
            AdvanceMedicalTreatments();
            MaintainDirectBarters();
            MaintainShopTrades();
            MaintainToolMakingRequests();
            DrainNeeds();
            AdvanceMedicalTreatments();
            RemoveDeadPhysicalState();
            MaintainMarriages();
            ReconcileMedicalSupplyTrips();
            ProcessBoatTransport(targetTick);
            ReconcileHandcartHitches();
            AdvanceAnimals(targetTick);
            CancelFieldWorkForUnavailableWorkers();
            AdvanceFoodStewardship();
            AdvanceTownCouncil();
            townSystem.SettleTownAdmissions(civicWorld);
            MaintainTownProjects();
            MaintainMarkets();
            MaintainLessons();
            MaintainPartnerships();
            MaintainHousing();
            MaintainRelocation();
            MaintainMovingCareGroups();
            MaintainParenthood();
            MaintainContinuity();
            MaintainDependentCare();
            ReconcileGuardianPlacements();
            DiscoverIdentityMoments();
            UpdateConversationsForTick(targetTick);
            RefreshTownLandHearings();
            EnqueueDueCognition(activeModelCallIds);
            var deferredDecisions = new List<SocietyCognitionDispatchResult>();
            if (deferModelCalls)
            {
                foreach (var item in completed)
                {
                    var id = item.Request.Observation.AgentId;
                    if (!agents.TryGetValue(id, out var physical)) continue;
                    var outcome = await item.Task.ConfigureAwait(false);
                    var request = item.Request;
                    var completedRequestedOrder = IsFinishedOrderDecisionAwaitingReply(request.Observation);
                    var legal = completedRequestedOrder
                        ? request.Observation.Candidates.Select(candidate => candidate.Id)
                            .ToHashSet(StringComparer.Ordinal)
                        : CreateCandidates(id, physical).Select(candidate => candidate.Id)
                            .ToHashSet(StringComparer.Ordinal);
                    // Choices that merely disappeared leave the accepted reply valid; only a
                    // choice the request lacked or a change in urgent needs asks the model again.
                    var requestedIds = request.Observation.Candidates.Select(candidate => candidate.Id)
                        .ToHashSet(StringComparer.Ordinal);
                    bool? decisionContextChanged = item.DecisionContext is { } previousContext &&
                        physical.LastDecisionContext is { } currentContext
                        ? !string.Equals(previousContext, currentContext, StringComparison.Ordinal) &&
                            (!previousContext.StartsWith($"{NeedsUrgentFood(physical)}:{NeedsUrgentWarmth(physical)}:", StringComparison.Ordinal) ||
                             legal.Any(candidate => !requestedIds.Contains(candidate) &&
                                (!candidate.StartsWith(MedicalAllowPrefix, StringComparison.Ordinal) ||
                                 physical.Survival is { IllnessBasisPoints: >= 2_500 })))
                        : null;
                    var plannedAdmission = DeferredPlanAdmission.Prepare(item.Request, outcome.Response, legal,
                        CheckActionPlan, PendingInstructionFor(id) is not null);
                    var decision = plannedAdmission.Complete(society.CompleteDeferredCognition(item.Request, outcome.Response,
                        outcome.Failure, plannedAdmission.LegalCandidateIds, decisionContextChanged));
                    if (decision is not null)
                    {
                        RecordModelCompletion(id, decision.Admission, outcome.Failure);
                        ApplyPersonalIdentityChoice(id, item.Request, outcome.Response, decision.Admission);
                        if (decision.PlanRefusal is null && decision.Admission.Accepted && !decision.Admission.FellBack &&
                            outcome.Response is
                            {
                                Provider: DecisionProviderKind.LargeLanguageModel,
                                PrivateThought: { } thought
                            } && agents.TryGetValue(id, out var thinking))
                        {
                            agentStore.RecordRecentThoughts(id, thinking, CognitionSystem.AppendPrivateThought(thinking.RecentThoughts, targetTick, thought));
                            checkpointSchemaVersion = StateSchemaVersion;
                        }
                        if (outcome.Response is { } response)
                            ApplyChosenNameOutcome(item.Request, response, decision.Admission);
                        CloseUnresolvedNameRetry(item.Request);
                        if (physical.IdentityChoicePending && agents[id] is { IdentityChoicePending: false } identified)
                            society.CompleteQueuedIdentityChoice(id, identified.Personality, identified.Aspiration);
                        deferredDecisions.Add(decision);
                        AppendEvent(new HostedDecisionCompletedEventPayload(id, decision.Admission.Outcome));
                    }
                    else
                    {
                        RecordModelAttempt(id, "canceled");
                        AppendEvent(new HostedDecisionDiscardedEventPayload(id));
                    }
                }
            }
            var dispatch = deferModelCalls
                ? await society.DispatchDeterministicCognitionAsync(
                    activeModelCallIds.Concat(agents.Keys.Where(id =>
                            IsConversationBusy(id) && !ShouldDispatchConversationChoice(id)))
                        .ToHashSet(StringComparer.Ordinal), IsQueuedObservationReady, cancellationToken).ConfigureAwait(false)
                : await society.DispatchCognitionAsync(cancellationToken).ConfigureAwait(false);
            var decisions = deferredDecisions.Concat(dispatch.Decisions)
                .OrderBy(item => item.AgentId, StringComparer.Ordinal).Select(CheckAcceptedPlan).ToArray();
            foreach (var decision in decisions)
            {
                ApplyDecision(decision);
            }
            var waiting = deferModelCalls ? society.PendingModelAgentIds() : new HashSet<string>(StringComparer.Ordinal);
            var orderActorsHandledThisTick = ApplyContinuingIntentions(
                decisions.Select(item => item.AgentId), waiting);
            AdvanceMedicalTreatments();
            ReconcileMedicalSupplyTrips();
            // An agent whose reply was accepted this tick already acted, even if newer work stays queued.
            if (deferModelCalls) ApplySafeRoutinesWhileWaiting(waiting.Except(decisions.Select(item => item.AgentId), StringComparer.Ordinal), orderActorsHandledThisTick);
            CompleteActionPhase(orderActorsHandledThisTick);

            ArchiveOldAgentMemories(activeModelCallIds);
            AppendEvent(new TickAdvancedEventPayload(null, targetTick));
            var newEvents = events.Skip(startingEvent).ToArray();
            return new WorldStepResult(true, "advanced", targetTick, decisions, newEvents)
            {
                MemoryCompactionTransitions = memoryCompactionTransitions.ToArray(),
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private void CompleteActionPhase(IEnumerable<string> handledActors)
    {
        ReconcileGuardianPlacements();
        AdvanceGuardianPlacementFollowers(handledActors);
        AdvanceBridgeTraffic();
        SettleGuardianSearches();
        ReconcileGuardianPlacements();
        MaintainTownProjects();
        MaintainMarkets();
        MaintainToolMakingRequests();
        RefreshTownLandHearings();
        MaintainKnowledgeWriting();
        ProcessBoatQueue();
        ReconcileAnimalCustody();
        CompleteClosedTalkOrders();

    }

}
