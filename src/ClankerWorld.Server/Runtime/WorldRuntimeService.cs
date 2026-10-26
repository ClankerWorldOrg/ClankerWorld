using System.Collections.Concurrent;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Runtime;

public enum ShutdownSaveOutcome { Saved, PreservedHalted, WriteFailed }

/// <summary>
/// Advances the integrated world alpha at a deliberately readable
/// cadence. Model providers are not called once per render frame.
/// </summary>
public sealed partial class WorldRuntimeService(
    WorldRuntime runtime,
    WorldStateFile stateFile,
    OwnerClientPresenceLease clientPresence,
    ILogger<WorldRuntimeService>? logger = null,
    WorldAutosaveStore? autosave = null,
    ManualWorldSaveStore? manualSaves = null,
    ProviderConfigurationStore? providers = null,
    WorldTickFailurePolicy failurePolicy = WorldTickFailurePolicy.Strict,
    LauncherHostOptions? hosting = null,
    IHostApplicationLifetime? lifetime = null) : BackgroundService
{
    private string? lastGateState;
    private bool recoveryWritePending;
    private bool invalidStateHalt;
    private bool writingCheckpoint;
    private bool hostedClientSeen;
    public bool HostIdleSaveFailed { get; private set; }
    private readonly ConcurrentDictionary<string, FrozenChildModelBinding> pendingChildModelBindings = new(StringComparer.Ordinal);

    [LoggerMessage(EventId = 2323, Level = LogLevel.Warning,
        Message = "world_action_refused tick={WorldTick} agent={AgentId} choice_kind={ChoiceKind} check={Check} outcome=waiting")]
    private static partial void LogActionRefused(ILogger logger, long worldTick, string agentId, string choiceKind, string check);

    private static string RefusedChoiceKind(string candidate) => candidate.Split(':', 2)[0] switch
    {
        "explore" or "explore_for" => "exploration",
        "consume_food" or "drink_milk" => "food",
        "produce_item" => "production",
        "repair_equipment" => "repair",
        "gather_food" => "gathering",
        _ => "other",
    };

    [LoggerMessage(EventId = 2287, Level = LogLevel.Error,
        Message = "world_recovery outcome={Outcome} tick={WorldTick} reason={Reason}")]
    private static partial void LogRecovery(ILogger logger, string outcome, long worldTick, string reason);


    [LoggerMessage(EventId = 2215, Level = LogLevel.Information,
        Message = "work_practice tick={WorldTick} agent={AgentId} building={Building} farming={Farming} crafting={Crafting}")]
    private static partial void LogWorkPractice(ILogger logger, long worldTick, string agentId, int building, int farming, int crafting);

    [LoggerMessage(EventId = 2216, Level = LogLevel.Information,
        Message = "social_standing tick={WorldTick} agent={AgentId} subject={SubjectId} trust={Trust} reason={Reason}")]
    private static partial void LogSocialStanding(ILogger logger, long worldTick, string agentId, string subjectId, int trust, string reason);

    [LoggerMessage(EventId = 2271, Level = LogLevel.Information,
        Message = "tree_planting tick={WorldTick} agent={AgentId} outcome={Outcome} detail={Detail}")]
    private static partial void LogTreePlanting(ILogger logger, long worldTick, string agentId, string outcome, string detail);

    [LoggerMessage(EventId = 2218, Level = LogLevel.Information,
        Message = "model_decision_progress tick={WorldTick} agent={AgentId} outcome={Outcome}")]
    private static partial void LogModelDecisionProgress(ILogger logger, long worldTick, string agentId, string outcome);

    [LoggerMessage(EventId = 2288, Level = LogLevel.Information,
        Message = "agent_identity_changed tick={WorldTick} agent={AgentId}")]
    private static partial void LogIdentityChanged(ILogger logger, long worldTick, string agentId);

    [LoggerMessage(EventId = 2255, Level = LogLevel.Information,
        Message = "estate_will tick={WorldTick} estate={EstateId} deceased={DeceasedId} outcome={Outcome} reason={Reason}")]
    private static partial void LogEstateWill(ILogger logger, long worldTick, string estateId, string deceasedId, string outcome, string reason);

    [LoggerMessage(EventId = 2220, Level = LogLevel.Information,
        Message = "agent_belief_transition tick={WorldTick} owner={OwnerId} belief={BeliefId} outcome={Outcome} provenance={Provenance} confidence_basis_points={ConfidenceBasisPoints}")]
    private static partial void LogAgentBeliefTransition(ILogger logger, long worldTick, string ownerId,
        string beliefId, string outcome, SocietyBeliefProvenance provenance, int confidenceBasisPoints);

    [LoggerMessage(EventId = 2221, Level = LogLevel.Information,
        Message = "agent_memory_compaction tick={WorldTick} owner={OwnerId} assessed={AssessedCount} index_size={IndexSize}")]
    private static partial void LogAgentMemoryCompaction(ILogger logger, long worldTick, string ownerId,
        int assessedCount, int indexSize);

    [LoggerMessage(EventId = 2222, Level = LogLevel.Information,
        Message = "agent_knowledge tick={WorldTick} outcome={Outcome} agent={AgentId} recipient={RecipientId} artifact={ArtifactId} facts={FactCount}")]
    private static partial void LogAgentKnowledgeTransition(ILogger logger, long worldTick, string outcome,
        string agentId, string recipientId, string artifactId, int factCount);

    [LoggerMessage(EventId = 2253, Level = LogLevel.Information,
        Message = "autosave outcome=created save={SaveId} tick={WorldTick}")]
    private static partial void LogAutosaveCreated(ILogger logger, string saveId, long worldTick);

    [LoggerMessage(EventId = 2254, Level = LogLevel.Warning,
        Message = "autosave outcome=failed reason={Reason} tick={WorldTick}")]
    private static partial void LogAutosaveFailed(ILogger logger, string reason, long worldTick);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        runtime.AgentBeliefChanged += OnAgentBeliefChanged;
        if (logger is not null)
        {
            foreach (var town in runtime.Towns)
                TownTelemetry.LogTownTransition(logger, runtime.WorldTick, town.Id, TownTransitionKind.StateLoaded,
                    town.ResidentIds.Count, town.AssignedBuildingIds.Count, town.BorderTiles.Count);
        }
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        runtime.AgentBeliefChanged -= OnAgentBeliefChanged;
        return base.StopAsync(cancellationToken);
    }

    [LoggerMessage(EventId = 2322, Level = LogLevel.Information,
        Message = "server_shutdown outcome={Outcome} tick={WorldTick}")]
    private static partial void LogShutdownSave(ILogger logger, string outcome, long worldTick);

    /// <summary>
    /// Cancels model work, completes pending child bindings and writes the checkpoint. Local server shutdown pauses
    /// first, so a prepared tick cannot commit after this save. A failed write
    /// leaves the local server alive and paused; a halted world stays untouched.
    /// </summary>
    public ShutdownSaveOutcome SaveBeforeShutdown(bool pauseWorld = false) => SaveWorld(pauseWorld, shuttingDown: true);

    private ShutdownSaveOutcome SaveWorld(bool pauseWorld, bool shuttingDown)
    {
        void Log(string outcome)
        {
            if (logger is null) return;
            if (shuttingDown) LogShutdownSave(logger, outcome, runtime.WorldTick);
            else LogHostIdleSave(logger, outcome, runtime.WorldTick);
        }
        if (invalidStateHalt)
        {
            Log("skipped_halted");
            return ShutdownSaveOutcome.PreservedHalted;
        }
        var gate = providers?.WorldMutationGate ?? new object();
        try
        {
            lock (gate)
            {
                if (pauseWorld) runtime.Pause();
                runtime.CancelPendingModelCalls();
                if (!TryBindPendingChildModels(out _))
                {
                    Log("save_failed");
                    return ShutdownSaveOutcome.WriteFailed;
                }
                _ = stateFile.Save(runtime);
            }
            recoveryWritePending = false;
            Log("saved");
            return ShutdownSaveOutcome.Saved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log("save_failed");
            return ShutdownSaveOutcome.WriteFailed;
        }
    }

    private void OnAgentBeliefChanged(WorldBeliefTransition transition)
    {
        if (logger is null) return;
        LogAgentBeliefTransition(logger, transition.WorldTick, transition.OwnerId, transition.BeliefId,
            transition.Outcome, transition.Provenance, transition.ConfidenceBasisPoints);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!clientPresence.HasActiveClient)
            {
                _ = await TryAdvanceOnceAsync(stoppingToken);
                if (!HostIdleSaveFailed && !recoveryWritePending)
                    await clientPresence.WaitForClientAsync(stoppingToken);
            }
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _ = await TryAdvanceOnceAsync(stoppingToken);
                if (!clientPresence.HasActiveClient && !HostIdleSaveFailed && !recoveryWritePending) break;
            }
        }
    }

    [LoggerMessage(EventId = 2326, Level = LogLevel.Information,
        Message = "launcher_host outcome={Outcome} tick={WorldTick}")]
    private static partial void LogHostIdleSave(ILogger logger, string outcome, long worldTick);

    /// <summary>
    /// Advances one world tick only while at least one authenticated
    /// game client has a current presence lease. Manual world pause remains a
    /// separate persistent simulation state and is never cleared here.
    /// </summary>
    public async ValueTask<bool> TryAdvanceOnceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (invalidStateHalt)
        {
            runtime.Pause();
            return false;
        }
        try
        {
            if (recoveryWritePending)
            {
                // Keep the advanced in-memory state, retry only its checkpoint,
                // and require a later explicit Resume after recovery succeeds.
                runtime.Pause();
                writingCheckpoint = true;
                stateFile.Save(runtime);
                writingCheckpoint = false;
                recoveryWritePending = false;
                if (logger is not null) LogRecovery(logger, "saved_paused", runtime.WorldTick, "write_recovered");
                return false;
            }
            return await TryAdvanceCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            runtime.Pause();
            runtime.CancelPendingModelCalls();
            var writeFailure = writingCheckpoint && exception is (IOException or UnauthorizedAccessException);
            writingCheckpoint = false;
            if (writeFailure)
            {
                if (!recoveryWritePending && logger is not null)
                    LogRecovery(logger, "held_for_write", runtime.WorldTick, exception.GetType().Name);
                recoveryWritePending = true;
            }
            else
            {
                invalidStateHalt = true;
                if (logger is not null) LogRecovery(logger, "halted_for_inspection", runtime.WorldTick, exception.GetType().Name);
            }
            return false;
        }
    }

    private async ValueTask<bool> TryAdvanceCoreAsync(CancellationToken cancellationToken)
    {
        if (!clientPresence.HasActiveClient)
        {
            runtime.CancelPendingModelCalls();
            if (hosting is not null && hostedClientSeen)
            {
                var saved = SaveWorld(pauseWorld: true, shuttingDown: false);
                HostIdleSaveFailed = saved == ShutdownSaveOutcome.WriteFailed;
                if (saved == ShutdownSaveOutcome.Saved)
                {
                    // A game can reconnect while the checkpoint waits or writes.
                    hostedClientSeen = clientPresence.HasActiveClient;
                    if (!hostedClientSeen && hosting.StopWhenGameQuits) lifetime?.StopApplication();
                }
            }
            LogGateTransition("waiting_for_client", runtime.WorldTick);
            return false;
        }
        if (hosting is not null) hostedClientSeen = true;

        if (!TryBindPendingChildModels(out var recoveredChildBindings))
        {
            LogGateTransition("waiting_for_child_model_binding", runtime.WorldTick);
            return false;
        }
        if (recoveredChildBindings)
        {
            writingCheckpoint = true;
            _ = stateFile.Save(runtime);
            writingCheckpoint = false;
        }

        _ = runtime.StageStarterContent();
        using var monitorLifetime = new CancellationTokenSource();
        using var tickCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitor = MonitorTickGateAsync(tickCancellation, monitorLifetime.Token);
        var preparedChildBindings = new Dictionary<string, FrozenChildModelBinding>(StringComparer.Ordinal);
        var checkpointCompacted = false;
        IReadOnlyList<PreparedChildModelSelection> PrepareModels(WorldRuntime proposed, IReadOnlyList<WorldEventEntry> worldEvents)
        {
            preparedChildBindings.Clear();
            var selections = PrepareChildModelSelections(proposed, worldEvents, preparedChildBindings);
            // Keep each frozen key before its prepared birth can become visible.
            // Shutdown can run before this tick handler resumes after the commit.
            foreach (var (childId, binding) in preparedChildBindings)
                pendingChildModelBindings[childId] = binding;
            return selections;
        }
        Action PrepareCheckpoint(WorldRuntime proposed)
        {
            var write = stateFile.PrepareSave(proposed, out var compacted);
            return () =>
            {
                writingCheckpoint = true;
                write();
                writingCheckpoint = false;
                checkpointCompacted = compacted;
            };
        }
        WorldStepResult result;
        try
        {
            result = failurePolicy == WorldTickFailurePolicy.Strict
                ? await runtime.AdvanceOneTickNonBlockingAsync(() => clientPresence.HasActiveClient,
                    PrepareModels, tickCancellation.Token)
                : await runtime.AdvanceOneTickWithCheckpointAsync(PrepareCheckpoint,
                    () => clientPresence.HasActiveClient, PrepareModels, failurePolicy, tickCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && tickCancellation.IsCancellationRequested)
        {
            LogGateTransition(runtime.Society.IsPaused ? "paused" : "waiting_for_client", runtime.WorldTick);
            return false;
        }
        finally
        {
            await monitorLifetime.CancelAsync();
            await monitor;
        }
        LogGateTransition(result.Advanced ? "advancing" : result.Outcome, result.WorldTick);
        if (result.Advanced)
        {
            if (logger is not null)
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "action_refused"))
                {
                    var refusal = System.Text.Json.JsonSerializer.Deserialize<WorldActionRefusal>(worldEvent.Detail)!;
                    LogActionRefused(logger, worldEvent.WorldTick, refusal.AgentId,
                        RefusedChoiceKind(refusal.CandidateId), refusal.Check);
                }
            var bornChildren = result.Events.Where(item => item.Kind == "child_born").Select(item => item.Detail)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (bornChildren.Length > 0)
            {
                // The birth descriptor is part of the runtime tick commit. Persist it
                // before touching provider storage, so manual saves, world switches,
                // and shutdown cannot publish an unbound newborn.
                if (failurePolicy == WorldTickFailurePolicy.Strict)
                {
                    writingCheckpoint = true;
                    if (providers is null) _ = stateFile.Save(runtime);
                    else lock (providers.WorldMutationGate)
                        _ = stateFile.Save(runtime);
                    writingCheckpoint = false;
                }
                if (!TryBindPendingChildModels(out _))
                {
                    LogGateTransition("waiting_for_child_model_binding", runtime.WorldTick);
                    return false;
                }
            }
            var compacted = checkpointCompacted;
            if (failurePolicy == WorldTickFailurePolicy.Strict)
            {
                writingCheckpoint = true;
                compacted = stateFile.Save(runtime);
                writingCheckpoint = false;
            }
            if (compacted && logger is not null)
            {
                var checkpoint = runtime.ExportState();
                LogHistoryCompacted(logger, result.WorldTick, checkpoint.EventHistoryFloor, checkpoint.Events.Count,
                    checkpoint.BoatTransport.Requests.Count, checkpoint.BoatTransport.RetiredRequestRanges.Count);
            }
            try
            {
                var saved = autosave is not null && manualSaves is not null && providers is not null
                    ? autosave.MaybeSave(DateTimeOffset.UtcNow, runtime, providers, manualSaves)
                    : null;
                if (saved is not null && logger is not null)
                    LogAutosaveCreated(logger, saved.Id, saved.WorldTick);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or
                UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                // The active per-tick recovery save already committed; a
                // failed rotating copy must not halt world simulation.
                if (logger is not null) LogAutosaveFailed(logger, exception.GetType().Name, result.WorldTick);
            }
            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                foreach (var transition in result.MemoryCompactionTransitions)
                {
                    LogAgentMemoryCompaction(logger, transition.WorldTick, transition.OwnerId,
                        transition.AssessedCount, transition.IndexSize);
                }
                var actors = runtime.Society.Agents.Select(person => person.Id)
                    .OrderByDescending(id => id.Length).ToArray();
                string? EventActor(string detail) => actors.FirstOrDefault(id => detail == id || detail.StartsWith(id + ":", StringComparison.Ordinal));
                foreach (var worldEvent in result.Events.Where(item => item.Kind.StartsWith("hosted_decision_", StringComparison.Ordinal)))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null) continue;
                    LogModelDecisionProgress(logger, result.WorldTick, actor,
                        worldEvent.Kind["hosted_decision_".Length..] + worldEvent.Detail[actor.Length..]);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "tree_planted" or
                             "tree_planting_refused" or "tree_replanted" or "tree_seed_collected"))
                {
                    // Details after the actor are bounded IDs, species and refusal codes.
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null || worldEvent.Detail.Length <= actor.Length + 1) continue;
                    LogTreePlanting(logger, result.WorldTick, actor, worldEvent.Kind,
                        worldEvent.Detail[(actor.Length + 1)..]);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind.StartsWith("estate_will_", StringComparison.Ordinal)))
                {
                    var estate = runtime.Society.Estates.FirstOrDefault(item =>
                        worldEvent.Detail == item.Id || worldEvent.Detail.StartsWith(item.Id + ":", StringComparison.Ordinal));
                    if (estate is not null)
                    {
                        var outcome = worldEvent.Kind["estate_will_".Length..];
                        var reason = EstateWillReason(worldEvent, estate.Id, outcome);
                        LogEstateWill(logger, result.WorldTick, estate.Id, estate.DeceasedId,
                            outcome, reason);
                    }
                }
                var projects = runtime.Agents.Where(person => person.Project is not null)
                    .ToDictionary(person => person.AgentId, person => person.Project!, StringComparer.Ordinal);
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "agent_identity_revised"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is not null) LogIdentityChanged(logger, worldEvent.WorldTick, actor);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind.StartsWith("town_", StringComparison.Ordinal) ||
                             item.Kind is "bridge_built" or "traffic_bridge_not_built"))
                    LogTownEvent(worldEvent);
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "work_practice_earned"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is not null && runtime.Agents.FirstOrDefault(person => person.AgentId == actor)?.Proficiency is { } practice)
                        LogWorkPractice(logger, result.WorldTick, actor, practice.Building, practice.Farming, practice.Crafting);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "social_standing_changed"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null || worldEvent.Detail.Length <= actor.Length + 1) continue;
                    var remainder = worldEvent.Detail[(actor.Length + 1)..];
                    var subject = actors.FirstOrDefault(id => remainder == id || remainder.StartsWith(id + ":", StringComparison.Ordinal));
                    var standing = runtime.Agents.FirstOrDefault(person => person.AgentId == actor)?.SocialStanding?
                        .FirstOrDefault(item => item.SubjectId == subject);
                    if (subject is null || standing is null) continue;
                    var reason = remainder.Length > subject.Length ? remainder[(subject.Length + 1)..] : "cooperation";
                    LogSocialStanding(logger, result.WorldTick, actor, subject, standing.Trust, reason);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "project_chosen" or "project_progress" or
                             "project_request_fulfilled" or "town_resources_stored" or "town_resource_collected" or
                             "owner_stock_picked_up" or "owner_stock_delivered"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null) continue;
                    var delivery = worldEvent.Kind is "owner_stock_picked_up" or "owner_stock_delivered";
                    var project = delivery ? null : projects.GetValueOrDefault(actor);
                    LogProjectActivity(logger, result.WorldTick, worldEvent.Kind, actor,
                        worldEvent.Kind is "town_resources_stored" or "town_resource_collected"
                            ? "warehouse" : delivery ? "delivery" : project?.Stage ?? "helping", project?.WorkDone ?? 0,
                        worldEvent.Kind is not ("town_resources_stored" or "town_resource_collected") &&
                        project?.Blocker is not null);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind.StartsWith("handcart_", StringComparison.Ordinal)))
                    HandcartTelemetry.Record(logger, worldEvent, runtime.Society.Inventory, actors);
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "survival_condition_changed"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null) continue;
                    if (runtime.Agents.FirstOrDefault(person => person.AgentId == actor)?.Survival is { } condition)
                    {
                        var illnessWorkPercent = IllnessRules.WorkRatePercent(condition.IllnessBasisPoints);
                        var illnessTravelDelay = IllnessRules.TravelDelayTicks(condition.IllnessBasisPoints);
                        LogSurvivalCondition(logger, result.WorldTick, actor, condition.WarmthBasisPoints, condition.IllnessBasisPoints,
                            illnessWorkPercent, illnessTravelDelay);
                    }
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "fire_fuelled" or "fire_extinguished"))
                {
                    LogSurvivalEnvironment(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "production_worker_unavailable"))
                {
                    var job = runtime.WorldSimulation.ProductionJobs.Concat(runtime.WorldSimulation.CropBuilds ?? [])
                        .FirstOrDefault(item => item.JobId == worldEvent.Detail);
                    if (job is not null) LogProductionCancelled(logger, result.WorldTick, job.JobId, job.WorkerId);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "barter_offered" or
                             "barter_declined" or "barter_completed" or "barter_cancelled"))
                {
                    LogDirectBarter(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind.StartsWith("agent_knowledge_", StringComparison.Ordinal)))
                {
                    LogKnowledgeEvent(worldEvent);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "food_steward_changed" or
                             "food_policy_proposed" or "food_policy_vote_recorded" or "food_policy_adopted" or "food_policy_rejected"))
                {
                    LogFoodStewardship(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "lesson_requested" or "lesson_accepted" or
                             "lesson_training" or "lesson_completed" or "lesson_declined" or "lesson_cancelled"))
                {
                    LogLessonActivity(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "partnership_proposed" or "partnership_accepted" or
                             "partnership_refused" or "partnership_ended" or "partnership_expired" or
                             "parenthood_requested" or "parenthood_preparing" or "parenthood_cancelled" or "parenthood_completed" or
                             "parenthood_postponed" or "continuity_partnership_formed" or "continuity_rule_on" or "continuity_rule_off" or "continuity_plan_proceeded" or
                             "child_born" or "child_cared_for" or "guardian_needed" or "guardian_assigned" or
                             "guardian_placement_pending" or "guardian_placement_completed" or "guardian_placement_cancelled" or
                             "primary_caregiver_assigned" or "caregiver_proposed" or "caregiver_assigned" or
                             "caregiver_accepted" or "caregiver_refused" or "caregiver_proposal_expired" or "caregiver_ended" or
                             "dependent_cared_for"))
                {
                    LogFamilyActivity(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "housing_request_made" or
                             "housing_answer_recorded" or "household_joined" or "housing_request_refused" or
                             "housing_request_expired" or "housing_request_cancelled" or "housing_blocked" or
                             "relocation_notice" or "relocation_cancelled" or
                             "household_left" or "household_founded" or "personal_goods_collected" or "personal_goods_stored" or
                             "borrowed_goods_returned" or "replacement_care_accepted" or "household_work_resumed"))
                {
                    LogHousingActivity(logger, result.WorldTick, worldEvent.Kind);
                }
            }
        }

        foreach (var decision in result.Decisions.OrderBy(item => item.AgentId, StringComparer.Ordinal))
        {
            var intention = decision.Admission.Intention;
            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                var provider = intention?.Provider.ToString().ToLowerInvariant() ?? "none";
                LogCognitionDecision(
                    logger,
                    result.WorldTick,
                    decision.AgentId,
                    decision.Admission.Accepted,
                    decision.Admission.FellBack,
                    decision.Admission.Outcome,
                    provider,
                    intention?.CandidateId ?? "none",
                    intention?.Confidence ?? 0,
                    intention?.Usage?.ModelId ?? "none",
                    intention?.Usage?.InputTokens ?? 0,
                    intention?.Usage?.OutputTokens ?? 0);
            }
        }

        return result.Advanced;
    }

    private bool TryBindPendingChildModels(out bool changed)
    {
        changed = false;
        if (providers is null) return true;

        try
        {
            lock (providers.WorldMutationGate)
            {
                var configuration = providers.CaptureRuntimeConfiguration();
                var people = runtime.Agents.ToDictionary(item => item.AgentId, StringComparer.Ordinal);
                foreach (var childId in pendingChildModelBindings.Keys.ToArray())
                {
                    if (!people.TryGetValue(childId, out var child))
                    {
                        pendingChildModelBindings.TryRemove(childId, out _);
                        continue;
                    }
                    if (child.ChildModelSelection is null)
                        throw new InvalidDataException("A newly born child has no birth-bound model choice.");
                    if (pendingChildModelBindings[childId].Selection != child.ChildModelSelection)
                        pendingChildModelBindings[childId] = new FrozenChildModelBinding(child.ChildModelSelection, null);
                }

                foreach (var child in people.Values.Where(item => item.ChildModelSelection is not null))
                {
                    var selection = child.ChildModelSelection!;
                    var childRows = (configuration.Assignments ?? []).Where(item =>
                        item.AgentId == child.AgentId &&
                        item.Role is PlayerDecisionProviders.RoutineRole or PlayerDecisionProviders.PlanningRole).ToArray();
                    var birthRows = childRows.Where(item => item.SelectionReason is not null).ToArray();
                    if (selection.Provider is null)
                    {
                        if (birthRows.Length > 0)
                            throw new InvalidDataException("An unconfigured child has a saved birth model assignment.");
                        if (!pendingChildModelBindings.ContainsKey(child.AgentId))
                            continue;
                    }
                    else
                    {
                        if (birthRows.Any(item => item.Provider != selection.Provider || item.Model != selection.ModelId ||
                                item.CredentialSlotId != selection.CredentialSlotId || item.SelectionReason != selection.ChoiceReason))
                            throw new InvalidDataException("A child's saved route does not match its birth model choice.");
                        var bothRolesConfigured = new[] { PlayerDecisionProviders.RoutineRole, PlayerDecisionProviders.PlanningRole }
                            .All(role => childRows.Any(item => item.Role == role));
                        if (bothRolesConfigured)
                        {
                            pendingChildModelBindings.TryRemove(child.AgentId, out _);
                            continue;
                        }
                    }
                    if (!pendingChildModelBindings.ContainsKey(child.AgentId))
                        pendingChildModelBindings.TryAdd(child.AgentId, new FrozenChildModelBinding(selection, null));
                }

                foreach (var childId in pendingChildModelBindings.Keys.Order(StringComparer.Ordinal).ToArray())
                {
                    if (!people.TryGetValue(childId, out var child)) continue;
                    var binding = pendingChildModelBindings[childId];
                    if (child.ChildModelSelection != binding.Selection)
                        throw new InvalidDataException("The pending child model choice no longer matches the saved birth descriptor.");
                    providers.ConfigureChildModelSelectionWithCommit(childId, binding,
                        boundSelection => runtime.ApplyChildModelSelection(childId, boundSelection));
                    pendingChildModelBindings.TryRemove(childId, out _);
                    changed = true;
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            runtime.Pause();
            runtime.CancelPendingModelCalls();
            if (logger is not null)
                LogRecovery(logger, "waiting_for_child_model_binding", runtime.WorldTick, exception.GetType().Name);
            return false;
        }
    }

    private List<PreparedChildModelSelection> PrepareChildModelSelections(
        WorldRuntime proposed,
        IReadOnlyList<WorldEventEntry> events,
        Dictionary<string, FrozenChildModelBinding> preparedBindings)
    {
        if (providers is null) return [];
        var births = events.Where(item => item.Kind == "child_born")
            .Select(item => item.Detail).Distinct(StringComparer.Ordinal).ToArray();
        if (births.Length == 0) return [];

        var configuration = providers.CaptureRuntimeConfiguration();
        var people = proposed.Agents.ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        var selections = new List<PreparedChildModelSelection>(births.Length);
        foreach (var childId in births)
        {
            if (!people.TryGetValue(childId, out var child) || child.ChildModelSelection is not null)
                throw new InvalidDataException("A newborn child cannot receive a second model choice.");
            var initiator = people.Values.FirstOrDefault(item => item.Parenthood?.ChildId == childId);
            if (initiator?.Parenthood is not { } plan)
                throw new InvalidDataException("A newly born child has no initiating parent record.");

            var selection = ChildModelSelectionResolver.Choose(initiator.AgentId, plan.PartnerId, configuration);
            var frozen = ProviderConfigurationStore.FreezeChildModelSelection(selection, configuration);
            preparedBindings.Add(childId, frozen);
            selections.Add(new PreparedChildModelSelection(childId, frozen.Selection));
        }
        return selections;
    }

    private void LogTownEvent(WorldEventEntry worldEvent)
    {
        if (logger is null) return;
        var transferKind = worldEvent.Kind switch
        {
            "land_transfer_proposed" => TownLandTransferTransitionKind.Proposed,
            "land_transfer_read" => TownLandTransferTransitionKind.Read,
            "land_transfer_consent" => TownLandTransferTransitionKind.Consent,
            "land_transfer_withdrawn" => TownLandTransferTransitionKind.Withdrawn,
            "land_transfer_settled" => TownLandTransferTransitionKind.Settled,
            "land_transfer_blocked" => TownLandTransferTransitionKind.Blocked,
            _ => (TownLandTransferTransitionKind?)null,
        };
        if (transferKind is { } transferTransition)
        {
            var fields = worldEvent.Detail.Split('|', 5);
            var transferTown = fields.Length == 5 ? runtime.Towns.FirstOrDefault(item => item.Id == fields[0]) : null;
            var transfer = transferTown?.LandRecords.Transfers.FirstOrDefault(item => item.Id == fields[1]);
            if (transferTown is not null && transfer is not null)
                TownTelemetry.LogLandTransfer(logger, worldEvent.WorldTick, transferTown.Id, transfer.Id, transferTransition,
                    transfer.Status, transfer.Parties.Count, transfer.Responses.Count, transfer.Tiles.Count);
            return;
        }
        var hearingKind = worldEvent.Kind switch
        {
            "land_case_opened" => TownLandHearingTransitionKind.Opened,
            "land_case_notice" => TownLandHearingTransitionKind.NoticePublished,
            "land_case_evidence" => TownLandHearingTransitionKind.EvidenceAdded,
            "land_case_response" => TownLandHearingTransitionKind.ResponseRecorded,
            "land_case_judge_consent" => TownLandHearingTransitionKind.JudgeConsent,
            "land_case_judge_election" => TownLandHearingTransitionKind.JudgeElection,
            "land_case_judge_assigned" => TownLandHearingTransitionKind.JudgeAssigned,
            "land_case_ruling" => TownLandHearingTransitionKind.Ruling,
            "land_case_reopen_requested" => TownLandHearingTransitionKind.ReopenRequested,
            "land_case_reopened" => TownLandHearingTransitionKind.Reopened,
            "land_case_inspected" => TownLandHearingTransitionKind.Inspected,
            "land_case_relayed" => TownLandHearingTransitionKind.Relayed,
            "land_case_rejected" => TownLandHearingTransitionKind.Rejected,
            _ => (TownLandHearingTransitionKind?)null,
        };
        if (hearingKind is { } hearingTransition)
        {
            var fields = worldEvent.Detail.Split('|', 5);
            var hearingTown = fields.Length >= 3 ? runtime.Towns.FirstOrDefault(item => item.Id == fields[0]) : null;
            var hearing = hearingTown?.LandRecords?.Cases.FirstOrDefault(item => item.Id == fields[1]);
            if (hearingTown is not null && hearing is not null)
                TownTelemetry.LogLandHearing(logger, worldEvent.WorldTick, hearingTown.Id, hearing.Id, hearingTransition,
                    hearing.Status, hearing.Revisions[^1].Number, hearing.Revisions[^1].Parties.Count,
                    hearing.Evidence.Count, hearing.Responses.Count, hearing.Rulings.Count);
            return;
        }
        var civicKind = worldEvent.Kind switch
        {
            "town_civic_law" => TownCivicTransitionKind.LawRecorded,
            "town_civic_government" => TownCivicTransitionKind.GovernmentRecorded,
            "town_civic_mayor" => TownCivicTransitionKind.MayorRecorded,
            "town_civic_council" => TownCivicTransitionKind.CouncilChanged,
            "town_civic_election" => TownCivicTransitionKind.ElectionOpened,
            "town_civic_runoff" => TownCivicTransitionKind.RunoffOpened,
            "town_civic_proposal" => TownCivicTransitionKind.ProposalOpened,
            "town_civic_result" or "town_civic_land_use" or "land_use_granted" or "town_land_claimed" => TownCivicTransitionKind.DecisionRecorded,
            "town_civic_cancelled" => TownCivicTransitionKind.ElectionCancelled,
            _ => (TownCivicTransitionKind?)null,
        };
        if (civicKind is { } civicTransition)
        {
            var fields = worldEvent.Detail.Split('|', 3);
            var civicTown = fields.Length == 3 ? runtime.Towns.FirstOrDefault(t => t.Id == fields[0]) : null;
            if (civicTown?.Council is { } council)
            {
                var proposal = council.Proposals.FirstOrDefault(p => p.Id == fields[1]);
                var election = council.Election?.Id == fields[1] ? council.Election :
                    council.ElectionHistory.FirstOrDefault(e => e.Id == fields[1]);
                var change = civicTown.Government?.Changes.FirstOrDefault(c => c.Id == fields[1]);
                var mayor = civicTown.Government?.Contest ?? (civicTown.Government?.ContestHistory is { Count: > 0 } history ? history[^1] : null);
                TownTelemetry.LogTownCivic(logger, worldEvent.WorldTick, civicTown.Id, civicTransition,
                    council.Form == "representative", council.Members.Count, proposal?.Status ?? election?.Stage ?? change?.Status ?? mayor?.Stage ?? "none",
                    proposal?.Votes.Count(v => v.Yes) ?? change?.Votes.Count(v => v.Yes) ?? 0,
                    proposal?.Votes.Count(v => !v.Yes) ?? change?.Votes.Count(v => !v.Yes) ?? 0, election?.Ballots.Count ?? mayor?.Ballots.Count ?? 0);
            }
            return;
        }
        if (worldEvent.Kind is "town_admission_accepted" or "town_admission_approved" or "town_admission_lapsed")
        {
            var fields = worldEvent.Detail.Split('|');
            var accepted = worldEvent.Kind == "town_admission_accepted";
            // An approval names the Town, newcomer and proposal; acceptance and lapse add a fourth field.
            if (fields.Length == (worldEvent.Kind == "town_admission_approved" ? 3 : 4) &&
                runtime.Towns.FirstOrDefault(t => t.Id == fields[0]) is { } admittingTown)
            {
                TownTelemetry.LogTownAdmission(logger, worldEvent.WorldTick, admittingTown.Id,
                    accepted ? "admitted" : worldEvent.Kind == "town_admission_approved" ? "awaiting_acceptance" : "lapsed:" + fields[3],
                    accepted ? fields[2] : "none",
                    accepted && int.TryParse(fields[3], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var members) ? members : 0,
                    admittingTown.ResidentIds.Count);
            }
            return;
        }
        if (worldEvent.Kind == "town_layout_site_rejected")
        {
            var fields = worldEvent.Detail.Split('|', StringSplitOptions.None);
            if (fields.Length == 6 &&
                int.TryParse(fields[3], out var x) &&
                int.TryParse(fields[4], out var y) &&
                fields[5] == "site_unavailable")
            {
                TownTelemetry.LogTownSiteRejected(logger, worldEvent.WorldTick, fields[0], fields[1],
                    fields[2], x, y, fields[5]);
            }
            return;
        }
        if (worldEvent.Kind is "bridge_built" or "traffic_bridge_not_built" or "town_road_unconnected")
        {
            LogRoadOrBridgeEvent(logger, worldEvent, runtime.Towns);
            return;
        }
        var kind = worldEvent.Kind switch
        {
            "town_resident_joined" => TownTransitionKind.ResidentJoined,
            "town_resident_left" => TownTransitionKind.ResidentLeft,
            "town_abandoned" => TownTransitionKind.Abandoned,
            "town_revived" => TownTransitionKind.Revived,
            "town_resettled" => TownTransitionKind.Resettled,
            "town_stock_salvaged" => TownTransitionKind.StockSalvaged,
            "town_membership_evaluated" => TownTransitionKind.ResidentUnaffiliated,
            "town_founded" => TownTransitionKind.Founded,
            "town_building_assigned" => TownTransitionKind.BuildingAssigned,
            "town_border_expanded" => TownTransitionKind.BorderExpanded,
            _ => (TownTransitionKind?)null,
        };
        if (kind is null) return;
        var townId = worldEvent.Kind == "town_membership_evaluated"
            ? "none"
            : runtime.Towns.OrderByDescending(item => item.Id.Length).FirstOrDefault(item =>
                worldEvent.Detail == item.Id || worldEvent.Detail.StartsWith(item.Id + ":", StringComparison.Ordinal) ||
                worldEvent.Detail.StartsWith(item.Id + "|", StringComparison.Ordinal))?.Id;
        if (townId is null) return;
        var town = runtime.Towns.FirstOrDefault(item => item.Id == townId);
        TownTelemetry.LogTownTransition(logger, worldEvent.WorldTick, townId, kind.Value,
            town?.ResidentIds.Count ?? 0, town?.AssignedBuildingIds.Count ?? 0, town?.BorderTiles.Count ?? 0);
    }

    /// <summary>Bounded Road and bridge outcomes from accepted events; details carry only IDs and reason codes.</summary>
    private static void LogRoadOrBridgeEvent(ILogger logger, WorldEventEntry worldEvent,
        IReadOnlyList<TownRuntimeState> towns)
    {
        var detail = worldEvent.Detail;
        var reasonAt = detail.LastIndexOf(':');
        switch (worldEvent.Kind)
        {
            case "bridge_built":
                var fields = detail.Split(':', 3);
                if (fields.Length >= 2)
                    TownTelemetry.LogBridge(logger, worldEvent.WorldTick, fields[1], fields[0], "built", "none");
                break;
            case "traffic_bridge_not_built" when reasonAt > 0:
                TownTelemetry.LogBridge(logger, worldEvent.WorldTick, detail[..reasonAt], "traffic", "not_built",
                    detail[(reasonAt + 1)..]);
                break;
            case "town_road_unconnected":
                var town = towns.OrderByDescending(item => item.Id.Length)
                    .FirstOrDefault(item => detail.StartsWith(item.Id + ":", StringComparison.Ordinal));
                if (town is not null && reasonAt > town.Id.Length + 1)
                    TownTelemetry.LogRoadUnconnected(logger, worldEvent.WorldTick, town.Id,
                        detail[(town.Id.Length + 1)..reasonAt], detail[(reasonAt + 1)..]);
                break;
        }
    }

    private static string EstateWillReason(WorldEventEntry worldEvent, string estateId, string outcome)
    {
        if (outcome == "started") return "decision_dispatch_attempted";
        if (outcome == "accepted") return "valid_will_accepted";
        if (outcome != "default" || !worldEvent.Detail.StartsWith(estateId + ":", StringComparison.Ordinal))
            return "unspecified";

        var reason = worldEvent.Detail[(estateId.Length + 1)..];
        return reason is "empty_estate" or "no_personal_model" or "timeout_or_cancelled" or
            "household_selected" or "invalid_response" or "deadline" or "interrupted" or
            "provider_changed" or "provider_unavailable" or "run_epoch_changed" or
            "invalid_estate_or_heir" || reason.StartsWith("provider_", StringComparison.Ordinal) && reason.Length <= 64 ||
            reason.StartsWith("setup_", StringComparison.Ordinal) && reason.Length <= 64
            ? reason
            : "unspecified";
    }

    private async Task MonitorTickGateAsync(CancellationTokenSource tickCancellation, CancellationToken monitorToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(monitorToken))
            {
                if (!clientPresence.HasActiveClient || runtime.Society.IsPaused)
                {
                    await tickCancellation.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (monitorToken.IsCancellationRequested)
        {
            // The proposed tick completed; no lifecycle watcher survives it.
        }
    }

    private void LogGateTransition(string state, long worldTick)
    {
        if (string.Equals(lastGateState, state, StringComparison.Ordinal))
        {
            return;
        }

        lastGateState = state;
        if (logger is not null)
        {
            LogWorldTickGate(logger, state, worldTick, clientPresence.ActiveClientCount);
        }
    }

    private void LogKnowledgeEvent(WorldEventEntry worldEvent)
    {
        if (logger is null || !logger.IsEnabled(LogLevel.Information)) return;
        var fields = worldEvent.Detail.Split('|');
        var agentId = fields.ElementAtOrDefault(0) ?? "unknown";
        var recipientId = worldEvent.Kind is "agent_knowledge_shared" or "agent_knowledge_artifact_read"
            ? fields.ElementAtOrDefault(1) ?? "unknown"
            : "";
        var artifactId = worldEvent.Kind switch
        {
            "agent_knowledge_artifact_created" => fields.ElementAtOrDefault(1) ?? "",
            "agent_knowledge_shared" or "agent_knowledge_artifact_read" => fields.ElementAtOrDefault(2) ?? "",
            _ => "",
        };
        var factCountIndex = worldEvent.Kind switch
        {
            "agent_knowledge_artifact_created" or "agent_knowledge_shared" or "agent_knowledge_artifact_read" => 3,
            _ => 2,
        };
        var factCount = 0;
        if (int.TryParse(fields.ElementAtOrDefault(factCountIndex), out var parsedFactCount) &&
            parsedFactCount is >= 0 and <= 9)
        {
            factCount = parsedFactCount;
        }
        LogAgentKnowledgeTransition(logger, worldEvent.WorldTick, worldEvent.Kind,
            agentId, recipientId, artifactId, factCount);
    }

    [LoggerMessage(EventId = 2212, Level = LogLevel.Information,
        Message = "production_cancelled tick={WorldTick} job={JobId} agent={AgentId} reason=worker_unavailable")]
    private static partial void LogProductionCancelled(ILogger logger, long worldTick, string jobId, string agentId);

    [LoggerMessage(EventId = 2209, Level = LogLevel.Information,
        Message = "lesson_activity tick={WorldTick} event={EventKind}")]
    private static partial void LogLessonActivity(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2210, Level = LogLevel.Information,
        Message = "family_activity tick={WorldTick} event={EventKind}")]
    private static partial void LogFamilyActivity(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2208, Level = LogLevel.Information,
        Message = "food_stewardship tick={WorldTick} event={EventKind}")]
    private static partial void LogFoodStewardship(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2211, Level = LogLevel.Information,
        Message = "housing_activity tick={WorldTick} event={EventKind}")]
    private static partial void LogHousingActivity(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2207, Level = LogLevel.Information,
        Message = "barter_activity tick={WorldTick} event={EventKind}")]
    private static partial void LogDirectBarter(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Information,
        Message = "world_history_compacted tick={WorldTick} event_floor={EventFloor} recent_events={RecentEvents} boat_requests={BoatRequests} retired_boat_ranges={RetiredBoatRanges}")]
    private static partial void LogHistoryCompacted(ILogger logger, long worldTick, long eventFloor, int recentEvents,
        int boatRequests, int retiredBoatRanges);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information,
        Message = "project_activity tick={WorldTick} event={EventKind} agent={AgentId} stage={Stage} work={WorkDone} blocked={Blocked}")]
    private static partial void LogProjectActivity(ILogger logger, long worldTick, string eventKind,
        string agentId, string stage, int workDone, bool blocked);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Information,
        Message = "survival_condition tick={WorldTick} agent={AgentId} warmth={Warmth} illness={Illness} illness_work_percent={IllnessWorkPercent} illness_travel_delay={IllnessTravelDelay}")]
    private static partial void LogSurvivalCondition(ILogger logger, long worldTick, string agentId, int warmth, int illness,
        int illnessWorkPercent, int illnessTravelDelay);

    [LoggerMessage(EventId = 2206, Level = LogLevel.Information,
        Message = "survival_environment tick={WorldTick} event={EventKind}")]
    private static partial void LogSurvivalEnvironment(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "model_decision tick={WorldTick} agent={AgentId} accepted={Accepted} fallback={FellBack} outcome={Outcome} provider={Provider} candidate={CandidateId} confidence={Confidence} model={Model} input_tokens={InputTokens} output_tokens={OutputTokens}")]
    private static partial void LogCognitionDecision(
        ILogger logger,
        long worldTick,
        string agentId,
        bool accepted,
        bool fellBack,
        string outcome,
        string provider,
        string candidateId,
        double confidence,
        string model,
        int inputTokens,
        int outputTokens);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Information,
        Message = "world_tick_gate state={State} tick={WorldTick} active_clients={ActiveClientCount}")]
    private static partial void LogWorldTickGate(
        ILogger logger,
        string state,
        long worldTick,
        int activeClientCount);
}
