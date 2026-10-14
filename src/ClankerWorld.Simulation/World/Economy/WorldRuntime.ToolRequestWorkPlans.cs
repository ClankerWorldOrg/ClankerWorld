namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
            person.Equipment?.Repair is not null || FarmWorkFor(plan.Actor) is not null || IsConversationBusy(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected commission project is no longer available");
        return HasIngredientsAtBuilding(plan.Recipe.Inputs, current.SellerHouseholdId, plan.Smith.InstanceId)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Blacksmith no longer has the required inputs");
    }

    private void ApplyToolRequestWorkPlan(ToolRequestWorkActionPlan plan, bool freshChoice)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedToolRequestWork(plan, freshChoice);
    }

    private void ApplyCheckedToolRequestWork(ToolRequestWorkActionPlan plan, bool freshChoice)
    {
        if (!freshChoice && agents[plan.Actor].Project!.RequiresFreshChoice) return;
        CompleteToolRequestWork(plan);
    }
}
