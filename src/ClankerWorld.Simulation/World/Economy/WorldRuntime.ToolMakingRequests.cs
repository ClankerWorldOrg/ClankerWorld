using ShopQuote = ClankerWorld.Simulation.World.MarketSystem.ShopQuote;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

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
        agents[actor].Equipment?.Repair is null && FarmWorkFor(actor) is null && !IsConversationBusy(actor);

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
                if (ToolRequestConsentDefinition.Offer(this, actor, withdrawChoice) is { } withdrawal)
                    candidates.Add(new(withdrawal.CandidateId,
                        "Withdraw your tool request. Household materials and work remain household property.", PersonalToolRequestPriority, request.BuildingInstanceId)
                    { ActionPlan = withdrawal });
                if (ToolRequestCollectionChoice.Decode(ToolRequestChoice("collect", request.Id)) is { } collectChoice &&
                    ToolRequestCollectionDefinition.Offer(this, actor, collectChoice) is { } collection)
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
                if (IdleForToolRequest(actor) && ToolRequestConsentDefinition.Offer(this, actor, acceptChoice) is { } acceptance)
                    candidates.Add(new(acceptance.CandidateId,
                        $"Accept {name}'s {request.ItemKind.Replace('_', ' ')} request using household materials; payment is discussed after it is made.", PersonalToolRequestPriority, request.BuildingInstanceId)
                    { ActionPlan = acceptance });
                var refuseChoice = ToolRequestConsentChoice.Decode(ToolRequestChoice("refuse", request.Id))!;
                if (ToolRequestConsentDefinition.Offer(this, actor, refuseChoice) is { } refusal)
                    candidates.Add(new(refusal.CandidateId, $"Refuse {name}'s tool request without taking anything.", PersonalToolRequestPriority, request.BuildingInstanceId)
                    { ActionPlan = refusal });
            }
            else if (ToolRequestWorkChoice.Decode(ToolRequestChoice("work", request.Id)) is { } workChoice &&
                     ToolRequestWorkDefinition.Offer(this, actor, workChoice) is { } work)
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
                ? ToolRequestConsentDefinition.Offer(this, actor, ToolRequestConsentChoice.Decode(id)!)
                : ToolRequestVisitDefinition.Offer(this, actor, ToolRequestVisitChoice.Decode(id)!);
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

    private void CompleteToolRequestWork(ToolRequestWorkActionPlan plan) =>
        BeginProject(plan.Actor, agents[plan.Actor], plan.Project.CandidateId, plan.Request.Id);

    private void CompleteToolRequestVisit(ToolRequestVisitActionPlan plan) =>
        MoveToward(plan.Actor, agents[plan.Actor], plan.Smith.Position, "tool_request", ResourceInteractionRange);

    private ShopQuote? ToolRequestQuote(string actor, ToolMakingRequestState request, PlacedBuilding shop) =>
        request.JobId is { } jobId && worldSimulation.ProductionJobs.Any(job => job.JobId == jobId && job.State == WorldProductionJobState.Completed)
            ? ShopOpportunity(actor, shop, ProductionOutputLotId(jobId, 0), requestedGoods: true) : null;

    private bool HasToolMakingDemand(RecipeDefinition recipe, string? owner, string? worker) => toolMakingRequests.Any(request =>
        request.SellerHouseholdId == owner && request.RecipeId == recipe.CanonicalId &&
        request.Status == ToolMakingRequestStatus.Accepted && request.JobId is null && (worker is null || request.WorkerId == worker));

    private string? ToolMakingRequestJobFor(string worker, RecipeDefinition recipe, string building) =>
        agents.TryGetValue(worker, out var person) && person.Project?.ToolMakingRequestId is { } id &&
        person.Project.WorkDone >= ProjectWorkTicks &&
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
        if (offer is { State: DirectBarterState.Open } && binding is not null) CancelShopTrade(binding, offer, reason);
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
                    !IsLooseShopLot(lot) || AvailableLotQuantity(lot) < 1)
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
}
