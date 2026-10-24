using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ProductionSystem
{
    internal ProductionStartResult StartProduction(
        IProductionStartWorld world, ActorCapabilityPolicy goalCapabilities,
        string recipeId,
        string buildingInstanceId,
        string workerId,
        string eventKind,
        string? orderInstructionId = null,
        RecipeProjectActionPlan? originalPlan = null)
    {
        try
        {
            var normalizedRecipeId = world.NormalizeRequiredText(recipeId, nameof(recipeId));
            var normalizedBuildingId = world.NormalizeRequiredText(buildingInstanceId, nameof(buildingInstanceId));
            var normalizedWorkerId = world.NormalizeRequiredText(workerId, nameof(workerId));
            var recipe = world.Content.Recipes.SingleOrDefault(item => item.CanonicalId == normalizedRecipeId);
            if (recipe is null)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId, "The recipe is not active.");
            }
            if (HasUnsupportedWaterOutput(recipe))
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    "Workstation recipes cannot produce fresh water. Collect it into a water jug instead.");
            if (IsGenericFoodRecipe(recipe))
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    "Cook named ingredients at your household House or Restaurant.");
            if (recipe.Outputs.Any(output => output.ResourceId == "bedding"))
                return ProductionStartResult.Rejected(normalizedRecipeId, "Bedding production was retired with sleep.");
            if (recipe.IsCrop)
                return ProductionStartResult.Rejected(normalizedRecipeId, "Prepare and work a tilled field for crops; workstation production cannot grow them.");

            var placed = world.Buildings.SingleOrDefault(item => item.InstanceId == normalizedBuildingId);
            if (placed is null)
                return ProductionStartResult.Rejected(normalizedRecipeId, "The workstation building is not placed.");
            if (recipe.WorkstationBuildingId != placed.DefinitionId)
                return ProductionStartResult.Rejected(normalizedRecipeId, "The placed building is not a valid workstation for this recipe.");
            var buildingDefinition = world.Content.Buildings.Single(item => item.CanonicalId == placed.DefinitionId);
            if (State.Jobs.Count(item => item.BuildingInstanceId == placed.InstanceId &&
                    (item.State is WorldProductionJobState.Running or WorldProductionJobState.Paused)) >= buildingDefinition.Capacity)
                return ProductionStartResult.Rejected(normalizedRecipeId, "The workstation has no free production capacity.");
            var workPosition = placed.Position;

            var worker = world.Society.Agents.SingleOrDefault(item => item.Id == normalizedWorkerId);
            if (worker is null || worker.Status != SocietyAgentStatus.Active)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId, "The production worker is not active.");
            }
            if (!ActorCapabilities.Check(world.CapabilitiesFor(normalizedWorkerId), $"build:recipe:{recipe.CanonicalId}", goalCapabilities).Allowed)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    "This worker is too young to start production. Choose an adult worker.");
            }

            var workstation = placed is null ? null : world.Content.Buildings.Single(item => item.CanonicalId == placed.DefinitionId);
            if (placed?.HouseholdId is { } workOwner && worker.HouseholdId != workOwner)
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    "Only a member of the building's household can work there.");

            if (workstation is not null && world.IsHouseholdBuilding(workstation) && placed?.HouseholdId is null)
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    "The household workshop must be claimed before production.");

            var personalCartRecipe = IsHandcartRecipe(recipe);
            if (personalCartRecipe && !world.HasCarriedUnreservedQuantities(normalizedWorkerId, recipe.Inputs))
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    "Carry the wood, iron fittings and rope to the Blacksmith before building a handcart.");
            var onSiteHouseholdRecipe = !personalCartRecipe && placed?.HouseholdId is not null && workstation is not null && world.IsHouseholdBuilding(workstation);
            if (onSiteHouseholdRecipe && !HasIngredientsAtBuilding(world, recipe.Inputs, worker.HouseholdId!, placed!.InstanceId))
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    MissingProductionIngredients(world, recipe, worker.HouseholdId!, placed!.InstanceId));

            if (!world.Agents.TryGetValue(normalizedWorkerId, out var physical) || physical.Position != workPosition)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId, "The worker must be standing at the build site.");
            }

            if (placed is not null && BuildingStorageRules.Capacity(workstation!, placed) is not null &&
                Math.Max(0, recipe.Outputs.Sum(item => item.Amount) - recipe.Inputs.Sum(item => item.Amount)) > world.DestinationRoom(placed.InstanceId))
                return ProductionStartResult.Rejected(normalizedRecipeId, "There is no storage room for this recipe's finished output.");

            var plan = originalPlan ?? PlanRecipeProject(world, normalizedWorkerId, recipe, placed!);
            if (plan.Inputs is null)
                plan = plan with { Inputs = PlanRecipeInputs(world, recipe, plan.OwnerId, plan.RequiredStorageBuildingId, plan.RequireCarried) };
            if (plan.Inputs is null)
            {
                var missing = recipe.Inputs.First(input => world.ProductionGoods(world.Society.Inventory,
                    GoodsRequestForRecipe(plan.OwnerId, input.ResourceId, plan.RequiredStorageBuildingId), plan.RequireCarried)
                    .Sum(match => (long)match.Quantity) < input.Amount);
                var available = world.ProductionGoods(world.Society.Inventory,
                    GoodsRequestForRecipe(plan.OwnerId, missing.ResourceId, plan.RequiredStorageBuildingId), plan.RequireCarried).Sum(match => (long)match.Quantity);
                return ProductionStartResult.Rejected(normalizedRecipeId,
                    $"Insufficient '{missing.ResourceId}' for production; missing {(missing.Amount - available).ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            }
            if (CheckRecipeInputs(world, plan).Refusal is { } refusal)
                return ProductionStartResult.Rejected(normalizedRecipeId, refusal.Explanation);
            var knife = plan.Knife;
            var workDuration = knife is null ? recipe.DurationTicks :
                ToolProgressionRules.WorkDuration(recipe.DurationTicks, knife.WorkUnits);
            workDuration = world.SkilledRecipeWorkTicks(normalizedWorkerId, recipe, workDuration);
            var jobId = world.ProductionJobId(State.NextSequence);
            var completionTick = checked(world.WorldTick + workDuration);
            // A handcart belongs to the adult who builds it, not to the Blacksmith's household.
            var productionOwner = plan.OwnerId;
            var reservationIds = world.ReserveProductionInputs(plan, $"{jobId}:input", completionTick);

            var job = new WorldProductionJob(
                jobId,
                recipe.CanonicalId,
                normalizedBuildingId,
                normalizedWorkerId,
                world.WorldTick,
                completionTick,
                WorldProductionJobState.Running,
                reservationIds.Select(Identity.Parse).ToArray(), knife?.ToolLotId, world.ToolMakingRequestJobFor(normalizedWorkerId, recipe, normalizedBuildingId))
            { OwnerId = productionOwner, OrderInstructionId = orderInstructionId };
            StartJob(job);
            world.BindToolMakingJob(job);
            if (knife is not null)
                world.MarkCurrentSchema();
            world.AppendEvent(WorldEvents.ProductionStarted(eventKind, job.WorkerId, job.JobId, job.RecipeId, job.BuildingInstanceId));
            return ProductionStartResult.Success(job);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ProductionStartResult.Rejected(recipeId?.Trim() ?? string.Empty, exception.Message);
        }
    }

}
