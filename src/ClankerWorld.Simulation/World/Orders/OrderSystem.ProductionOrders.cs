using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static CognitionCandidate? ProductionOrderCandidateFor(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        var recipe = WorldProductionOrderCatalog.Find(world.Content, order.TargetRecipeId)?.Recipe;
        if (recipe is null || !world.ReadyForBriefInteraction(instruction.TargetAgentId)) return null;
        if (BoundProductionJob(world, instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
            return ProductionOrderBuilding(world, instruction, recipe) is null ? null :
                new("produce_item", "Continue the ordered production at its reserved workstation.", 0, job.BuildingInstanceId);
        if (!world.TryFindRecipeSite(recipe, out var buildingId, out _, instruction.TargetAgentId)) return null;
        return new("produce_item", "Prepare and complete the requested recipe using real workstation inputs.", 0, buildingId);
    }

    internal static PlacedBuilding? ProductionOrderBuilding(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, RecipeDefinition recipe)
    {
        var order = instruction.Task!;
        var actor = instruction.TargetAgentId;
        var building = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == order.ProductionBuildingId);
        if (building is null || building.DefinitionId != recipe.WorkstationBuildingId ||
            order.TargetPosition is { } target && building.Position != target ||
            building.HouseholdId is { } household && world.Society.GetAgent(actor).HouseholdId != household)
            return null;
        var definition = world.Content.Buildings.FirstOrDefault(item => item.CanonicalId == building.DefinitionId);
        return definition is null || building.HouseholdId is null && definition.Tags.Any(world.IsHouseholdBuildingTag)
            ? null : building;
    }

    internal static WorldProductionJob? BoundProductionJob(IProductionOrdersWorld world, OwnerQueuedInstruction instruction) =>
        instruction.Task is { Action: "produce_item", ProductionJobId: { } id } order
            ? world.Simulation.ProductionJobs.FirstOrDefault(job => job.JobId == id &&
                job.OrderInstructionId == instruction.InstructionId &&
                job.WorkerId == instruction.TargetAgentId && job.RecipeId == order.TargetRecipeId &&
                job.BuildingInstanceId == order.ProductionBuildingId && job.StartedTick >= order.ProductionProjectStartedTick)
            : null;

    internal static void ExecuteProductionOrderStep(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        instruction = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = instruction.Task!;
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
        {
            world.SetOrderStatus(instruction, "interrupted", "Food or warmth needs come first.");
            return;
        }
        var entry = WorldProductionOrderCatalog.Find(world.Content, order.TargetRecipeId);
        if (entry is null || ProductionOrderCandidateFor(world, instruction, person)?.DestinationId is not { } buildingId)
        {
            world.SetOrderStatus(instruction, "blocked", ProductionOrderBlockedReason(world, instruction));
            return;
        }

        if (BoundProductionJob(world, instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
        {
            var building = ProductionOrderBuilding(world, instruction, entry.Recipe)!;
            if (person.Position != building.Position)
            {
                PauseProductionOrderJob(world, instruction, job);
                world.MoveToward(actor, person, building.Position, "project", 0);
                return;
            }
            if (job.State == WorldProductionJobState.Paused)
            {
                if (!world.CanResumeHeldInputs(actor, job.BuildingInstanceId, job.InputReservationIds) ||
                    !world.TryResumeKnife(actor, job.JobId, out var knifeId))
                {
                    world.SetOrderStatus(instruction, "blocked", "The reserved ingredients or production tool are no longer available.");
                    return;
                }
                var deadline = checked(world.WorldTick + job.CompletionTick - job.PausedAtTick!.Value);
                world.ExtendOrderReservations(job.InputReservationIds, deadline);
                world.ResumeProductionJob(job.JobId, deadline, knifeId);
                world.AppendEvent("production_order_resumed", $"{instruction.InstructionId}:{job.JobId}");
            }
            return;
        }

        if (!world.IsProductionOrderProject(instruction, person.Project) ||
            person.Project!.Stage is "completed" or "cancelled" || person.Project.JobId is not null)
        {
            var project = new AgentProject("build:recipe:" + entry.Recipe.CanonicalId,
                entry.Recipe.DisplayName, world.WorldTick, "acquiring", LastTransitionTick: world.WorldTick);
            order = OrderStore.PrepareProductionBinding(order, buildingId, null, world.WorldTick);
            instruction = OrderStore.PrepareTaskBinding(instruction, order);
            world.Orders.RecordProductionOrderStep(instruction.IdempotencyKey, instruction);
            world.PhysicalAgents.RecordProject(actor, person, project);
            world.MarkCheckpointCurrent();
            world.AppendEvent("project_chosen", $"{actor}:{project.CandidateId}");
        }
        if (world.Agents[actor].Project is { RequiresFreshChoice: true } pausedProject)
            world.SetProject(actor, pausedProject with { Stage = "acquiring", RequiresFreshChoice = false, Blocker = null });
        world.ContinueProject(actor, world.Agents[actor]);
        var currentProject = world.Agents[actor].Project!;
        if (currentProject.JobId is { } startedJobId && world.Simulation.ProductionJobs.Any(job =>
                job.JobId == startedJobId && job.OrderInstructionId == instruction.InstructionId &&
                job.WorkerId == actor && job.RecipeId == order.TargetRecipeId &&
                job.BuildingInstanceId == order.ProductionBuildingId && job.StartedTick == world.WorldTick))
        {
            var current = world.Orders.Instructions[instruction.IdempotencyKey];
            world.Orders.RecordProductionOrderStep(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareProductionJob(current.Task!, startedJobId)));
        }
        if (currentProject.Stage is "blocked" or "cancelled")
            world.SetOrderStatus(instruction, "blocked", currentProject.Blocker ?? "The requested production cannot continue yet.");
    }

    internal static string ProductionOrderBlockedReason(IProductionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (!world.MayFollowOwnerOrder(instruction.TargetAgentId, instruction.Task!))
            return "This agent is too young to do workstation production.";
        if (WorldProductionOrderCatalog.Find(world.Content, instruction.Task!.TargetRecipeId) is null)
            return "The requested recipe is not active.";
        if (world.IsProductionOrderProject(instruction, world.Agents[instruction.TargetAgentId].Project) &&
            world.Agents[instruction.TargetAgentId].Project?.Blocker is { } blocker)
            return blocker;
        return "The requested workstation is unavailable, full, outside this household's access, or cannot be reached.";
    }

    internal static void MaintainProductionOrdersBeforeTick(IProductionOrdersWorld world)
    {
        foreach (var instruction in world.Orders.Instructions.Values.Where(item =>
                     item.Task is { Action: "produce_item" } order && world.IsActiveOrder(order.Status)).ToArray())
        {
            var job = BoundProductionJob(world, instruction);
            if (job is not { State: WorldProductionJobState.Running or WorldProductionJobState.Paused }) continue;
            var recipe = world.Content.Recipes.FirstOrDefault(item => item.CanonicalId == job.RecipeId);
            if (!world.Agents.TryGetValue(instruction.TargetAgentId, out var person) || recipe is null ||
                !world.IsAdult(instruction.TargetAgentId) || ProductionOrderBuilding(world, instruction, recipe) is not { } building ||
                !world.IsProductionOrderProject(instruction, person.Project))
            {
                CancelProductionForOrder(world, instruction);
                world.SetOrderStatus(instruction, "blocked", "The worker or workstation for this order is no longer available.");
                continue;
            }
            if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
            {
                PauseProductionOrderJob(world, instruction, job);
                world.SetOrderStatus(instruction, "interrupted", "Food or warmth needs come first.");
            }
            else if (person.Position != building.Position || world.IsConversationBusy(person.AgentId) ||
                     instruction.Task!.WaitForDecisionAfterFailure)
                PauseProductionOrderJob(world, instruction, job);
        }
    }

    internal static void PauseProductionOrderJob(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, WorldProductionJob job)
    {
        if (job.State != WorldProductionJobState.Running) return;
        world.HoldOrderReservations(job.InputReservationIds);
        world.PauseProductionJob(job.JobId, world.WorldTick);
        world.AppendEvent("production_order_paused", $"{instruction.InstructionId}:{job.JobId}");
    }

    internal static bool IsActiveProductionOrderJob(IProductionOrdersWorld world, string jobId) => world.Orders.Instructions.Values.Any(instruction =>
        instruction.Task is { Action: "produce_item" } order && world.IsActiveOrder(order.Status) &&
        order.ProductionJobId == jobId);

    internal static void CancelProductionForOrder(IProductionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (instruction.Task?.Action != "produce_item") return;
        if (BoundProductionJob(world, instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
        {
            world.ReleaseProductionOrderInputs(job);
            world.CancelProductionJobs(new HashSet<string>(StringComparer.Ordinal) { job.JobId });
            world.AppendEvent("recipe_cancelled", $"{job.JobId}:{job.RecipeId}");
        }
        if (world.Agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            world.IsProductionOrderProject(instruction, person.Project))
            world.SetProject(person.AgentId, person.Project! with { Stage = "cancelled", Blocker = "The production order stopped." });
    }

    internal static void CreditProductionOrderJob(IProductionOrdersWorld world, WorldProductionJob job, RecipeDefinition recipe)
    {
        var instruction = world.PendingInstructionFor(job.WorkerId);
        if (instruction?.Task is not { Action: "produce_item" } order || BoundProductionJob(world, instruction)?.JobId != job.JobId ||
            !world.Agents.TryGetValue(job.WorkerId, out var person) || !world.IsProductionOrderProject(instruction, person.Project)) return;
        var units = order.ProgressUnit == "production_batches" ? 1 :
            recipe.Outputs.Where(output => output.ResourceId == order.TargetOutputKind).Sum(output => output.Amount);
        var receipt = world.ProductionOrderReceipt(job.JobId);
        world.SetProject(job.WorkerId, person.Project! with { Stage = "completed", Blocker = null });
        world.CreditOrderEffect(instruction, receipt, units);
    }
}
