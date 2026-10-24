using ChoiceContext = ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext;
using ChoiceGroup = ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static List<CognitionCandidate> CreateCandidates(ICognitionCandidatesWorld world,
        string agentId,
        AgentState state,
        bool restrictForOrder = true) => world.CreateRegisteredCandidates(agentId, state, restrictForOrder);

    internal static List<CognitionCandidate> CreateUnfilteredCandidates(ICognitionCandidatesWorld world,
        string agentId, AgentState state, bool restrictForOrder)
    {
        if (world.PassengerBoat(agentId) is not null)
        {
            var aboard = new List<CognitionCandidate> { world.WaitingCandidate(agentId, "Stay aboard while the boat travels or waits for a safe Port.") };
            if (state.FullnessBasisPoints < world.ComfortableFullness)
                world.AddFoodPlanCandidate(aboard, world.PlanFoodConsumption(agentId), "Eat one carried food item aboard the boat.", 0);
            if (state.FullnessBasisPoints < world.ComfortableFullness) world.AddMilkCandidate(aboard, agentId);
            return aboard;
        }
        var currentConversation = world.ConversationFor(agentId);
        var candidates = currentConversation is null
            ? new List<CognitionCandidate>()
            : world.ConversationCandidates(agentId).ToList();
        if (currentConversation is not null &&
            (currentConversation.Status is AgentConversationStatus.Ready or AgentConversationStatus.AwaitingSpeaker or AgentConversationStatus.WrapUp ||
             currentConversation.Status == AgentConversationStatus.Proposed && currentConversation.InitiatorId == agentId))
        {
            if (candidates.Count == 0)
                candidates.Add(world.WaitingCandidate(agentId, "Continue safely without starting a new task."));
            return candidates;
        }
        var context = new ChoiceContext(candidates, agentId, state, currentConversation is not null);
        world.AddRegisteredChoices(ChoiceGroup.Prelude, context);
        if (world.IsAdult(agentId)) world.AddRegisteredChoices(ChoiceGroup.Adult, context);
        if (!world.NeedsUrgentWarmth(state) && world.IsChild(agentId)) world.AddRegisteredChoices(ChoiceGroup.Child, context);
        if (!world.NeedsUrgentFood(state) && world.IsAdult(agentId))
        {
            var agent = world.Society.GetAgent(agentId);
            var reachableToolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
            var accessibleWarehouses = new Lazy<PlacedBuilding[]>(() => world.WarehousesAccessibleTo(agentId).ToArray(),
                LazyThreadSafetyMode.None);
            var buildingsWithoutSites = new List<string>();
            world.AddRegisteredChoices(ChoiceGroup.UnhurriedAdult, context with
            {
                Agent = agent,
                ReachableTools = reachableToolCache,
                AccessibleWarehouses = accessibleWarehouses,
                BuildingsWithoutSites = buildingsWithoutSites,
            });
        }
        if (world.NeedsUrgentFood(state) && world.IsAdult(agentId)) world.AddRegisteredChoices(ChoiceGroup.UrgentAdult, context);
        world.AddRegisteredChoices(ChoiceGroup.Final, context);
        if (restrictForOrder && world.PendingInstructionFor(agentId) is { } order)
            return world.RestrictCandidatesForOrder(candidates, order, state);
        return candidates;
    }


    internal static List<CognitionCandidate> RestrictCandidatesForOrder(ICognitionCandidatesWorld world,
        List<CognitionCandidate> candidates,
        OwnerQueuedInstruction order,
        AgentState state)
    {
        var safeIdle = candidates.Single(item => item.Id == "safe_idle");
        var urgent = world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state);
        var urgentCandidate = urgent
            ? world.SelectOrderSurvivalCandidate(candidates, state, order)
            : null;
        if (world.TalkOrderHasConversation(order))
            return candidates.Where(item => item.Id == "safe_idle" || item.Id.StartsWith("conversation_", StringComparison.Ordinal) ||
                urgent && world.IsSurvivalCandidate(state.AgentId, item.Id)).ToList();
        var taskCandidate = world.OrderCandidateFor(order, state);
        if (world.ShouldInterruptOrder(state, order, taskCandidate, urgentCandidate))
            taskCandidate = null;
        if (taskCandidate is null)
        {
            return candidates.Where(item => item.Id == "safe_idle" || urgent && world.IsSurvivalCandidate(state.AgentId, item.Id))
                .ToList();
        }

        var selected = candidates.Where(item => item.Id == taskCandidate.Id ||
            urgent && world.IsSurvivalCandidate(state.AgentId, item.Id)).ToList();
        if (world.IsShelterOrder(order.Task!.Action))
            selected.RemoveAll(item => item.Id == taskCandidate.Id);
        if (selected.All(item => item.Id != taskCandidate.Id)) selected.Add(taskCandidate);
        if (selected.All(item => item.Id != safeIdle.Id)) selected.Add(safeIdle);
        return selected;
    }


    internal static void AddBuildCandidates(ICognitionCandidatesWorld world,
        List<CognitionCandidate> candidates,
        SocietyAgent agent,
        AgentState state,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache,
        Lazy<PlacedBuilding[]> accessibleWarehouses,
        List<string> buildingsWithoutSites)
    {
        var needsWarmth = world.NeedsUrgentWarmth(state);
        if (agent.HouseholdId is { } planningHousehold)
            world.AddHouseholdBuildingPlans(candidates, agent, needsWarmth, planningHousehold, buildingsWithoutSites);

        // Work follows what the household holds, not a role: crops need the
        // household's Farmhouse, and workstation recipes need a building the
        // household holds or a communal one (see TryFindRecipeSite).
        var placedWorkstations = world.Simulation.Buildings.Select(building => building.DefinitionId).ToHashSet(StringComparer.Ordinal);
        foreach (var recipe in world.Content.Recipes.Where(item =>
                     !item.Outputs.Any(output => output.ResourceId == "bedding") &&
                     !item.IsCrop && !world.IsGenericFoodRecipe(item) &&
                     (!needsWarmth || item.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId))) &&
                     item.WorkstationBuildingId is { } placedId && placedWorkstations.Contains(placedId)))
        {
            var householdWorkstation = recipe.WorkstationBuildingId is { } workstationId &&
                world.Content.BuildingDefinitions.TryGetValue(workstationId, out var workstation) && world.IsHouseholdLayout(workstation);
            var personalCart = world.IsHandcartRecipe(recipe);
            var recipeOwner = personalCart ? agent.Id : world.ProductionOwnerFor(null, agent.Id);
            if (personalCart && (!world.HasCarriedUnreservedQuantities(agent.Id, recipe.Inputs) ||
                world.Society.Inventory.Lots.Any(lot => lot.ItemKind == InventoryContainerRules.Handcart && lot.OwnerId == agent.Id)))
                continue;
            if (!world.NeedsRecipeOutput(recipe, recipeOwner, agent.Id) || world.AnotherAgentWaitsForWorkSite(agent.Id, recipe) ||
                !world.CanAcquireProjectInputs(recipe.Inputs, recipeOwner, agent.Id, reachableToolCache, accessibleWarehouses) ||
                !world.TryFindRecipeSite(recipe, out var siteId, out var position, agent.Id) ||
                householdWorkstation && !personalCart &&
                (recipeOwner is null || !world.HasIngredientsAtBuilding(recipe.Inputs, recipeOwner, siteId)))
            {
                continue;
            }

            var plan = world.PlanRecipeProject(agent.Id, recipe, world.Simulation.Buildings.Single(item => item.InstanceId == siteId));
            if (world.CheckActionPlan(plan).Allowed) candidates.Add(new CognitionCandidate(
                plan.CandidateId,
                $"{recipe.DisplayName} at the household work site.",
                recipe.Tags.Contains("named-meal", StringComparer.Ordinal) ? 20 : recipe.IsCrop ? 20 :
                    HouseToolsContent.IsCrudeToolRecipe(recipe) ? 23 :
                    world.OutdoorExposure(state.Position) > 0 && recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)) ? 25 : 30,
                IdentityRules.PrefixedReference("build-site:", $"{position.X},{position.Y}"))
            { ActionPlan = plan });
        }
    }


    internal static string? DestinationNameForModel(ICognitionCandidatesWorld world, string? id)
    {
        if (id is null) return null;
        return world.Society.Households.SingleOrDefault(item => item.Id == id)?.Name ??
            world.Animal(id)?.Name ??
            world.Towns.SingleOrDefault(item => item.Id == id)?.Name ??
            world.Society.Agents.SingleOrDefault(item => item.Id == id)?.Name ??
            world.Map.Resources.SingleOrDefault(item => item.Id == id)?.Kind.Replace('_', ' ');
    }


    internal static int PriorityFor(ICognitionCandidatesWorld world, AgentState state) => world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state) ? 20 : 0;
    internal static void ApplyBuildDecision(ICognitionConstructionWorld world,
        string agentId,
        AgentState state,
        string candidateId)
    {
        if (!TownConstructionCandidateIds.TryParse(candidateId, out var selection))
        {
            world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "unknown_target"));
            return;
        }

        if (selection.IsBuilding)
        {
            var buildingPlan = state.Project is { } constructionProject ? world.RetainedConstruction(agentId, constructionProject) : null;
            if (buildingPlan is not null && world.CheckConstruction(buildingPlan).Refusal is { } refusal)
            { world.RefuseChoice(buildingPlan, refusal); return; }
            var definition = world.Content.Buildings.SingleOrDefault(item => item.CanonicalId == selection.DefinitionId);
            var layout = world.CreateTownLayoutContext(agentId, selection.SitePosition, definition);
            TownConstructionSiteCandidate? rankedSite = null;
            if (definition is not null)
            {
                if (selection.SitePosition is { } selectedSite)
                    TownLayoutService.TryEvaluateConstructionSite(layout, definition, selectedSite, out rankedSite);
                else
                {
                    var sites = TownLayoutService.RankConstructionSites(layout, definition);
                    rankedSite = sites.Count == 0 ? null : sites[0];
                }
            }

            if (definition is null || rankedSite is null)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "selected_site_no_longer_legal"));
                if (selection.SitePosition is { } rejectedSite && definition is not null)
                    world.AppendEvent(new TownLayoutSiteRejectedEventPayload(agentId, world.TownForResident(agentId) ?? "none", definition.CanonicalId, rejectedSite.X, rejectedSite.Y));
                return;
            }
            var householdProperty = definition.Tags.Any(world.IsHouseholdBuildingTag);
            var houseOwner = householdProperty
                ? world.Society.GetAgent(agentId).HouseholdId : null;
            if (householdProperty && houseOwner is null)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "household_required"));
                return;
            }
            if (definition.Tags.Contains("house", StringComparer.Ordinal) && houseOwner is not null &&
                world.HouseForHousehold(houseOwner) is not null)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "existing_house"));
                return;
            }

            var position = rankedSite.Position;
            if (state.Position != position)
            {
                world.MoveToward(agentId, state, position, "build");
                return;
            }

            var constructionOrder = world.ConstructionInstructionForProject(agentId, state.Project);
            if (state.Project?.OrderInstructionId is not null && (constructionOrder is null ||
                state.Project.WorkDone < world.ProjectWorkTicks ||
                !world.TryConstructionOrderSite(constructionOrder, state, out _, out _, out _, out _)))
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "ordered_project_changed"));
                return;
            }
            var materialOwner = buildingPlan?.MaterialOwnerId ?? world.BuildingConstructionOwner(agentId, definition);
            var placement = world.PlaceBuildingCore(
                buildingPlan?.InstanceId ?? constructionOrder?.Task!.ConstructionInstanceId ?? world.BuildInstanceId(agentId, definition),
                definition.CanonicalId,
                position,
                "build_completed",
                world.TownForResident(agentId),
                houseOwner,
                materialOwner,
                constructionOrder?.InstructionId, buildingPlan?.Inputs);
            if (!placement.Applied)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, placement.Failure));
            }
            else
            {
                world.CreditCompletedWork(agentId, "building");
                if (constructionOrder is not null)
                    world.RecordConstructionOrderCompletion(constructionOrder, materialOwner);
            }

            return;
        }

        var recipeId = selection.DefinitionId;
        var recipe = world.Content.Recipes.SingleOrDefault(item => item.CanonicalId == recipeId);
        var plan = state.Project is { } recipeProject ? world.RetainedRecipeProject(agentId, recipeProject) : null;
        var siteId = plan?.Workstation.InstanceId;
        var sitePosition = plan?.Workstation.Position ?? default;
        if (recipe is null || plan is null && !world.TryFindRecipeSite(recipe, out siteId, out sitePosition, agentId))
        {
            world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "no_valid_site"));
            return;
        }

        if (state.Position != sitePosition)
        {
            world.MoveToward(agentId, state, sitePosition, "build");
            return;
        }

        var productionInstruction = world.PendingInstructionFor(agentId);
        var orderInstructionId = productionInstruction is not null &&
            world.IsProductionOrderProject(productionInstruction, state.Project) &&
            state.Project?.WorkDone == world.ProjectWorkTicks && productionInstruction.Task!.ProductionBuildingId == siteId
                ? productionInstruction.InstructionId : null;
        var started = world.StartProductionCore(recipe.CanonicalId, siteId!, agentId, "build_started", orderInstructionId, plan);
        if (!started.Applied)
        {
            world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, started.Failure));
        }
    }
    internal static bool HasWaitingIdentityMoment(AgentState physical) =>
        !physical.IdentityChoicePending && (physical.IdentityMoments ?? [])
            .Any(moment => moment.Outcome is "waiting" or "requested");

    internal static string? CognitionHouseholdId(string? id) => id is null ? null : DerivedId.Create(id, "household-sha256:");

    internal static string GuardianCandidateContext(IEnumerable<CognitionCandidate> candidates)
    {
        var ids = candidates.Where(candidate => (candidate.Id.StartsWith("guardian_accept:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_relocate:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_follow:", StringComparison.Ordinal)))
            .Select(candidate => Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate.Id))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'))
            .Order(StringComparer.Ordinal).ToArray();
        return string.Join(',', ids);
    }

    internal static bool HasNewGuardianCandidate(string? lastDecisionContext, IEnumerable<CognitionCandidate> candidates)
    {
        var previouslyOffered = GuardianCandidateContextFromLastDecision(lastDecisionContext);
        return candidates.Where(candidate => (candidate.Id.StartsWith("guardian_accept:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_relocate:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_follow:", StringComparison.Ordinal)))
            .Select(candidate => Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate.Id))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'))
            .Any(candidate => !previouslyOffered.Contains(candidate));
    }

    internal static HashSet<string> GuardianCandidateContextFromLastDecision(string? context)
    {
        const string marker = "|guardian_candidates=";
        if (context is null) return new(StringComparer.Ordinal);
        var start = context.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return new(StringComparer.Ordinal);
        start += marker.Length;
        var end = context.IndexOf('|', start);
        var encodedIds = end < 0 ? context[start..] : context[start..end];
        return encodedIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    }

    internal static bool HasPromptedConversationChoice(string? lastDecisionContext, string conversationChoiceContext) =>
        lastDecisionContext?.EndsWith(
            $"|conversation_choice={ConversationChoiceContextDigest(conversationChoiceContext)}",
            StringComparison.Ordinal) == true;

    internal static string ConversationChoiceContextDigest(string conversationChoiceContext) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conversationChoiceContext)));

    internal static string ObservationDigest(
        string agentId,
        string worldId,
        AgentState state,
        IReadOnlyList<CognitionCandidate> candidates,
        IReadOnlyList<CognitionMemoryExcerpt> memories,
        IReadOnlyList<CognitionMemoryCompactionCandidate> compactionCandidates,
        IReadOnlyList<CognitionKnowledgeFact> knownMapFacts,
        CognitionSelfContext self,
        IReadOnlyList<CognitionObserverGuidance> observerGuidance,
        IReadOnlyList<CognitionMemorySummaryOption>? summaryOptions = null)
    {
        var text = new StringBuilder()
            .Append("clankerworld.world-observation/v2|")
            .Append(agentId).Append('|')
            .Append(worldId.Length).Append(':').Append(worldId).Append('|')
            .Append(state.Position.X).Append(',').Append(state.Position.Y).Append('|')
            .Append(state.FullnessBasisPoints).Append('|')
            .Append(string.Join(',', candidates.Select(candidate => candidate.Id)));
        foreach (var memory in memories)
            text.Append('|').Append(memory.Id.Length).Append(':').Append(memory.Id)
                .Append('|').Append(memory.SourceTick)
                .Append('|').Append(memory.SubjectId.Length).Append(':').Append(memory.SubjectId)
                .Append('|').Append(memory.Summary.Length).Append(':').Append(memory.Summary)
                .Append('|').Append(memory.Kind)
                .Append('|').Append(memory.Visibility ?? "none")
                .Append('|').Append(memory.Provenance ?? "none")
                .Append('|').Append(memory.ConfidenceBasisPoints ?? -1)
                .Append('|').Append(memory.SourceAgentId ?? "none")
                .Append('|').Append(memory.SourceEventId ?? -1)
                .Append('|').Append(memory.IsCorrected)
                .Append('|').Append(memory.ImportanceBasisPoints)
                .Append('|').Append(memory.ImportanceConfidenceBasisPoints);
        text.Append("|summaries=").Append(JsonSerializer.Serialize(summaryOptions ?? []));
        text.Append("|summary_sources=").Append(JsonSerializer.Serialize(memories.Select(memory => memory.SummarySources)));
        foreach (var memory in compactionCandidates)
            text.Append('|').Append(memory.Kind)
                .Append('|').Append(memory.Id.Length).Append(':').Append(memory.Id)
                .Append('|').Append(memory.SourceTick)
                .Append('|').Append(memory.SubjectId.Length).Append(':').Append(memory.SubjectId)
                .Append('|').Append(memory.Summary.Length).Append(':').Append(memory.Summary)
                .Append('|').Append(memory.Visibility ?? "none")
                .Append('|').Append(memory.Provenance ?? "none")
                .Append('|').Append(memory.ConfidenceBasisPoints ?? -1)
                .Append('|').Append(memory.SourceAgentId ?? "none")
                .Append('|').Append(memory.SourceEventId ?? -1)
                .Append('|').Append(memory.IsCorrected);
        foreach (var fact in knownMapFacts)
            text.Append("|map=").Append(fact.X).Append(',').Append(fact.Y).Append('|')
                .Append(fact.Terrain).Append('|').Append(string.Join(',', fact.ResourceKinds))
                .Append('|').Append(fact.DiscovererId).Append('|').Append(fact.Acquisition)
                .Append('|').Append(fact.LearnedTick);
        foreach (var message in observerGuidance)
            text.Append("|observer=").Append(message.InstructionId.Length).Append(':').Append(message.InstructionId)
                .Append('|').Append(message.IssuerId.Length).Append(':').Append(message.IssuerId)
                .Append('|').Append(message.TargetAgentId.Length).Append(':').Append(message.TargetAgentId)
                .Append('|').Append(message.Kind)
                .Append('|').Append(message.Text.Length).Append(':').Append(message.Text)
                .Append('|').Append(message.SubmittedTick)
                .Append('|').Append(message.RunEpoch)
                .Append('|').Append(message.SubmissionSequence)
                .Append('|').Append(message.UnderstoodTask ?? "none")
                .Append('|').Append(message.ReplyAllowed);
        text.Append("|self=").Append(JsonSerializer.Serialize(self))
            .Append("|identity_pending=").Append(state.IdentityChoicePending);
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))}";
    }

    internal static async Task<ConversationTurnOutcome> RunConversationProviderAsync(
        IAgentConversationProvider provider,
        AgentConversationTurnRequest request,
        TimeProvider modelTimeProvider, CancellationToken cancellationToken)
    {
        try
        {
            var response = await AgentConversationProviderExecution.SpeakAsync(
                provider, request, ConversationProviderTimeout, modelTimeProvider, cancellationToken).ConfigureAwait(false);
            return new ConversationTurnOutcome(response, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ConversationTurnOutcome(null, exception);
        }
    }
    internal static void ApplySafeRoutinesWhileWaiting(ICognitionWaitingRoutinesWorld world,
        IEnumerable<string> waitingIds,
        IEnumerable<string> orderActorsHandledThisTick)
    {
        var handledOrders = orderActorsHandledThisTick.ToHashSet(StringComparer.Ordinal);
        foreach (var id in waitingIds.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (!world.Agents.TryGetValue(id, out var state)) continue;
            if (handledOrders.Contains(id) || world.HasGuardianPlacementTask(id) && world.GuardianPlacementActions.Contains(id)) continue;
            if (world.PendingInstructionFor(id) is not null) continue;
            world.TryApplyWaitingRoutine(id, state);
        }
    }


    internal static bool TryApplyWaitingRoutine(ICognitionWaitingRoutinesWorld world, string id, AgentState state)
    {
        if (world.WithheldActions?.Contains(id) == true) return false;
        if (world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state))
        {
            var candidate = world.CreateCandidates(id, state, restrictForOrder: false)
                .Where(item => world.IsWaitingSurvivalCandidate(item.Id))
                .OrderBy(item => item.DeterministicPriority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is not null) world.ApplyCandidate(id, state, candidate.Id, reportIdle: false);
            return true;
        }
        if (!world.IsAdult(id) || !world.ReadyForBriefInteraction(id)) return false;
        var nearby = world.ChildrenNeedingCare(id).Select(child => child.AgentId).Concat(world.IllDependentsNeedingCare(id))
            .Where(dependent => world.IsWithinInteractionRange(state.Position, world.Agents[dependent].Position, world.ResourceInteractionRange))
            .ToHashSet(StringComparer.Ordinal);
        if (nearby.Count == 0) return false;
        var care = world.CreateCandidates(id, state, restrictForOrder: false).Where(candidate =>
                candidate.Id.StartsWith("care:", StringComparison.Ordinal) && nearby.Contains(candidate.Id[5..]) ||
                candidate.Id.StartsWith("guardian_tend:", StringComparison.Ordinal) && nearby.Contains(candidate.Id[14..]))
            .OrderBy(candidate => candidate.DeterministicPriority).ThenBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
        if (care is null) return false;
        // Use the ordinary physical care boundary without cancelling an existing
        // repair or starting, ending or accepting a caregiving relationship.
        if (care.Id.StartsWith("care:", StringComparison.Ordinal)) world.CareForChild(id, care.Id[5..], care.ActionPlan);
        else world.TendToIllDependent(id, care.Id[14..], care.ActionPlan);
        return true;
    }


    internal static bool CanContinuePendingTask(ICognitionWaitingRoutinesWorld world, AgentState state, string id) => world.IsWaitingSurvivalCandidate(id) || world.IsTownProjectCandidate(id) || id is "knowledge_continue" or "haul_household_stock" or
            "store_household_food" or "store_town_resources" or "haul_farm_grain" or "haul_farm_flour" or
            "haul_smith_input" or "gather_smith_ore" or "deliver_smith_ore" or "collect_water_jug" or "return_water_jug" or
            "store_food_in_pot" ||
        world.KnowledgeWritingFor(state.AgentId) is { OrderInstructionId: null } writing && writing.CandidateId == id ||
        id == "explore" && state.Exploration?.OutingPath.Count > 0 ||
        state.Exploration is { Returning: false, OutingPath.Count: > 0, Goal: { OrderInstructionId: null } goal } &&
            world.ExplorationGoalCandidateId(goal) == id ||
        id.StartsWith("assist:", StringComparison.Ordinal) ||
        id.StartsWith(world.GatherBlacksmithInputPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.GatherBuildingMaterialPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.RareMiningPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.SupplyWorkstationPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.ReturnEmptyVesselPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.FillWaterJugPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.PullCartPrefix, StringComparison.Ordinal) ||
        id.StartsWith("boat_queue:", StringComparison.Ordinal) ||
        id.StartsWith("animal:supply_trip:", StringComparison.Ordinal) ||
        id.StartsWith("animal:care:", StringComparison.Ordinal) ||
        id.StartsWith("animal:collect:", StringComparison.Ordinal) ||
        id.StartsWith("animal:lead_home:", StringComparison.Ordinal);
}
