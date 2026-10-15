using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ProductionSystem
{
    internal static RecipeProjectActionPlan PlanRecipeProject(IRecipeWorld world, string actor, RecipeDefinition recipe, PlacedBuilding workstation)
    {
        var personal = IsHandcartRecipe(recipe);
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == workstation.DefinitionId);
        var storage = !personal && workstation.HouseholdId is not null && world.IsHouseholdBuilding(definition)
            ? workstation.InstanceId : null;
        var owner = personal ? actor : world.ProductionOwnerFor(workstation, actor);
        var knife = ToolProgressionRules.UsesKnife(recipe)
            ? ToolProgressionRules.PlanWork(world.Society.Inventory, actor, ToolFamily.Knife) : null;
        return new(actor, recipe, workstation, owner, storage, personal,
            PlanRecipeInputs(world, recipe, owner, storage, personal), knife);
    }

    internal static RecipeProjectInput[]? PlanRecipeInputs(IRecipeWorld world, RecipeDefinition recipe, string owner, string? storage, bool carried)
    {
        var selected = new List<RecipeProjectInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < recipe.Inputs.Count; index++)
        {
            var input = recipe.Inputs[index];
            var remaining = input.Amount;
            foreach (var match in world.ProductionGoods(world.Society.Inventory, GoodsRequestForRecipe(owner, input.ResourceId, storage), carried))
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

    internal static ActorCapabilityPolicy RecipeCapabilities(RecipeDefinition recipe) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false,
            AllowUrgentWarmth: recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)), AllowActiveOrder: true);

    internal ActionCheck CheckRecipeProject(IRecipeWorld world, RecipeProjectActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? RecipeCapabilities(plan.Recipe));
        if (!capability.Allowed) return capability;
        var workstation = world.Buildings.FirstOrDefault(item => item.InstanceId == plan.Workstation.InstanceId);
        var definition = world.Content.Buildings.FirstOrDefault(item => item.CanonicalId == plan.Workstation.DefinitionId);
        if (workstation is null || definition is null || workstation.Position != plan.Workstation.Position ||
            workstation.DefinitionId != plan.Workstation.DefinitionId || workstation.HouseholdId != plan.Workstation.HouseholdId ||
            workstation.TownId != plan.Workstation.TownId ||
            !world.Content.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId && TownHearingProcedure.Digest(recipe) == TownHearingProcedure.Digest(plan.Recipe)) ||
            workstation.HouseholdId is { } household && household != world.HouseholdFor(plan.Actor) ||
            workstation.HouseholdId is null && world.IsHouseholdBuilding(definition) ||
            plan.OwnerId != (plan.RequireCarried ? plan.Actor : world.ProductionOwnerFor(workstation, plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipe or workstation access has changed");
        if (State.Jobs.Count(job => job.BuildingInstanceId == workstation.InstanceId &&
                job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused) >= definition.Capacity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected workstation has no free production capacity");
        var inputs = CheckRecipeInputs(world, plan);
        if (!inputs.Allowed) return inputs;
        return world.Agents[plan.Actor].Position == workstation.Position || world.Reach(plan.Actor, workstation.Position, ReachPurpose.RecipeProduction, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected workstation is out of reach");
    }

    internal static ActionCheck CheckRecipeInputs(IRecipeWorld world, RecipeProjectActionPlan plan)
    {
        if (plan.Inputs is not null && plan.Inputs.GroupBy(input => (input.LotId, input.ItemKind)).Any(group =>
                world.ProductionGoods(world.Society.Inventory, GoodsRequestForRecipe(plan.OwnerId, group.Key.ItemKind, plan.RequiredStorageBuildingId), plan.RequireCarried)
                    .FirstOrDefault(match => match.Lot.Id == group.Key.LotId) is not { } match || match.Quantity < group.Sum(input => input.Quantity)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected recipe input lots are unavailable");
        if (plan.Knife is not null && ToolProgressionRules.PlanWorkForLot(world.Society.Inventory, plan.Actor, ToolFamily.Knife, plan.Knife.ToolLotId) is null)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected production knife is unavailable");
        return new();
    }

    internal static GoodsRequest GoodsRequestForRecipe(string owner, string kind, string? building = null) =>
        new(GoodsUse.ConsumeAt, null, GoodsOwners.One(owner), GoodsKinds.One(kind), AtBuilding: building);

    internal static bool IsHandcartRecipe(RecipeDefinition recipe) => recipe.Outputs.Any(output => output.ResourceId == InventoryContainerRules.Handcart);
    internal const string MissingHouseholdIngredientsPrefix = "The household building needs ";

    internal static bool IsGenericFoodRecipe(RecipeDefinition recipe) =>
        recipe.Inputs.Any(input => input.ResourceId == "food") && recipe.Outputs.Any(output => output.ResourceId == "food");

    internal static string MissingProductionIngredients(IRecipeWorld world, RecipeDefinition recipe, string owner, string buildingId)
    {
        var missing = recipe.Inputs.First(input => !HasIngredientsAtBuilding(world, [input], owner, buildingId));
        var available = world.ProductionGoods(world.Society.Inventory, GoodsRequestForRecipe(owner, missing.ResourceId, buildingId), false)
            .Sum(match => (long)match.Quantity);
        return $"{MissingHouseholdIngredientsPrefix}{missing.Amount - available} {missing.ResourceId.Replace('_', ' ')} in its on-site stock. Bring it here before starting work.";
    }

    internal static bool HasIngredientsAtBuilding(IRecipeWorld world, IReadOnlyList<ContentQuantity> inputs, string owner, string building) =>
        inputs.All(input => world.ProductionGoods(world.Society.Inventory, GoodsRequestForRecipe(owner, input.ResourceId, building), false)
            .Sum(match => (long)match.Quantity) >= input.Amount);
    internal bool TryFindRecipeSite(
        IRecipeWorld world,
        RecipeDefinition recipe,
        out string siteId,
        out GridPoint position,
        string? actorId = null)
    {
        if (recipe.WorkstationBuildingId is null)
        {
            siteId = string.Empty;
            position = default;
            return false;
        }

        var productionOrder = actorId is null ? null : world.PendingInstructionFor(actorId)?.Task;
        if (productionOrder?.Action != "produce_item" || productionOrder.TargetRecipeId != recipe.CanonicalId)
            productionOrder = null;
        foreach (var placed in world.Buildings.OrderBy(item => item.InstanceId, StringComparer.Ordinal))
        {
            if (productionOrder is not null &&
                (productionOrder.ProductionBuildingId is { } requiredBuilding && placed.InstanceId != requiredBuilding ||
                 productionOrder.TargetPosition is { } requestedPosition && placed.Position != requestedPosition))
                continue;
            if (placed.DefinitionId != recipe.WorkstationBuildingId ||
                placed.HouseholdId is not null && (actorId is null ||
                    placed.HouseholdId != world.Society.GetAgent(actorId).HouseholdId))
            {
                continue;
            }

            var definition = world.Content.Buildings.Single(item => item.CanonicalId == placed.DefinitionId);
            // A household building nobody holds is not anyone's to use.
            if (placed.HouseholdId is null && world.IsHouseholdBuilding(definition))
                continue;
            var activeJobs = State.Jobs.Count(item =>
                item.BuildingInstanceId == placed.InstanceId && item.State is WorldProductionJobState.Running or WorldProductionJobState.Paused);
            if (activeJobs < definition.Capacity &&
                (actorId is null || world.Reach(actorId, placed.Position, ReachPurpose.RecipeProduction, 0, 0, SwimmingPolicy.WhenCapable).Count > 0))
            {
                siteId = placed.InstanceId;
                position = placed.Position;
                return true;
            }
        }

        siteId = string.Empty;
        position = default;
        return false;
    }

    internal RecipeActionPlan? PlanRecipeStart(IRecipeWorld world, string actor, RecipeChoice choice)
    {
        if (world.Content.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == choice.DefinitionId) is not { } recipe) return null;
        return TryFindRecipeSite(world, recipe, out var site, out _, actor)
            ? PlanRecipeProject(world, actor, recipe, world.Buildings.Single(building => building.InstanceId == site))
            : new RecipeGoalActionPlan(actor, recipe);
    }

    internal static ActionCheck CheckRecipeGoal(IRecipeWorld world, RecipeGoalActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        return world.Content.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipe goal is no longer available");
    }

}
