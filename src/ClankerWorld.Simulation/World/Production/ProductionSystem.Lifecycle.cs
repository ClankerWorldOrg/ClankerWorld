using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ProductionSystem
{
    internal void CancelUnavailableWorkers(IProductionWorld world)
    {
        var active = world.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var cancelled = State.Jobs.Concat(State.CropBuilds ?? [])
            .Where(job => job.State == WorldProductionJobState.Running && !active.Contains(job.WorkerId))
            .OrderBy(job => job.JobId, StringComparer.Ordinal).ToArray();
        CancelProductionJobs(world, cancelled, "production_worker_unavailable");
    }

    internal void CancelProductionJobs(IProductionWorld world, WorldProductionJob[] cancelled, string reason)
    {
        if (cancelled.Length == 0) return;
        world.ReleaseProductionInputs(cancelled.SelectMany(job => job.InputReservationIds.Order(StringComparer.Ordinal)).ToArray(), reason);
        CancelJobs(cancelled.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal), includeCropBuilds: true);
        foreach (var job in cancelled) world.AppendEvent(reason, job.JobId);
    }

    internal void ProcessProduction(IProductionWorld world, long targetTick)
    {
        var due = State.Jobs
            .Where(job => job.State == WorldProductionJobState.Running && job.CompletionTick <= targetTick)
            .OrderBy(job => job.CompletionTick)
            .ThenBy(job => job.JobId, StringComparer.Ordinal)
            .ToArray();
        foreach (var job in due)
        {
            var recipe = world.Content.Recipes.SingleOrDefault(item => item.CanonicalId == job.RecipeId);
            if (recipe is null)
            {
                throw new InvalidDataException($"Production job '{job.JobId}' references a recipe that is no longer active.");
            }

            var completed = CompleteProductionJob(world, job, recipe, targetTick);

            FinishJob(job.JobId, completed);
            if (completed && HouseToolsContent.IsCrudeToolRecipe(recipe))
                world.AppendEvent("house_tool_made", $"{job.WorkerId}|{recipe.Outputs.Single().ResourceId}",
                    world.Buildings.FirstOrDefault(building => building.InstanceId == job.BuildingInstanceId)?.Position);
            else
                world.AppendEvent(completed ? "recipe_completed" : "recipe_cancelled", $"{job.JobId}:{recipe.CanonicalId}");
            if (completed)
            {
                world.LearnProducedRecipe(job, recipe, targetTick);
                world.CreditProductionOrderJob(job, recipe);
            }
        }
    }

    internal static bool CompleteProductionJob(
        IProductionWorld world, WorldProductionJob job,
        RecipeDefinition recipe,
        long targetTick)
    {
        var inventoryState = world.Society.Inventory;
        var inputs = job.InputReservationIds.Select(inventoryState.GetReservation).ToArray();
        var knifePlan = job.ToolLotId is null ? null : ToolProgressionRules.PlanWorkForLot(inventoryState,
            job.WorkerId, ToolFamily.Knife, job.ToolLotId);
        var inputUnusable = inputs.Any(reservation =>
            reservation.State is not (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) ||
            reservation.ExpiryTick < targetTick || inventoryState.Lots.FirstOrDefault(lot => lot.Id == reservation.LotId) is not { FreshnessBasisPoints: > 0, ConditionBasisPoints: > 0 });
        var unsupportedOutput = HasUnsupportedWaterOutput(recipe);
        if (unsupportedOutput || inputUnusable || job.ToolLotId is not null && knifePlan is null)
        {
            world.ReleaseProductionInputs(inputs.Where(reservation => reservation.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                .Select(reservation => reservation.Id).ToArray(), unsupportedOutput ? "production_output_unsupported" : "production_input_unusable");
            world.AppendEvent(unsupportedOutput ? "production_output_unsupported" :
                inputUnusable ? "production_input_unusable" : "production_tool_unusable", job.JobId);
            return false;
        }
        world.CompleteRecipeInventory(job, recipe, knifePlan, targetTick);
        world.CreditProducedWork(job.WorkerId, recipe);
        return true;
    }

    internal static bool HasUnsupportedWaterOutput(RecipeDefinition recipe) =>
        recipe.Outputs.Any(output => output.ResourceId == InventoryContainerRules.FreshWater);
}
