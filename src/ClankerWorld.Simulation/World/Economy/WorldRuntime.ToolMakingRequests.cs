using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Text.Json;
using ShopQuote = ClankerWorld.Simulation.World.MarketSystem.ShopQuote;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string ToolRequestPrefix = "tool_request_";
    // Routine choices cannot commit these verbs; keep safe idle ahead of them.
    private const int PersonalToolRequestPriority = 200;
    private IReadOnlyList<ToolMakingRequestState> toolMakingRequests => productionSystem.State.Requests;

    public IReadOnlyList<ToolMakingRequestState> ToolMakingRequests => toolMakingRequests
        .OrderBy(request => request.Id, StringComparer.Ordinal).ToArray();

    public string? ToolMakingRequestNote(string actor)
    {
        gate.Wait();
        try { return ToolMakingRequestNoteCore(actor); }
        finally { gate.Release(); }
    }

    private string? ToolMakingRequestNoteCore(string actor) => agents.ContainsKey(actor)
        ? ToolMakingRequestRules.Note(toolMakingRequests, actor, HouseholdFor(actor)) : null;

    private static string ToolRequestChoice(string verb, params string[] identity) =>
        Enum.TryParse<ToolRequestConsentKind>(verb, true, out var consent)
            ? ToolRequestConsentChoice.Encode(consent, identity)
            : verb == "visit" ? ToolRequestVisitChoice.Encode(identity[0], identity[1])
            : verb == "work" ? ToolRequestWorkChoice.Encode(identity[0])
            : verb == "collect" ? ToolRequestCollectionChoice.Encode(identity[0])
            : DerivedId.Hash(JsonSerializer.Serialize(identity), ToolRequestPrefix + verb + ":");

    private static string ToolMakingRequestId(string actor, string building, string recipe, long tick) =>
        ProductionSystem.ToolMakingRequestId(actor, building, recipe, tick);

    private bool IsToolRequestSeller(string actor, ToolMakingRequestState request) =>
        agents.ContainsKey(actor) && IsAdult(actor) && HouseholdFor(actor) == request.SellerHouseholdId &&
        ToolRequestShop(request) is not null;

    private PlacedBuilding? ToolRequestShop(ToolMakingRequestState request) => worldSimulation.Buildings.FirstOrDefault(building =>
        building.InstanceId == request.BuildingInstanceId && building.HouseholdId == request.SellerHouseholdId &&
        worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
            definition.Tags.Contains("blacksmith", StringComparer.Ordinal)));

    private bool IdleForToolRequest(string actor) => agents[actor].Project is null or { Stage: "completed" or "cancelled" } &&
        agents[actor].Equipment?.Repair is null && this.farmingSystem.FarmWorkFor(this.farmingWorld, actor) is null && !IsConversationBusy(actor);

    private bool ToolRequestWanted(string actor, RecipeDefinition recipe)
    {
        var tool = ToolProgressionRules.Find(recipe.Outputs[0].ResourceId)!;
        if (tool.Family is ToolFamily.Hoe or ToolFamily.Sickle && FarmhouseForHousehold(HouseholdFor(actor)) is null)
            return false;
        var carried = ToolProgressionRules.BestUsableTool(society.Checkpoint.Inventory, actor, tool.Family);
        return carried is null || ToolProgressionRules.Find(carried.ItemKind)!.Tier < tool.Tier;
    }

    private bool MayStartToolRequest(string actor) => IsAdult(actor) &&
        !toolMakingRequests.Any(request => request.RequesterId == actor && !ToolMakingRequestRules.IsTerminal(request.Status)) &&
        toolMakingRequests.Count(request => !ToolMakingRequestRules.IsTerminal(request.Status)) < ToolMakingRequestRules.MaximumActiveRequests;

    private bool MayRequestToolAt(string actor, PlacedBuilding building, RecipeDefinition recipe)
    {
        var position = agents[actor].Position;
        return recipe.WorkstationBuildingId == building.DefinitionId && ToolMakingRequestRules.IsToolRecipe(recipe) &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("blacksmith", StringComparer.Ordinal)) &&
            MayStartToolRequest(actor) && building.HouseholdId is { } household && household != HouseholdFor(actor) &&
            agents.Keys.Any(seller => IsAdult(seller) && HouseholdFor(seller) == household) &&
            (IsWithinInteractionRange(position, building.Position, ResourceInteractionRange) ||
                KnowsMapFact(actor, building.Position) && Reach(actor, building.Position, ReachPurpose.ToolRequest, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) &&
            ToolRequestWanted(actor, recipe) && !FindGoods(new(GoodsUse.ToolRequestStock, actor, GoodsOwners.One(household), GoodsKinds.One(recipe.Outputs[0].ResourceId), AtBuilding: building.InstanceId)).Matches.Any();
    }

    private IEnumerable<(PlacedBuilding Building, RecipeDefinition Recipe)> ToolRequestOptions(string actor) =>
        !MayStartToolRequest(actor) ? [] : worldSimulation.Buildings.OrderBy(building => building.InstanceId, StringComparer.Ordinal)
            .SelectMany(building => worldContent.Recipes.OrderBy(recipe => recipe.CanonicalId, StringComparer.Ordinal)
                .Where(recipe => MayRequestToolAt(actor, building, recipe)).Select(recipe => (building, recipe)));

    private void AddToolMakingRequestCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor)) return;
        foreach (var request in toolMakingRequests.Where(request => !ToolMakingRequestRules.IsTerminal(request.Status))
                     .OrderBy(request => request.Id, StringComparer.Ordinal))
        {
            if (request.RequesterId == actor)
            {
                var withdrawChoice = ToolRequestConsentChoice.Decode(ToolRequestChoice("withdraw", request.Id))!;
                if (global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Consent.Offer(this, actor, withdrawChoice) is { } withdrawal)
                    candidates.Add(new(withdrawal.CandidateId,
                        "Withdraw your tool request. Household materials and work remain household property.", PersonalToolRequestPriority, request.BuildingInstanceId)
                    { ActionPlan = withdrawal });
                if (ToolRequestCollectionChoice.Decode(ToolRequestChoice("collect", request.Id)) is { } collectChoice &&
                    global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Collection.Offer(this, actor, collectChoice) is { } collection)
                {
                    if (!IsWithinInteractionRange(agents[actor].Position, collection.Smith.Position, ResourceInteractionRange))
                        candidates.Add(new(ToolRequestChoice("collect", request.Id), "Walk to the Blacksmith to discuss payment for the finished tool.", 14, collection.Smith.InstanceId) { ActionPlan = collection });
                    else if (collection.Quote is { } quote)
                        candidates.Add(new(ToolRequestChoice("collect", request.Id),
                            $"Offer {quote.PaymentQuantity} {quote.Payment.ItemKind} for the actual finished {request.ItemKind.Replace('_', ' ')}. The household may refuse.", 14, collection.Smith.InstanceId)
                        { ActionPlan = collection });
                }
            }
            if (!IsToolRequestSeller(actor, request)) continue;
            if (request.Status == ToolMakingRequestStatus.Requested)
            {
                var name = society.Checkpoint.GetAgent(request.RequesterId).Name;
                var acceptChoice = ToolRequestConsentChoice.Decode(ToolRequestChoice("accept", request.Id))!;
                if (IdleForToolRequest(actor) && global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Consent.Offer(this, actor, acceptChoice) is { } acceptance)
                    candidates.Add(new(acceptance.CandidateId,
                        $"Accept {name}'s {request.ItemKind.Replace('_', ' ')} request using household materials; payment is discussed after it is made.", PersonalToolRequestPriority, request.BuildingInstanceId)
                    { ActionPlan = acceptance });
                var refuseChoice = ToolRequestConsentChoice.Decode(ToolRequestChoice("refuse", request.Id))!;
                if (global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Consent.Offer(this, actor, refuseChoice) is { } refusal)
                    candidates.Add(new(refusal.CandidateId, $"Refuse {name}'s tool request without taking anything.", PersonalToolRequestPriority, request.BuildingInstanceId)
                    { ActionPlan = refusal });
            }
            else if (ToolRequestWorkChoice.Decode(ToolRequestChoice("work", request.Id)) is { } workChoice &&
                     global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Work.Offer(this, actor, workChoice) is { } work)
                candidates.Add(new(ToolRequestChoice("work", request.Id),
                    "Continue the accepted tool work using its ordinary household production plan.", 30, request.BuildingInstanceId)
                { ActionPlan = work });
        }
        if (!IdleForToolRequest(actor) || NeedsUrgentFood(agents[actor]) || NeedsUrgentWarmth(agents[actor])) return;
        var offered = 0;
        foreach (var (building, recipe) in ToolRequestOptions(actor))
        {
            var local = IsWithinInteractionRange(agents[actor].Position, building.Position, ResourceInteractionRange);
            var id = ToolRequestChoice(local ? "place" : "visit", building.InstanceId, recipe.CanonicalId);
            ActionPlan? plan = local
                ? global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Consent.Offer(this, actor, ToolRequestConsentChoice.Decode(id)!)
                : global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Visit.Offer(this, actor, ToolRequestVisitChoice.Decode(id)!);
            if (plan is null) continue;
            candidates.Add(new(plan.CandidateId, local
                ? $"Ask this Blacksmith household to make a {recipe.Outputs[0].ResourceId.Replace('_', ' ')}. No payment or material changes hands now."
                : $"Visit the known Blacksmith to ask about making a {recipe.Outputs[0].ResourceId.Replace('_', ' ')}.", local ? PersonalToolRequestPriority : 33, building.InstanceId)
            { ActionPlan = plan });
            if (++offered == 16) break;
        }
    }

    private bool ApplyToolMakingRequestDecision(SocietyCognitionDispatchResult decision)
    {
        var selected = decision.Admission.Intention;
        if (selected is null || ToolRequestConsentChoice.Decode(selected.CandidateId) is not { } choice) return false;
        if (PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.Order) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack || selected.Provider != DecisionProviderKind.LargeLanguageModel ||
            selected.AgentId != decision.AgentId || !agents.ContainsKey(decision.AgentId) || !IsAdult(decision.AgentId)) return true;
        var plan = decision.ActionPlan as ToolRequestConsentActionPlan ?? PlanToolRequestConsent(decision.AgentId, choice);
        if (plan is not null) ApplyToolRequestConsentPlan(plan);
        return true;
    }

    private void CompleteToolRequestConsent(ToolRequestConsentActionPlan plan)
    {
        var actor = plan.Actor;
        if (plan.Choice.Kind == ToolRequestConsentKind.Place)
        {
            var id = ToolMakingRequestId(actor, plan.Shop.InstanceId, plan.Recipe.CanonicalId, WorldTick);
            productionSystem.PlaceRequest(new(id, actor, plan.Shop.HouseholdId!, plan.Shop.InstanceId,
                plan.Recipe.CanonicalId, plan.Recipe.Outputs[0].ResourceId, WorldTick, WorldTick));
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent(new ToolRequestPlacedEventPayload(actor, id));
            return;
        }
        var request = toolMakingRequests.Single(item => item.Id == plan.Original!.Id);
        switch (plan.Choice.Kind)
        {
            case ToolRequestConsentKind.Withdraw:
                if (request.OfferId is { } paidOffer && society.Checkpoint.Inventory.Offers.Any(offer =>
                        offer.Id == paidOffer && offer.State == DirectBarterState.Settled))
                {
                    SetToolRequest(request with { Status = ToolMakingRequestStatus.Fulfilled, Blocker = null }, "tool_request_completed", actor);
                    return;
                }
                CloseToolRequestOffer(request, "The customer withdrew the request.");
                SetToolRequest(request with { Status = ToolMakingRequestStatus.Withdrawn, Blocker = null }, "tool_request_withdrawn", actor);
                break;
            case ToolRequestConsentKind.Refuse:
                SetToolRequest(request with { Status = ToolMakingRequestStatus.Refused }, "tool_request_refused", actor);
                break;
            case ToolRequestConsentKind.Accept:
                SetToolRequest(request with { Status = ToolMakingRequestStatus.Accepted, WorkerId = actor, AcceptedTick = WorldTick }, "tool_request_accepted", actor);
                BeginProject(actor, agents[actor], "build:recipe:" + request.RecipeId, request.Id);
                break;
        }
    }

    // Authority-changing actions above never execute from a continuing or fallback intention.
    private void CompleteToolRequestCollection(ToolRequestCollectionActionPlan plan)
    {
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Smith.Position, ResourceInteractionRange))
            MoveToward(plan.Actor, agents[plan.Actor], plan.Smith.Position, "tool_request", ResourceInteractionRange);
        else if (plan.Quote is { } quote)
        {
            var offerId = OpenShopQuote(plan.Actor, plan.Smith, new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity));
            SetToolRequest(plan.Request with { Status = ToolMakingRequestStatus.Offered, OfferId = Identity.Parse(offerId), Blocker = null });
        }
    }

    private ShopQuote? ToolRequestQuote(string actor, ToolMakingRequestState request, PlacedBuilding shop) =>
        request.JobId is { } jobId && worldSimulation.ProductionJobs.Any(job => job.JobId == jobId && job.State == WorldProductionJobState.Completed)
            ? ShopOpportunity(actor, shop, ProductionOutputLotId(jobId, 0), requestedGoods: true) : null;

    private bool HasToolMakingDemand(RecipeDefinition recipe, string? owner, string? worker) => toolMakingRequests.Any(request =>
        request.SellerHouseholdId == owner && request.RecipeId == recipe.CanonicalId &&
        request.Status == ToolMakingRequestStatus.Accepted && request.JobId is null && (worker is null || request.WorkerId == worker));

    private string? ToolMakingRequestJobFor(string worker, RecipeDefinition recipe, string building) =>
        agents.TryGetValue(worker, out var person) && person.Project?.ToolMakingRequestId is { } id &&
        person.Project.WorkDone >= global::ClankerWorld.Simulation.World.ProductionSystem.ProjectWorkTicks &&
        toolMakingRequests.Any(request => request.Id == id && request.Status == ToolMakingRequestStatus.Accepted &&
            request.JobId is null && request.WorkerId == worker && request.AcceptedTick == person.Project.StartedTick &&
            request.RecipeId == recipe.CanonicalId && request.BuildingInstanceId == building && IsToolRequestSeller(worker, request)) ? id : null;

    private void BindToolMakingJob(WorldProductionJob job)
    {
        if (job.ToolMakingRequestId is not { } id) return;
        var request = toolMakingRequests.Single(request => request.Id == id);
        SetToolRequest(request with { JobId = job.JobId, Blocker = null });
    }

    private void CloseToolRequestOffer(ToolMakingRequestState request, string reason)
    {
        if (request.OfferId is null) return;
        var offer = society.Checkpoint.Inventory.Offers.FirstOrDefault(offer => offer.Id == request.OfferId);
        var binding = shopTrades.FirstOrDefault(trade => trade.OfferId == request.OfferId);
        if (offer is { State: DirectBarterState.Open } && binding is not null) this.marketSystem.CancelShopTrade(this.MarketShopWorld, binding, offer, reason);
    }

    private void SetToolRequest(ToolMakingRequestState request, string? eventKind = null, string? actor = null)
    {
        if (!productionSystem.TransitionRequest(request, WorldTick)) return;
        if ((ToolMakingRequestRules.IsTerminal(request.Status) || request.Status == ToolMakingRequestStatus.Ready) &&
            request.WorkerId is { } worker && agents.TryGetValue(worker, out var person) &&
            person.Project?.ToolMakingRequestId == request.Id)
            agentStore.RecordProject(worker, person, person.Project with { ToolMakingRequestId = null });
        checkpointSchemaVersion = StateSchemaVersion;
        if (eventKind is not null) AppendEvent(WorldEvents.ToolRequest(eventKind, actor ?? request.RequesterId, request.Id));
    }

    private string? ToolMakingInputBlocker(ToolMakingRequestState request, AgentProject project)
    {
        var recipe = worldContent.Recipes.First(item => item.CanonicalId == request.RecipeId);
        var missing = recipe.Inputs.Where(input => society.Checkpoint.Inventory.Lots.Where(lot =>
                lot.OwnerId == request.SellerHouseholdId && lot.StorageBuildingId == request.BuildingInstanceId &&
                lot.ItemKind == input.ResourceId && lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0)
            .Sum(lot => (long)AvailableLotQuantity(lot)) < input.Amount).Select(input => input.ResourceId.Replace('_', ' ')).ToArray();
        if (missing.Length == 0) return PublicToolWorkBlocker(project.Blocker);
        return DestinationRoom(request.BuildingInstanceId) == 0
            ? "Waiting for room at the Blacksmith before more materials can arrive."
            : "Waiting for household materials at the Blacksmith: " + string.Join(", ", missing) + ".";
    }

    // The customer and the shop card see the request's progress, not the worker's own errands or household storage.
    private static string? PublicToolWorkBlocker(string? projectBlocker) => projectBlocker switch
    {
        null => null,
        WaitingForWorkSiteBlocker => "Waiting for a free work site at the Blacksmith.",
        _ => "The household is still preparing this work.",
    };

    private void MaintainToolMakingRequests()
    {
        foreach (var original in toolMakingRequests.ToArray())
        {
            var request = original;
            if (ToolMakingRequestRules.IsTerminal(request.Status)) continue;
            if (request.Status == ToolMakingRequestStatus.Offered &&
                society.Checkpoint.Inventory.Offers.FirstOrDefault(offer => offer.Id == request.OfferId) is { State: DirectBarterState.Settled })
            {
                SetToolRequest(request with { Status = ToolMakingRequestStatus.Fulfilled }, "tool_request_completed");
                continue;
            }
            var unavailable = !agents.ContainsKey(request.RequesterId) || !IsAdult(request.RequesterId) || ToolRequestShop(request) is null ||
                !worldContent.Recipes.Any(recipe => recipe.CanonicalId == request.RecipeId && ToolMakingRequestRules.IsToolRecipe(recipe)) ||
                !agents.Keys.Any(seller => IsAdult(seller) && HouseholdFor(seller) == request.SellerHouseholdId);
            var joinedSeller = !unavailable && HouseholdFor(request.RequesterId) == request.SellerHouseholdId;
            if (unavailable || joinedSeller)
            {
                var reason = joinedSeller ? "The customer joined the household that holds this shop." : "The requester or shop is no longer available.";
                CloseToolRequestOffer(request, reason);
                SetToolRequest(request with { Status = ToolMakingRequestStatus.Interrupted, Blocker = reason }, "tool_request_interrupted");
                continue;
            }
            if (request.Status == ToolMakingRequestStatus.Accepted)
            {
                if (request.WorkerId is not { } worker || !IsToolRequestSeller(worker, request))
                {
                    SetToolRequest(request with { Status = ToolMakingRequestStatus.Interrupted, Blocker = "The worker no longer has access to this shop." }, "tool_request_interrupted");
                    continue;
                }
                var project = agents[worker].Project;
                if (request.JobId is null && project?.ToolMakingRequestId == request.Id)
                    request = request with
                    {
                        JobId = project.JobId,
                        Blocker = project.JobId is null
                        ? ToolMakingInputBlocker(request, project) : PublicToolWorkBlocker(project.Blocker)
                    };
                if (request.JobId is { } jobId)
                {
                    var job = worldSimulation.ProductionJobs.FirstOrDefault(job => job.JobId == jobId);
                    if (job is { State: WorldProductionJobState.Completed })
                        SetToolRequest(request with { Status = ToolMakingRequestStatus.Ready, Blocker = null }, "tool_request_ready");
                    else if (job is null or { State: WorldProductionJobState.Cancelled })
                        SetToolRequest(request with { Status = ToolMakingRequestStatus.Interrupted, Blocker = "Production was removed or cancelled." }, "tool_request_interrupted");
                    else SetToolRequest(request);
                }
                else if (project?.ToolMakingRequestId != request.Id || project.Stage is "completed" or "cancelled")
                    SetToolRequest(request with { Status = ToolMakingRequestStatus.Interrupted, Blocker = "The worker chose another task." }, "tool_request_interrupted");
                else SetToolRequest(request);
            }
            else if (request.Status == ToolMakingRequestStatus.Ready)
            {
                var linkedTrade = shopTrades.FirstOrDefault(trade => trade.BuyerId == request.RequesterId &&
                    trade.BuildingInstanceId == request.BuildingInstanceId && society.Checkpoint.Inventory.Offers.Any(offer =>
                        offer.Id == trade.OfferId && offer.FirstLotId == ProductionOutputLotId(request.JobId!, 0) &&
                        offer.State is DirectBarterState.Open or DirectBarterState.Settled));
                if (linkedTrade is not null)
                {
                    var offer = society.Checkpoint.Inventory.GetOffer(linkedTrade.OfferId);
                    SetToolRequest(request with
                    {
                        OfferId = offer.Id,
                        Status = offer.State == DirectBarterState.Settled
                        ? ToolMakingRequestStatus.Fulfilled : ToolMakingRequestStatus.Offered
                    },
                        offer.State == DirectBarterState.Settled ? "tool_request_completed" : null);
                    continue;
                }
                var lot = society.Checkpoint.Inventory.Lots.FirstOrDefault(lot => lot.Id == ProductionOutputLotId(request.JobId!, 0));
                if (lot is null || lot.OwnerId != request.SellerHouseholdId || lot.StorageBuildingId != request.BuildingInstanceId ||
                    !MarketSystem.IsLooseShopLot(lot, false) || AvailableLotQuantity(lot) < 1)
                    SetToolRequest(request with { Status = ToolMakingRequestStatus.Interrupted, Blocker = "The household's finished tool is no longer available." }, "tool_request_interrupted");
            }
            else if (request.Status == ToolMakingRequestStatus.Offered)
            {
                var offer = society.Checkpoint.Inventory.Offers.FirstOrDefault(offer => offer.Id == request.OfferId);
                if (offer is { State: DirectBarterState.Settled })
                    SetToolRequest(request with { Status = ToolMakingRequestStatus.Fulfilled }, "tool_request_completed");
                else if (offer is null or { State: DirectBarterState.Cancelled })
                    SetToolRequest(request with { Status = ToolMakingRequestStatus.Interrupted, Blocker = "The exchange was cancelled; goods keep their owners." }, "tool_request_interrupted");
            }
        }
        var retired = toolMakingRequests.Where(request => ToolMakingRequestRules.IsTerminal(request.Status))
            .OrderByDescending(request => request.LastTransitionTick).ThenBy(request => request.Id, StringComparer.Ordinal)
            .Skip(ToolMakingRequestRules.MaximumTerminalRequests).Select(request => request.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var request in toolMakingRequests.Where(request => ToolMakingRequestRules.IsTerminal(request.Status) &&
            request.OfferId is { } offer && !society.Checkpoint.Inventory.Offers.Any(item => item.Id == offer))) retired.Add(request.Id);
        if (retired.Count > 0)
        {
            productionSystem.RetireRequests(retired);
        }
        productionSystem.CheckMaintainedRequests();
    }

    private ToolRequestCollectionActionPlan? PlanToolRequestCollection(string actor, ToolRequestCollectionChoice choice)
    {
        if (!agents.TryGetValue(actor, out var person)) return null;
        var request = toolMakingRequests.FirstOrDefault(item => ToolRequestCollectionChoice.Encode(item.Id) == choice.Encode());
        if (request is null || ToolRequestShop(request) is not { } smith) return null;
        var local = IsWithinInteractionRange(person.Position, smith.Position, ResourceInteractionRange);
        var quote = local ? ToolRequestQuote(actor, request, smith) : null;
        return new(actor, choice, request, smith,
            Reach(actor, smith.Position, ReachPurpose.ToolRequest, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            quote is null ? null : new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity));
    }

    private ActionCheck CheckToolRequestCollectionPlan(ToolRequestCollectionActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestCapabilities);
        if (!capability.Allowed) return capability;
        if (toolMakingRequests.FirstOrDefault(item => item.Id == plan.Request.Id) != plan.Request ||
            plan.Request.Status != ToolMakingRequestStatus.Ready || plan.Request.RequesterId != plan.Actor ||
            !IdleForToolRequest(plan.Actor) || ToolRequestShop(plan.Request) != plan.Smith)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected finished-tool request or Blacksmith has changed");
        if (plan.Quote is not { } quote)
            return !IsWithinInteractionRange(agents[plan.Actor].Position, plan.Smith.Position, ResourceInteractionRange)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "no selected tool exchange is available at the Blacksmith");
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Smith.Position, ResourceInteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Blacksmith is no longer within reach");
        if (plan.Request.JobId is not { } jobId ||
            !worldSimulation.ProductionJobs.Any(job => job.JobId == jobId && job.State == WorldProductionJobState.Completed) ||
            quote.Goods.Id != ProductionOutputLotId(jobId, 0))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected finished tool is no longer available");
        return this.marketSystem.CheckSelectedShopQuote(this.MarketShopPlanningWorld, plan.Actor, plan.Smith, quote, true);
    }

    private static ActorCapabilityPolicy ToolRequestCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy ToolRequestVisitCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    private static ActorCapabilityPolicy ToolRequestConsentCapabilities(ToolRequestConsentKind kind) =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: kind != ToolRequestConsentKind.Place,
            AllowUrgentWarmth: kind != ToolRequestConsentKind.Place, AllowActiveOrder: true);

    private static class ToolRequestActions
    {
        internal static readonly ActionDefinition<ToolRequestConsentChoice, ToolRequestConsentActionPlan> Consent = new(
            DecodeReferenceChoice(ToolRequestConsentChoice.Decode), static choice => choice.Encode(), ToolRequestConsentCapabilities(ToolRequestConsentKind.Place),
            static (world, actor, choice, _) => world.PlanToolRequestConsent(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestConsentPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteToolRequestConsent(plan); return true; },
            PlanCapabilities: static plan => ToolRequestConsentCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<ToolRequestVisitChoice, ToolRequestVisitActionPlan> Visit = new(
            DecodeReferenceChoice(ToolRequestVisitChoice.Decode), static choice => choice.Encode(), ToolRequestVisitCapabilities,
            static (world, actor, choice, _) => world.PlanToolRequestVisit(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestVisitPlan(plan, policy),
            static (world, plan, _, _) => { world.MoveToward(plan.Actor, world.agents[plan.Actor], plan.Smith.Position, "tool_request", WorldRuntime.ResourceInteractionRange); return true; });

        internal static readonly ActionDefinition<ToolRequestWorkChoice, ToolRequestWorkActionPlan> Work = new(
            DecodeReferenceChoice(ToolRequestWorkChoice.Decode), static choice => choice.Encode(), ToolRequestCapabilities,
            static (world, actor, choice, _) => world.PlanToolRequestWork(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestWorkPlan(plan, policy),
            static (world, plan, action, _) => { world.ApplyCheckedToolRequestWork(plan, action.ReportIdle); return true; });

        internal static readonly ActionDefinition<ToolRequestCollectionChoice, ToolRequestCollectionActionPlan> Collection = new(
            DecodeReferenceChoice(ToolRequestCollectionChoice.Decode), static choice => choice.Encode(), ToolRequestCapabilities,
            static (world, actor, choice, _) => world.PlanToolRequestCollection(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestCollectionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteToolRequestCollection(plan); return true; });
    }

    private ToolRequestWorkActionPlan? PlanToolRequestWork(string actor, ToolRequestWorkChoice choice)
    {
        if (!agents.TryGetValue(actor, out var person) || person.Project is not { } project) return null;
        var request = toolMakingRequests.FirstOrDefault(item => ToolRequestWorkChoice.Encode(item.Id) == choice.Encode());
        if (request is null || ToolRequestShop(request) is not { } smith ||
            worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == request.RecipeId) is not { } definition) return null;
        return new(actor, choice, request, project, smith, definition);
    }

    private ActionCheck CheckToolRequestWorkPlan(ToolRequestWorkActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestCapabilities);
        if (!capability.Allowed) return capability;
        if (toolMakingRequests.FirstOrDefault(request => request.Id == plan.Request.Id) is not { } current ||
            current.Status != ToolMakingRequestStatus.Accepted || current.WorkerId != plan.Actor ||
            current.RequestedTick != plan.Request.RequestedTick || current.AcceptedTick != plan.Request.AcceptedTick ||
            current.RecipeId != plan.Request.RecipeId || current.BuildingInstanceId != plan.Request.BuildingInstanceId ||
            ToolRequestShop(current) != plan.Smith || !IsToolRequestSeller(plan.Actor, current) ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected commission, worker or Blacksmith has changed");
        var person = agents[plan.Actor];
        if (person.Project is not { } project || project.ToolMakingRequestId != current.Id ||
            project.StartedTick != plan.Project.StartedTick || project.CandidateId != plan.Project.CandidateId ||
            person.Equipment?.Repair is not null || this.farmingSystem.FarmWorkFor(this.farmingWorld, plan.Actor) is not null || IsConversationBusy(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected commission project is no longer available");
        return HasIngredientsAtBuilding(plan.Recipe.Inputs, current.SellerHouseholdId, plan.Smith.InstanceId)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Blacksmith no longer has the required inputs");
    }

    private void ApplyCheckedToolRequestWork(ToolRequestWorkActionPlan plan, bool freshChoice)
    {
        if (!freshChoice && agents[plan.Actor].Project!.RequiresFreshChoice) return;
        this.BeginProject(plan.Actor, this.agents[plan.Actor], plan.Project.CandidateId, plan.Request.Id);
    }

    private ToolGatheringPlan? PendingSmithHarvest(string actor) => HouseholdFor(actor) is { } household &&
        this.HouseholdBuildingWithTag(household, "blacksmith") is { } smith && BlacksmithHarvestToPrepare(household, smith.InstanceId, actor) is { } missing
        ? ProjectMaterialHarvest(actor, missing.ItemKind, missing.Source) : null;

    private ToolCollectionActionPlan? PlanToolCollectionCandidate(string actor, string candidate) =>
        ToolCollectionChoice.Decode(candidate) is { } choice ? PlanToolCollection(actor, choice.ItemKind, pendingHarvest: PendingSmithHarvest(actor)) : null;

    private ToolCollectionActionPlan? PlanToolCollection(string actor, string itemKind,
        InventoryLot? selectedStock = null, ToolGatheringPlan? pendingHarvest = null)
    {
        var choice = new ToolCollectionChoice(itemKind);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), SmithDeliveryPolicy).Allowed ||
            ToolProgressionRules.Find(itemKind) is not { } definition || !MayCollectToolFamily(actor, definition.Family) ||
            (selectedStock ?? SharedItem(itemKind, actor)) is not { ContainerLotId: null } stock)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var pendingFamily = pendingHarvest?.ToolLotId is { } requiredTool
            ? ToolProgressionRules.Find(inventory.GetLot(requiredTool).ItemKind)?.Family : null;
        var minimumFree = pendingHarvest is null ? 0 : checked((definition.Family == pendingFamily
            ? definition.GatherQuantity : pendingHarvest.Quantity) + pendingHarvest.TreeSeedQuantity);
        PlannedCargoStorage? futureStorage = null;
        var missing = minimumFree - (FreeCarryCapacity(actor) - 1);
        if (missing > 0)
        {
            if (definition.Family != pendingFamily || HouseholdFor(actor) is not { } household) return null;
            var cargo = SpareCargoForFood(actor, missing);
            if (cargo.Count == 0 || SpareCargoDestination(actor, agents[actor], cargo.Sum(move => move.PhysicalQuantity)) is not { } storage)
                return null;
            futureStorage = new(actor, household, CaptureCargoMoves(cargo), storage.Position, storage.Range, storage.StorageBuildingId,
                Reach(actor, storage.Position, ReachPurpose.FoodCargoStorage, storage.Range, 0, SwimmingPolicy.WhenCapable));
        }
        var target = HouseholdStockPosition(stock);
        var range = HouseholdStockInteractionRange(stock);
        return new(actor, choice, HouseholdFor(actor), new(stock.Id, stock.OwnerId, stock.ItemKind, target, range),
            minimumFree, futureStorage, Reach(actor, target, ReachPurpose.MaterialGathering, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckToolCollectionPlan(ToolCollectionActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || ToolProgressionRules.Find(plan.Choice.ItemKind) is not { } definition ||
            !MayCollectToolFamily(plan.Actor, definition.Family))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household can no longer use this tool family");
        if (HasCarriedEquipmentAtLeast(plan.Actor, plan.Choice.ItemKind))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry a tool at least this good");
        var pickup = CheckEquipmentPickup(plan.Actor, plan.Pickup);
        if (!pickup.Allowed) return pickup;
        if (FreeCarryCapacity(plan.Actor) - 1 + (plan.FutureCargoStorage?.Cargo.Sum(move => move.PhysicalQuantity) ?? 0) < plan.MinimumFreeAfterPickup)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected tool would leave no room for the planned harvest");
        return plan.FutureCargoStorage is { } cargo ? CheckCargoStorage(cargo) : new();
    }

    private ActionCheck CheckEquipmentPickup(string actor, PlannedEquipmentPickup pickup, string description = "tool",
        ReachPurpose purpose = ReachPurpose.MaterialGathering)
    {
        if (FreeCarryCapacity(actor) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, $"there is no room for the selected {description}");
        if (RecheckGoods(SharedCollectionRequest(actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { Lot: var selected } ||
            selected.ContainerLotId is not null || selected.CarrierId is not null ||
            HouseholdStockPosition(selected) != pickup.Position || HouseholdStockInteractionRange(selected) != pickup.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, $"the selected shared {description} is no longer available");
        return IsWithinInteractionRange(agents[actor].Position, pickup.Position, pickup.InteractionRange) ||
            Reach(actor, pickup.Position, purpose, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, $"the selected {description} is out of reach now");
    }

    private IEnumerable<InventoryLot> CarriedRepairTools(string actor, string household) =>
        FindGoods(new(GoodsUse.CarriedInputs, actor, new([actor, household]),
            new(ToolProgressionRules.All.Select(tool => tool.ItemKind).ToArray()))).Matches.Select(match => match.Lot)
            .Where(tool => ToolProgressionRules.IsTopLevelCarriedLot(tool, actor) && tool.ConditionBasisPoints is > 0 and < 10_000)
            .OrderBy(tool => tool.Id, StringComparer.Ordinal);

    private ToolRepairActionPlan? PlanToolRepair(string actor, string toolId, InventoryLot? selectedTool = null)
    {
        var choice = new ToolRepairChoice(toolId);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), SmithDeliveryPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || this.HouseholdBuildingWithTag(household, "blacksmith") is not { } smith ||
            (selectedTool ?? CarriedRepairTools(actor, household).SingleOrDefault(tool => tool.Id == toolId)) is not { } target)
            return null;
        var needs = ToolProgressionRules.RepairMaterials(target.ItemKind);
        if (needs.Count == 0) return null;
        var inventory = society.Checkpoint.Inventory;
        var materials = new List<PlannedRepairMaterial>();
        var inputs = new List<PlannedRepairInput>();
        foreach (var need in needs)
        {
            var carried = SpendingGoods(actor, GoodsUse.Spend, [actor], need.ResourceId)
                .FirstOrDefault(match => ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor) && match.Quantity >= need.Amount)?.Lot;
            if (carried is not null) { materials.Add(new(carried.Id, need.ResourceId, need.Amount)); continue; }
            if (SharedPreparationItem(need.ResourceId, actor, smith.Position) is { } stock)
            {
                if (AvailableLotQuantity(stock) < need.Amount) return null;
                inputs.Add(new(need.ResourceId, need.Amount, stock.Id, stock.OwnerId, HouseholdStockPosition(stock),
                    HouseholdStockInteractionRange(stock), null, null, null, null));
            }
            else
            {
                if (MaterialSource(need.ResourceId, actor, smith.Position) is not { } source ||
                    ProjectMaterialHarvest(actor, need.ResourceId, source) is not { } harvest) return null;
                inputs.Add(new(need.ResourceId, need.Amount, null, null, source.Position, ResourceInteractionRange, source, harvest,
                    harvest.ToolLotId is { } harvestTool ? inventory.GetLot(harvestTool).OwnerId : null,
                    harvest.ToolLotId is { } selectedHarvestTool ? inventory.GetLot(selectedHarvestTool).ItemKind : null));
            }
        }
        PlannedCargoStorage? storage = null;
        var required = inputs.Sum(input => input.CarryUnits);
        if (required > FreeCarryCapacity(actor))
        {
            var retained = materials.ToDictionary(material => material.StockId, material => material.Quantity, StringComparer.Ordinal);
            var protectedTools = inputs.Where(input => input.Harvest?.ToolLotId is not null).Select(input => input.Harvest!.ToolLotId!).ToArray();
            var cargo = OrdinarySpareCargo(actor, required - FreeCarryCapacity(actor), toolId, protectedTools, retained);
            if (cargo.Sum(move => move.PhysicalQuantity) < required - FreeCarryCapacity(actor) ||
                SpareCargoDestination(actor, agents[actor], cargo.Sum(move => move.PhysicalQuantity)) is not { } destination)
                return null;
            storage = new(actor, household, CaptureCargoMoves(cargo), destination.Position, destination.Range, destination.StorageBuildingId,
                Reach(actor, destination.Position, ReachPurpose.FoodCargoStorage, destination.Range, 0, SwimmingPolicy.WhenCapable));
        }
        else
        {
            var steps = inputs.Select(input => RepairCollectionStep(input)).ToArray();
            if (PlanInputCollection(actor, steps, smith.Position) is not { } ordered) return null;
            inputs = ordered.Select(step => inputs.Single(input => input.ItemKind == step.Kind)).ToList();
        }
        return new(actor, choice, target.OwnerId, target.ItemKind, household, smith.InstanceId, smith.Position,
            materials.AsReadOnly(), inputs.AsReadOnly(), storage,
            Reach(actor, smith.Position, ReachPurpose.ToolRepair, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private InputCollectionStep RepairCollectionStep(PlannedRepairInput input) => new(input.ItemKind, input.Target,
        input.InteractionRange, input.CarryUnits, input.StockId is { } stock ? society.Checkpoint.Inventory.GetLot(stock) : null,
        input.Source, input.Harvest?.ToolLotId);

    private ActionCheck CheckToolRepairPlan(ToolRepairActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || this.HouseholdBuildingWithTag(plan.HouseholdId, "blacksmith") is not { } smith ||
            smith.InstanceId != plan.SmithId || smith.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household Blacksmith has changed");
        if (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId), GoodsKinds.One(plan.ToolKind)),
                plan.Choice.ToolId) is not { Lot: var target } || !ToolProgressionRules.IsTopLevelCarriedLot(target, plan.Actor) ||
            target.ConditionBasisPoints is not (> 0 and < 10_000))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected worn tool is no longer available for repair");
        foreach (var material in plan.Materials)
            if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(material.ItemKind)),
                    material.StockId) is not { } match || match.Quantity < material.Quantity ||
                !ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material is no longer available");
        foreach (var input in plan.Inputs)
        {
            if (HasCarriedMaterial(plan.Actor, input.ItemKind, input.NeededQuantity))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry this repair input");
            if (input.StockId is { } stockId)
            {
                if (RecheckGoods(new(GoodsUse.ReachableHoldings, plan.Actor, GoodsOwners.One(input.OwnerId!), GoodsKinds.One(input.ItemKind)),
                        stockId) is not { } match || match.Quantity < input.NeededQuantity || !MayCollectGoodsAt(plan.Actor, match.Root) ||
                    match.Lot.ContainerLotId is not null || match.Lot.CarrierId is not null ||
                    HouseholdStockPosition(match.Lot) != input.Target || HouseholdStockInteractionRange(match.Lot) != input.InteractionRange)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair supplies are no longer available");
            }
            else
            {
                if (input.Source is not { } source || input.Harvest is not { } harvest || !map.Resources.Contains(source) ||
                    resources.GetValueOrDefault(source.Id) != ResourceState.Available ||
                    !EcologyRules.Harvest(worldSystems.Ecology.GetResource(source.Id), 1).IsValid)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material source is no longer available");
                if (harvest.ToolLotId is { } harvestTool &&
                    (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(input.ToolOwnerId!), GoodsKinds.One(input.ToolKind!)),
                        harvestTool) is not { Lot: var tool } || !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor)))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair gathering tool is no longer available");
                if (HarvestReturnRoute(plan.Actor, source, plan.Destination, ReachPurpose.ToolRepair, input.CarryUnits).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected repair harvest cannot return to the Blacksmith");
            }
        }
        var required = plan.Inputs.Sum(input => input.CarryUnits);
        if (plan.CargoPreparation is { } cargo)
        {
            if (FreeCarryCapacity(plan.Actor) + cargo.Cargo.Sum(move => move.PhysicalQuantity) < required)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected cargo cannot make room for repair inputs");
            var storage = CheckCargoStorage(cargo);
            if (!storage.Allowed) return storage;
        }
        else if (required > FreeCarryCapacity(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected repair inputs");
        else if (!RecheckInputCollection(plan.Actor, plan.Inputs.Select(RepairCollectionStep).ToArray(), plan.Destination))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected repair input trip is no longer possible");
        return agents[plan.Actor].Position == plan.Destination || Reach(plan.Actor, plan.Destination, ReachPurpose.ToolRepair,
            0, 0, SwimmingPolicy.WhenCapable).Count > 0 ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the Blacksmith is out of reach now");
    }

    private void ApplyToolRepairPlan(ToolRepairActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedToolRepairPlan(plan);
    }

    private void ApplyCheckedToolRepairPlan(ToolRepairActionPlan plan)
    {
        if (plan.CargoPreparation is { } cargo) { ApplyCargoStorage(cargo); return; }
        var state = agents[plan.Actor];
        if (plan.Inputs.Count > 0)
        {
            var input = plan.Inputs[0];
            if (input.StockId is { } stock)
                ApplyPlannedEquipmentPickup(plan.Actor, new(stock, input.OwnerId!, input.ItemKind, input.Target, input.InteractionRange));
            else GatherPlannedProjectMaterial(plan.Actor, state, input.ItemKind, input.Source!, input.Harvest!);
            return;
        }
        if (state.Position != plan.Destination) { MoveToward(plan.Actor, state, plan.Destination, "repair_tool", 0); return; }
        CompletePlannedToolRepair(plan.Actor, state, society.Checkpoint.Inventory.GetLot(plan.Choice.ToolId),
            plan.SmithId, plan.Materials);
    }
}
