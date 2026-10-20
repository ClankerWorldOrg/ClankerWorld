using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ToolRequestConsentActionPlan? PlanToolRequestConsent(string actor, ToolRequestConsentChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        if (choice.Kind == ToolRequestConsentKind.Place)
        {
            foreach (var (shop, recipe) in ToolRequestOptions(actor))
                if (ToolRequestConsentChoice.Encode(choice.Kind, shop.InstanceId, recipe.CanonicalId) == choice.Encode())
                    return new(actor, choice, shop, recipe, null);
            return null;
        }
        var request = toolMakingRequests.FirstOrDefault(item => ToolRequestConsentChoice.Encode(choice.Kind, item.Id) == choice.Encode());
        if (request is null || ToolRequestShop(request) is not { } building ||
            worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == request.RecipeId) is not { } definition) return null;
        return new(actor, choice, building, definition, request);
    }

    private ActionCheck CheckToolRequestConsentPlan(ToolRequestConsentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var placing = plan.Choice.Kind == ToolRequestConsentKind.Place;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestConsentCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (PendingInstructionFor(plan.Actor)?.Kind == OwnerInstructionKind.Order ||
            worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.Shop.InstanceId) != plan.Shop ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Blacksmith, recipe or personal choice is unavailable");
        if (placing)
            return IdleForToolRequest(plan.Actor) && IsWithinInteractionRange(agents[plan.Actor].Position, plan.Shop.Position, ResourceInteractionRange) &&
                MayRequestToolAt(plan.Actor, plan.Shop, plan.Recipe)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that tool request is no longer available at the selected Blacksmith");
        if (plan.Original is not { } original || toolMakingRequests.FirstOrDefault(item => item.Id == original.Id) is not { } request ||
            (plan.Choice.Kind == ToolRequestConsentKind.Withdraw
                ? request.RequesterId != original.RequesterId || request.SellerHouseholdId != original.SellerHouseholdId ||
                    request.BuildingInstanceId != original.BuildingInstanceId || request.RecipeId != original.RecipeId ||
                    request.ItemKind != original.ItemKind || request.RequestedTick != original.RequestedTick
                : request != original))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected tool request has changed");
        var allowed = plan.Choice.Kind switch
        {
            ToolRequestConsentKind.Withdraw => request.RequesterId == plan.Actor && !ToolMakingRequestRules.IsTerminal(request.Status),
            ToolRequestConsentKind.Accept => request.Status == ToolMakingRequestStatus.Requested && IsToolRequestSeller(plan.Actor, request) && IdleForToolRequest(plan.Actor),
            _ => request.Status == ToolMakingRequestStatus.Requested && IsToolRequestSeller(plan.Actor, request),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that tool-request response is no longer available");
    }

    private void AddToolMakingRequestCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddToolMakingRequestCandidateOptions(options, actor);
        foreach (var candidate in options)
        {
            if (ToolRequestCollectionChoice.Decode(candidate.Id) is { } collect)
            {
                if ((candidate.ActionPlan as ToolRequestCollectionActionPlan ?? PlanToolRequestCollection(actor, collect)) is { } collection &&
                    CheckActionPlan(collection).Allowed)
                    candidates.Add(candidate with { ActionPlan = collection });
                continue;
            }
            if (ToolRequestWorkChoice.Decode(candidate.Id) is { } work)
            {
                if ((candidate.ActionPlan as ToolRequestWorkActionPlan ?? PlanToolRequestWork(actor, work)) is { } continuation &&
                    CheckActionPlan(continuation).Allowed)
                    candidates.Add(candidate with { ActionPlan = continuation });
                continue;
            }
            if (ToolRequestVisitChoice.Decode(candidate.Id) is { } visit)
            {
                if (PlanToolRequestVisit(actor, visit) is { } journey && CheckActionPlan(journey).Allowed)
                    candidates.Add(candidate with { ActionPlan = journey });
                continue;
            }
            if (ToolRequestConsentChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (PlanToolRequestConsent(actor, choice) is { } plan && CheckActionPlan(plan).Allowed)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void ApplyToolRequestConsentPlan(ToolRequestConsentActionPlan plan)
    {
        TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
    }
}
