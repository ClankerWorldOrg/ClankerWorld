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

    private ActionCheck CheckToolRequestConsentPlan(ToolRequestConsentActionPlan plan)
    {
        var placing = plan.Choice.Kind == ToolRequestConsentKind.Place;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: !placing, AllowUrgentWarmth: !placing, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (PendingInstructionFor(plan.Actor)?.Kind == OwnerInstructionKind.Order ||
            worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.Shop.InstanceId) != plan.Shop ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Blacksmith, recipe or personal choice is unavailable");
        if (placing)
            return IdleForToolRequest(plan.Actor) && IsWithinInteractionRange(agents[plan.Actor].Position, plan.Shop.Position, ResourceInteractionRange) &&
                MayRequestToolAt(plan.Actor, plan.Shop, plan.Recipe)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that tool request is no longer available at the selected Blacksmith");
        if (plan.Original is not { } request || toolMakingRequests.FirstOrDefault(item => item.Id == request.Id) != request)
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
            if (ToolRequestVisitChoice.Decode(candidate.Id) is { } visit)
            {
                if (PlanToolRequestVisit(actor, visit) is { } journey && CheckToolRequestVisitPlan(journey).Allowed)
                    candidates.Add(candidate with { ActionPlan = journey });
                continue;
            }
            if (ToolRequestConsentChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (PlanToolRequestConsent(actor, choice) is { } plan && CheckToolRequestConsentPlan(plan).Allowed)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void ApplyToolRequestConsentPlan(ToolRequestConsentActionPlan plan)
    {
        if (CheckToolRequestConsentPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteToolRequestConsent(plan);
    }
}
