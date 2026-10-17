using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private readonly Dictionary<string, (long StartedTick, string? InstructionId, RecipeProjectActionPlan Plan)> recipeProjectPlans = new(StringComparer.Ordinal);

    private void CopyRecipeProjectPlansFrom(WorldRuntime source)
    {
        if (ReferenceEquals(this, source)) return;
        recipeProjectPlans.Clear();
        foreach (var (actor, retained) in source.recipeProjectPlans)
            if (agents.TryGetValue(actor, out var person) && person.Project is { Stage: not ("completed" or "cancelled") } project &&
                project.StartedTick == retained.StartedTick && project.CandidateId == retained.Plan.CandidateId &&
                retained.InstructionId == RecipeProjectInstructionId(actor, retained.Plan.Recipe.CanonicalId))
                recipeProjectPlans.Add(actor, retained);
    }

    private RecipeProjectActionPlan PlanRecipeProject(string actor, RecipeDefinition recipe, PlacedBuilding workstation)
    {
        var personal = IsHandcartRecipe(recipe);
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == workstation.DefinitionId);
        var storage = !personal && workstation.HouseholdId is not null && definition.Tags.Any(IsHouseholdBuildingTag)
            ? workstation.InstanceId : null;
        var owner = personal ? actor : ProductionOwnerFor(workstation, actor);
        var knife = ToolProgressionRules.UsesKnife(recipe)
            ? ToolProgressionRules.PlanWork(society.Checkpoint.Inventory, actor, ToolFamily.Knife) : null;
        return new(actor, recipe, workstation, owner, storage, personal,
            PlanRecipeInputs(recipe, owner, storage, personal), knife);
    }

    private RecipeProjectInput[]? PlanRecipeInputs(RecipeDefinition recipe, string owner, string? storage, bool carried)
    {
        var selected = new List<RecipeProjectInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < recipe.Inputs.Count; index++)
        {
            var input = recipe.Inputs[index];
            var remaining = input.Amount;
            foreach (var match in ProductionGoods(society.Checkpoint.Inventory, ProductionGoodsRequest(owner, input.ResourceId, storage), carried))
            {
                var quantity = Math.Min(remaining, Math.Max(0, match.Quantity - used.GetValueOrDefault(match.Lot.Id)));
                if (quantity == 0) continue;
                selected.Add(new(index, match.Lot.Id, input.ResourceId, quantity));
                used[match.Lot.Id] = used.GetValueOrDefault(match.Lot.Id) + quantity;
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining > 0) return null;
        }
        return selected.ToArray();
    }

    private ActionCheck CheckRecipeProject(RecipeProjectActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: false,
                AllowUrgentWarmth: plan.Recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)), AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var workstation = worldSimulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.Workstation.InstanceId);
        var definition = worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == plan.Workstation.DefinitionId);
        if (workstation is null || definition is null || workstation.Position != plan.Workstation.Position ||
            workstation.DefinitionId != plan.Workstation.DefinitionId || workstation.HouseholdId != plan.Workstation.HouseholdId ||
            workstation.TownId != plan.Workstation.TownId ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId && TownHearingProcedure.Digest(recipe) == TownHearingProcedure.Digest(plan.Recipe)) ||
            workstation.HouseholdId is { } household && household != HouseholdFor(plan.Actor) ||
            workstation.HouseholdId is null && definition.Tags.Any(IsHouseholdBuildingTag) ||
            plan.OwnerId != (plan.RequireCarried ? plan.Actor : ProductionOwnerFor(workstation, plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipe or workstation access has changed");
        if (worldSimulation.ProductionJobs.Count(job => job.BuildingInstanceId == workstation.InstanceId &&
                job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused) >= definition.Capacity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected workstation has no free production capacity");
        var inputs = CheckRecipeInputs(plan);
        if (!inputs.Allowed) return inputs;
        return agents[plan.Actor].Position == workstation.Position || Reach(plan.Actor, workstation.Position, ReachPurpose.RecipeProduction, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected workstation is out of reach");
    }

    private ActionCheck CheckRecipeInputs(RecipeProjectActionPlan plan)
    {
        if (plan.Inputs is not null && plan.Inputs.GroupBy(input => (input.LotId, input.ItemKind)).Any(group =>
                ProductionGoods(society.Checkpoint.Inventory, ProductionGoodsRequest(plan.OwnerId, group.Key.ItemKind, plan.RequiredStorageBuildingId), plan.RequireCarried)
                    .FirstOrDefault(match => match.Lot.Id == group.Key.LotId) is not { } match || match.Quantity < group.Sum(input => input.Quantity)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected recipe input lots are unavailable");
        if (plan.Knife is not null && ToolProgressionRules.PlanWorkForLot(society.Checkpoint.Inventory, plan.Actor, ToolFamily.Knife, plan.Knife.ToolLotId) is null)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected production knife is unavailable");
        return new();
    }

    private RecipeProjectActionPlan? RetainedRecipeProject(string actor, AgentProject project) =>
        recipeProjectPlans.TryGetValue(actor, out var retained) && retained.StartedTick == project.StartedTick && retained.Plan.CandidateId == project.CandidateId &&
        retained.InstructionId == RecipeProjectInstructionId(actor, retained.Plan.Recipe.CanonicalId)
            ? retained.Plan : null;

    private string? RecipeProjectInstructionId(string actor, string recipeId) =>
        PendingInstructionFor(actor) is { Task.Action: "produce_item" } instruction && instruction.Task.TargetRecipeId == recipeId
            ? instruction.InstructionId : null;
}
